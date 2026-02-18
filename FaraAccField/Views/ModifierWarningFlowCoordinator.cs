using System;
using BeatSaberMarkupLanguage;
using FaraAccField.Patches;

namespace FaraAccField.Views
{
    internal class ModifierWarningFlowCoordinator : HMUI.FlowCoordinator
    {
        private ModifierWarningView? _viewController;
        private Action? _onOk;
        private Action? _onCancel;
        private HMUI.FlowCoordinator? _presentingFC;
        private bool _dismissed;

        public void Setup(ModifierWarningView viewController, Action onOk, Action onCancel,
            HMUI.FlowCoordinator presentingFC)
        {
            _viewController = viewController;
            _onOk = onOk;
            _onCancel = onCancel;
            _presentingFC = presentingFC;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    SetTitle("FaraAccField");
                    showBackButton = true;
                }

                if (_viewController != null)
                    ProvideInitialViewControllers(_viewController);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"ModifierWarningFlowCoordinator.DidActivate error: {ex}");
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            // Safety net: clear stored state only if FC was removed without explicit OK/Cancel
            // (e.g., parent FC dismissed, scene transition)
            if (removedFromHierarchy && !_dismissed)
                ModifierWarningPatch.ClearStoredState();
        }

        protected override void BackButtonWasPressed(HMUI.ViewController topViewController)
        {
            OnCancel();
        }

        public void OnOk()
        {
            try
            {
                _dismissed = true;
                var parent = _presentingFC ?? BeatSaberUI.MainFlowCoordinator;
                parent.DismissFlowCoordinator(this,
                    null,
                    HMUI.ViewController.AnimationDirection.Horizontal,
                    true);
                _onOk?.Invoke();
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"ModifierWarningFlowCoordinator.OnOk error: {ex}");
            }
        }

        public void OnCancel()
        {
            try
            {
                _dismissed = true;
                var parent = _presentingFC ?? BeatSaberUI.MainFlowCoordinator;
                parent.DismissFlowCoordinator(this);
                _onCancel?.Invoke();
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"ModifierWarningFlowCoordinator.OnCancel error: {ex}");
            }
        }
    }
}
