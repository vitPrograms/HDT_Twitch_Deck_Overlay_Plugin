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
using Hearthstone_Deck_Tracker.Hearthstone;
using TwitchDeckOverlay.UI; // Додано для ImprovedOverlayView

namespace TwitchDeckOverlay
{
    public class TwitchDeckManager : INotifyPropertyChanged
    {
        private readonly TwitchService _twitchService;
        private readonly BlizzardApiService _blizzardApi;
        private readonly PluginConfig _config;
        private readonly Regex _deckCodeRegex = new Regex(@"(?:^|\s)(?:deck code: )?([a-zA-Z0-9+/=]{20,})(?:\s|$)", RegexOptions.Compiled);
        private readonly Dispatcher _dispatcher;
        private Canvas _canvas;
        private UserControl _overlayView;
        // MaxDeckCount is now configurable via PluginConfig.MaxDecksToShow
        
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

        private string _twitchChannelNickname;
        public string TwitchChannelNickname
        {
            get => _twitchChannelNickname;
            set
            {
                if (_twitchChannelNickname != value)
                {
                    _twitchChannelNickname = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isLoadingHSGuruData;
        public bool IsLoadingHSGuruData
        {
            get => _isLoadingHSGuruData;
            set
            {
                if (_isLoadingHSGuruData != value)
                {
                    _isLoadingHSGuruData = value;
                    OnPropertyChanged();
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

        public void UpdateCollection()
        {
            Log.Info("Triggered TwitchDeckManager collection update.");
            var collection = Hearthstone_Deck_Tracker.Hearthstone.CollectionHelpers.Hearthstone.GetCollection().Result;
            if (_overlayView is ImprovedOverlayView improvedOverlayView)
            {
                _dispatcher.Invoke(() => improvedOverlayView.UpdateCardCollection(collection));
            }
        }

        public void ApplyConfig(PluginConfig config)
        {
            TwitchChannel = config.TwitchChannel;
            _blizzardApi.UpdateBearerToken(config.BlizzardBearerToken);
 
            Log.Info("Applied new config");
        }

        public async Task ProcessClipboardDeckCodeAsync(string clipboardContent)
        {
            using (var operation = _performanceMonitor.StartOperation("ProcessClipboardDeckCode"))
            {
                Log.Info($"Processing clipboard content for deck code: {clipboardContent}");
                var match = _deckCodeRegex.Match(clipboardContent);
                if (match.Success)
                {
                    string deckCode = match.Groups[1].Value;
                    Log.Info($"Found deck code in clipboard: {deckCode}");
                    // Use a dummy TwitchMessage to reuse existing logic
                    var dummyMessage = new TwitchMessage("Clipboard", deckCode) { Timestamp = DateTime.Now };
                    await HandleTwitchMessage(dummyMessage);
                }
                else
                {
                    Log.Info("No deck code found in clipboard content.");
                }
            }
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

        private async void HandleRawMessage(string raw)
        {
            using (var operation = _performanceMonitor.StartOperation("HandleRawMessage"))
            {
                try
                {
                    if (!raw.Contains("PRIVMSG"))
                    {
                        return;
                    }



                    var match = Regex.Match(raw, @":(?<user>[^!]+)![^ ]+ PRIVMSG #[^ ]+ :(?<msg>.+)");
                    if (match.Success)
                    {
                        var username = match.Groups["user"].Value;
                        var content = match.Groups["msg"].Value;



                        var message = new TwitchMessage(username, content);
                        await HandleTwitchMessage(message);
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

        private async Task HandleTwitchMessage(TwitchMessage message)
        {
            using (var operation = _performanceMonitor.StartOperation("HandleTwitchMessage"))
            {
                try
                {
                    var match = _deckCodeRegex.Match(message.Content);
                    if (match.Success)
                    {
                        string deckCode = match.Groups[1].Value;

                        
                        using (var deckParseOperation = _performanceMonitor.StartOperation("ParseDeckCode"))
                        {
                            DeckInfo deckInfo = null;
                            
                            // Спробуємо спочатку HearthDb парсер
                            if (HearthDbDeckParser.IsAvailable)
                            {
                                Log.Info("Using HearthDb parser for deck code");
                                deckInfo = HearthDbDeckParser.ParseDeckCode(deckCode, message.Username);
                            }
                            
                            // Якщо HearthDb не спрацював, використовуємо Blizzard API
                            if (deckInfo == null)
                            {
                                Log.Info("Using Blizzard API for deck code");
                                deckInfo = await _blizzardApi.GetDeckInfoAsync(deckCode);
                            }
                            if (deckInfo != null)
                            {
                                deckInfo.Author = message.Username;
                                deckInfo.TimeAdded = message.Timestamp;
                                deckInfo.DeckCode = deckCode;

                                _dispatcher.Invoke(() =>
                                {
                                    using (var uiOperation = _performanceMonitor.StartOperation("UpdateDeckCollection"))
                                    {
                                        Decks.Insert(0, deckInfo);
                                        Log.Info($"Added deck to collection. Current deck count: {Decks.Count}");



                                        while (Decks.Count > _config.MaxDecksInList)
                                        {
                                            Decks.RemoveAt(Decks.Count - 1);
                                            Log.Info($"Removed oldest deck. Current deck count: {Decks.Count}");
                                        }
                                    }
                                });
                                

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