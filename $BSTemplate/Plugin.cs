using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using FaraRhythmMarker.Configuration;
using FaraRhythmMarker.Installers;
using IPALogger = IPA.Logging.Logger;

namespace $BSTemplate
{
    [Plugin(RuntimeOptions.DynamicInit)]
    [NoEnableDisable]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; } = null!;
        internal static IPALogger Log { get; private set; } = null!;

        [Init]
        public Plugin(IPALogger logger, Config config, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;

            PluginConfig.Instance = config.Generated<PluginConfig>();

            Log.Info("$BSTemplate initialized!");
        }
    }
}