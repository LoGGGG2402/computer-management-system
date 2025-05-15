using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace CMSAgent.Communication
{
    /// <summary>
    /// Wrapper for HttpClient, adding retry, handling headers, and serialization.
    /// </summary>
    public class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientWrapper> _logger;
        private readonly HttpClientSettingsOptions _settings;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly string _baseUrl;
        
        // Create static JsonSerializerOptions for reuse
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of HttpClientWrapper.
        /// </summary>
        /// <param name="options">HttpClient configuration.</param>
        /// <param name="logger">Logger for logging.</param>
        public HttpClientWrapper(IOptions<CmsAgentSettingsOptions> options, ILogger<HttpClientWrapper> logger)
        {
            _logger = logger;
            _settings = options.Value.HttpClientSettings;
            _baseUrl = options.Value.ServerUrl.TrimEnd('/') + options.Value.ApiPath.TrimEnd('/');
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSec),
                BaseAddress = new Uri(_baseUrl)
            };

            // Set up retry policy
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3, 
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry attempt {RetryCount} after {RetrySeconds} seconds due to error: {ErrorMessage}",
                            retryCount, timeSpan.TotalSeconds, exception.Message);
                    });
        }

        /// <summary>
        /// Performs an HTTP GET request.
        /// </summary>
        /// <typeparam name="TResponse">Response body data type.</typeparam>
        /// <param name="endpoint">API endpoint to call.</param>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="token">Authentication token (can be null).</param>
        /// <param name="queryParams">Query string parameters (optional).</param>
        /// <returns>Response deserialized from JSON to TResponse type.</returns>
        public async Task<TResponse> GetAsync<TResponse>(string endpoint, string agentId, string? token, Dictionary<string, string>? queryParams = null)
        {
            var requestUri = BuildRequestUri(endpoint, queryParams);
            
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            AddHeaders(request, agentId, token);
            
            return await SendRequestAsync<TResponse>(request);
        }

        /// <summary>
        /// Performs an HTTP POST request and receives a response.
        /// </summary>
        /// <typeparam name="TRequest">Request body data type.</typeparam>
        /// <typeparam name="TResponse">Response body data type.</typeparam>
        /// <param name="endpoint">API endpoint to call.</param>
        /// <param name="data">Data to send to the server.</param>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="token">Authentication token (can be null).</param>
        /// <returns>Response deserialized from JSON to TResponse type.</returns>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, string agentId, string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            AddHeaders(request, agentId, token);
            
            if (data != null)
            {
                var json = JsonSerializer.Serialize(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            
            return await SendRequestAsync<TResponse>(request);
        }

        /// <summary>
        /// Downloads a file from the server.
        /// </summary>
        /// <param name="endpoint">API endpoint to call.</param>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="token">Authentication token (can be null).</param>
        /// <returns>Stream containing the downloaded file data.</returns>
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
                        _logger.LogError("HTTP error {StatusCode} when downloading file: {ErrorContent}", 
                            response.StatusCode, errorContent);
                        
                        throw new HttpRequestException($"HTTP error {response.StatusCode} when downloading file");
                    }
                    
                    return await response.Content.ReadAsStreamAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from {Endpoint}", endpoint);
                throw;
            }
        }
        /// <summary>
        /// Creates URI with query string parameters.
        /// </summary>
        private string BuildRequestUri(string endpoint, Dictionary<string, string>? queryParams)
        {
            if (queryParams == null || queryParams.Count == 0)
            {
                return endpoint;
            }

            var queryString = new StringBuilder(endpoint);
            _ = queryString.Append(endpoint.Contains('?') ? '&' : '?');
            
            bool isFirst = true;
            foreach (var param in queryParams)
            {
                if (!isFirst)
                {
                    _ = queryString.Append('&');
                }

                _ = queryString.Append(WebUtility.UrlEncode(param.Key));
                _ = queryString.Append('=');
                _ = queryString.Append(WebUtility.UrlEncode(param.Value));
                
                isFirst = false;
            }
            
            return queryString.ToString();
        }

        /// <summary>
        /// Adds necessary headers to the request.
        /// </summary>
        private static void AddHeaders(HttpRequestMessage request, string agentId, string? token)
        {
            // Add Agent-ID header
            if (!string.IsNullOrEmpty(agentId))
            {
                request.Headers.Add(Common.Constants.HttpHeaders.AgentIdHeader, agentId);
            }
            
            // Add Client-Type header
            request.Headers.Add(Common.Constants.HttpHeaders.ClientTypeHeader, Common.Constants.HttpHeaders.ClientTypeValue);
            
            // Add Authorization header if token exists
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            
            // Accept JSON
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Sends request and processes response with body.
        /// </summary>
        private async Task<T> SendRequestAsync<T>(HttpRequestMessage originalRequest)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    // Create a new request for each attempt
                    using var request = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri);
                    
                    // Copy headers
                    foreach (var header in originalRequest.Headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                    
                    // Copy content if exists
                    if (originalRequest.Content != null)
                    {
                        var content = await originalRequest.Content.ReadAsStringAsync();
                        request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                    }

                    var response = await _httpClient.SendAsync(request);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("HTTP error {StatusCode} when calling {Method} {Uri}: {ErrorContent}",
                            response.StatusCode, request.Method, request.RequestUri, errorContent);
                        
                        throw new HttpRequestException($"HTTP error {response.StatusCode} when calling {request.Method} {request.RequestUri}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)
                           ?? throw new JsonException("Response body was null or empty");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request {Method} {Uri}", 
                    originalRequest.Method, originalRequest.RequestUri);
                throw;
            }
        }
    }
}
