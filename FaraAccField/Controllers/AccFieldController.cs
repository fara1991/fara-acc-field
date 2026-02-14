using System;
using System.Collections.Generic;
using FaraAccField.Configuration;
using FaraAccField.Models;
using FaraAccField.Services;
using UnityEngine;
using Zenject;

namespace FaraAccField.Controllers
{
    /// <summary>
    /// Main controller that coordinates pre-swing detection,
    /// trajectory line display, and note grid highlighting during gameplay.
    /// </summary>
    internal class AccFieldController : IInitializable, ITickable, IDisposable
    {
        private readonly BeatmapObjectManager _beatmapObjectManager;
        private readonly SaberManager _saberManager;
        private readonly ColorManager? _colorManager;
        private readonly TrajectoryLineService _trajectoryService;
        private readonly NoteGlowService _noteGlowService;
        private readonly NoteGridService _noteGridService;
        private readonly NoteTrackingModel _noteTrackingModel;

        private readonly SaberState _leftSaberState = new();
        private readonly SaberState _rightSaberState = new();

        // Track which notes have already triggered pre-swing haptic
        private readonly HashSet<NoteController> _preSwingTriggered = new();

        // Note grid highlight proximity threshold
        private const float GridHighlightZThreshold = 1.5f;

        // Track which notes are currently highlighting grid cubes
        private readonly Dictionary<NoteController, (int lineIndex, int lineLayer)> _gridHighlightedNotes = new();

        // Reusable buffers to avoid per-frame List allocations.
        // Safe: all usage is sequential within single-threaded Tick().
        private readonly List<Vector3> _trajectoryTargetBuffer = new(16);
        private readonly List<Vector3> _arrowDirectionBuffer = new(16);
        private readonly List<NoteController> _notesToUnhighlightBuffer = new();

        // First-tick SpawnController setup
        private bool _gridYInitialized;

        public AccFieldController(
            BeatmapObjectManager beatmapObjectManager,
            SaberManager saberManager,
            [InjectOptional] ColorManager? colorManager)
        {
            _beatmapObjectManager = beatmapObjectManager;
            _saberManager = saberManager;
            _colorManager = colorManager;

            _trajectoryService = new TrajectoryLineService();
            _noteGlowService = new NoteGlowService();
            _noteGridService = new NoteGridService();
            _noteTrackingModel = new NoteTrackingModel();
        }

        public void Initialize()
        {
            if (!PluginConfig.Instance.Enabled)
            {
                Plugin.Log?.Info("AccFieldController: Disabled by config");
                return;
            }

            // Subscribe to note events
            _beatmapObjectManager.noteWasSpawnedEvent += OnNoteSpawned;
            _beatmapObjectManager.noteWasCutEvent += OnNoteCut;
            _beatmapObjectManager.noteWasMissedEvent += OnNoteMissed;

            // Get note colors from the game's color scheme
            Color? leftColor = null;
            Color? rightColor = null;
            if (_colorManager != null)
            {
                leftColor = _colorManager.ColorForSaberType(SaberType.SaberA);
                rightColor = _colorManager.ColorForSaberType(SaberType.SaberB);
                Plugin.Log?.Info($"Note colors from ColorManager: Left=({leftColor.Value.r:F2},{leftColor.Value.g:F2},{leftColor.Value.b:F2}), Right=({rightColor.Value.r:F2},{rightColor.Value.g:F2},{rightColor.Value.b:F2})");
            }

            // Initialize services with colors (grid starts with default Y, corrected on first tick)
            _trajectoryService.Initialize(leftColor, rightColor);
            _noteGridService.Initialize(leftColor, rightColor);

            Plugin.Log?.Info("AccFieldController initialized");
        }

        private void OnNoteSpawned(NoteController note)
        {
            _noteTrackingModel.OnNoteSpawned(note);
        }

        private void OnNoteCut(NoteController note, in NoteCutInfo info)
        {
            _noteTrackingModel.OnNoteCut(note);
            _preSwingTriggered.Remove(note);
            _noteGlowService.RemoveGlow(note);
            ResetNoteGridHighlight(note);
        }

        private void OnNoteMissed(NoteController note)
        {
            _noteTrackingModel.OnNoteMissed(note);
            _preSwingTriggered.Remove(note);
            _noteGlowService.RemoveGlow(note);
            ResetNoteGridHighlight(note);
        }

