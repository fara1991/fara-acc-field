using UnityEngine;

namespace FaraAccField.Models
{
    /// <summary>
    /// Tracks the state and movement history of a saber for angle calculations.
    /// </summary>
    internal class SaberState
    {
        private const int HistorySize = 10;
        private readonly Vector3[] _positionHistory = new Vector3[HistorySize];
        private readonly Vector3[] _directionHistory = new Vector3[HistorySize];
        private readonly float[] _timeHistory = new float[HistorySize];
        private int _historyIndex = 0;
        private int _historyCount = 0;

        /// <summary>
        /// Current saber blade tip position
        /// </summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>
        /// Direction from saber handle to blade tip (the actual blade direction).
        /// Computed from real blade endpoint positions, not from rotation * axis.
        /// </summary>
        public Vector3 BladeDirection { get; private set; }

        /// <summary>
        /// Velocity of the saber tip
        /// </summary>
        public Vector3 TipVelocity { get; private set; }

        /// <summary>
        /// Speed of the saber tip
        /// </summary>
        public float TipSpeed => TipVelocity.magnitude;

        /// <summary>
        /// Angular speed of the blade direction change (degrees per second).
        /// High values indicate active swing motion; low values indicate rest.
        /// </summary>
        public float BladeAngularSpeed { get; private set; }

        /// <summary>
        /// Updates the saber state with blade tip and handle positions.
        /// BladeDirection is computed from (tipPosition - handlePosition).
        /// </summary>
        public void UpdateState(Vector3 tipPosition, Vector3 handlePosition, float time)
        {
            int prevIndex = (_historyIndex - 1 + HistorySize) % HistorySize;

            // Compute blade direction from actual endpoint positions
            Vector3 bladeDir = tipPosition - handlePosition;
            if (bladeDir.sqrMagnitude > 0.0001f)
                bladeDir = bladeDir.normalized;
            else
                bladeDir = Vector3.up;

            // Store current state in history
            _positionHistory[_historyIndex] = tipPosition;
            _directionHistory[_historyIndex] = bladeDir;
            _timeHistory[_historyIndex] = time;

            // Update current values
            CurrentPosition = tipPosition;
            BladeDirection = bladeDir;

            // Calculate velocity and angular speed from history
            if (_historyCount > 0)
            {
                float deltaTime = time - _timeHistory[prevIndex];
                if (deltaTime > 0.0001f)
                {
                    TipVelocity = (_positionHistory[_historyIndex] - _positionHistory[prevIndex]) / deltaTime;

                    float angleDelta = Vector3.Angle(
                        _directionHistory[_historyIndex],
                        _directionHistory[prevIndex]);
                    BladeAngularSpeed = angleDelta / deltaTime;
                }
            }

            // Advance history index
            _historyIndex = (_historyIndex + 1) % HistorySize;
            if (_historyCount < HistorySize)
                _historyCount++;
        }

        /// <summary>
        /// Gets the average blade direction over the last few frames for smoother calculations.
        /// </summary>
        public Vector3 GetAverageBladeDirection(int frames = 3)
        {
            if (_historyCount == 0)
                return BladeDirection;

            frames = Mathf.Min(frames, _historyCount);
            Vector3 sum = Vector3.zero;

            for (int i = 0; i < frames; i++)
            {
                int index = (_historyIndex - 1 - i + HistorySize) % HistorySize;
                sum += _directionHistory[index];
            }

            return (sum / frames).normalized;
        }

        /// <summary>
        /// Resets the state history.
        /// </summary>
        public void Reset()
        {
            _historyIndex = 0;
            _historyCount = 0;
            TipVelocity = Vector3.zero;
            BladeAngularSpeed = 0f;
        }
    }
}
