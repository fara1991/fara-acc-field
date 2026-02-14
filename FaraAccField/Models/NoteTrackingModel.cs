using System.Collections.Generic;
using UnityEngine;

namespace FaraAccField.Models
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

        // Reusable buffer for GetAllNotesForSaber
        private readonly List<NoteController> _allForSaberResult = new();

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
        /// <remarks>
        /// Returns a shared internal buffer. Contents are only valid until the next call
        /// to this method. Do not cache the returned list across frames.
        /// </remarks>
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
        /// Gets all active notes for the specified saber type, sorted by Z (closest first).
        /// Unlike GetNearestNotes, this returns every note ahead of the saber with no count limit.
        /// </summary>
        /// <remarks>
        /// Returns a shared internal buffer. Contents are only valid until the next call
        /// to this method. Do not cache the returned list across frames.
        /// </remarks>
        public List<NoteController> GetAllNotesForSaber(SaberType saberType, Vector3 saberPosition)
        {
            _allForSaberResult.Clear();

            lock (_lock)
            {
                foreach (var note in _activeNotes)
                {
                    if (note == null || note.noteData == null)
                        continue;

                    if ((int)note.noteData.colorType != (int)saberType)
                        continue;

                    Vector3 notePos = note.noteTransform.position;
                    if (notePos.z < saberPosition.z - 0.5f)
                        continue;

                    _allForSaberResult.Add(note);
                }
            }

            _allForSaberResult.Sort((a, b) =>
                a.noteTransform.position.z.CompareTo(b.noteTransform.position.z));

            return _allForSaberResult;
        }

        /// <summary>
        /// Gets the cut direction vector for a note in world space.
        /// The note mesh arrow points in local Vector3.down, and noteTransform.rotation
        /// encodes the cut direction, so rotation * down gives the swing direction.
        /// </summary>
        public Vector3 GetNoteCutDirection(NoteController note)
        {
            if (note?.noteData == null)
                return Vector3.forward;

            if (note.noteData.cutDirection == NoteCutDirection.Any)
                return Vector3.forward;

            return note.noteTransform.rotation * Vector3.down;
        }

        /// <summary>
        /// Gets the visual arrow direction for a note in world space.
        /// Returns Vector3.zero for dot notes (NoteCutDirection.Any).
        /// </summary>
        public Vector3 GetNoteArrowDirection(NoteController note)
        {
            if (note?.noteData == null)
                return Vector3.zero;

            if (note.noteData.cutDirection == NoteCutDirection.Any)
                return Vector3.zero;

            return note.noteTransform.rotation * Vector3.down;
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
        /// <remarks>
        /// Returns a shared internal buffer. Contents are only valid until the next call
        /// to this method. Do not cache the returned list across frames.
        /// </remarks>
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
