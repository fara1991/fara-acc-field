using FaraAccSupporter.Controllers;
using Zenject;

namespace FaraAccSupporter.Installers
{
    /// <summary>
    /// Zenject installer for game scene bindings.
    /// Binds the main AccSupporterController that runs during gameplay.
    /// </summary>
    internal class AccSupporterGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<AccSupporterController>().AsSingle();
        }
    }
}
