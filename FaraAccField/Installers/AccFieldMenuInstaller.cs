using FaraAccField.Views;
using Zenject;

namespace FaraAccField.Installers
{
    /// <summary>
    /// Zenject installer for menu scene bindings.
    /// Binds the settings menu manager.
    /// </summary>
    internal class AccFieldMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<SettingsMenuManager>().AsSingle();
        }
    }
}
