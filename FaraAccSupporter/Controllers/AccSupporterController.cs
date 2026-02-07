using System;
using System.Collections.Generic;
using FaraAccSupporter.Configuration;
using FaraAccSupporter.Models;
using FaraAccSupporter.Patches;
using FaraAccSupporter.Services;
using UnityEngine;
using Zenject;

namespace FaraAccSupporter.Controllers
{
    /// <summary>
    /// Main controller that coordinates pre-swing detection, follow-through tracking,
    /// trajectory line display, and haptic feedback during gameplay.
    /// </summary>
    internal class AccSupporterController : IInitializable, ITickable, IDisposable
    {
        private readonly BeatmapObjectManager _beatmapObjectManager;
        private readonly SaberManager _saberManager;
        private readonly HapticFeedbackService _hapticService;
        private readonly TrajectoryLineService _trajectoryService;
        private readonly NoteGlowService _noteGlowService;
        private readonly NoteGridService _noteGridService;
        private readonly NoteTrackingModel _noteTrackingModel;

        private readonly SaberState _leftSaberState = new();
        private readonly SaberState _rightSaberState = new();

        // Track which notes have already triggered pre-swing haptic
        private readonly HashSet<NoteController> _preSwingTriggered = new();

        // Track active cuts for follow-through detection
        private readonly Dictionary<NoteController, CutTrackingData> _activeCuts = new();

        // Follow-through tracking timeout
        private const float FollowThroughTimeout = 0.4f;

        // Note grid highlight proximity threshold
        private const float GridHighlightZThreshold = 1.5f;

        // Track which notes are currently highlighting grid cubes
        private readonly Dictionary<NoteController, (int lineIndex, int lineLayer)> _gridHighlightedNotes = new();

        // First-tick SpawnController setup
        private bool _gridYInitialized;

        public AccSupporterController(
            BeatmapObjectManager beatmapObjectManager,
            SaberManager saberManager,
            [InjectOptional] HapticFeedbackController? hapticController)
        {
            _beatmapObjectManager = beatmapObjectManager;
            _saberManager = saberManager;

            _hapticService = new HapticFeedbackService(hapticController);
            _trajectoryService = new TrajectoryLineService();
            _noteGlowService = new NoteGlowService();
            _noteGridService = new NoteGridService();
            _noteTrackingModel = new NoteTrackingModel();
        }

        public void Initialize()
        {
            if (!PluginConfig.Instance.Enabled)
            {
                Plugin.Log?.Info("AccSupporterController: Disabled by config");
                return;
            }

            // Subscribe to note events
            _beatmapObjectManager.noteWasSpawnedEvent += OnNoteSpawned;
            _beatmapObjectManager.noteWasCutEvent += OnNoteCut;
            _beatmapObjectManager.noteWasMissedEvent += OnNoteMissed;

            // Subscribe to Harmony patch event for follow-through
            NoteCutPatch.OnNoteCutEvent += HandleNoteCutForFollowThrough;

            // Initialize services (grid starts with default Y, corrected on first tick)
            _trajectoryService.Initialize();
            _noteGridService.Initialize();

            Plugin.Log?.Info("AccSupporterController initialized");
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

        /// <summary>
        /// Called from Harmony patch when a note is cut.
        /// Starts follow-through tracking.
        /// </summary>
        private void HandleNoteCutForFollowThrough(NoteController note, NoteCutInfo cutInfo)
        {
            if (note?.noteData == null)
                return;

            // Only track normal notes
            if (note.noteData.gameplayType != NoteData.GameplayType.Normal)
                return;

            // Store cut data for follow-through tracking
            _activeCuts[note] = new CutTrackingData
            {
                Note = note,
                CutInfo = cutInfo,
                CutDirection = _noteTrackingModel.GetNoteCutDirection(note),
                StartTime = Time.time,
                FollowThroughTriggered = false
            };
        }

        public void Tick()
        {
            if (!PluginConfig.Instance.Enabled)
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
            ProcessPreSwing(SaberType.SaberA, _leftSaberState);
            ProcessPreSwing(SaberType.SaberB, _rightSaberState);

            // Process follow-through for active cuts
            ProcessFollowThrough();

            // Update glow effects (pulse animation)
            _noteGlowService.Update();

            // Update trajectory lines
            UpdateTrajectoryLines();

            // Update note grid guide
            _noteGridService.Update();

            // Update note grid highlighting based on note proximity
            UpdateNoteGridHighlights();
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

        private void ProcessPreSwing(SaberType saberType, SaberState saberState)
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
                // Apply glow effect to the note (instead of haptic)
                if (PluginConfig.Instance.PreSwingGlowEnabled)
                {
                    _noteGlowService.ApplyGlow(nearestNote);
                }
                _preSwingTriggered.Add(nearestNote);
            }
        }

