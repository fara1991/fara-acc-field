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
        /// Whether trajectory lines consider Time Dependency (TD/TI).
        /// When enabled, outer notes show optimal cut position based on TI rules.
        /// When disabled, always show trajectory to note center.
        /// </summary>
        public virtual bool TrajectoryTDEnabled { get; set; } = true;

        /// <summary>
        /// Whether to glow notes when pre-swing threshold is reached
        /// </summary>
        public virtual bool PreSwingGlowEnabled { get; set; } = true;

        /// <summary>
        /// Follow-through vibration strength (0.0 - 1.0)
        /// </summary>
        public virtual float FollowThroughVibrationStrength { get; set; } = 0.5f;

        /// <summary>
        /// Whether to show the note grid guide (12 semi-transparent cubes at cut plane)
        /// </summary>
        public virtual bool ShowNoteGrid { get; set; } = true;

        /// <summary>
        /// Note grid cube opacity (0.1 - 1.0)
        /// </summary>
        public virtual float NoteGridAlpha { get; set; } = 0.25f;

        /// <summary>
        /// Whether to link the note grid Z position to FaraRhythmMarker's MarkerZOffset.
        /// When disabled or FaraRhythmMarker is not installed, uses Z=0.5.
        /// </summary>
        public virtual bool LinkRhythmMarkerZOffset { get; set; } = true;

        /// <summary>
        /// Whether to log note grid and note position coordinates for debugging
        /// </summary>
        public virtual bool NoteGridDebugLog { get; set; } = false;

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
