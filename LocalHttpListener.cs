using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ChatGalvanometer
{
    public class LocalHttpListener
    {
        private readonly HttpListener _listener;
        private readonly int _port = 3000;
        public event Action<string>? OnAuthCodeReceived;
        private readonly string _htmlFilePath;


        public LocalHttpListener()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _htmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authPage.html");
        }

        public async Task StartListeningAsync()
        {
            try
            {
                _listener.Start();
                Trace.WriteLine($"Listening for authentication callback on http://localhost:{_port}/");

                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync(); // Wait for a request
                    var request = context.Request;

                    if (request.Url?.AbsolutePath == "/") // Ensure it's the right path
                    {
                        string responseString = File.Exists(_htmlFilePath)
                        ? File.ReadAllText(_htmlFilePath)
                        : "<html><body><h2>File Not Found</h2></body></html>";

                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    }
                    else if(request.Url?.AbsolutePath == "/authCode")
                    {
                        string authCode = request.QueryString["access_token"]; // Get auth code

                        // Respond to the request
                        string responseString = "<html><body><h2>Authentication Successful! You can close this window.</h2></body></html>";
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                        OnAuthCodeReceived?.Invoke(authCode); // Fire event with the auth code
                    }
                    else
                    {
                        // Respond to the request
                        string responseString = "<html><body><h2>What are you doing?</h2></body></html>";
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"HTTP Listener Error: {ex.Message}");
            }
            finally
            {
                _listener.Stop();
            }
        }

        public void StopListening()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
        }
    }
}
