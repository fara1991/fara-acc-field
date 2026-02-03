using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace FaraAccSupporter.Configuration
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; } = null!;

        /// <summary>
        /// Whether the mod is enabled
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether haptic/vibration feedback is enabled
        /// </summary>
        public virtual bool VibrationEnabled { get; set; } = true;

        /// <summary>
        /// Whether to show trajectory line from saber to note
        /// </summary>
        public virtual bool ShowTrajectoryLine { get; set; } = true;

        /// <summary>
        /// Whether to glow notes when pre-swing threshold is reached
        /// </summary>
        public virtual bool PreSwingGlowEnabled { get; set; } = true;

        /// <summary>
        /// Follow-through vibration strength (0.0 - 1.0)
        /// </summary>
        public virtual float FollowThroughVibrationStrength { get; set; } = 0.5f;

        /// <summary>
        /// Called when config changes
        /// </summary>
        public virtual void Changed()
        {
            // Auto-save handled by IPA
        }

        /// <summary>
        /// Called when config is reloaded
        /// </summary>
        public virtual void OnReload()
        {
            Plugin.Log?.Info($"Config reloaded: Enabled={Enabled}, VibrationEnabled={VibrationEnabled}");
        }
    }
}
