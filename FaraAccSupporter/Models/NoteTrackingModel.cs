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

        // Reusable buffers to avoid per-frame allocations in GetNearestNotes
        private readonly List<NoteController> _nearestResult = new();
        private readonly List<(NoteController note, float distance, float z)> _nearestCandidates = new();

        // Reusable buffer for GetActiveNotes snapshot
        private readonly List<NoteController> _activeNotesSnapshot = new();

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
            _nearestResult.Clear();
            _nearestCandidates.Clear();

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
                    _nearestCandidates.Add((note, distance, notePos.z));
                }
            }

            if (_nearestCandidates.Count == 0)
                return _nearestResult;

            // Sort by Z position (closest first), then by distance
            _nearestCandidates.Sort((a, b) =>
            {
                int zCompare = a.z.CompareTo(b.z);
                if (zCompare != 0) return zCompare;
                return a.distance.CompareTo(b.distance);
            });

            // Get the nearest note's Z position
            float nearestZ = _nearestCandidates[0].z;

            // Include all notes within the Z threshold of the nearest note
            foreach (var (note, distance, z) in _nearestCandidates)
            {
                if (_nearestResult.Count >= maxCount)
                    break;

                // Include notes that are at similar Z position (simultaneous)
                if (z - nearestZ <= SimultaneousNoteZThreshold)
                {
                    _nearestResult.Add(note);
                }
            }

            return _nearestResult;
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
        /// Gets a snapshot of all active notes for iteration.
        /// </summary>
        public List<NoteController> GetActiveNotes()
        {
            lock (_lock)
            {
                _activeNotesSnapshot.Clear();
                _activeNotesSnapshot.AddRange(_activeNotes);
                return _activeNotesSnapshot;
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
