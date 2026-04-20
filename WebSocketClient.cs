using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChatGalvanometer
{
    public class MessageNotification
    {
        public readonly string MessageText;
        public readonly string MessageTime;

        public MessageNotification(string _messageText, string _messageTime = "")
        {
            MessageText = _messageText;
            MessageTime = _messageTime;
        }
    }

    public class WebSocketClient : IDisposable
    {
        private ClientWebSocket _webSocket;
        private readonly Uri _serverUri;
        private CancellationTokenSource? _cts;

        public event Action<MessageNotification>? OnMessageReceived;
        public event Action? OnWelcomed;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public string? SessionId { get; private set; }

        public WebSocketClient(string serverUrl)
        {
            _webSocket = new ClientWebSocket();
            _serverUri = new Uri(serverUrl);
        }

        public async Task ConnectAsync()
        {
            if (_webSocket.State != WebSocketState.None)
            {
                _webSocket.Dispose();
                _webSocket = new ClientWebSocket();
            }
            _cts = new CancellationTokenSource();
            try
            {
                await _webSocket.ConnectAsync(_serverUri, _cts.Token);
                OnConnected?.Invoke();
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WebSocket connection failed: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts!.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        OnDisconnected?.Invoke();
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        JObject? parsedMessage;
                        try
                        {
                            parsedMessage = JObject.Parse(message);
                        }
                        catch
                        {
                            continue;
                        }

                        string? messageType = parsedMessage?["metadata"]?["message_type"]?.ToString();
                        switch (messageType)
                        {
                            case "session_welcome":
                                SessionId = parsedMessage?["payload"]?["session"]?["id"]?.ToString();
                                OnWelcomed?.Invoke();
                                break;
                            case "notification":
                                string? text = parsedMessage?["payload"]?["event"]?["message"]?["text"]?.ToString();
                                if (text != null)
                                {
                                    string asciiMessage = Regex.Replace(text, @"[^\u0020-\u007e]", "");
                                    string? timestamp = parsedMessage?["metadata"]?["message_timestamp"]?.ToString();
                                    OnMessageReceived?.Invoke(new MessageNotification(asciiMessage.Trim(), timestamp ?? ""));
                                }
                                break;
                            case "session_keepalive":
                                Trace.WriteLine("Keepalive, not parsing");
                                break;
                            case null:
                                Trace.WriteLine("Couldn't parse message type");
                                break;
                            default:
                                Trace.WriteLine($"Unknown message type ({messageType})");
                                OnMessageReceived?.Invoke(new MessageNotification(message));
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
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts!.Token);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                _cts?.Cancel();
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"WebSocket close error: {ex.Message}");
                }
                // OnDisconnected is fired by ReceiveMessagesAsync when the cancellation unwinds it
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _webSocket?.Dispose();
        }
    }
}
