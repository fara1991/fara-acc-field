using FaraAccSupporter.Views;
using Zenject;

namespace FaraAccSupporter.Installers
{
    /// <summary>
    /// Zenject installer for menu scene bindings.
    /// Binds the settings menu manager.
    /// </summary>
    internal class AccSupporterMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<SettingsMenuManager>().AsSingle();
        }
    }
}
