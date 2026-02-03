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
        private readonly NoteTrackingModel _noteTrackingModel;

        private readonly SaberState _leftSaberState = new();
        private readonly SaberState _rightSaberState = new();

        // Track which notes have already triggered pre-swing haptic
        private readonly HashSet<NoteController> _preSwingTriggered = new();

        // Track active cuts for follow-through detection
        private readonly Dictionary<NoteController, CutTrackingData> _activeCuts = new();

        // Follow-through tracking timeout
        private const float FollowThroughTimeout = 0.4f;

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

            // Initialize trajectory line service
            _trajectoryService.Initialize();

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
        }

        private void OnNoteMissed(NoteController note)
        {
            _noteTrackingModel.OnNoteMissed(note);
            _preSwingTriggered.Remove(note);
            _noteGlowService.RemoveGlow(note);
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
            foreach (var note in notes)
            {
                var (targetPos, isTI) = _noteTrackingModel.GetOptimalTargetPosition(note, saberPosition);
                targets.Add(new TrajectoryLineService.TrajectoryTarget
                {
                    Position = targetPos,
                    IsTimeIndependent = isTI
                });
            }

            // Update all trajectory lines for this saber
            _trajectoryService.UpdateTrajectories(saberType, saberPosition, targets);
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

            // Clear tracking data
            _noteTrackingModel.Clear();
            _preSwingTriggered.Clear();
            _activeCuts.Clear();

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
