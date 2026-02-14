using FaraAccField.Controllers;
using Zenject;

namespace FaraAccField.Installers
{
    /// <summary>
    /// Zenject installer for game scene bindings.
    /// Binds the main AccFieldController that runs during gameplay.
    /// </summary>
    internal class AccFieldGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<AccFieldController>().AsSingle();
        }
    }
}
