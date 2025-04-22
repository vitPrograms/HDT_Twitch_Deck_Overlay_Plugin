using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.UI;
using TwitchDeckOverlay.Config;

namespace TwitchDeckOverlay
{
    public class Plugin : IPlugin
    {
        private OverlayView _overlay;
        private TwitchDeckManager _deckManager;
        private readonly PluginConfig _config;
        private MenuItem _menuItem;

        public string Name => "Twitch Deck Overlay (beta)";
        public string Description => "Displays decks shared in a Twitch chat";
        public string ButtonText => "Settings";
        public string Author => "Proogro";
        public Version Version => new Version(1, 0, 0);
        public MenuItem MenuItem => _menuItem;

        public Plugin()
        {
            // Load config
            _config = PluginConfig.Instance;

            // Create menu item
            _menuItem = new MenuItem { Header = "TwitchDeckOverlay Settings" };
            _menuItem.Click += (sender, args) => OpenSettings();
        }

        public void OnLoad()
        {
            _deckManager = new TwitchDeckManager(_config, Core.OverlayCanvas, null);

            _overlay = new OverlayView(_deckManager);
            _deckManager.SetOverlayView(_overlay);

            Core.OverlayCanvas.Children.Add(_overlay);

            _deckManager.Initialize();
            Log.Info("TwitchDeckOverlay plugin loaded");
        }

        public void OnUnload()
        {
            _deckManager.Shutdown();
            Core.OverlayCanvas.Children.Remove(_overlay);
            Log.Info("TwitchDeckOverlay plugin unloaded");
        }

        public void OnButtonPress()
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            try
            {
                var settingsWindow = new SettingsView(_config);

                if (settingsWindow.ShowDialog() == true)
                {
                    _deckManager.ApplyConfig(_config);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error opening settings: {ex.Message}");
            }
        }

        public void OnUpdate()
        {
        }
    }
}