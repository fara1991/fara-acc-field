using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace FaraAccField.Views
{
    [ViewDefinition("FaraAccField.Views.ModifierWarningView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\ModifierWarningView.bsml")]
    internal class ModifierWarningView : BSMLAutomaticViewController
    {
        private string _warningText = "";
        private ModifierWarningFlowCoordinator? _flowCoordinator;

        public void Setup(string warningText, ModifierWarningFlowCoordinator flowCoordinator)
        {
            _warningText = warningText;
            _flowCoordinator = flowCoordinator;
        }

        [UIValue("warning-text")]
        public string WarningText
        {
            get => _warningText;
            set => _warningText = value;
        }

        [UIAction("on-ok-click")]
        private void OnOkClicked()
        {
            _flowCoordinator?.OnOk();
        }

        [UIAction("on-cancel-click")]
        private void OnCancelClicked()
        {
            _flowCoordinator?.OnCancel();
        }
    }
}
