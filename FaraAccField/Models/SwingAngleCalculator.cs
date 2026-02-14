using UnityEngine;

namespace FaraAccField.Models
{
    /// <summary>
    /// Calculates swing angles for pre-swing and follow-through scoring.
    /// Beat Saber scoring:
    /// - Pre-swing: 0-100 degrees = 0-70 points (linear)
    /// - Follow-through: 0-60 degrees = 0-30 points (linear)
    /// </summary>
    internal static class SwingAngleCalculator
    {
        /// <summary>
        /// Pre-swing angle required for maximum 70 points
        /// </summary>
        public const float PreSwingMaxAngle = 100f;

        /// <summary>
        /// Maximum points from pre-swing
        /// </summary>
        public const float PreSwingMaxPoints = 70f;

        /// <summary>
        /// Follow-through angle required for maximum 30 points
        /// </summary>
        public const float FollowThroughMaxAngle = 60f;

        /// <summary>
        /// Maximum points from follow-through
        /// </summary>
        public const float FollowThroughMaxPoints = 30f;

        /// <summary>
        /// Calculates the angle between saber swing direction and note cut direction.
        /// This represents how much the saber has "wound up" before hitting the note.
        /// </summary>
        /// <param name="saberDirection">Current direction the saber blade is pointing</param>
        /// <param name="noteCutDirection">The direction the note should be cut</param>
        /// <returns>Angle in degrees (0-180)</returns>
        public static float CalculatePreSwingAngle(Vector3 saberDirection, Vector3 noteCutDirection)
        {
            if (saberDirection.sqrMagnitude < 0.001f || noteCutDirection.sqrMagnitude < 0.001f)
                return 0f;

            // The pre-swing angle is the angle between the saber's current direction
            // and the OPPOSITE of the cut direction (because you wind up in the opposite direction)
            Vector3 windupDirection = -noteCutDirection;
            float dot = Vector3.Dot(saberDirection.normalized, windupDirection.normalized);
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

            // Convert to "how much we've wound up" - 0 means pointing away, 180 means fully wound up
            return 180f - angle;
        }

        /// <summary>
        /// Checks if the current pre-swing angle meets the 70 point threshold (100 degrees)
        /// </summary>
        public static bool MeetsPreSwingThreshold(float angle)
        {
            return angle >= PreSwingMaxAngle;
        }

        /// <summary>
        /// Calculates estimated pre-swing points based on angle
        /// </summary>
        public static float CalculatePreSwingPoints(float angle)
        {
            float normalizedAngle = Mathf.Clamp01(angle / PreSwingMaxAngle);
            return normalizedAngle * PreSwingMaxPoints;
        }

        /// <summary>
        /// Calculates the follow-through angle after cutting a note.
        /// </summary>
        /// <param name="saberDirection">Current direction the saber blade is pointing after cut</param>
        /// <param name="noteCutDirection">The direction the note was supposed to be cut</param>
        /// <returns>Angle in degrees representing follow-through</returns>
        public static float CalculateFollowThroughAngle(Vector3 saberDirection, Vector3 noteCutDirection)
        {
            if (saberDirection.sqrMagnitude < 0.001f || noteCutDirection.sqrMagnitude < 0.001f)
                return 0f;

            // Follow-through angle is how much the saber has continued in the cut direction
            float dot = Vector3.Dot(saberDirection.normalized, noteCutDirection.normalized);
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

            // Convert: 0 degrees from cut direction = maximum follow-through
            return 180f - angle;
        }

        /// <summary>
        /// Checks if the current follow-through angle meets the 30 point threshold (60 degrees)
        /// </summary>
        public static bool MeetsFollowThroughThreshold(float angle)
        {
            return angle >= FollowThroughMaxAngle;
        }

        /// <summary>
        /// Calculates estimated follow-through points based on angle
        /// </summary>
        public static float CalculateFollowThroughPoints(float angle)
        {
            float normalizedAngle = Mathf.Clamp01(angle / FollowThroughMaxAngle);
            return normalizedAngle * FollowThroughMaxPoints;
        }
    }
}