        public void Tick()
        {
            var config = PluginConfig.Instance;
            if (!config.Enabled)
                return;

            // On first tick, find SpawnController and correct grid Y positions
            if (!_gridYInitialized)
            {
                _gridYInitialized = true;
                InitializeGridYFromSpawnController();
            }

            // Update saber states
            UpdateSaberStates();

            // Process pre-swing for both sabers
            ProcessPreSwing(SaberType.SaberA, _leftSaberState, config);
            ProcessPreSwing(SaberType.SaberB, _rightSaberState, config);

            // Update glow effects (pulse animation)
            _noteGlowService.Update();

            // Update trajectory lines
            UpdateTrajectoryLines();

            // Update note grid guide
            _noteGridService.Update();

            // Update note grid highlighting based on note proximity
            UpdateNoteGridHighlights(config);
        }

        private void UpdateSaberStates()
        {
            if (_saberManager?.leftSaber != null)
            {
                var leftSaber = _saberManager.leftSaber;
                _leftSaberState.UpdateState(
                    leftSaber.saberBladeTopPos,
                    leftSaber.transform.rotation,
                    Time.time);
            }

            if (_saberManager?.rightSaber != null)
            {
                var rightSaber = _saberManager.rightSaber;
                _rightSaberState.UpdateState(
                    rightSaber.saberBladeTopPos,
                    rightSaber.transform.rotation,
                    Time.time);
            }
        }

        private void ProcessPreSwing(SaberType saberType, SaberState saberState, PluginConfig config)
        {
            // Find the nearest note for this saber
            var nearestNote = _noteTrackingModel.GetNearestNote(saberType, saberState.CurrentPosition);
            if (nearestNote == null)
                return;

            // Skip if we already triggered for this note
            if (_preSwingTriggered.Contains(nearestNote))
                return;

            // Get the cut direction for this note
            Vector3 cutDirection = _noteTrackingModel.GetNoteCutDirection(nearestNote);

            // Calculate pre-swing angle using the saber's blade direction
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(
                saberState.BladeDirection,
                cutDirection);

            // Check if threshold is met
            if (SwingAngleCalculator.MeetsPreSwingThreshold(angle))
            {
                if (config.PreSwingGlowEnabled)
                {
                    _noteGlowService.ApplyGlow(nearestNote);
                }
                _preSwingTriggered.Add(nearestNote);
            }
        }

        private void UpdateTrajectoryLines()
        {
            // Update left saber trajectory with multiple notes support
            UpdateSaberTrajectory(SaberType.SaberA, _leftSaberState.CurrentPosition);

            // Update right saber trajectory with multiple notes support
            UpdateSaberTrajectory(SaberType.SaberB, _rightSaberState.CurrentPosition);
        }

        private void UpdateSaberTrajectory(SaberType saberType, Vector3 saberPosition)
        {
            var notes = _noteTrackingModel.GetAllNotesForSaber(saberType, saberPosition);

            if (notes.Count == 0)
            {
                _trajectoryService.HideAllLines(saberType);
                return;
            }

            _trajectoryTargetBuffer.Clear();
            _arrowDirectionBuffer.Clear();
            foreach (var note in notes)
            {
                _trajectoryTargetBuffer.Add(note.noteTransform?.position ?? Vector3.zero);
                _arrowDirectionBuffer.Add(_noteTrackingModel.GetNoteArrowDirection(note));
            }

            _trajectoryService.UpdateTrajectories(saberType, saberPosition, _trajectoryTargetBuffer, _arrowDirectionBuffer);
        }

