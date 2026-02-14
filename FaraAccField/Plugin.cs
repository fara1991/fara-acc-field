using System.Reflection;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using FaraAccField.Configuration;
using FaraAccField.Installers;
using IPALogger = IPA.Logging.Logger;

namespace FaraAccField
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
            _harmony = new Harmony("com.fara.accfield");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Register installers
            zenjector.Install<AccFieldMenuInstaller>(Location.Menu);
            zenjector.Install<AccFieldGameInstaller>(Location.GameCore);

            Log.Info("FaraAccField initialized!");
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
