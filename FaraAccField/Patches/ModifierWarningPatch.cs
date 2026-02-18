using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatSaberMarkupLanguage;
using FaraAccField.Configuration;
using FaraAccField.Views;
using HarmonyLib;

namespace FaraAccField.Patches
{
    [HarmonyPatch]
    internal static class ModifierWarningPatch
    {
        internal static bool GhostNotesActive { get; private set; }
        internal static bool DisappearingArrowsActive { get; private set; }

        private static MenuTransitionsHelper? _storedInstance;
        private static MethodBase? _storedMethod;
        private static object[]? _storedArgs;
        private static bool _bypassing;
        private static bool _warningShown;

        static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            try
            {
                foreach (var method in typeof(MenuTransitionsHelper).GetMethods(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name == "StartStandardLevel"
                        && method.GetParameters().Any(p => p.ParameterType == typeof(GameplayModifiers)))
                    {
                        methods.Add(method);
                    }
                }

                if (methods.Count == 0)
                    Plugin.Log?.Warn("ModifierWarningPatch: No StartStandardLevel method found");
                else
                    Plugin.Log?.Info($"ModifierWarningPatch: Patching {methods.Count} StartStandardLevel overload(s)");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"ModifierWarningPatch.TargetMethods failed: {ex.Message}");
            }
            return methods;
        }

        static bool Prefix(MenuTransitionsHelper __instance, object[] __args, MethodBase __originalMethod)
        {
            try
            {
                // Extract modifier state (always, for AccFieldController to read)
                GameplayModifiers? modifiers = null;
                foreach (var arg in __args)
                {
                    if (arg is GameplayModifiers gm)
                    {
                        modifiers = gm;
                        break;
                    }
                }

                GhostNotesActive = modifiers?.ghostNotes ?? false;
                DisappearingArrowsActive = modifiers?.disappearingArrows ?? false;

                // When replaying after user clicked OK, allow through
                if (_bypassing)
                {
                    _bypassing = false;
                    return true;
                }

                // Prevent duplicate warnings from rapid clicks
                if (_warningShown)
                    return false;

                var config = PluginConfig.Instance;
                if (!config.Enabled || modifiers == null)
                    return true;

                bool ghostConflict = modifiers.ghostNotes;
                bool daConflict = modifiers.disappearingArrows && config.ShowArrowIndicator;

                if (!ghostConflict && !daConflict)
                    return true;

                // Store the call for replay on OK
                _storedInstance = __instance;
                _storedMethod = __originalMethod;
                _storedArgs = (object[])__args.Clone();

                ShowWarning(ghostConflict);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"ModifierWarningPatch.Prefix error: {ex}");
                return true;
            }
        }

        private static void ShowWarning(bool ghostNotes)
        {
            try
            {
                string warningText;
                if (ghostNotes)
                {
                    warningText =
                        "Ghost Notes is ON.\nFaraAccField Mod will be disabled.\n\nAre you sure?";
                }
                else
                {
                    warningText =
                        "Disappearing Arrows is ON.\nFaraAccField's Show Direction will be disabled.\n\nAre you sure?";
                }

                // Find the deepest active FlowCoordinator (e.g. SoloFreePlayFlowCoordinator)
                // so the warning replaces the center panel properly
                var parentFC = FindTopmostFlowCoordinator();

                var viewController = BeatSaberUI.CreateViewController<ModifierWarningView>();
                var flowCoordinator = BeatSaberUI.CreateFlowCoordinator<ModifierWarningFlowCoordinator>();

                viewController.Setup(warningText, flowCoordinator);
                flowCoordinator.Setup(viewController, OnOk, OnCancel, parentFC);

                parentFC.PresentFlowCoordinator(
                    flowCoordinator,
                    null,
                    HMUI.ViewController.AnimationDirection.Horizontal,
                    false,
                    false);

                _warningShown = true;
                string reason = ghostNotes ? "Ghost Notes" : "Disappearing Arrows + ShowNotesGrid";
                Plugin.Log?.Info($"Modifier conflict warning shown (reason: {reason}, parentFC: {parentFC.GetType().Name})");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to show modifier warning: {ex}");
                // If warning fails, proceed with level start
                ReplayLevelStart();
            }
        }

        private static HMUI.FlowCoordinator FindTopmostFlowCoordinator()
        {
            var fc = (HMUI.FlowCoordinator)BeatSaberUI.MainFlowCoordinator;

            // Traverse childFlowCoordinator chain to find the deepest active FC
            var childProp = typeof(HMUI.FlowCoordinator).GetProperty("childFlowCoordinator",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (childProp != null)
            {
                while (true)
                {
                    var child = childProp.GetValue(fc) as HMUI.FlowCoordinator;
                    if (child == null) break;
                    fc = child;
                }
            }
            else
            {
                Plugin.Log?.Warn("childFlowCoordinator property not found, falling back to MainFlowCoordinator");
            }

            return fc;
        }

        internal static void OnOk()
        {
            _warningShown = false;
            ReplayLevelStart();
        }

        internal static void OnCancel()
        {
            ClearStoredState();
            Plugin.Log?.Info("User cancelled level start due to modifier conflict");
        }

        internal static void ClearStoredState()
        {
            _warningShown = false;
            _storedInstance = null;
            _storedMethod = null;
            _storedArgs = null;
        }

        private static void ReplayLevelStart()
        {
            if (_storedInstance == null || _storedMethod == null || _storedArgs == null)
                return;

            var instance = _storedInstance;
            var method = _storedMethod;
            var args = _storedArgs;
            _storedInstance = null;
            _storedMethod = null;
            _storedArgs = null;

            try
            {
                _bypassing = true;
                method.Invoke(instance, args);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to replay level start: {ex}");
            }
            finally
            {
                _bypassing = false;
            }
        }
    }
}
