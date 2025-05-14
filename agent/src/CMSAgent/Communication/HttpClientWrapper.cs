using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace CMSAgent.Communication
{
    /// <summary>
    /// Wrapper cho HttpClient, thêm retry, xử lý headers, và serialization.
    /// </summary>
    public class HttpClientWrapper : IHttpClientWrapper, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientWrapper> _logger;
        private readonly HttpClientSettingsOptions _settings;
        private readonly AsyncRetryPolicy _retryPolicy;
        private bool _disposed = false;

        /// <summary>
        /// Khởi tạo một instance mới của HttpClientWrapper.
        /// </summary>
        /// <param name="options">Cấu hình HttpClient.</param>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public HttpClientWrapper(IOptions<HttpClientSettingsOptions> options, ILogger<HttpClientWrapper> logger)
        {
            _logger = logger;
            _settings = options.Value;
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSec)
            };

            // Thiết lập chính sách retry
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3, 
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning("Lần thử lại thứ {RetryCount} sau {RetrySeconds} giây do lỗi: {ErrorMessage}",
                            retryCount, timeSpan.TotalSeconds, exception.Message);
                    });
        }

        /// <summary>
        /// Thực hiện HTTP GET request.
        /// </summary>
        /// <typeparam name="TResponse">Kiểu dữ liệu của response body.</typeparam>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <param name="queryParams">Các tham số query string (tùy chọn).</param>
        /// <returns>Response được deserialize từ JSON sang kiểu TResponse.</returns>
        public async Task<TResponse> GetAsync<TResponse>(string endpoint, string agentId, string? token, Dictionary<string, string>? queryParams = null)
        {
            var requestUri = BuildRequestUri(endpoint, queryParams);
            
            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                AddHeaders(request, agentId, token);
                
                return await SendRequestAsync<TResponse>(request);
            }
        }

        /// <summary>
        /// Thực hiện HTTP POST request mà không cần response body.
        /// </summary>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="data">Dữ liệu cần gửi lên server.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Task đại diện cho request.</returns>
        public async Task PostAsync(string endpoint, object data, string agentId, string? token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                AddHeaders(request, agentId, token);
                
                if (data != null)
                {
                    var json = JsonSerializer.Serialize(data);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                
                await SendRequestWithNoResponseBodyAsync(request);
            }
        }

        /// <summary>
        /// Thực hiện HTTP POST request và nhận response.
        /// </summary>
        /// <typeparam name="TRequest">Kiểu dữ liệu của request body.</typeparam>
        /// <typeparam name="TResponse">Kiểu dữ liệu của response body.</typeparam>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="data">Dữ liệu cần gửi lên server.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Response được deserialize từ JSON sang kiểu TResponse.</returns>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, string agentId, string? token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                AddHeaders(request, agentId, token);
                
                if (data != null)
                {
                    var json = JsonSerializer.Serialize(data);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                
                return await SendRequestAsync<TResponse>(request);
            }
        }

        /// <summary>
        /// Tải xuống file từ server.
        /// </summary>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Stream chứa dữ liệu file được tải xuống.</returns>
        public async Task<Stream> DownloadFileAsync(string endpoint, string agentId, string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddHeaders(request, agentId, token);
            
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Lỗi HTTP {StatusCode} khi tải file: {ErrorContent}", 
                            response.StatusCode, errorContent);
                        
                        throw new HttpRequestException($"Lỗi HTTP {response.StatusCode} khi tải file");
                    }
                    
                    return await response.Content.ReadAsStreamAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải file từ {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// Tải xuống file từ server và lưu vào stream đích.
        /// </summary>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="destinationStream">Stream đích để lưu file.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Task đại diện cho quá trình tải xuống.</returns>
        public async Task DownloadFileAsync(string endpoint, Stream destinationStream, string agentId, string? token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                AddHeaders(request, agentId, token);
                
                try
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogError("Lỗi HTTP {StatusCode} khi tải file: {ErrorContent}", 
                                response.StatusCode, errorContent);
                            
                            throw new HttpRequestException($"Lỗi HTTP {response.StatusCode} khi tải file");
                        }
                        
                        await (await response.Content.ReadAsStreamAsync()).CopyToAsync(destinationStream);
                        return true;
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tải file từ {Endpoint}", endpoint);
                    throw;
                }
            }
        }

        /// <summary>
        /// Tạo URI với các tham số query string.
        /// </summary>
        private string BuildRequestUri(string endpoint, Dictionary<string, string>? queryParams)
        {
            if (queryParams == null || queryParams.Count == 0)
            {
                return endpoint;
            }

            var queryString = new StringBuilder(endpoint);
            queryString.Append(endpoint.Contains("?") ? "&" : "?");
            
            bool isFirst = true;
            foreach (var param in queryParams)
            {
                if (!isFirst)
                {
                    queryString.Append("&");
                }
                
                queryString.Append(WebUtility.UrlEncode(param.Key));
                queryString.Append("=");
                queryString.Append(WebUtility.UrlEncode(param.Value));
                
                isFirst = false;
            }
            
            return queryString.ToString();
        }

        /// <summary>
        /// Thêm các header cần thiết vào request.
        /// </summary>
        private void AddHeaders(HttpRequestMessage request, string agentId, string? token)
        {
            // Thêm header Agent-ID
            if (!string.IsNullOrEmpty(agentId))
            {
                request.Headers.Add(CMSAgent.Common.Constants.HttpHeaders.AgentIdHeader, agentId);
            }
            
            // Thêm header Client-Type
            request.Headers.Add(CMSAgent.Common.Constants.HttpHeaders.ClientTypeHeader, CMSAgent.Common.Constants.HttpHeaders.ClientTypeValue);
            
            // Thêm header Authorization nếu có token
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            
            // Chấp nhận JSON
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Gửi request và xử lý response với body.
        /// </summary>
        private async Task<T> SendRequestAsync<T>(HttpRequestMessage request)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var response = await _httpClient.SendAsync(request);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Lỗi HTTP {StatusCode} khi gọi {Method} {Url}: {ErrorContent}", 
                            response.StatusCode, request.Method, request.RequestUri, errorContent);
                        
                        throw new HttpRequestException($"Lỗi HTTP {response.StatusCode} khi gọi {request.Method} {request.RequestUri}");
                    }
                    
                    var content = await response.Content.ReadAsStringAsync();
                    
                    if (string.IsNullOrEmpty(content))
                    {
                        return typeof(T) == typeof(string) ? (T)(object)string.Empty : default;
                    }
                    
                    try
                    {
                        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? default;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Lỗi khi deserialize JSON từ response của {Method} {Url}", 
                            request.Method, request.RequestUri);
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi request {Method} {Url}", 
                    request.Method, request.RequestUri);
                throw;
            }
        }

        /// <summary>
        /// Gửi request mà không cần xử lý response body.
        /// </summary>
        private async Task SendRequestWithNoResponseBodyAsync(HttpRequestMessage request)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    var response = await _httpClient.SendAsync(request);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Lỗi HTTP {StatusCode} khi gọi {Method} {Url}: {ErrorContent}", 
                            response.StatusCode, request.Method, request.RequestUri, errorContent);
                        
                        throw new HttpRequestException($"Lỗi HTTP {response.StatusCode} khi gọi {request.Method} {request.RequestUri}");
                    }
                    
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi request {Method} {Url}", 
                    request.Method, request.RequestUri);
                throw;
            }
        }

        /// <summary>
        /// Giải phóng tài nguyên.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Giải phóng tài nguyên.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
