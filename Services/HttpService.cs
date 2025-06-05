using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CyclerSim.Services
{
    // HttpService.cs 수정
    public class HttpService : IHttpService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpService> _logger;
        private string _serverUrl = "https://localhost:7090";

        public HttpService(ILogger<HttpService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // 타임아웃 증가

            // SSL 인증서 검증 우회 (개발용)
            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
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
                var json = JsonConvert.SerializeObject(data, Formatting.None);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_serverUrl}{endpoint}";

                _logger.LogDebug("Sending POST request to: {Url}", url);
                _logger.LogDebug("Request payload: {Json}", json);

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Successfully posted to {Endpoint}. Response: {Response}",
                        endpoint, responseContent);
                    return true;
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to post to {Endpoint}. Status: {StatusCode}, Response: {Response}",
                        endpoint, response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error posting to {Endpoint}: {Message}", endpoint, ex.Message);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout posting to {Endpoint}: {Message}", endpoint, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error posting to {Endpoint}: {Message}", endpoint, ex.Message);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // 새로운 테스트 엔드포인트 사용
                var url = $"{_serverUrl}/api/ClientData/test";

                _logger.LogInformation("Testing connection to: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Connection test successful. Response: {Response}", responseContent);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Connection test failed. Status: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Connection test failed - HTTP error: {Message}", ex.Message);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Connection test failed - Timeout: {Message}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed - Unexpected error: {Message}", ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}