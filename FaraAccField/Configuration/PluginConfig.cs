using System;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace FaraAccField.Configuration
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; } = null!;

        /// <summary>
        /// Display language for settings UI ("English" or "Japanese")
        /// </summary>
        public virtual string Language { get; set; } = "English";

        /// <summary>
        /// Whether the mod is enabled
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether to show trajectory line from saber to note
        /// </summary>
        public virtual bool ShowTrajectoryLine { get; set; } = true;

        /// <summary>
        /// Whether to show center accuracy sphere on notes
        /// </summary>
        public virtual bool ShowCenterSphere { get; set; } = true;

        /// <summary>
        /// Center accuracy target for trajectory sphere size (1-15).
        /// The sphere shows the scoring zone where this many center points are awarded.
        /// </summary>
        public virtual int CenterAccuracyTarget { get; set; } = 15;

        /// <summary>
        /// Glow condition for target notes: "None", "Next", or "PreSwing70"
        /// </summary>
        public virtual string GlowCondition { get; set; } = GlowConditions.Next;

        /// <summary>
        /// Length of the X/Y/Z axis lines extending from the note center sphere (0.20 - 1.00)
        /// </summary>
        public virtual float AxisLineLength { get; set; } = 0.40f;

        /// <summary>
        /// Width of the X/Y/Z axis lines (0.001 - 0.05)
        /// </summary>
        public virtual float AxisLineWidth { get; set; } = 0.03f;

        /// <summary>
        /// Whether to show X/Y/Z axis lines on note center spheres
        /// </summary>
        public virtual bool ShowAxisLine { get; set; } = true;

        /// <summary>
        /// Whether to show arrow direction indicators on notes
        /// </summary>
        public virtual bool ShowArrowIndicator { get; set; } = true;

        /// <summary>
        /// Width of the direction arrow indicator (0.10 - 1.00)
        /// </summary>
        public virtual float ArrowIndicatorWidth { get; set; } = 0.50f;

        /// <summary>
        /// Height of the direction arrow indicator (0.10 - 1.00)
        /// </summary>
        public virtual float ArrowIndicatorHeight { get; set; } = 0.50f;

        /// <summary>
        /// Whether to show the note grid guide (12 semi-transparent cubes at cut plane)
        /// </summary>
        public virtual bool ShowNotesGrid { get; set; } = true;

        /// <summary>
        /// Note grid cube opacity (0.01 - 0.3)
        /// </summary>
        public virtual float NotesGridAlpha { get; set; } = 0.15f;

        /// <summary>
        /// Whether to link the note grid Z position to FaraRhythmMarker's MarkerZOffset.
        /// When disabled or FaraRhythmMarker is not installed, uses Z=0.5.
        /// </summary>
        public virtual bool LinkRhythmMarkerZOffset { get; set; } = true;

        /// <summary>
        /// Note grid Z offset when not linked to RhythmMarker (0.00 - 2.00)
        /// </summary>
        public virtual float NotesGridZOffset { get; set; } = 0.90f;

        /// <summary>
        /// Step size for the Z offset slider (0.01, 0.05, or 0.10)
        /// </summary>
        public virtual float NotesGridZOffsetStep { get; set; } = 0.10f;

        /// <summary>
        /// Debug mode: logs grid coordinates, note spawn directions, and cut scores
        /// </summary>
        public virtual bool DebugMode { get; set; } = false;

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
            CenterAccuracyTarget = Math.Max(1, Math.Min(15, CenterAccuracyTarget));
            NotesGridAlpha = Math.Max(0.01f, Math.Min(0.3f, NotesGridAlpha));
            AxisLineLength = Math.Max(0.20f, Math.Min(1.00f, AxisLineLength));
            AxisLineWidth = Math.Max(0.01f, Math.Min(0.05f, AxisLineWidth));
            ArrowIndicatorWidth = Math.Max(0.10f, Math.Min(1.00f, ArrowIndicatorWidth));
            ArrowIndicatorHeight = Math.Max(0.10f, Math.Min(1.00f, ArrowIndicatorHeight));
            NotesGridZOffset = Math.Max(0.00f, Math.Min(2.00f, NotesGridZOffset));
            NotesGridZOffsetStep = Math.Max(0.01f, Math.Min(0.10f, NotesGridZOffsetStep));
            if (GlowCondition != GlowConditions.None
                && GlowCondition != GlowConditions.Next
                && GlowCondition != GlowConditions.PreSwing70
                && GlowCondition != GlowConditions.Always)
                GlowCondition = GlowConditions.Next;
            Plugin.Log?.Info($"Config reloaded: Enabled={Enabled}");
        }
    }

    internal static class GlowConditions
    {
        public const string None = "None";
        public const string Next = "Next";
        public const string PreSwing70 = "PreSwing70";
        public const string Always = "Always";
    }
}
