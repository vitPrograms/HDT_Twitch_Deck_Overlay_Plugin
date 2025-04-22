using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace TwitchDeckOverlay.Services
{
    public class TwitchService
    {
        private TcpClient _client; private StreamReader _reader; private StreamWriter _writer; private CancellationTokenSource _cts; private bool _isConnected;

        public event Action<string> MessageReceived;

        public async Task ConnectAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                Log.Error("Twitch channel is empty or null");
                return;
            }

            try
            {
                Disconnect(); // Очистити попереднє з’єднання

                Log.Info($"Attempting to connect to Twitch channel #{channel}");
                _client = new TcpClient();
                await _client.ConnectAsync("irc.chat.twitch.tv", 6667);

                var stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Анонімна авторизація
                await _writer.WriteLineAsync("PASS oauth:anonymous");
                await _writer.WriteLineAsync("NICK justinfan12345");
                await _writer.WriteLineAsync($"JOIN #{channel.ToLower()}");

                _cts = new CancellationTokenSource();
                _isConnected = true;

                Log.Info($"Successfully connected to Twitch channel #{channel}");
                _ = Task.Run(() => ReadChatAsync(channel, _cts.Token));
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log.Error($"ConnectAsync error: {ex.Message}");
            }
        }

        private async Task ReadChatAsync(string channel, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _client?.Connected == true)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null)
                    {
                        await ReconnectAsync(channel);
                        return;
                    }


                    if (line.StartsWith("PING"))
                    {
                        await _writer.WriteLineAsync("PONG :tmi.twitch.tv");
                    }
                    else if (line.Contains("PRIVMSG"))
                    {
                        MessageReceived?.Invoke(line);
                    }
                }
            }
            catch (IOException ex)
            {
                Log.Error($"IOException in ReadChatAsync: {ex.Message}");
                await ReconnectAsync(channel);
            }
            catch (Exception ex)
            {
                Log.Error($"ReadChatAsync error: {ex.Message}");
            }
        }

        private async Task ReconnectAsync(string channel)
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(channel))
            {
                Log.Warn("Reconnect skipped: Not connected or invalid channel");
                return;
            }

            _isConnected = false;

            try
            {
                Disconnect();
                await Task.Delay(5000);
                await ConnectAsync(channel);
            }
            catch (Exception ex)
            {
                Log.Error($"ReconnectAsync error: {ex.Message}");
                await Task.Delay(10000);
                await ReconnectAsync(channel);
            }
        }

        public void Disconnect()
        {
            try
            {
                _isConnected = false;
                _cts?.Cancel();
                _reader?.Dispose();
                _writer?.Dispose();
                if (_client != null)
                {
                    _client.Close();
                    _client.Dispose();
                }
                Log.Info("Disconnected from Twitch");
            }
            catch (Exception ex)
            {
                Log.Error($"Disconnect error: {ex.Message}");
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

}