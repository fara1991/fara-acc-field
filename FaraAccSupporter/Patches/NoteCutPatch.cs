using System;
using HarmonyLib;

namespace FaraAccSupporter.Patches
{
    /// <summary>
    /// Harmony patches for intercepting note cut events.
    /// Used to track follow-through after a note is cut.
    /// </summary>
    [HarmonyPatch]
    internal static class NoteCutPatch
    {
        /// <summary>
        /// Event fired when a note is cut, providing cut information for follow-through tracking.
        /// </summary>
        public static event Action<NoteController, NoteCutInfo>? OnNoteCutEvent;

        /// <summary>
        /// Patches BeatmapObjectManager.HandleNoteControllerNoteWasCut to intercept cut events.
        /// </summary>
        [HarmonyPatch(typeof(BeatmapObjectManager), nameof(BeatmapObjectManager.HandleNoteControllerNoteWasCut))]
        [HarmonyPostfix]
        static void HandleNoteWasCut_Postfix(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            try
            {
                OnNoteCutEvent?.Invoke(noteController, noteCutInfo);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Error in NoteCutPatch: {ex}");
            }
        }
    }
}
