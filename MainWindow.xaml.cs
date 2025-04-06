using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO.Ports;
using System.IO;

namespace ChatGalvanometer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WebSocketClient     _webSocketClient;
        private HttpClient          _httpClient;
        private Settings            _settings;
        private LocalHttpListener   _httpListener;
        private SerialCommunicator  serial;

        private static readonly object _lock = new();

        private int rawSentiment;
        private int lastSentimentCheckPoint;
        private decimal lastPercentiment;
        private Queue<int> rollingSentiment;
        private int zeroCycles;
        private List<string> messages;

        Timer callbackTimer;

        public MainWindow()
        {
            InitializeComponent();

            _webSocketClient = new WebSocketClient("wss://eventsub.wss.twitch.tv/ws");
            _webSocketClient.OnConnected += () => Dispatcher.Invoke(() => webSocketConnected());
            _webSocketClient.OnMessageReceived += _message => Dispatcher.Invoke(() => webSocketMessageReceived(_message));
            _webSocketClient.OnDisconnected += () => Dispatcher.Invoke(() => webSocketDisconnected());
            _webSocketClient.OnWelcomed += () => Dispatcher.Invoke(() => webSocketWelcomed());

            _httpClient = new HttpClient();
            _httpListener = new LocalHttpListener();
            _httpListener.OnAuthCodeReceived += _authCode => Dispatcher.Invoke(() => authCodeReceived(_authCode));

            _settings = Settings.Load();
            DataContext = _settings;

            // Apply settings
            this.Width = _settings.WindowWidth;
            this.Height = _settings.WindowHeight;

            rollingSentiment = new Queue<int>();
            lastPercentiment = 0;
            zeroCycles = 0;

            messages = new List<string>();

            if (_settings.ComPort != null && _settings.ComPort != "" && _settings.COMPorts.Count != 0)
            {
                for (int i = 0; i < _settings.COMPorts.Count; i++)
                {
                    if (_settings.COMPorts[i] == _settings.ComPort)
                    {
                        COMList.SelectedIndex = i;
                        break;
                    }
                }
            }

            callbackTimer = new Timer(SentimentCallback, this, 1000, 500);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            // Save window size before closing
            _settings.WindowWidth = (int)this.Width;
            _settings.WindowHeight = (int)this.Height;
            _settings.Save();
        }

        private async void ConnectToWebSocket()
        {
            await _webSocketClient.ConnectAsync();
        }

        private async void DisconnectFromWebSocket()
        {
            await _webSocketClient.DisconnectAsync();
        }

        private void webSocketConnected()
        {
            Trace.WriteLine("ConnectedToWebSocket");
        }

        private void webSocketDisconnected()
        {
            Trace.WriteLine("WebSocket Disconnected");
        }

        private void webSocketMessageReceived(string _message)
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

        private void UpdateSentiment(bool _positive)
        {
            rawSentiment += _positive == true ? 1 : -1;
        }

        static void SentimentCallback(object _objectState)
        {
            MainWindow caller = _objectState as MainWindow;

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
                        caller.serial = new SerialCommunicator(caller._settings.ComPort);
                        Thread.Sleep(10);
                        caller.serial.SendDecimal(percentSentiment);
                        caller.serial.Close();
                        caller._settings.PercentSentiment = percentSentiment;
                    }
                    else if (percentSentiment == 0)
                    {
                        caller.zeroCycles += 1;
                        if (caller.zeroCycles > 10)
                        {
                            caller.zeroCycles = 0;
                            caller.serial = new SerialCommunicator(caller._settings.ComPort);
                            Thread.Sleep(10);
                            caller.serial.SendDecimal(percentSentiment);
                            caller.serial.Close();
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

        private void webSocketWelcomed()
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
            _httpClient.PostJsonAsync("https://api.twitch.tv/helix/eventsub/subscriptions", payload, _settings.BearerToken, _settings.ClientId);
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectToWebSocket();
        }

        private async void getTokenButton_Click(object sender, RoutedEventArgs e)
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

        private void authCodeReceived(string _authCode)
        {
            Trace.WriteLine("New auth code success");
            _settings.BearerToken = _authCode;
            _httpListener.StopListening();
        }

        private async void UserIdLookupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string jsonResponse = await _httpClient.GetAsync($"https://api.twitch.tv/helix/users?login={_settings.UserName}", _settings.BearerToken, _settings.ClientId);
                dynamic parsedMessage = JObject.Parse(jsonResponse);
                _settings.UserId = parsedMessage?.data[0]?.id;
            }
            catch
            {
                Trace.WriteLine("Error parsing User Id lookup");
            }
        }

        private async void BroadcasterIdLookup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string jsonResponse = await _httpClient.GetAsync($"https://api.twitch.tv/helix/users?login={_settings.BroadcasterName}", _settings.BearerToken, _settings.ClientId);
                dynamic parsedMessage = JObject.Parse(jsonResponse);
                _settings.BroadcasterId = parsedMessage?.data[0]?.id;
            }
            catch
            {
                Trace.WriteLine("Error parsing Broadcaster Id lookup");
            }
        }

        private void COMList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.ComPort = e.AddedItems?[0].ToString();
        }

        private void COMTestButton_Click(object sender, RoutedEventArgs e)
        {
            lock (_lock)
            {
                serial = new SerialCommunicator(_settings.ComPort);
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