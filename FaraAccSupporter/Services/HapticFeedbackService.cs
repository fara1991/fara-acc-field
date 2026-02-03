using FaraAccSupporter.Configuration;
using UnityEngine;
using UnityEngine.XR;

namespace FaraAccSupporter.Services
{
    /// <summary>
    /// Handles haptic feedback (controller vibration) for follow-through events.
    /// Pre-swing feedback is now handled by NoteGlowService.
    /// </summary>
    internal class HapticFeedbackService
    {
        private readonly HapticFeedbackController? _hapticController;

        private float _lastLeftFollowThroughTime = 0f;
        private float _lastRightFollowThroughTime = 0f;

        private const float FollowThroughCooldown = 0.1f;
        private const float FollowThroughDuration = 0.08f;

        public HapticFeedbackService(HapticFeedbackController? hapticController)
        {
            _hapticController = hapticController;
        }

        /// <summary>
        /// Triggers haptic feedback when follow-through threshold is met.
        /// </summary>
        public void TriggerFollowThroughHaptic(SaberType saberType)
        {
            if (!PluginConfig.Instance.VibrationEnabled)
                return;

            float currentTime = Time.time;
            bool isLeft = saberType == SaberType.SaberA;

            // Check cooldown
            if (isLeft && currentTime - _lastLeftFollowThroughTime < FollowThroughCooldown)
                return;
            if (!isLeft && currentTime - _lastRightFollowThroughTime < FollowThroughCooldown)
                return;

            float strength = PluginConfig.Instance.FollowThroughVibrationStrength;
            TriggerHaptic(saberType, strength, FollowThroughDuration);

            if (isLeft)
                _lastLeftFollowThroughTime = currentTime;
            else
                _lastRightFollowThroughTime = currentTime;
        }

        private void TriggerHaptic(SaberType saberType, float strength, float duration)
        {
            var node = saberType == SaberType.SaberA ? XRNode.LeftHand : XRNode.RightHand;

            // Try using Unity's XR input system directly
            if (TryTriggerXRHaptic(node, strength, duration))
                return;

            // Fallback to Beat Saber's haptic controller if available
            if (_hapticController != null)
            {
                try
                {
                    // Use reflection to call the method if signature differs
                    var method = _hapticController.GetType().GetMethod("PlayHapticFeedback");
                    if (method != null)
                    {
                        // Different versions may have different signatures
                        var parameters = method.GetParameters();
                        if (parameters.Length == 2)
                        {
                            method.Invoke(_hapticController, new object[] { node, duration });
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.Warn($"Failed to trigger haptic via controller: {ex.Message}");
                }
            }
        }

        private bool TryTriggerXRHaptic(XRNode node, float amplitude, float duration)
        {
            try
            {
                var device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid)
                {
                    // SendHapticImpulse takes channel (0), amplitude, and duration
                    device.SendHapticImpulse(0, amplitude, duration);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.Debug($"XR haptic failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Resets cooldown timers.
        /// </summary>
        public void Reset()
        {
            _lastLeftFollowThroughTime = 0f;
            _lastRightFollowThroughTime = 0f;
        }
    }
}
