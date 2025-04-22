using System.Windows;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.Config;

namespace TwitchDeckOverlay.UI
{
    public partial class SettingsView : Window
    {
        private readonly PluginConfig _config;

        public SettingsView(PluginConfig config)
        {
            InitializeComponent();
            _config = config;

            // Load current settings
            TwitchChannelTextBox.Text = _config.TwitchChannel;
            BlizzardTokenTextBox.Text = _config.BlizzardBearerToken;
            Log.Info("SettingsView opened with current config");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            _config.TwitchChannel = TwitchChannelTextBox.Text.Trim();
            _config.BlizzardBearerToken = BlizzardTokenTextBox.Text.Trim();
            PluginConfig.Save();
            Log.Info("Settings saved from SettingsView");

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Info("SettingsView closed without saving");
            DialogResult = false;
            Close();
        }
    }
}