using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CyclerSim.Services
{
    public class HttpService : IHttpService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpService> _logger;
        private string _serverUrl = "https://localhost:7090";

        public HttpService(ILogger<HttpService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public void SetServerUrl(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _logger.LogInformation("Server URL set to: {ServerUrl}", _serverUrl);
        }

        public async Task<bool> PostAsync<T>(string endpoint, T data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_serverUrl}{endpoint}";
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully posted to {Endpoint}", endpoint);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to post to {Endpoint}. Status: {StatusCode}", endpoint, response.StatusCode);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error posting to {Endpoint}", endpoint);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout posting to {Endpoint}", endpoint);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error posting to {Endpoint}", endpoint);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var url = $"{_serverUrl}/api/ClientData/channel";
                var response = await _httpClient.GetAsync(url);

                // Even if it returns 405 (Method Not Allowed), it means the server is reachable
                return response.StatusCode != System.Net.HttpStatusCode.NotFound;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}