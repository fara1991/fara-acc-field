using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.MenuButtons;
using FaraAccField.Configuration;
using FaraAccField.Services;
using Zenject;

namespace FaraAccField.Views
{
    /// <summary>
    /// Handles the settings menu UI registration and data binding.
    /// </summary>
    internal class SettingsMenuManager : IInitializable, IDisposable
    {
        private MenuButton? _menuButton;
        private SettingsModalView? _modalView;
        private MenuPreviewService? _previewService;

        private string _language = null!;
        private bool _enabled;
        private bool _showTrajectory;
        private bool _showCenterSphere;
        private float _centerAccuracyTarget;
        private float _axisLineLength;
        private float _axisLineWidth;
        private bool _showAxisLine;
        private bool _showArrowIndicator;
        private float _arrowIndicatorWidth;
        private float _arrowIndicatorHeight;
        private string _glowCondition = null!;
        private bool _showNotesGrid;
        private float _notesGridAlpha;
        private bool _linkRhythmMarkerZ;
        private float _notesGridZOffset;
        private bool _notesGridDebugLog;

        #region UI Values

        [UIValue("language-options")]
        public List<object> LanguageOptions => new List<object> { "English", "Japanese" };

        [UIValue("language")]
        public string Language
        {
            get => _language;
            set
            {
                _language = value;
                PluginConfig.Instance.Language = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("enabled")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PluginConfig.Instance.Enabled = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateEnabled(value);
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

        [UIValue("show-center-sphere")]
        public bool ShowCenterSphere
        {
            get => _showCenterSphere;
            set
            {
                _showCenterSphere = value;
                PluginConfig.Instance.ShowCenterSphere = value;
                PluginConfig.Instance.Changed();
                _modalView?.NotifyInteractableChanged();
            }
        }

        [UIValue("center-accuracy-target")]
        public float CenterAccuracyTarget
        {
            get => _centerAccuracyTarget;
            set
            {
                _centerAccuracyTarget = UnityEngine.Mathf.RoundToInt(value);
                PluginConfig.Instance.CenterAccuracyTarget = (int)_centerAccuracyTarget;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateCenterAccuracyTarget((int)_centerAccuracyTarget);
            }
        }

        [UIValue("axis-line-length")]
        public float AxisLineLength
        {
            get => _axisLineLength;
            set
            {
                _axisLineLength = value;
                PluginConfig.Instance.AxisLineLength = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("axis-line-width")]
        public float AxisLineWidth
        {
            get => _axisLineWidth;
            set
            {
                _axisLineWidth = value;
                PluginConfig.Instance.AxisLineWidth = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("show-axis-line")]
        public bool ShowAxisLine
        {
            get => _showAxisLine;
            set
            {
                _showAxisLine = value;
                PluginConfig.Instance.ShowAxisLine = value;
                PluginConfig.Instance.Changed();
                _modalView?.NotifyInteractableChanged();
            }
        }

        [UIValue("show-arrow-indicator")]
        public bool ShowArrowIndicator
        {
            get => _showArrowIndicator;
            set
            {
                _showArrowIndicator = value;
                PluginConfig.Instance.ShowArrowIndicator = value;
                PluginConfig.Instance.Changed();
                _modalView?.NotifyInteractableChanged();
            }
        }

        [UIValue("arrow-indicator-width")]
        public float ArrowIndicatorWidth
        {
            get => _arrowIndicatorWidth;
            set
            {
                _arrowIndicatorWidth = value;
                PluginConfig.Instance.ArrowIndicatorWidth = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("arrow-indicator-height")]
        public float ArrowIndicatorHeight
        {
            get => _arrowIndicatorHeight;
            set
            {
                _arrowIndicatorHeight = value;
                PluginConfig.Instance.ArrowIndicatorHeight = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("glow-condition-options")]
        public List<object> GlowConditionOptions => new List<object> { GlowConditions.None, GlowConditions.Next, GlowConditions.PreSwing70 };

        [UIValue("glow-condition")]
        public string GlowCondition
        {
            get => _glowCondition;
            set
            {
                _glowCondition = value;
                PluginConfig.Instance.GlowCondition = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("show-notes-grid")]
        public bool ShowNotesGrid
        {
            get => _showNotesGrid;
            set
            {
                _showNotesGrid = value;
                PluginConfig.Instance.ShowNotesGrid = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateShowNotesGrid(value);
                _modalView?.NotifyInteractableChanged();
            }
        }

        [UIValue("notes-grid-alpha")]
        public float NotesGridAlpha
        {
            get => _notesGridAlpha;
            set
            {
                _notesGridAlpha = value;
                PluginConfig.Instance.NotesGridAlpha = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateAlpha(value);
            }
        }

        [UIValue("link-rhythm-marker-z")]
        public bool LinkRhythmMarkerZ
        {
            get => _linkRhythmMarkerZ;
            set
            {
                _linkRhythmMarkerZ = value;
                PluginConfig.Instance.LinkRhythmMarkerZOffset = value;
                PluginConfig.Instance.Changed();
                _modalView?.NotifyInteractableChanged();
                _previewService?.RecalculateZOffset();
            }
        }

        [UIValue("z-offset-interactable")]
        public bool ZOffsetInteractable => !_linkRhythmMarkerZ;

        [UIValue("notes-grid-z-offset")]
        public float NotesGridZOffset
        {
            get => _notesGridZOffset;
            set
            {
                _notesGridZOffset = value;
                PluginConfig.Instance.NotesGridZOffset = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateZOffset(value);
            }
        }

        [UIValue("notes-grid-debug-log")]
        public bool NotesGridDebugLog
        {
            get => _notesGridDebugLog;
            set
            {
                _notesGridDebugLog = value;
                PluginConfig.Instance.NotesGridDebugLog = value;
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
            _language = PluginConfig.Instance.Language;
            _enabled = PluginConfig.Instance.Enabled;
            _showTrajectory = PluginConfig.Instance.ShowTrajectoryLine;
            _showCenterSphere = PluginConfig.Instance.ShowCenterSphere;
            _centerAccuracyTarget = PluginConfig.Instance.CenterAccuracyTarget;
            _axisLineLength = PluginConfig.Instance.AxisLineLength;
            _axisLineWidth = PluginConfig.Instance.AxisLineWidth;
            _showAxisLine = PluginConfig.Instance.ShowAxisLine;
            _showArrowIndicator = PluginConfig.Instance.ShowArrowIndicator;
            _arrowIndicatorWidth = PluginConfig.Instance.ArrowIndicatorWidth;
            _arrowIndicatorHeight = PluginConfig.Instance.ArrowIndicatorHeight;
            _glowCondition = PluginConfig.Instance.GlowCondition;
            _showNotesGrid = PluginConfig.Instance.ShowNotesGrid;
            _notesGridAlpha = PluginConfig.Instance.NotesGridAlpha;
            _linkRhythmMarkerZ = PluginConfig.Instance.LinkRhythmMarkerZOffset;
            _notesGridZOffset = PluginConfig.Instance.NotesGridZOffset;
            _notesGridDebugLog = PluginConfig.Instance.NotesGridDebugLog;
        }

        private void RegisterSettingsMenu()
        {
            try
            {
                _menuButton = new MenuButton("Fara Acc Field", "Configure accuracy support settings", ShowSettings);
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

                _previewService?.Dispose();
                _previewService = new MenuPreviewService();

                var flowCoordinator = BeatSaberUI.CreateFlowCoordinator<SettingsFlowCoordinator>();
                flowCoordinator.SetViewController(_modalView);
                flowCoordinator.SetPreviewService(_previewService);

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
            try
            {
                _previewService?.Dispose();
                _previewService = null;

                if (_menuButton != null)
                {
#if BS_1_29_1
                    if (MenuButtons.instance != null)
                    {
                        MenuButtons.instance.UnregisterButton(_menuButton);
                    }
#else
                    if (MenuButtons.Instance != null)
                    {
                        MenuButtons.Instance.UnregisterButton(_menuButton);
                    }
#endif
                }

                if (_modalView != null && _modalView.gameObject != null)
                {
                    UnityEngine.Object.Destroy(_modalView.gameObject);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"Error during SettingsMenuManager disposal: {ex.Message}");
            }
        }
    }
}