        private void ProcessFollowThrough()
        {
            var completedCuts = new List<NoteController>();
            float currentTime = Time.time;

            foreach (var kvp in _activeCuts)
            {
                var tracking = kvp.Value;

                // Check for timeout
                if (currentTime - tracking.StartTime > FollowThroughTimeout)
                {
                    completedCuts.Add(kvp.Key);
                    continue;
                }

                // Skip if already triggered
                if (tracking.FollowThroughTriggered)
                    continue;

                // Get the saber state based on which saber made the cut
                var saberState = tracking.CutInfo.saberType == SaberType.SaberA
                    ? _leftSaberState
                    : _rightSaberState;

                // Calculate follow-through angle
                float angle = SwingAngleCalculator.CalculateFollowThroughAngle(
                    saberState.BladeDirection,
                    tracking.CutDirection);

                // Check if threshold is met
                if (SwingAngleCalculator.MeetsFollowThroughThreshold(angle))
                {
                    _hapticService.TriggerFollowThroughHaptic(tracking.CutInfo.saberType);
                    tracking.FollowThroughTriggered = true;
                }
            }

            // Clean up completed cuts
            foreach (var note in completedCuts)
            {
                _activeCuts.Remove(note);
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
            // Get multiple nearest notes for this saber
            var notes = _noteTrackingModel.GetNearestNotes(saberType, saberPosition);

            if (notes.Count == 0)
            {
                _trajectoryService.HideAllLines(saberType);
                return;
            }

            // Build trajectory targets for each note
            var targets = new List<TrajectoryLineService.TrajectoryTarget>();
            bool tdEnabled = PluginConfig.Instance.TrajectoryTDEnabled;

            foreach (var note in notes)
            {
                Vector3 targetPos;
                bool isTI;

                if (tdEnabled)
                {
                    // TD/TI aware: use optimal position based on note type
                    (targetPos, isTI) = _noteTrackingModel.GetOptimalTargetPosition(note, saberPosition);
                }
                else
                {
                    // TD disabled: always target note center, no TI coloring
                    targetPos = note.noteTransform?.position ?? Vector3.zero;
                    isTI = false;
                }

                targets.Add(new TrajectoryLineService.TrajectoryTarget
                {
                    Position = targetPos,
                    IsTimeIndependent = isTI
                });
            }

            // Update all trajectory lines for this saber
            _trajectoryService.UpdateTrajectories(saberType, saberPosition, targets);
        }

        private void UpdateNoteGridHighlights()
        {
            if (!PluginConfig.Instance.ShowNoteGrid)
                return;

            float gridZ = _noteGridService.CurrentZOffset;
            var activeNotes = _noteTrackingModel.GetActiveNotes();

            // Calibrate Y from notes passing near grid Z, and unhighlight those that passed
            var notesToUnhighlight = new List<NoteController>();
            foreach (var kvp in _gridHighlightedNotes)
            {
                var note = kvp.Key;
                if (note == null || note.noteTransform == null)
                {
                    notesToUnhighlight.Add(note!);
                    continue;
                }

                float noteZ = note.noteTransform.position.z;

                // When note is near grid Z, use its Y for calibration
                if (noteZ >= gridZ - 0.3f && noteZ <= gridZ + 0.3f)
                {
                    int lineLayer = kvp.Value.lineLayer;
                    float noteY = note.noteTransform.position.y;
                    _noteGridService.CalibrateYFromNote(lineLayer, noteY);
                }

                if (noteZ < gridZ - 0.1f)
                    notesToUnhighlight.Add(note);
            }

            foreach (var note in notesToUnhighlight)
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
                if (noteZ >= gridZ - 0.1f && noteZ <= gridZ + GridHighlightZThreshold)
                {
                    int lineIndex = note.noteData.lineIndex;
                    int lineLayer = (int)note.noteData.noteLineLayer;
                    bool isLeft = note.noteData.colorType == ColorType.ColorA;

                    if (PluginConfig.Instance.NoteGridDebugLog)
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

                if (PluginConfig.Instance.NoteGridDebugLog)
                    Plugin.Log?.Info($"SpawnController found: centerPos=({center.x:F3}, {center.y:F3}, {center.z:F3}), jumpOffsetY={jumpOffsetY:F3}, gridZ={gridZ:F3}");

                for (int layer = 0; layer < 3; layer++)
                {
                    float yAtDistance = spawnData.JumpPosYForLineLayerAtDistanceFromPlayerWithoutJumpOffset(
                        (NoteLineLayer)layer, gridZ);
                    float finalY = yAtDistance + jumpOffsetY;
                    if (PluginConfig.Instance.NoteGridDebugLog)
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
            Plugin.Log?.Info("AccSupporterController.Dispose called");

            // Unsubscribe from events
            if (_beatmapObjectManager != null)
            {
                _beatmapObjectManager.noteWasSpawnedEvent -= OnNoteSpawned;
                _beatmapObjectManager.noteWasCutEvent -= OnNoteCut;
                _beatmapObjectManager.noteWasMissedEvent -= OnNoteMissed;
            }

            NoteCutPatch.OnNoteCutEvent -= HandleNoteCutForFollowThrough;

            // Dispose services
            _trajectoryService.Dispose();
            _noteGlowService.Dispose();
            _noteGridService.Dispose();

            // Clear tracking data
            _noteTrackingModel.Clear();
            _preSwingTriggered.Clear();
            _activeCuts.Clear();
            _gridHighlightedNotes.Clear();
            _gridYInitialized = false;

            // Reset haptic service
            _hapticService.Reset();
        }

        /// <summary>
        /// Internal class for tracking active cuts and their follow-through status.
        /// </summary>
        private class CutTrackingData
        {
            public NoteController Note = null!;
            public NoteCutInfo CutInfo;
            public Vector3 CutDirection;
            public float StartTime;
            public bool FollowThroughTriggered;
        }
    }
}
