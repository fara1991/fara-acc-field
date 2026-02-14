using System;
using BeatSaberMarkupLanguage;
using FaraAccField.Services;

namespace FaraAccField.Views
{
    /// <summary>
    /// Flow coordinator for the settings view.
    /// </summary>
    internal class SettingsFlowCoordinator : HMUI.FlowCoordinator
    {
        private SettingsModalView? _viewController;
        private MenuPreviewService? _previewService;

        public void SetViewController(SettingsModalView viewController)
        {
            _viewController = viewController;
        }

        public void SetPreviewService(MenuPreviewService previewService)
        {
            _previewService = previewService;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    SetTitle("Fara Acc Field");
                    showBackButton = true;
                }

                if (_viewController != null)
                {
                    ProvideInitialViewControllers(_viewController);
                }

                _previewService?.Show();
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Error in SettingsFlowCoordinator.DidActivate: {ex}");
            }
        }

        protected override void BackButtonWasPressed(HMUI.ViewController topViewController)
        {
            try
            {
                _previewService?.Hide();
                BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Error dismissing flow coordinator: {ex}");
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            try
            {
                _previewService?.Hide();
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Error in SettingsFlowCoordinator.DidDeactivate: {ex}");
            }
        }
    }
}
