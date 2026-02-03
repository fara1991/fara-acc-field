using System.Collections.Generic;
using UnityEngine;

namespace FaraAccSupporter.Models
{
    /// <summary>
    /// Tracks active notes and provides methods to find the nearest note for each saber.
    /// </summary>
    internal class NoteTrackingModel
    {
        private readonly List<NoteController> _activeNotes = new();
        private readonly object _lock = new();

        // Maximum number of notes to track per saber
        private const int MaxNotesPerSaber = 4;

        // Distance threshold for grouping "simultaneous" notes (notes at similar Z position)
        private const float SimultaneousNoteZThreshold = 0.3f;

        /// <summary>
        /// Gets the nearest upcoming note for the specified saber type.
        /// </summary>
        /// <param name="saberType">Which saber (left=SaberA/red notes, right=SaberB/blue notes)</param>
        /// <param name="saberPosition">Current saber position</param>
        /// <returns>The nearest note controller, or null if none found</returns>
        public NoteController? GetNearestNote(SaberType saberType, Vector3 saberPosition)
        {
            var notes = GetNearestNotes(saberType, saberPosition, 1);
            return notes.Count > 0 ? notes[0] : null;
        }

        /// <summary>
        /// Gets multiple nearest upcoming notes for the specified saber type.
        /// Groups notes that are at similar Z positions (simultaneous notes).
        /// </summary>
        /// <param name="saberType">Which saber (left=SaberA/red notes, right=SaberB/blue notes)</param>
        /// <param name="saberPosition">Current saber position</param>
        /// <param name="maxCount">Maximum number of notes to return</param>
        /// <returns>List of nearest note controllers, sorted by distance</returns>
        public List<NoteController> GetNearestNotes(SaberType saberType, Vector3 saberPosition, int maxCount = MaxNotesPerSaber)
        {
            var result = new List<NoteController>();
            var candidates = new List<(NoteController note, float distance, float z)>();

            lock (_lock)
            {
                foreach (var note in _activeNotes)
                {
                    if (note == null || note.noteData == null)
                        continue;

                    // Match saber type to note color
                    if ((int)note.noteData.colorType != (int)saberType)
                        continue;

                    Vector3 notePos = note.noteTransform.position;

                    // Only consider notes ahead of the saber
                    if (notePos.z < saberPosition.z - 0.5f)
                        continue;

                    float distance = Vector3.Distance(saberPosition, notePos);
                    candidates.Add((note, distance, notePos.z));
                }
            }

            if (candidates.Count == 0)
                return result;

            // Sort by Z position (closest first), then by distance
            candidates.Sort((a, b) =>
            {
                int zCompare = a.z.CompareTo(b.z);
                if (zCompare != 0) return zCompare;
                return a.distance.CompareTo(b.distance);
            });

            // Get the nearest note's Z position
            float nearestZ = candidates[0].z;

            // Include all notes within the Z threshold of the nearest note
            foreach (var (note, distance, z) in candidates)
            {
                if (result.Count >= maxCount)
                    break;

                // Include notes that are at similar Z position (simultaneous)
                if (z - nearestZ <= SimultaneousNoteZThreshold)
                {
                    result.Add(note);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the cut direction vector for a note.
        /// </summary>
        public Vector3 GetNoteCutDirection(NoteController note)
        {
            if (note?.noteData == null)
                return Vector3.forward;

            // Get the world-space cut direction based on note's rotation
            Vector3 localCutDir = GetLocalCutDirection(note.noteData.cutDirection);

            // Transform by note's rotation to get world direction
            return note.noteTransform.rotation * localCutDir;
        }

        /// <summary>
        /// Gets the local cut direction vector based on NoteCutDirection enum.
        /// </summary>
        private Vector3 GetLocalCutDirection(NoteCutDirection cutDirection)
        {
            return cutDirection switch
            {
                NoteCutDirection.Up => Vector3.down,           // Swing down to cut up arrow
                NoteCutDirection.Down => Vector3.up,           // Swing up to cut down arrow
                NoteCutDirection.Left => Vector3.right,        // Swing right to cut left arrow
                NoteCutDirection.Right => Vector3.left,        // Swing left to cut right arrow
                NoteCutDirection.UpLeft => new Vector3(1, -1, 0).normalized,
                NoteCutDirection.UpRight => new Vector3(-1, -1, 0).normalized,
                NoteCutDirection.DownLeft => new Vector3(1, 1, 0).normalized,
                NoteCutDirection.DownRight => new Vector3(-1, 1, 0).normalized,
                NoteCutDirection.Any => Vector3.forward,       // Dot notes - any direction
                _ => Vector3.forward
            };
        }

        /// <summary>
        /// Gets the center position of a note.
        /// </summary>
        public Vector3 GetNoteCenterPosition(NoteController note)
        {
            return note?.noteTransform?.position ?? Vector3.zero;
        }

        /// <summary>
        /// Checks if a note is in an outer column (lineIndex 0 or 3).
        /// Outer notes allow for Time Independent cuts.
        /// </summary>
        public bool IsOuterNote(NoteController note)
        {
            if (note?.noteData == null)
                return false;

            int lineIndex = note.noteData.lineIndex;
            return lineIndex == 0 || lineIndex == 3;
        }

        /// <summary>
        /// Gets the optimal target position for achieving center accuracy.
        /// For outer notes (TI), returns a position that suggests cutting along the Z-axis.
        /// For inner notes (TD), returns the note's current center.
        /// </summary>
        /// <param name="note">The target note</param>
        /// <param name="saberPosition">Current saber position</param>
        /// <returns>Optimal target position and whether it's a TI note</returns>
        public (Vector3 targetPosition, bool isTimeIndependent) GetOptimalTargetPosition(
            NoteController note,
            Vector3 saberPosition)
        {
            if (note?.noteTransform == null)
                return (Vector3.zero, false);

            Vector3 noteCenter = note.noteTransform.position;
            bool isOuter = IsOuterNote(note);

            if (!isOuter)
            {
                // Inner note (TD): target the note's current center
                // Timing is important for these
                return (noteCenter, false);
            }

            // Outer note (TI): calculate optimal approach for time-independent cut
            // The key is to cut with a swing plane parallel to the Z-axis
            // This means approaching from the side (X direction) rather than front

            // Get the note's X position (which side it's on)
            float noteX = noteCenter.x;
            float saberX = saberPosition.x;

            // Calculate a target point that encourages cutting along Z-axis
            // The target should be at the same X as the note, but the approach
            // should guide the saber to swing parallel to Z

            // For TI cuts, we want to show where to position the saber
            // so that when it swings through, the cut plane is parallel to Z
            Vector3 cutDirection = GetNoteCutDirection(note);

            // Project the cut direction onto the XY plane (remove Z component)
            // This gives us the "time independent" swing direction
            Vector3 tiSwingDirection = new Vector3(cutDirection.x, cutDirection.y, 0).normalized;

            // If the cut direction is purely in Z, fall back to note center
            if (tiSwingDirection.sqrMagnitude < 0.01f)
            {
                return (noteCenter, true);
            }

            // Calculate an approach point that's offset from the note center
            // in the direction opposite to the swing, at the saber's current Z
            float approachDistance = 0.3f; // Distance from note center to show approach
            Vector3 approachOffset = -tiSwingDirection * approachDistance;

            // The target point guides the saber to approach from the correct angle
            // Keep the Z at the note's Z so the line points to where to cut
            Vector3 tiTarget = noteCenter + approachOffset;
            tiTarget.z = noteCenter.z; // Target the note's Z plane

            return (tiTarget, true);
        }

        /// <summary>
        /// Gets the line index (column) of a note.
        /// 0 = far left, 1 = center-left, 2 = center-right, 3 = far right
        /// </summary>
        public int GetNoteLineIndex(NoteController note)
        {
            return note?.noteData?.lineIndex ?? -1;
        }

        /// <summary>
        /// Called when a note is spawned.
        /// </summary>
        public void OnNoteSpawned(NoteController note)
        {
            if (note?.noteData == null)
                return;

            // Only track normal notes (not bombs, etc.)
            if (note.noteData.gameplayType != NoteData.GameplayType.Normal)
                return;

            lock (_lock)
            {
                if (!_activeNotes.Contains(note))
                    _activeNotes.Add(note);
            }
        }

        /// <summary>
        /// Called when a note is cut.
        /// </summary>
        public void OnNoteCut(NoteController note)
        {
            lock (_lock)
            {
                _activeNotes.Remove(note);
            }
        }

        /// <summary>
        /// Called when a note is missed.
        /// </summary>
        public void OnNoteMissed(NoteController note)
        {
            lock (_lock)
            {
                _activeNotes.Remove(note);
            }
        }

        /// <summary>
        /// Clears all tracked notes.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _activeNotes.Clear();
            }
        }

        /// <summary>
        /// Gets the count of currently tracked notes.
        /// </summary>
        public int ActiveNoteCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeNotes.Count;
                }
            }
        }
    }
}