        private void UpdateNoteGridHighlights(PluginConfig config)
        {
            if (!config.ShowNotesGrid)
                return;

            float gridZ = _noteGridService.CurrentZOffset;
            var activeNotes = _noteTrackingModel.GetActiveNotes();

            // Calibrate Y from notes passing near grid Z, and unhighlight those that passed
            _notesToUnhighlightBuffer.Clear();
            foreach (var kvp in _gridHighlightedNotes)
            {
                var note = kvp.Key;
                if (note == null || note.noteTransform == null)
                {
                    _notesToUnhighlightBuffer.Add(note!);
                    continue;
                }

                float noteZ = note.noteTransform.position.z;

                // When note is near saber Z=0, use its Y for calibration
                if (noteZ >= -0.3f && noteZ <= 0.3f)
                {
                    int lineLayer = kvp.Value.lineLayer;
                    float noteY = note.noteTransform.position.y;
                    _noteGridService.CalibrateYFromNote(lineLayer, noteY);
                }

                if (noteZ < -0.1f)
                    _notesToUnhighlightBuffer.Add(note);
            }

            foreach (var note in _notesToUnhighlightBuffer)
            {
                if (_gridHighlightedNotes.TryGetValue(note, out var pos))
                {
                    _noteGridService.ResetCube(pos.lineIndex, pos.lineLayer);
                    _gridHighlightedNotes.Remove(note);
                }
            }

            // Highlight notes approaching the grid
            foreach (var note in activeNotes)
            {
                if (note?.noteData == null || note.noteTransform == null)
                    continue;

                if (_gridHighlightedNotes.ContainsKey(note))
                    continue;

                float noteZ = note.noteTransform.position.z;
                if (noteZ >= -0.1f && noteZ <= GridHighlightZThreshold)
                {
                    int lineIndex = note.noteData.lineIndex;
                    int lineLayer = (int)note.noteData.noteLineLayer;
                    bool isLeft = note.noteData.colorType == ColorType.ColorA;

                    if (config.NotesGridDebugLog)
                    {
                        var notePosition = note.noteTransform.position;
                        var side = isLeft ? "L" : "R";
                        Plugin.Log?.Info($"  NoteHighlight[{side} line={lineIndex},layer={lineLayer}] pos=({notePosition.x:F3}, {notePosition.y:F3}, {notePosition.z:F3})");
                    }

                    _noteGridService.HighlightCube(lineIndex, lineLayer, isLeft);
                    _gridHighlightedNotes[note] = (lineIndex, lineLayer);
                }
            }
        }

        private void ResetNoteGridHighlight(NoteController note)
        {
            if (_gridHighlightedNotes.TryGetValue(note, out var pos))
            {
                _noteGridService.ResetCube(pos.lineIndex, pos.lineLayer);
                _gridHighlightedNotes.Remove(note);
            }
        }

        private void InitializeGridYFromSpawnController()
        {
            try
            {
                var spawnCtrl = UnityEngine.Object.FindObjectOfType<BeatmapObjectSpawnController>();
                if (spawnCtrl == null)
                {
                    Plugin.Log?.Warn("SpawnController not found on first tick");
                    return;
                }

                var spawnData = spawnCtrl.beatmapObjectSpawnMovementData;
                var center = spawnData.centerPos;
                float jumpOffsetY = spawnCtrl.jumpOffsetY;
                float gridZ = _noteGridService.CurrentZOffset;

                bool debugLog = PluginConfig.Instance.NotesGridDebugLog;
                if (debugLog)
                    Plugin.Log?.Info($"SpawnController found: centerPos=({center.x:F3}, {center.y:F3}, {center.z:F3}), jumpOffsetY={jumpOffsetY:F3}, gridZ={gridZ:F3}");

                for (int layer = 0; layer < 3; layer++)
                {
                    float yAtDistance = spawnData.JumpPosYForLineLayerAtDistanceFromPlayerWithoutJumpOffset(
                        (NoteLineLayer)layer, gridZ);
                    float finalY = yAtDistance + jumpOffsetY;
                    if (debugLog)
                        Plugin.Log?.Info($"  Layer {layer}: yAtDistance={yAtDistance:F3}, +jumpOffset={jumpOffsetY:F3} => Y={finalY:F3}");
                    _noteGridService.CalibrateYFromNote(layer, finalY);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"Failed to init grid Y from SpawnController: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Plugin.Log?.Info("AccFieldController.Dispose called");

            // Unsubscribe from events
            if (_beatmapObjectManager != null)
            {
                _beatmapObjectManager.noteWasSpawnedEvent -= OnNoteSpawned;
                _beatmapObjectManager.noteWasCutEvent -= OnNoteCut;
                _beatmapObjectManager.noteWasMissedEvent -= OnNoteMissed;
            }

            // Dispose services
            _trajectoryService.Dispose();
            _noteGlowService.Dispose();
            _noteGridService.Dispose();

            // Clear tracking data
            _noteTrackingModel.Clear();
            _preSwingTriggered.Clear();
            _gridHighlightedNotes.Clear();
            _gridYInitialized = false;
        }
    }
}
