using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace worldGenAccelerator
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class worldGenAcceleratorPlugin : BaseUnityPlugin
    {
        private const string ModName = "worldGenAccelerator";
        private const string ModVersion = "1.0.0";
        private const string Author = "warpalicious";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        private readonly Harmony HarmonyInstance = new(ModGUID);

        public static readonly ManualLogSource TemplateLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static ConfigEntry<bool> _enableOptimization = null!;
        private static ConfigEntry<bool> _enableTimingLogs = null!;
        public static bool OptimizationEnabled => _enableOptimization.Value;
        public static bool TimingLogsEnabled => _enableTimingLogs.Value;

        public void Awake()
        {
            _enableOptimization = Config.Bind("General", "EnableOptimization", true,
                "Enable the biome zone cache optimization for faster world generation. " +
                "Disabling this uses vanilla generation logic. " +
                "Note: Optimized worlds will have different layouts than vanilla for the same seed.");

            _enableTimingLogs = Config.Bind("General", "EnableTimingLogs", true,
                "Log detailed timing information for each location placement and total generation time.");

            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }
        
        private void SetupWatcher()
        {
            _lastReloadTime = DateTime.Now;
            FileSystemWatcher watcher = new(BepInEx.Paths.ConfigPath, ConfigFileName);
            // Due to limitations of technology this can trigger twice in a row
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        private DateTime _lastReloadTime;
        private const long RELOAD_DELAY = 10000000; // One second

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.Now;
            var time = now.Ticks - _lastReloadTime.Ticks;
            if (!File.Exists(ConfigFileFullPath) || time < RELOAD_DELAY) return;

            try
            {
                TemplateLogger.LogInfo("Attempting to reload configuration...");
                Config.Reload();
                TemplateLogger.LogInfo("Configuration reloaded successfully!");
            }
            catch
            {
                TemplateLogger.LogError($"There was an issue loading {ConfigFileName}");
                return;
            }

            _lastReloadTime = now;

            // Update any runtime configurations here
            if (ZNet.instance != null && !ZNet.instance.IsDedicated())
            {
                TemplateLogger.LogInfo("Updating runtime configurations...");
                // Add your configuration update logic here
            }
        }
    }
} 