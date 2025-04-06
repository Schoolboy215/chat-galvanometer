using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Threading;
using System.IO.Ports;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;

namespace ChatGalvanometer
{
    public class Handler
    {
        private readonly WebSocketClient _webSocketClient;
        private readonly HttpClient _httpClient;
        public readonly Settings _settings;
        private readonly LocalHttpListener _httpListener;
        private          SerialCommunicator? _serial;

        private static readonly object _lock = new();

        private int rawSentiment;
        private int lastSentimentCheckPoint;
        private decimal lastPercentiment;
        private Queue<int> rollingSentiment;
        private int zeroCycles;
        private List<string> messages;

        private readonly Timer callbackTimer;

        public Handler()
        {
            _webSocketClient = new WebSocketClient("wss://eventsub.wss.twitch.tv/ws");
            _webSocketClient.OnConnected += () => Application.Current.Dispatcher.Invoke(() => WebSocketConnected());
            _webSocketClient.OnMessageReceived += _message => Application.Current.Dispatcher.Invoke(() => WebSocketMessageReceived(_message));
            _webSocketClient.OnDisconnected += () => Application.Current.Dispatcher.Invoke(() => WebSocketDisconnected());
            _webSocketClient.OnWelcomed += () => Application.Current.Dispatcher.Invoke(() => WebSocketWelcomed());

            _httpClient = new HttpClient();
            _httpListener = new LocalHttpListener();
            _httpListener.OnAuthCodeReceived += _authCode => Application.Current.Dispatcher.Invoke(() => authCodeReceived(_authCode));

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


                    int windowSentiment = 0;
                    for (var i = 0; i < caller.rollingSentiment.Count; i++)
                    {
                        windowSentiment += caller.rollingSentiment.ElementAt(i);
                    }

                    if (Math.Abs(windowSentiment) > caller._settings.MaxSentiment)
                    {
                        windowSentiment = (int)(caller._settings.MaxSentiment * (windowSentiment > 0 ? 1 : -1));
                    }

                    decimal percentSentiment = ((decimal)windowSentiment / (decimal)caller._settings.MaxSentiment);
                    percentSentiment = Math.Round(percentSentiment, 2);

                    if (percentSentiment != caller.lastPercentiment)
                    {
                        Trace.WriteLine($"New percent sentiment is {percentSentiment}");
                        caller._serial = new SerialCommunicator(caller._settings.ComPort);
                        Thread.Sleep(10);
                        caller._serial.SendDecimal(percentSentiment);
                        caller._serial.Close();
                        caller._settings.PercentSentiment = percentSentiment;
                    }
                    else if (percentSentiment == 0)
                    {
                        caller.zeroCycles += 1;
                        if (caller.zeroCycles > 10)
                        {
                            caller.zeroCycles = 0;
                            caller._serial = new SerialCommunicator(caller._settings.ComPort);
                            Thread.Sleep(10);
                            caller._serial.SendDecimal(percentSentiment);
                            caller._serial.Close();
                        }
                    }
                    caller.lastPercentiment = percentSentiment;
                }
            }
            catch (Exception ex)
            {
                int i = 0;
            }
        }

        private void UpdateSentiment(bool _positive)
        {
            rawSentiment += _positive == true ? 1 : -1;
        }

        private static void WebSocketConnected()
        {
            Trace.WriteLine("ConnectedToWebSocket");
        }

        private async void WebSocketDisconnected()
        {
            Trace.WriteLine("WebSocket Disconnected");
            await _webSocketClient.ConnectAsync();
        }

        private void WebSocketMessageReceived(string _message)
        {

            // Uncomment to save chat messages in a file
            //messages.Add(_message);
            //if (messages.Count > 1000)
            //{
            //    File.AppendAllLines("chatLog.txt", messages);
            //    messages.Clear();
            //}

            Trace.WriteLine($"Received: {_message}");

            // Good message
            if (_settings.GoodItems.Contains(_message))
            {
                lock (_lock)
                {
                    UpdateSentiment(true);
                }
            }

            // Bad message
            else if (_settings.BadItems.Contains(_message))
            {
                lock (_lock)
                {
                    UpdateSentiment(false);
                }
            }
        }

        private async void WebSocketWelcomed()
        {
            Trace.WriteLine("Welcomed to server, subscribing to chat event");
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
                    session_id = _webSocketClient._sessionId
                }
            };
            await _httpClient.PostJsonAsync("https://api.twitch.tv/helix/eventsub/subscriptions", payload, _settings.BearerToken, _settings.ClientId);
        }

        public async Task GetToken()
        {
            string authUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={_settings.ClientId}&response_type=token&redirect_uri=http://localhost:3000&scope=user%3Aread%3Achat";

            try
            {
                // Open the authentication URL in the default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                // Start listening for the authentication callback
                await _httpListener.StartListeningAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting authentication: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<String> GetUserId(String _name)
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

        private void authCodeReceived(string _authCode)
        {
            Trace.WriteLine("New auth code success");
            _settings.BearerToken = _authCode;
            _httpListener.StopListening();
        }

        public void TestCOMPort()
        {
            lock (_lock)
            {
                _serial = new SerialCommunicator(_settings.ComPort);
                Thread.Sleep(10);
                _serial.SendDecimal(-1);
                Thread.Sleep(500);
                _serial.SendDecimal(0);
                Thread.Sleep(500);
                _serial.SendDecimal(1);
                Thread.Sleep(500);
                _serial.SendDecimal(0);
                Thread.Sleep(500);
                _serial.Close();
            }
        }
    }
}
