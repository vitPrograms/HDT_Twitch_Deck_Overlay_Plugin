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
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private bool _isConnected;
        private string _currentChannel;

        public event Action<string> MessageReceived;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _connectionLock = new object();

        public async Task ConnectAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                Log.Error("Twitch channel is empty or null");
                return;
            }

            lock (_connectionLock)
            {
                if (_isConnected && _currentChannel == channel.ToLower())
                {
                    Log.Info($"Already connected to channel #{channel}");
                    return;
                }
            }

            try
            {
                Disconnect(); // Очистити попереднє з'єднання

                _client = new TcpClient();
                _client.ReceiveTimeout = 30000; // 30 секунд таймаут
                _client.SendTimeout = 10000; // 10 секунд таймаут
                
                await _client.ConnectAsync("irc.chat.twitch.tv", 6667);

                var stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Анонімна авторизація
                await _writer.WriteLineAsync("PASS oauth:anonymous");
                await _writer.WriteLineAsync("NICK justinfan12345");
                await _writer.WriteLineAsync($"JOIN #{channel.ToLower()}");

                lock (_connectionLock)
                {
                    _isConnected = true;
                    _currentChannel = channel.ToLower();
                }
                
                Log.Info($"Successfully connected to Twitch channel #{channel}");
                _ = Task.Run(() => ReadChatAsync(channel, _cts.Token));
            }
            catch (Exception ex)
            {
                lock (_connectionLock)
                {
                    _isConnected = false;
                    _currentChannel = null;
                }
                Log.Error($"ConnectAsync error: {ex.Message}");
                // Не скасовуємо _cts тут, щоб цикл перепідключення тривав
            }
        }

        private async Task ReadChatAsync(string channel, CancellationToken token)
        {
            try
            {
                DateTime lastPingReceived = DateTime.UtcNow;
                const int pingTimeoutMinutes = 6;
                const int sendPingIntervalMinutes = 4;
                DateTime lastPingSent = DateTime.UtcNow;

                while (!token.IsCancellationRequested && IsConnectionValid())
                {
                    // Задаємо таймаут для ReadLineAsync через Task.WhenAny
                    var readTask = _reader.ReadLineAsync();
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(pingTimeoutMinutes), token);
                    var completedTask = await Task.WhenAny(readTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        TimeSpan timeSinceLastPing = DateTime.UtcNow - lastPingReceived;
                        if (timeSinceLastPing.TotalMinutes >= pingTimeoutMinutes)
                        {
                            Log.Warn($"No PING received for {pingTimeoutMinutes} minutes. Attempting to reconnect...");
                            await ReconnectAsync(channel);
                            return;
                        }

                        TimeSpan timeSinceLastSentPing = DateTime.UtcNow - lastPingSent;
                        if (timeSinceLastSentPing.TotalMinutes >= sendPingIntervalMinutes)
                        {
                            if (IsConnectionValid())
                            {
                                try
                                {
                                    await _writer.WriteLineAsync("PING :tmi.twitch.tv");
                                    lastPingSent = DateTime.UtcNow;
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"Failed to send PING: {ex.Message}");
                                    await ReconnectAsync(channel);
                                    return;
                                }
                            }
                        }
                        continue;
                    }

                    // Обробляємо результат читання
                    var line = await readTask;
                    if (line == null)
                    {
                        Log.Warn("Received null line from Twitch IRC, connection may be closed");
                        await ReconnectAsync(channel);
                        return;
                    }

                    if (line.StartsWith("PING"))
                    {
                        if (IsConnectionValid())
                        {
                            try
                            {
                                await _writer.WriteLineAsync("PONG :tmi.twitch.tv");
                                lastPingReceived = DateTime.UtcNow;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Failed to send PONG: {ex.Message}");
                                await ReconnectAsync(channel);
                                return;
                            }
                        }
                    }
                    else if (line.Contains("PRIVMSG"))
                    {
                        try
                        {
                            MessageReceived?.Invoke(line);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error in MessageReceived handler: {ex.Message}");
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Log.Error($"IOException in ReadChatAsync: {ex.Message}");
                await ReconnectAsync(channel);
            }
            catch (ObjectDisposedException ex)
            {
                Log.Info($"Connection disposed in ReadChatAsync: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"ReadChatAsync error: {ex.Message}");
                await ReconnectAsync(channel);
            }
        }

        private bool IsConnectionValid()
        {
            lock (_connectionLock)
            {
                return _isConnected && _client?.Connected == true && _reader != null && _writer != null;
            }
        }

        private async Task ReconnectAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                Log.Warn("Reconnect skipped: Invalid channel");
                return;
            }

            lock (_connectionLock)
            {
                _isConnected = false;
                _currentChannel = null;
            }

            int retryDelaySeconds = 10; // Початкова затримка 10 секунд
            const int maxRetryDelaySeconds = 300; // Максимальна затримка 5 хвилин
            int attemptCount = 0;
            const int maxAttempts = 10; // Обмежуємо кількість спроб

            while (!IsConnectionValid() && !(_cts?.Token.IsCancellationRequested ?? true) && attemptCount < maxAttempts)
            {
                attemptCount++;
                try
                {
                    Log.Info($"Reconnect attempt #{attemptCount} for channel #{channel} with delay {retryDelaySeconds}s");
                    Disconnect(); // Очищаємо попереднє з'єднання
                    
                    if (!(_cts?.Token.IsCancellationRequested ?? true))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), _cts?.Token ?? CancellationToken.None);
                        await ConnectAsync(channel);
                        
                        // Перевіряємо чи справді підключилися
                        if (IsConnectionValid())
                        {
                            Log.Info($"Successfully reconnected to Twitch channel #{channel} on attempt #{attemptCount}");
                            return;
                        }
                        else
                        {
                            Log.Warn($"Connection attempt #{attemptCount} appeared successful but connection state is invalid");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Reconnect cancelled");
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error($"Reconnect attempt #{attemptCount} failed: {ex.Message}");
                    retryDelaySeconds = Math.Min(retryDelaySeconds * 2, maxRetryDelaySeconds); // Експоненціальна затримка
                }
            }

            if (IsConnectionValid())
            {
                Log.Info($"Successfully reconnected to Twitch channel #{channel}");
            }
            else
            {
                Log.Error($"Failed to reconnect to Twitch channel #{channel} after {attemptCount} attempts. Plugin may need manual restart.");
            }
        }

        public void Disconnect()
        {
            try
            {
                lock (_connectionLock)
                {
                    _isConnected = false;
                    _currentChannel = null;
                }
                
                _cts?.Cancel();
                
                try
                {
                    _reader?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error disposing reader: {ex.Message}");
                }
                
                try
                {
                    _writer?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error disposing writer: {ex.Message}");
                }
                
                if (_client != null)
                {
                    try
                    {
                        _client.Close();
                        _client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Error disposing client: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Disconnect error: {ex.Message}");
            }
            finally
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }
        }
    }
}