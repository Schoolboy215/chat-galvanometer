using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Windows;
using Newtonsoft.Json.Linq;
using Timer = System.Threading.Timer;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ChatGalvanometer
{
    public class Handler
    {
        private readonly WebSocketClient _webSocketClient;
        private readonly TwitchApiClient _httpClient;
        private readonly Settings _settings;
        private readonly LocalHttpListener _httpListener;

        public Settings Settings => _settings;

        private static readonly object _lock = new();
        private bool _intentionalDisconnect;

        private int rawSentiment;
        private int lastSentimentCheckPoint;
        private decimal lastPercentiment;
        private Queue<int> rollingSentiment;
        private int zeroCycles;
        private List<MessageNotification> messages;

        private CancellationTokenSource? _replayCts;

        private readonly Timer callbackTimer;

        public Handler()
        {
            _webSocketClient = new WebSocketClient("wss://eventsub.wss.twitch.tv/ws");
            _webSocketClient.OnConnected += () => Application.Current.Dispatcher.Invoke(() => WebSocketConnected());
            _webSocketClient.OnMessageReceived += _message => Application.Current.Dispatcher.Invoke(() => WebSocketMessageReceived(_message));
            _webSocketClient.OnDisconnected += () => Application.Current.Dispatcher.Invoke(() => WebSocketDisconnected());
            _webSocketClient.OnWelcomed += () => Application.Current.Dispatcher.Invoke(() => WebSocketWelcomed());

            _httpClient = new TwitchApiClient();
            _httpListener = new LocalHttpListener();
            _httpListener.OnAuthCodeReceived += _authCode => Application.Current.Dispatcher.Invoke(() => AuthCodeReceived(_authCode));

            _settings = Settings.Load();

            messages = [];

            rawSentiment = 0;
            lastSentimentCheckPoint = 0;
            lastPercentiment = 0;
            rollingSentiment = new Queue<int>();
            zeroCycles = 0;

            callbackTimer = new Timer(TimerCallback, this, 1000, 500);
        }

        public async Task ConnectToWebSocket()
        {
            await _webSocketClient.ConnectAsync();
        }

        public async Task DisconnectFromWebSocket()
        {
            _intentionalDisconnect = true;
            _settings.IsConnected = false;
            await _webSocketClient.DisconnectAsync();
        }

        static void TimerCallback(object _objectState)
        {
            Handler? caller = _objectState as Handler;

            if (caller == null)
            {
                return;
            }

            try
            {
                lock (_lock)
                {
                    int sentimentChange = caller.rawSentiment - caller.lastSentimentCheckPoint;

                    caller.lastSentimentCheckPoint = caller.rawSentiment;
                    caller._settings.RawSentiment = caller.rawSentiment;
                    caller.rollingSentiment.Enqueue(sentimentChange);

                    while (caller.rollingSentiment.Count > (caller._settings.EvaluationWindowLength * 2))
                    {
                        caller.rollingSentiment.Dequeue();
                    }

                    int windowSentiment = caller.rollingSentiment.Sum();

                    if (Math.Abs(windowSentiment) > caller._settings.MaxSentiment)
                    {
                        windowSentiment = (int)(caller._settings.MaxSentiment * (windowSentiment > 0 ? 1 : -1));
                    }

                    decimal percentSentiment = ((decimal)windowSentiment / (decimal)caller._settings.MaxSentiment);
                    percentSentiment = Math.Round(percentSentiment, 2);

                    if (percentSentiment != caller.lastPercentiment)
                    {
                        Trace.WriteLine($"New percent sentiment is {percentSentiment}");
                        var serial = new SerialCommunicator(caller._settings.ComPort);
                        Thread.Sleep(10);
                        serial.SendDecimal(percentSentiment);
                        serial.Close();
                        caller._settings.PercentSentiment = percentSentiment;
                    }
                    else if (percentSentiment == 0)
                    {
                        caller.zeroCycles += 1;
                        if (caller.zeroCycles > 10)
                        {
                            caller.zeroCycles = 0;
                            var serial = new SerialCommunicator(caller._settings.ComPort);
                            Thread.Sleep(10);
                            serial.SendDecimal(percentSentiment);
                            serial.Close();
                        }
                    }
                    caller.lastPercentiment = percentSentiment;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Timer callback error: {ex}");
            }
        }

        private void UpdateSentiment(bool _positive)
        {
            rawSentiment += _positive ? 1 : -1;
        }

        private static void WebSocketConnected()
        {
            Trace.WriteLine("ConnectedToWebSocket");
        }

        private async void WebSocketDisconnected()
        {
            Trace.WriteLine("WebSocket Disconnected");
            _settings.IsConnected = false;
            if (_intentionalDisconnect)
            {
                _intentionalDisconnect = false;
                return;
            }
            await _webSocketClient.ConnectAsync();
        }

        private void WebSocketMessageReceived(MessageNotification _message)
        {
            messages.Add(_message);
            if (messages.Count > 1000)
                DumpMessages();

            ProcessMessageSentiment(_message);
        }

        private void ProcessMessageSentiment(MessageNotification _message)
        {
            Trace.WriteLine($"Received: {_message.MessageText}");
            Application.Current.Dispatcher.BeginInvoke(() => _settings.LastMessage = _message.MessageText);

            bool isGood = _settings.MatchAnywhere
                ? _settings.GoodItems.Any(k => _message.MessageText.Contains(k, StringComparison.OrdinalIgnoreCase))
                : _settings.GoodItems.Contains(_message.MessageText);
            bool isBad = _settings.MatchAnywhere
                ? _settings.BadItems.Any(k => _message.MessageText.Contains(k, StringComparison.OrdinalIgnoreCase))
                : _settings.BadItems.Contains(_message.MessageText);

            if (isGood)
            {
                lock (_lock)
                {
                    UpdateSentiment(true);
                }
            }
            else if (isBad)
            {
                lock (_lock)
                {
                    UpdateSentiment(false);
                }
            }
        }

        public void StartReplay(string filePath, string? startTimeStr)
        {
            _replayCts?.Cancel();
            _replayCts = new CancellationTokenSource();
            var ct = _replayCts.Token;

            lock (_lock)
            {
                rawSentiment = 0;
                lastSentimentCheckPoint = 0;
                lastPercentiment = 0;
                rollingSentiment.Clear();
                zeroCycles = 0;
            }

            _settings.IsReplaying = true;
            Trace.WriteLine($"Replay starting: {filePath}, start time: '{startTimeStr}'");

            _ = Task.Run(async () =>
            {
                try
                {
                    int callbackCount = 0;
                    await ReplayController.Play(filePath, startTimeStr, (msg, ts) =>
                    {
                        if (callbackCount++ < 3)
                            Trace.WriteLine($"Replay callback #{callbackCount}: ts={ts.UtcDateTime:O}");
                        ProcessMessageSentiment(msg);
                        Application.Current.Dispatcher.BeginInvoke(() =>
                            _settings.ReplayCurrentTime = ts.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
                    }, ct);
                    Trace.WriteLine("Replay finished normally");
                }
                catch (OperationCanceledException)
                {
                    Trace.WriteLine("Replay stopped by user");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Replay error: {ex}");
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show($"Replay error: {ex.Message}", "Replay Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                finally
                {
                    Application.Current.Dispatcher.Invoke(() => _settings.IsReplaying = false);
                }
            });
        }

        public void StopReplay()
        {
            _replayCts?.Cancel();
        }

        public void DumpMessages()
        {
            var csv = new StringBuilder();

            foreach (var m in messages)
            {
                var escapedText = m.MessageText.Replace("\"", "\"\"");
                var newLine = string.Format("\"{0}\",{1}", escapedText, m.MessageTime);
                csv.AppendLine(newLine);
            }
            File.AppendAllText("chatLog.csv", csv.ToString());
            messages.Clear();
        }

        private async void WebSocketWelcomed()
        {
            Trace.WriteLine("Welcomed to server, subscribing to chat event");
            _settings.IsConnected = true;
            var payload = new
            {
                type = "channel.chat.message",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = _settings.BroadcasterId,
                    user_id = _settings.UserId
                },
                transport = new
                {
                    method = "websocket",
                    session_id = _webSocketClient.SessionId
                }
            };
            try
            {
                await _httpClient.PostJsonAsync("https://api.twitch.tv/helix/eventsub/subscriptions", payload, _settings.BearerToken, _settings.ClientId);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to subscribe to chat events: {ex.Message}");
                MessageBox.Show($"Failed to subscribe to chat events: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task GetToken()
        {
            string authUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={_settings.ClientId}&response_type=token&redirect_uri=http://localhost:3000&scope=user%3Aread%3Achat";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                await _httpListener.StartListeningAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting authentication: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<string> GetUserId(string _name)
        {
            try
            {
                string jsonResponse = await _httpClient.GetAsync($"https://api.twitch.tv/helix/users?login={_name}", _settings.BearerToken, _settings.ClientId);
                dynamic parsedMessage = JObject.Parse(jsonResponse);
                return parsedMessage?.data[0]?.id;
            }
            catch
            {
                throw new Exception("Error parsing User Id lookup");
            }
        }

        private void AuthCodeReceived(string _authCode)
        {
            Trace.WriteLine("New auth code success");
            _settings.BearerToken = _authCode;
            _httpListener.StopListening();
        }

        public void TestCOMPort()
        {
            lock (_lock)
            {
                var serial = new SerialCommunicator(_settings.ComPort);
                Thread.Sleep(10);
                serial.SendDecimal(-1);
                Thread.Sleep(500);
                serial.SendDecimal(0);
                Thread.Sleep(500);
                serial.SendDecimal(1);
                Thread.Sleep(500);
                serial.SendDecimal(0);
                Thread.Sleep(500);
                serial.Close();
            }
        }
    }
}
