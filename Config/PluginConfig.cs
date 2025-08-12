using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System;
using System.IO;

namespace TwitchDeckOverlay.Config
{
    [Serializable]
    public class PluginConfig
    {
        private static PluginConfig _instance;
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HearthstoneDeckTracker",
            "TwitchDeckOverlaySettings.xml"
        );

        public static PluginConfig Instance
        {
            get
            {
                if (_instance == null)
                    Load();
                if (_instance == null)
                    _instance = new PluginConfig();
                return _instance;
            }
        }

        public string TwitchChannel { get; set; } = string.Empty;
        public string BlizzardBearerToken { get; set; } = "EU0t5QWz3O8WoHLWQRj5eHMcGZIIRKUSfy";
        public double OverlayWindowLeft { get; set; } = 100.0;
        public double OverlayWindowTop { get; set; } = 100.0;

        // Нові налаштування
        public bool CheckCardsInCollectionEnabled { get; set; } = true;
        public bool CalculateTotalDustCostEnabled { get; set; } = true;
        public bool CalculateDustNeededEnabled { get; set; } = true;
        public bool ShowFocusWindowOnCopyEnabled { get; set; } = true;

        // Версія плагіна
        public string PluginVersion { get; set; } = "1.0.4";

        public static void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                Log.Info($"Config file not found at {ConfigPath}, creating new instance.");
                _instance = new PluginConfig();
                return;
            }

            try
            {
                _instance = XmlManager<PluginConfig>.Load(ConfigPath);
                Log.Info($"Loaded config from {ConfigPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, nameof(Load), "Error loading TwitchDeckOverlay config");
                _instance = new PluginConfig();
            }
        }

        public static void Save()
        {
            try
            {
                XmlManager<PluginConfig>.Save(ConfigPath, Instance);
                Log.Info($"Saved config to {ConfigPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, nameof(Save), "Error saving TwitchDeckOverlay config");
            }
        }
    }
}