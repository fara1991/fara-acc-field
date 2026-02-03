using System;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.ViewControllers;
using FaraAccSupporter.Configuration;
using Zenject;

namespace FaraAccSupporter.Views
{
    /// <summary>
    /// Handles the settings menu UI registration and data binding.
    /// </summary>
    internal class SettingsMenuManager : IInitializable, IDisposable
    {
        private MenuButton? _menuButton;
        private SettingsModalView? _modalView;

        private bool _enabled;
        private bool _vibrationEnabled;
        private bool _showTrajectory;
        private bool _preSwingGlow;
        private float _followThroughStrength;

        #region UI Values

        [UIValue("enabled")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PluginConfig.Instance.Enabled = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("vibration-enabled")]
        public bool VibrationEnabled
        {
            get => _vibrationEnabled;
            set
            {
                _vibrationEnabled = value;
                PluginConfig.Instance.VibrationEnabled = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("show-trajectory")]
        public bool ShowTrajectory
        {
            get => _showTrajectory;
            set
            {
                _showTrajectory = value;
                PluginConfig.Instance.ShowTrajectoryLine = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("pre-swing-glow")]
        public bool PreSwingGlow
        {
            get => _preSwingGlow;
            set
            {
                _preSwingGlow = value;
                PluginConfig.Instance.PreSwingGlowEnabled = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("follow-through-strength")]
        public float FollowThroughStrength
        {
            get => _followThroughStrength;
            set
            {
                _followThroughStrength = value;
                PluginConfig.Instance.FollowThroughVibrationStrength = value;
                PluginConfig.Instance.Changed();
            }
        }

        #endregion

        public void Initialize()
        {
            LoadCurrentSettings();
            RegisterSettingsMenu();
        }

        private void LoadCurrentSettings()
        {
            _enabled = PluginConfig.Instance.Enabled;
            _vibrationEnabled = PluginConfig.Instance.VibrationEnabled;
            _showTrajectory = PluginConfig.Instance.ShowTrajectoryLine;
            _preSwingGlow = PluginConfig.Instance.PreSwingGlowEnabled;
            _followThroughStrength = PluginConfig.Instance.FollowThroughVibrationStrength;
        }

        private void RegisterSettingsMenu()
        {
            try
            {
                _menuButton = new MenuButton("Fara Acc Supporter", "Configure accuracy support settings", ShowSettings);
#if BS_1_29_1
                MenuButtons.instance.RegisterButton(_menuButton);
#else
                MenuButtons.Instance.RegisterButton(_menuButton);
#endif
                Plugin.Log?.Info("Menu button registered");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to register menu button: {ex}");
            }
        }

        private void ShowSettings()
        {
            try
            {
                Plugin.Log?.Info("ShowSettings called");

                if (_modalView == null)
                {
                    _modalView = BeatSaberUI.CreateViewController<SettingsModalView>();
                    _modalView.Setup(this);
                    Plugin.Log?.Info("Modal view created");
                }

                var flowCoordinator = BeatSaberUI.CreateFlowCoordinator<SettingsFlowCoordinator>();
                flowCoordinator.SetViewController(_modalView);

                BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(
                    flowCoordinator,
                    null,
                    HMUI.ViewController.AnimationDirection.Horizontal,
                    false,
                    false);

                Plugin.Log?.Info("Flow coordinator presented");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to show settings: {ex}");
            }
        }

        public void Dispose()
        {
            if (_menuButton != null)
            {
#if BS_1_29_1
                MenuButtons.instance.UnregisterButton(_menuButton);
#else
                MenuButtons.Instance.UnregisterButton(_menuButton);
#endif
            }

            if (_modalView != null)
            {
                UnityEngine.Object.Destroy(_modalView.gameObject);
            }
        }
    }

    /// <summary>
    /// BSML View Controller for the settings modal.
    /// </summary>
    [ViewDefinition("FaraAccSupporter.Views.SettingsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\SettingsView.bsml")]
    internal class SettingsModalView : BSMLAutomaticViewController
    {
        private SettingsMenuManager? _manager;

        public void Setup(SettingsMenuManager manager)
        {
            _manager = manager;
        }

        [UIValue("enabled")]
        public bool Enabled
        {
            get => _manager?.Enabled ?? false;
            set
            {
                if (_manager != null)
                    _manager.Enabled = value;
            }
        }

        [UIValue("vibration-enabled")]
        public bool VibrationEnabled
        {
            get => _manager?.VibrationEnabled ?? true;
            set
            {
                if (_manager != null)
                    _manager.VibrationEnabled = value;
            }
        }

        [UIValue("show-trajectory")]
        public bool ShowTrajectory
        {
            get => _manager?.ShowTrajectory ?? true;
            set
            {
                if (_manager != null)
                    _manager.ShowTrajectory = value;
            }
        }

        [UIValue("pre-swing-glow")]
        public bool PreSwingGlow
        {
            get => _manager?.PreSwingGlow ?? true;
            set
            {
                if (_manager != null)
                    _manager.PreSwingGlow = value;
            }
        }

        [UIValue("follow-through-strength")]
        public float FollowThroughStrength
        {
            get => _manager?.FollowThroughStrength ?? 0.5f;
            set
            {
                if (_manager != null)
                    _manager.FollowThroughStrength = value;
            }
        }
    }

    /// <summary>
    /// Flow coordinator for the settings view.
    /// </summary>
    internal class SettingsFlowCoordinator : HMUI.FlowCoordinator
    {
        private SettingsModalView? _viewController;

        public void SetViewController(SettingsModalView viewController)
        {
            _viewController = viewController;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    SetTitle("Fara Acc Supporter");
                    showBackButton = true;
                }

                if (_viewController != null)
                {
                    ProvideInitialViewControllers(_viewController);
                }
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
                BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Error dismissing flow coordinator: {ex}");
            }
        }
    }
}
