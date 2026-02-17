using System;
using BeatSaberMarkupLanguage;

namespace FaraAccField.Views
{
    internal class ModifierWarningFlowCoordinator : HMUI.FlowCoordinator
    {
        private ModifierWarningView? _viewController;
        private Action? _onOk;
        private Action? _onCancel;
        private HMUI.FlowCoordinator? _presentingFC;

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

        protected override void BackButtonWasPressed(HMUI.ViewController topViewController)
        {
            OnCancel();
        }

        public void OnOk()
        {
            try
            {
                // Dismiss immediately (no animation) to restore FC hierarchy
                // before the level transition starts
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
                // Dismiss from the FC that presented us
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
