using System;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatGalvanometer
{
    public class HttpClient
    {
        private readonly System.Net.Http.HttpClient _httpClient;

        public HttpClient()
        {
            _httpClient = new System.Net.Http.HttpClient();
        }

        public async Task<string> PostJsonAsync<T>(string url, T payload, string bearerToken, string clientId)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);

                response.EnsureSuccessStatusCode(); // Throws exception if status code is not 2xx

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP POST Error: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetAsync(string url, string bearerToken, string clientId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);

                HttpResponseMessage response = await _httpClient.GetAsync(url);

                response.EnsureSuccessStatusCode(); // Throws exception if status code is not 2xx

                return await response.Content.ReadAsStringAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP GET Error: {ex.Message}");
                return null;
            }
        }
    }
}
