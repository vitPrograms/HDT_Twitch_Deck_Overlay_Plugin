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

namespace TwitchDeckOverlay
{
    public class TwitchDeckManager : INotifyPropertyChanged
    {
        private readonly TwitchService _twitchService;
        private readonly BlizzardApiService _blizzardApi;
        private readonly Regex _deckCodeRegex = new Regex(@"(?:^|\s)(?:deck code: )?([a-zA-Z0-9+/=]{20,})(?:\s|$)", RegexOptions.Compiled);
        private readonly Dispatcher _dispatcher;
        private Canvas _canvas;
        private UserControl _overlayView;
        private const int MaxDeckCount = 5;

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
            _blizzardApi = new BlizzardApiService(config.BlizzardBearerToken);
            _canvas = canvas;
            _overlayView = overlayView;

            _twitchService.MessageReceived += HandleRawMessage;

            TwitchChannel = config.TwitchChannel;
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
            Log.Info("TwitchDeckManager shutdown");
        }

        public void ApplyConfig(PluginConfig config)
        {
            TwitchChannel = config.TwitchChannel;
            _blizzardApi.UpdateBearerToken(config.BlizzardBearerToken);
            Log.Info("Applied new config");
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
                    Log.Info($"Parsed message - User: {username}, Content: {content}");

                    var message = new TwitchMessage(username, content);
                    HandleTwitchMessage(message);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"IRC parse error: {ex.Message}");
            }
        }

        private async void HandleTwitchMessage(TwitchMessage message)
        {
            try
            {
                Log.Info($"Processing Twitch message from {message.Username}: {message.Content}");
                var match = _deckCodeRegex.Match(message.Content);
                if (match.Success)
                {
                    string deckCode = match.Groups[1].Value;
                    var deckInfo = await _blizzardApi.GetDeckInfoAsync(deckCode);
                    if (deckInfo != null)
                    {
                        deckInfo.Author = message.Username;
                        deckInfo.TimeAdded = message.Timestamp;

                        _dispatcher.Invoke(() =>
                        {
                            Decks.Insert(0, deckInfo);
                            Log.Info($"Added deck to collection. Current deck count: {Decks.Count}");

                            while (Decks.Count > MaxDeckCount)
                            {
                                Decks.RemoveAt(Decks.Count - 1);
                                Log.Info($"Removed oldest deck. Current deck count: {Decks.Count}");
                            }
                        });
                    }
                }
                else
                {
                    Log.Info("No deck code found in message");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling Twitch message: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}