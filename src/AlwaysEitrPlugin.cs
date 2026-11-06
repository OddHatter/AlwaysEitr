using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Oddhatter.AlwaysEitr
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public sealed class AlwaysEitrPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.oddhatter.alwayseitr";
        public const string Name = "AlwaysEitr";
        public const string Version = "1.1.1";

        internal static ManualLogSource Log;
        internal static ConfigFile Cfg; // for settings bound later (per-staff summons, once ObjectDB loads)

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> BaseEitr;
        internal static ConfigEntry<float> EitrRegenMultiplier;

        /// <summary>Wait this long after the last file event before reloading (editors write in several steps).</summary>
        private const float ConfigReloadDelay = 0.5f;

        private Harmony _harmony;
        private FileSystemWatcher _configWatcher;
        private volatile bool _configFileChanged; // set on a thread-pool thread by the watcher
        private float _configReloadAt = -1f;
        private bool _configLockedLogged;

        private void Awake()
        {
            Log = Logger;
            Cfg = Config;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false the mod changes nothing: eitr comes only from food and summon limits are vanilla.");

            BaseEitr = Config.Bind("General", "BaseEitr", 200f,
                new ConfigDescription(
                    "Always-on eitr pool, no food needed. Eitr from food is added on top " +
                    "(200 here + 100 from food = 300, back to 200 when the food wears off). 0 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1000f)));

            EitrRegenMultiplier = Config.Bind("General", "EitrRegenMultiplier", 1.0f,
                new ConfigDescription(
                    "Eitr refill speed. 1 = vanilla, 2 = twice as fast. Gear and status effects that change eitr regen still apply on top.",
                    new AcceptableValueRange<float>(0f, 10f)));

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(AlwaysEitrPlugin).Assembly);
            WatchConfigFile();
            Log.LogInfo($"{Name} {Version} loaded.");
        }

        private void Update()
        {
            if (_configFileChanged)
            {
                _configFileChanged = false;
                _configReloadAt = Time.realtimeSinceStartup + ConfigReloadDelay;
            }
            if (_configReloadAt >= 0f && Time.realtimeSinceStartup >= _configReloadAt)
            {
                _configReloadAt = -1f;
                ReloadConfigFromDisk();
            }
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Picks up edits to the .cfg file made while the game runs (text editor, r2modman config editor).
        /// Max eitr is recalculated every second by the game, so a new BaseEitr shows up within a second.
        /// </summary>
        private void WatchConfigFile()
        {
            string path = Config.ConfigFilePath;
            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };
            _configWatcher.Changed += OnConfigFileEvent;
            _configWatcher.Created += OnConfigFileEvent;
            _configWatcher.Renamed += OnConfigFileEvent;
            _configWatcher.EnableRaisingEvents = true;
        }

        private void OnConfigFileEvent(object sender, FileSystemEventArgs e)
        {
            _configFileChanged = true; // handled on the main thread in Update
        }

        private void ReloadConfigFromDisk()
        {
            if (!File.Exists(Config.ConfigFilePath))
            {
                return;
            }
            // ConfigFile.Reload sets each entry; with SaveOnConfigSet every changed value would rewrite the file
            // and re-trigger this watcher. The file already holds these values, so don't save while reloading.
            bool saveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                Config.Reload();
                _configLockedLogged = false;
                Log.LogInfo("Configuration reloaded from disk.");
            }
            catch (IOException ex)
            {
                // File still being written or locked by an editor: keep retrying, but only log once.
                if (!_configLockedLogged)
                {
                    _configLockedLogged = true;
                    Log.LogWarning($"Config file busy, will retry: {ex.Message}");
                }
                _configReloadAt = Time.realtimeSinceStartup + ConfigReloadDelay;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to reload configuration: {ex.Message}");
            }
            finally
            {
                Config.SaveOnConfigSet = saveOnSet;
            }
        }
    }
}
