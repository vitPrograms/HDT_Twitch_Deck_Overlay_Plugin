using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using TwitchDeckOverlay.Models;
using TwitchDeckOverlay.Services;
using Hearthstone_Deck_Tracker.LogReader;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.Config;
using System.Windows;
using System.Windows.Media;
using TwitchDeckOverlay.Utility;

namespace TwitchDeckOverlay
{
    public class TwitchDeckManager : INotifyPropertyChanged
    {
        private readonly TwitchService _twitchService;
        private readonly BlizzardApiService _blizzardApi;
        private readonly PluginConfig _config; // Додаємо для передачі в BlizzardApiService
        private readonly Regex _deckCodeRegex = new Regex(@"(?:^|\s)(?:deck code: )?([a-zA-Z0-9+/=]{20,})(?:\s|$)", RegexOptions.Compiled);
        private readonly Dispatcher _dispatcher;
        private Canvas _canvas;
        private UserControl _overlayView;
        private const int MaxDeckCount = 5;
        
        // Моніторинг продуктивності
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ResourceMonitor _resourceMonitor;

        public ObservableCollection<DeckInfo> Decks { get; } = new ObservableCollection<DeckInfo>();

        private string _twitchChannel;
        public string TwitchChannel
        {
            get => _twitchChannel;
            set
            {
                if (_twitchChannel != value)
                {
                    _twitchChannel = value;
                    OnPropertyChanged();
                    Log.Info($"Twitch channel changed to: {value}");
                    _ = ReconnectAsync();
                }
            }
        }

        public TwitchDeckManager(PluginConfig config, Canvas canvas, UserControl overlayView)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _twitchService = new TwitchService();
            _config = config; // Зберігаємо config
            _blizzardApi = new BlizzardApiService(config.BlizzardBearerToken, config);
            _canvas = canvas;
            _overlayView = overlayView;

            _twitchService.MessageReceived += HandleRawMessage;

            TwitchChannel = config.TwitchChannel;
            
            // Ініціалізація моніторингу
            _performanceMonitor = new PerformanceMonitor();
            _resourceMonitor = new ResourceMonitor(30000); // Моніторинг кожні 30 секунд (менше спаму)
            
            Log.Info("TwitchDeckManager initialized with performance monitoring");
        }

        public void SetOverlayView(UserControl overlayView)
        {
            _overlayView = overlayView;

//#if DEBUG
//            var debugWindow = new Window
//            {
//                Content = overlayView,
//                Width = 2560,
//                Height = 1440,
//                Topmost = true,
//                Background = new SolidColorBrush(Color.FromRgb(0, 177, 64))
//            };
//            debugWindow.Show();
//#endif
        }

        public async void Initialize()
        {
            if (!string.IsNullOrWhiteSpace(TwitchChannel))
            {
                await _twitchService.ConnectAsync(TwitchChannel);
            }
        }

        public void Shutdown()
        {
            _twitchService.Disconnect();
            
            // Генеруємо фінальний звіт про продуктивність
            _performanceMonitor?.LogPerformanceReport();
            _resourceMonitor?.LogResourceReport();
            
            _performanceMonitor?.Dispose();
            _resourceMonitor?.Dispose();
            
            Log.Info("TwitchDeckManager shutdown with performance reports");
        }

        public void ApplyConfig(PluginConfig config)
        {
            TwitchChannel = config.TwitchChannel;
            _blizzardApi.UpdateBearerToken(config.BlizzardBearerToken);
 
            Log.Info("Applied new config");
        }

        // Додаємо метод для перевірки стану підключення
        public async Task CheckConnectionHealthAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(TwitchChannel))
                {
                    Log.Info("Performing connection health check...");
                    await _twitchService.ConnectAsync(TwitchChannel);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Connection health check failed: {ex.Message}");
            }
        }

        private async Task ReconnectAsync()
        {
            Log.Info("Reconnecting to Twitch...");
            _twitchService.Disconnect();
            if (!string.IsNullOrWhiteSpace(TwitchChannel))
            {
                try
                {
                    await _twitchService.ConnectAsync(TwitchChannel);
                }
                catch (Exception ex)
                {
                    Log.Error($"Twitch reconnect error: {ex.Message}");
                }
            }
        }

        private void HandleRawMessage(string raw)
        {
            using (var operation = _performanceMonitor.StartOperation("HandleRawMessage"))
            {
                try
                {
                    if (!raw.Contains("PRIVMSG"))
                    {
                        return;
                    }

                    Log.Debug($"Processing Twitch message: {raw}");

                    var match = Regex.Match(raw, @":(?<user>[^!]+)![^ ]+ PRIVMSG #[^ ]+ :(?<msg>.+)");
                    if (match.Success)
                    {
                        var username = match.Groups["user"].Value;
                        var content = match.Groups["msg"].Value;

                        Log.Debug($"Parsed message from {username}: {content}");

                        var message = new TwitchMessage(username, content);
                        HandleTwitchMessage(message);
                    }
                    else
                    {
                        Log.Warn($"Failed to parse Twitch message: {raw}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"IRC parse error: {ex.Message}");
                }
            }
        }

        private async void HandleTwitchMessage(TwitchMessage message)
        {
            using (var operation = _performanceMonitor.StartOperation("HandleTwitchMessage"))
            {
                try
                {
                    var match = _deckCodeRegex.Match(message.Content);
                    if (match.Success)
                    {
                        string deckCode = match.Groups[1].Value;
                        Log.Info($"Found deck code: {deckCode}, starting deck parsing...");
                        
                        using (var deckParseOperation = _performanceMonitor.StartOperation("ParseDeckCode"))
                        {
                            var deckInfo = await _blizzardApi.GetDeckInfoAsync(deckCode);
                            if (deckInfo != null)
                            {
                                deckInfo.Author = message.Username;
                                deckInfo.TimeAdded = message.Timestamp;

                                _dispatcher.Invoke(() =>
                                {
                                    using (var uiOperation = _performanceMonitor.StartOperation("UpdateDeckCollection"))
                                    {
                                        Decks.Insert(0, deckInfo);
                                        Log.Info($"Added deck to collection. Current deck count: {Decks.Count}");

                                        while (Decks.Count > MaxDeckCount)
                                        {
                                            Decks.RemoveAt(Decks.Count - 1);
                                            Log.Info($"Removed oldest deck. Current deck count: {Decks.Count}");
                                        }
                                    }
                                });
                                
                                Log.Info($"Successfully processed deck from {message.Username} with {deckInfo.Cards?.Count ?? 0} cards");
                            }
                            else
                            {
                                Log.Warn($"Failed to parse deck code: {deckCode}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error handling Twitch message: {ex.Message}");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs (propertyName));
        }
    }
}