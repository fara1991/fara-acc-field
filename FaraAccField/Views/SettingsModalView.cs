using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace FaraAccField.Views
{
    /// <summary>
    /// BSML View Controller for the settings modal.
    /// </summary>
    [ViewDefinition("FaraAccField.Views.SettingsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\SettingsView.bsml")]
    internal class SettingsModalView : BSMLAutomaticViewController
    {
        private SettingsMenuManager? _manager;

        public void Setup(SettingsMenuManager manager)
        {
            _manager = manager;
        }

        private bool IsJapanese => (_manager?.Language ?? "English") == "Japanese";
        private string L(string en, string ja) => IsJapanese ? ja : en;

        #region Localized Text

        [UIValue("common-header")]
        public string CommonHeader => L("Common", "共通");

        [UIValue("preswing-header")]
        public string PreswingHeader => L("Target Notes Assist", "ターゲットノーツアシスト");

        [UIValue("visual-header")]
        public string VisualHeader => L("Center-Point Assist", "センターポイントアシスト");

        [UIValue("direction-header")]
        public string DirectionHeader => L("Direction Assist", "ディレクションアシスト");

        [UIValue("grid-header")]
        public string GridHeader => L("Cut Position Assist", "カットポジションアシスト");

        [UIValue("language-hint")]
        public string LanguageHint => L("Select the display language for settings", "設定画面の表示言語を選択します");

        [UIValue("enabled-text")]
        public string EnabledText => L("Enabled", "有効");
        [UIValue("enabled-hint")]
        public string EnabledHint => L("Enable or disable the acc field mod", "MOD全体の有効/無効を切り替えます");

        [UIValue("debug-log-text")]
        public string DebugLogText => L("Debug Position Log", "デバッグ座標ログ");
        [UIValue("debug-log-hint")]
        public string DebugLogHint => L(
            "Log grid cube and notes X/Y/Z coordinates to file for debugging",
            "グリッドキューブとノーツの座標をログに出力します（デバッグ用）");

        [UIValue("condition-text")]
        public string ConditionText => L("Condition", "条件");
        [UIValue("condition-hint")]
        public string ConditionHint => L(
            "When to glow target notes: None=off, Next=always glow next notes, Pre-Swing 70pts=glow when swing angle reaches 100°",
            "ノーツを光らせる条件: None=オフ、Next=常に次のノーツを光らせる、Pre-Swing 70pts=振りかぶり100°達成時に光らせる");

        [UIValue("show-trajectory-text")]
        public string ShowTrajectoryText => L("Show Trajectory Line", "軌道線を表示");
        [UIValue("show-trajectory-hint")]
        public string ShowTrajectoryHint => L(
            "Show a trajectory line from each saber to its nearest matching notes",
            "セイバーから最寄りノーツへの軌道線を表示します");

        [UIValue("show-sphere-text")]
        public string ShowSphereText => L("Show Center Sphere", "中心精度オブジェクトを表示");
        [UIValue("show-sphere-hint")]
        public string ShowSphereHint => L(
            "Show center accuracy sphere on notes",
            "ノーツ位置に中心精度の球体を表示します");

        [UIValue("center-accuracy-text")]
        public string CenterAccuracyText => L("Center Accuracy Target", "中心精度ターゲット");
        [UIValue("center-accuracy-hint")]
        public string CenterAccuracyHint => L(
            "Sphere size at notes center shows the zone for this many center accuracy points (15=smallest, 1=largest)",
            "ノーツ中心の球体サイズ。狙うセンター精度の点数を設定します（15=最小、1=最大）");

        [UIValue("axis-length-text")]
        public string AxisLengthText => L("Axis Line Length", "軸線の長さ");
        [UIValue("axis-length-hint")]
        public string AxisLengthHint => L(
            "Length of X/Y/Z axis lines extending from the notes center sphere (0.2=short, 1.0=long)",
            "球体から伸びるX/Y/Z軸線の長さ（0.2=短い、1.0=長い）");

        [UIValue("axis-width-text")]
        public string AxisWidthText => L("Axis Line Width", "軸線の太さ");
        [UIValue("axis-width-hint")]
        public string AxisWidthHint => L(
            "Width of X/Y/Z axis lines (0.01=thin, 0.05=thick)",
            "X/Y/Z軸線の太さ（0.01=細い、0.05=太い）");

        [UIValue("show-axis-text")]
        public string ShowAxisText => L("Show Axis Lines", "軸線を表示");
        [UIValue("show-axis-hint")]
        public string ShowAxisHint => L(
            "Show X/Y/Z axis lines on notes",
            "ノーツ位置にX/Y/Z軸線を表示します");

        [UIValue("show-arrow-text")]
        public string ShowArrowText => L("Show Direction", "方向を表示");
        [UIValue("show-arrow-hint")]
        public string ShowArrowHint => L(
            "Show arrow indicators on notes pointing in the cut direction",
            "ノーツの切る方向を示す矢印インジケーターを表示します");

        [UIValue("arrow-width-text")]
        public string ArrowWidthText => L("Direction Width", "方向表示の横幅");
        [UIValue("arrow-width-hint")]
        public string ArrowWidthHint => L(
            "Width of the direction arrow indicator (0.1=narrow, 1.0=wide)",
            "方向矢印の横幅（0.1=狭い、1.0=広い）");

        [UIValue("arrow-height-text")]
        public string ArrowHeightText => L("Direction Height", "方向表示の縦幅");
        [UIValue("arrow-height-hint")]
        public string ArrowHeightHint => L(
            "Height of the direction arrow indicator (0.1=short, 1.0=tall)",
            "方向矢印の縦幅（0.1=短い、1.0=長い）");

        [UIValue("show-grid-text")]
        public string ShowGridText => L("Show Cut Position", "カット位置を表示");
        [UIValue("show-grid-hint")]
        public string ShowGridHint => L(
            "Show semi-transparent cubes at each of the 12 notes positions at cut depth",
            "12個の半透明キューブをノーツ位置に表示します");

        [UIValue("grid-opacity-text")]
        public string GridOpacityText => L("Opacity", "透明度");
        [UIValue("grid-opacity-hint")]
        public string GridOpacityHint => L(
            "Opacity of grid cubes (0.01=barely visible, 0.3=semi-transparent)",
            "キューブの透明度（0.01=ほぼ透明、0.3=半透明）");

        [UIValue("link-rhythm-text")]
        public string LinkRhythmText => L("Link FaraRhythmMarker Mod", "FaraRhythmMarker Modと位置連動");
        [UIValue("link-rhythm-hint")]
        public string LinkRhythmHint => L(
            "When FaraRhythmMarker mod is installed, syncs cube and marker Z positions. Otherwise uses manual Z Offset below",
            "FaraRhythmMarkerのModを導入している場合、キューブとマーカー表示のZ位置を連動させます");

        [UIValue("z-offset-text")]
        public string ZOffsetText => L("Notes Grid Z Offset", "グリッドZ位置");
        [UIValue("z-offset-hint")]
        public string ZOffsetHint => L(
            "Cube depth when FaraRhythmMarker link is OFF (0.00=near, 2.00=far)",
            "FaraRhythmMarkerのMod連動しない時のキューブ奥行き位置（0.00=手前、2.00=奥）");

        private void NotifyAllTextChanged()
        {
            NotifyPropertyChanged(nameof(CommonHeader));
            NotifyPropertyChanged(nameof(PreswingHeader));
            NotifyPropertyChanged(nameof(VisualHeader));
            NotifyPropertyChanged(nameof(DirectionHeader));
            NotifyPropertyChanged(nameof(GridHeader));
            NotifyPropertyChanged(nameof(LanguageHint));
            NotifyPropertyChanged(nameof(EnabledText));
            NotifyPropertyChanged(nameof(EnabledHint));
            NotifyPropertyChanged(nameof(DebugLogText));
            NotifyPropertyChanged(nameof(DebugLogHint));
            NotifyPropertyChanged(nameof(ConditionText));
            NotifyPropertyChanged(nameof(ConditionHint));
            NotifyPropertyChanged(nameof(ShowTrajectoryText));
            NotifyPropertyChanged(nameof(ShowTrajectoryHint));
            NotifyPropertyChanged(nameof(ShowSphereText));
            NotifyPropertyChanged(nameof(ShowSphereHint));
            NotifyPropertyChanged(nameof(CenterAccuracyText));
            NotifyPropertyChanged(nameof(CenterAccuracyHint));
            NotifyPropertyChanged(nameof(AxisLengthText));
            NotifyPropertyChanged(nameof(AxisLengthHint));
            NotifyPropertyChanged(nameof(AxisWidthText));
            NotifyPropertyChanged(nameof(AxisWidthHint));
            NotifyPropertyChanged(nameof(ShowAxisText));
            NotifyPropertyChanged(nameof(ShowAxisHint));
            NotifyPropertyChanged(nameof(ShowArrowText));
            NotifyPropertyChanged(nameof(ShowArrowHint));
            NotifyPropertyChanged(nameof(ArrowWidthText));
            NotifyPropertyChanged(nameof(ArrowWidthHint));
            NotifyPropertyChanged(nameof(ArrowHeightText));
            NotifyPropertyChanged(nameof(ArrowHeightHint));
            NotifyPropertyChanged(nameof(ShowGridText));
            NotifyPropertyChanged(nameof(ShowGridHint));
            NotifyPropertyChanged(nameof(GridOpacityText));
            NotifyPropertyChanged(nameof(GridOpacityHint));
            NotifyPropertyChanged(nameof(LinkRhythmText));
            NotifyPropertyChanged(nameof(LinkRhythmHint));
            NotifyPropertyChanged(nameof(ZOffsetText));
            NotifyPropertyChanged(nameof(ZOffsetHint));
        }

        #endregion

        #region UI Values

        [UIValue("language-options")]
        public List<object> LanguageOptions => _manager?.LanguageOptions ?? new List<object> { "English", "Japanese" };

        [UIValue("glow-condition-options")]
        public List<object> GlowConditionOptions => _manager?.GlowConditionOptions ?? new List<object> { "None", "Next", "PreSwing70" };

        [UIAction("glow-condition-formatter")]
        private string FormatGlowCondition(string value)
        {
            return value switch
            {
                "None" => L("None", "なし"),
                "Next" => L("Next", "次のノーツ"),
                "PreSwing70" => L("Pre-Swing 70pts", "プリスイング 70pts"),
                _ => value
            };
        }

        [UIValue("language")]
        public string Language
        {
            get => _manager?.Language ?? "English";
            set
            {
                if (_manager != null)
                {
                    _manager.Language = value;
                    NotifyAllTextChanged();
                }
            }
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

        [UIValue("show-center-sphere")]
        public bool ShowCenterSphere
        {
            get => _manager?.ShowCenterSphere ?? true;
            set
            {
                if (_manager != null)
                    _manager.ShowCenterSphere = value;
                NotifyInteractableChanged();
            }
        }

        [UIValue("center-accuracy-target")]
        public float CenterAccuracyTarget
        {
            get => _manager?.CenterAccuracyTarget ?? 15f;
            set
            {
                if (_manager != null)
                    _manager.CenterAccuracyTarget = value;
            }
        }

        [UIValue("axis-line-length")]
        public float AxisLineLength
        {
            get => _manager?.AxisLineLength ?? 0.40f;
            set
            {
                if (_manager != null)
                    _manager.AxisLineLength = value;
            }
        }

        [UIValue("axis-line-width")]
        public float AxisLineWidth
        {
            get => _manager?.AxisLineWidth ?? 0.03f;
            set
            {
                if (_manager != null)
                    _manager.AxisLineWidth = value;
            }
        }

        [UIValue("show-axis-line")]
        public bool ShowAxisLine
        {
            get => _manager?.ShowAxisLine ?? true;
            set
            {
                if (_manager != null)
                    _manager.ShowAxisLine = value;
                NotifyInteractableChanged();
            }
        }

        [UIValue("show-arrow-indicator")]
        public bool ShowArrowIndicator
        {
            get => _manager?.ShowArrowIndicator ?? true;
            set
            {
                if (_manager != null)
                    _manager.ShowArrowIndicator = value;
                NotifyInteractableChanged();
            }
        }

        [UIValue("arrow-indicator-width")]
        public float ArrowIndicatorWidth
        {
            get => _manager?.ArrowIndicatorWidth ?? 0.50f;
            set
            {
                if (_manager != null)
                    _manager.ArrowIndicatorWidth = value;
            }
        }

        [UIValue("arrow-indicator-height")]
        public float ArrowIndicatorHeight
        {
            get => _manager?.ArrowIndicatorHeight ?? 0.50f;
            set
            {
                if (_manager != null)
                    _manager.ArrowIndicatorHeight = value;
            }
        }

        [UIValue("glow-condition")]
        public string GlowCondition
        {
            get => _manager?.GlowCondition ?? "Next";
            set
            {
                if (_manager != null)
                    _manager.GlowCondition = value;
            }
        }

        [UIValue("show-notes-grid")]
        public bool ShowNotesGrid
        {
            get => _manager?.ShowNotesGrid ?? false;
            set
            {
                if (_manager != null)
                    _manager.ShowNotesGrid = value;
                NotifyInteractableChanged();
            }
        }

        [UIValue("notes-grid-alpha")]
        public float NotesGridAlpha
        {
            get => _manager?.NotesGridAlpha ?? 0.15f;
            set
            {
                if (_manager != null)
                    _manager.NotesGridAlpha = value;
            }
        }

        [UIValue("link-rhythm-marker-z")]
        public bool LinkRhythmMarkerZ
        {
            get => _manager?.LinkRhythmMarkerZ ?? true;
            set
            {
                if (_manager != null)
                    _manager.LinkRhythmMarkerZ = value;
                NotifyInteractableChanged();
            }
        }

#pragma warning disable CS0649 // assigned by BSML via reflection
        [UIObject("center-accuracy-slider")]
        private UnityEngine.GameObject? _centerAccuracySliderObj;

        [UIObject("axis-length-slider")]
        private UnityEngine.GameObject? _axisLengthSliderObj;

        [UIObject("axis-width-slider")]
        private UnityEngine.GameObject? _axisWidthSliderObj;

        [UIObject("arrow-width-slider")]
        private UnityEngine.GameObject? _arrowWidthSliderObj;

        [UIObject("arrow-height-slider")]
        private UnityEngine.GameObject? _arrowHeightSliderObj;

        [UIObject("grid-opacity-slider")]
        private UnityEngine.GameObject? _gridOpacitySliderObj;

        [UIObject("z-offset-slider")]
        private UnityEngine.GameObject? _zOffsetSliderObj;
#pragma warning restore CS0649

        [UIAction("#post-parse")]
        private void PostParse()
        {
            UpdateAllInteractable();
        }

        public void NotifyInteractableChanged()
        {
            UpdateAllInteractable();
        }

        private void UpdateAllInteractable()
        {
            bool sphereOn = _manager?.ShowCenterSphere ?? true;
            SetChildrenInteractable(_centerAccuracySliderObj, sphereOn);

            bool axisOn = _manager?.ShowAxisLine ?? true;
            SetChildrenInteractable(_axisLengthSliderObj, axisOn);
            SetChildrenInteractable(_axisWidthSliderObj, axisOn);

            bool arrowOn = _manager?.ShowArrowIndicator ?? true;
            SetChildrenInteractable(_arrowWidthSliderObj, arrowOn);
            SetChildrenInteractable(_arrowHeightSliderObj, arrowOn);

            bool gridOn = _manager?.ShowNotesGrid ?? true;
            SetChildrenInteractable(_gridOpacitySliderObj, gridOn);

            bool zOffsetOn = _manager?.ZOffsetInteractable ?? false;
            SetChildrenInteractable(_zOffsetSliderObj, zOffsetOn);
        }

        private static void SetChildrenInteractable(UnityEngine.GameObject? obj, bool interactable)
        {
            if (obj == null) return;
            foreach (var sel in obj.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                sel.interactable = interactable;
            float alpha = interactable ? 1f : 0.5f;
            foreach (var text in obj.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                text.alpha = alpha;
        }

        [UIValue("notes-grid-z-offset")]
        public float NotesGridZOffset
        {
            get => _manager?.NotesGridZOffset ?? 0.90f;
            set
            {
                if (_manager != null)
                    _manager.NotesGridZOffset = value;
            }
        }

        [UIValue("notes-grid-debug-log")]
        public bool NotesGridDebugLog
        {
            get => _manager?.NotesGridDebugLog ?? false;
            set
            {
                if (_manager != null)
                    _manager.NotesGridDebugLog = value;
            }
        }

        #endregion
    }
}
