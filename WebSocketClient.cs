using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace ChatGalvanometer
{
    public class WebSocketClient : IDisposable
    {
        private readonly ClientWebSocket _webSocket;
        private readonly Uri _serverUri;
        private CancellationTokenSource? _cts;

        public event Action<string>? OnMessageReceived;
        public event Action? OnWelcomed;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public string? _sessionId;

        public WebSocketClient(string serverUrl)
        {
            _webSocket = new ClientWebSocket();
            _serverUri = new Uri(serverUrl);
            _sessionId = null;
        }

        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();
            try
            {
                await _webSocket.ConnectAsync(_serverUri, _cts.Token);
                OnConnected?.Invoke();
                _ = ReceiveMessagesAsync(); // Start listening for messages in the background
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WebSocket connection failed: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[8192];
            dynamic parsedMessage;
            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        OnDisconnected?.Invoke();
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        try
                        {
                            parsedMessage = JObject.Parse(message);
                        }
                        catch
                        {
                            continue;
                        }
                        string? messageType = parsedMessage?.metadata?.message_type;
                        switch (messageType)
                        {
                            case "session_welcome":
                                _sessionId = parsedMessage?.payload?.session?.id;
                                OnWelcomed?.Invoke();
                                break;
                            case "notification":
                                if (parsedMessage?.payload?.@event?.message?.text != null)
                                {
                                    string asciiMessage = Regex.Replace((string)(parsedMessage?.payload?.@event?.message?.text), @"[^\u0020-\u007e]", "");
                                    OnMessageReceived?.Invoke(asciiMessage.Trim());
                                }
                                break;
                            case null:
                                Trace.WriteLine("Couldn't parse message type");
                                break;
                            default:
                                Trace.WriteLine($"Unknown message type ({messageType})");
                                OnMessageReceived?.Invoke(message);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WebSocket receive error: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                _cts.Cancel();
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                OnDisconnected?.Invoke();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _webSocket?.Dispose();
        }
    }
}
