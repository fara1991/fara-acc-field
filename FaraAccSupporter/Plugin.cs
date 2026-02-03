using System.Reflection;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using FaraAccSupporter.Configuration;
using FaraAccSupporter.Installers;
using IPALogger = IPA.Logging.Logger;

namespace FaraAccSupporter
{
    [Plugin(RuntimeOptions.DynamicInit)]
    [NoEnableDisable]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; } = null!;
        internal static IPALogger Log { get; private set; } = null!;

        private Harmony? _harmony;

        [Init]
        public Plugin(IPALogger logger, Config config, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;

            PluginConfig.Instance = config.Generated<PluginConfig>();

            // Initialize Harmony
            _harmony = new Harmony("com.fara.accsupporter");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Register installers
            zenjector.Install<AccSupporterMenuInstaller>(Location.Menu);
            zenjector.Install<AccSupporterGameInstaller>(Location.GameCore);

            Log.Info("FaraAccSupporter initialized!");
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
