using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.IO.Compression;
using System.Windows;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json.Linq;
using TwitchDeckOverlay.Config;
using Hearthstone_Deck_Tracker.Utility.Extensions;

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
            CheckCardsInCollectionCheckBox.IsChecked = _config.CheckCardsInCollectionEnabled;
            CalculateTotalDustCostCheckBox.IsChecked = _config.CalculateTotalDustCostEnabled;
            CalculateDustNeededCheckBox.IsChecked = _config.CalculateDustNeededEnabled;
            ShowFocusWindowOnCopyCheckBox.IsChecked = _config.ShowFocusWindowOnCopyEnabled;

            // Встановлюємо DataContext для прив'язки
            DataContext = _config;

            Log.Info("SettingsView opened with current config");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            _config.TwitchChannel = TwitchChannelTextBox.Text.Trim();
            _config.BlizzardBearerToken = BlizzardTokenTextBox.Text.Trim();
            _config.CheckCardsInCollectionEnabled = CheckCardsInCollectionCheckBox.IsChecked ?? true;
            _config.CalculateTotalDustCostEnabled = CalculateTotalDustCostCheckBox.IsChecked ?? true;
            _config.CalculateDustNeededEnabled = CalculateDustNeededCheckBox.IsChecked ?? true;
            _config.ShowFocusWindowOnCopyEnabled = ShowFocusWindowOnCopyCheckBox.IsChecked ?? true;

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

        // UPDATING FUNCTIONS

        private const string GitHubRepoOwner = "vitPrograms";
        private const string GitHubRepoName = "HDT_Twitch_Deck_Overlay_Plugin";
        private string GitHubApiUrl = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";
        // Отримуємо поточну версію з PluginConfig
        string CurrentVersion = PluginConfig.Instance.PluginVersion;

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckUpdatesButton.IsEnabled = false;
                CheckUpdatesButton.Content = "Checking for updates...";

                // Перевіряємо останню версію на GitHub
                string latestVersion = await GetLatestVersionFromGitHub();
                if (string.IsNullOrEmpty(latestVersion))
                {
                    MessageBox.Show("Unable to check for updates. Check your internet connection.", "Error");
                    return;
                }

                // Порівнюємо версії
                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    var result = MessageBox.Show($"New version {latestVersion} is available (current: {CurrentVersion}). Update plugin?",
                        "New version is available", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        await UpdatePlugin(latestVersion);
                    }
                }
                else
                {
                    MessageBox.Show($"You have the latest version ({CurrentVersion}) installed.", "No update required");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Update check error: {ex.Message}");
                MessageBox.Show("An error occurred while checking for updates.", "Error");
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check for updates";
            }
        }

        private async Task<string> GetLatestVersionFromGitHub()
        {
            using (var client = new HttpClient())
            {
                // Додаємо User-Agent, оскільки GitHub API вимагає його
                client.DefaultRequestHeaders.Add("User-Agent", "TwitchDeckOverlay");
                var response = await client.GetStringAsync(GitHubApiUrl);
                var json = JObject.Parse(response);
                return json["tag_name"]?.ToString()?.TrimStart('v'); // Наприклад, "v1.0.1" → "1.0.1"
            }
        }

        private bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            var latest = new Version(latestVersion);
            var current = new Version(currentVersion);
            return latest > current;
        }

        private async Task UpdatePlugin(string newVersion)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "TwitchDeckOverlay");
                    var response = await client.GetAsync(GitHubApiUrl);
                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());

                    // Логуємо всі assets для діагностики
                    Log.Info($"Available assets: {json["assets"]?.ToString()}");

                    // Шукаємо ZIP-файл серед assets
                    string downloadUrl = null;
                    foreach (var asset in json["assets"])
                    {
                        var url = asset["browser_download_url"]?.ToString();
                        if (url != null && url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = url;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        Log.Error("No ZIP file found in the release assets.");
                        MessageBox.Show("The update file (ZIP) could not be found in the release.", "Error");
                        return;
                    }

                    Log.Info($"Downloading update from: {downloadUrl}");

                    // Завантажуємо ZIP-файл
                    var zipBytes = await client.GetByteArrayAsync(downloadUrl);
                    var pluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker", "Plugins");
                    var tempZipPath = Path.Combine(pluginPath, "update.zip");

                    // Зберігаємо ZIP
                    File.WriteAllBytes(tempZipPath, zipBytes);
                    Log.Info($"Saved ZIP to: {tempZipPath}");

                    // Перевіряємо, чи файл є коректним ZIP
                    try
                    {
                        using (var testArchive = new ZipArchive(File.OpenRead(tempZipPath), ZipArchiveMode.Read))
                        {
                            Log.Info($"ZIP file validated successfully. Entries: {testArchive.Entries.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Invalid ZIP file: {ex.Message}");
                        MessageBox.Show("The downloaded file is not a valid ZIP archive.", "Error");
                        File.Delete(tempZipPath);
                        return;
                    }

                    // Розпаковуємо ZIP із підтримкою оверрайту
                    using (var archive = new ZipArchive(File.OpenRead(tempZipPath), ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Name == "TwitchDeckOverlay.dll") // Оновлюємо лише .dll
                            {
                                var destFile = Path.Combine(pluginPath, entry.Name);
                                using (var entryStream = entry.Open())
                                using (var fileStream = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                                {
                                    await entryStream.CopyToAsync(fileStream);
                                }
                                Log.Info($"Extracted {entry.Name} to {destFile}");
                            }
                        }
                    }

                    // Очищаємо тимчасові файли
                    File.Delete(tempZipPath);
                    Log.Info("Temporary ZIP file deleted.");

                    // Оновлюємо версію в конфігурації
                    PluginConfig.Instance.PluginVersion = newVersion;
                    PluginConfig.Save();
                    Log.Info($"Updated PluginVersion to {newVersion} in config.");

                    // Рекомендуємо перезапустити HDT
                    MessageBox.Show("Plugin successfully updated! Please restart Hearthstone Deck Tracker for the changes to take effect.",
                        "Updating is finished");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Plugin update error: {ex.Message}");
                MessageBox.Show("An error occurred while updating the plugin.", "Error");
            }
        }
    }
}