using CMSAgent.Configuration;
using CMSAgent.Models; // Ensure this is present for HardwareInfo, ApiPayloads etc.
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CMSAgent.Communication
{
    public class HttpClientService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;
        private readonly ConfigManager _configManager;
        private string? _deviceIdHeader; // Stores the device_id for X-Agent-Id header

        public HttpClientService(ILogger<HttpClientService> logger, ConfigManager configManager, StateManager stateManager) // Added StateManager
        {
            _logger = logger;
            _configManager = configManager;
            _httpClient = new HttpClient();
            var serverUrl = _configManager.GetServerUrl();
            if (string.IsNullOrEmpty(serverUrl))
            {
                _logger.LogCritical("Server URL is not configured. HttpClientService cannot operate.");
                throw new InvalidOperationException("Server URL is not configured.");
            }
            // BaseAddress should be just scheme://host:port, e.g., http://your-server-ip:3000
            // The /api/agent/ part will be added to each request endpoint path.
            _httpClient.BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CMSAgent.NET/1.0"); // Example User-Agent

            // Device ID for header is set once available
            _deviceIdHeader = stateManager.GetDeviceId(); 
            if (!string.IsNullOrEmpty(_deviceIdHeader))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Agent-Id", _deviceIdHeader);
            }
            else
            {
                _logger.LogWarning("Device ID is not yet available. X-Agent-Id header will not be set initially.");
            }
        }

        // This method is to update the X-Agent-Id header if DeviceId is obtained after constructor.
        public void UpdateAgentIdHeader(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.LogWarning("Attempted to update X-Agent-Id header with null or empty deviceId.");
                return;
            }
            _deviceIdHeader = deviceId;
            _httpClient.DefaultRequestHeaders.Remove("X-Agent-Id");
            _httpClient.DefaultRequestHeaders.Add("X-Agent-Id", _deviceIdHeader);
            _logger.LogInformation("X-Agent-Id header updated to: {DeviceId}", _deviceIdHeader);
        }

        private async Task<TResponse?> MakeRequestAsync<TResponse>(HttpMethod method, string endpointSuffix, HttpContent? content = null)
        {
            // endpointSuffix is e.g., "identify", "hardware-info"
            // It will be appended to "api/agent/"
            var requestUrl = $"api/agent/{endpointSuffix.TrimStart('/')}";

            var request = new HttpRequestMessage(method, requestUrl);
            if (content != null)
            {
                request.Content = content;
            }

            // X-Agent-Id is set in constructor or by UpdateAgentIdHeader.
            // No Bearer token for HTTP calls as per agent_standard.md (token is for WebSocket).
            
            _logger.LogDebug("Making HTTP {Method} request to {BaseAddress}{Endpoint}", method, _httpClient.BaseAddress, requestUrl);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogDebug("HTTP request to {Endpoint} successful with 204 No Content.", requestUrl);
                    return default; 
                }

                var responseContentString = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("HTTP response from {Endpoint} (Status: {StatusCode}): {ResponseContent}", requestUrl, response.StatusCode, responseContentString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("HTTP request to {Endpoint} failed with status code {StatusCode}. Response: {ResponseContentString}", requestUrl, response.StatusCode, responseContentString);
                    // Try to deserialize standard error response
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<ErrorResponsePayload>(responseContentString);
                        // Throw a custom exception or handle specific error codes (e.g., MFA_REQUIRED)
                        if (errorResponse != null)
                        {
                            var ex = new HttpRequestException($"API Error: {errorResponse.Message} (Code: {errorResponse.ErrorCode})");
                            throw ex;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Failed to deserialize error response from {Endpoint} as ErrorResponsePayload. Content: {ResponseContentString}", requestUrl, responseContentString);
                    }
                    response.EnsureSuccessStatusCode(); // Rethrow original exception if not handled above
                }
                
                return JsonConvert.DeserializeObject<TResponse>(responseContentString);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to {Endpoint} failed.", requestUrl);
                throw; 
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON response from {Endpoint}.", requestUrl);
                throw; 
            }
        }

        public async Task<IdentifyResponsePayload?> IdentifyAgentAsync(IdentifyRequestPayload payload)
        {
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            _logger.LogInformation("Identifying agent. DeviceId: {DeviceId}, AgentVersion: {AgentVersion}", payload.DeviceId, payload.AgentVersion);
            return await MakeRequestAsync<IdentifyResponsePayload>(HttpMethod.Post, "identify", content);
        }

        public async Task<VerifyMfaResponsePayload?> VerifyMfaAsync(VerifyMfaRequestPayload payload)
        {
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            _logger.LogInformation("Verifying MFA. DeviceId: {DeviceId}", payload.DeviceId);
            return await MakeRequestAsync<VerifyMfaResponsePayload>(HttpMethod.Post, "verify-mfa", content); 
        }

        public async Task<HardwareInfoResponsePayload?> SendHardwareInfoAsync(CMSAgent.Models.HardwareInfo hardwareInfo)
        {
            var content = new StringContent(JsonConvert.SerializeObject(hardwareInfo), Encoding.UTF8, "application/json");
            _logger.LogInformation("Sending hardware information for DeviceId (header): {DeviceId}", _deviceIdHeader);
            return await MakeRequestAsync<HardwareInfoResponsePayload>(HttpMethod.Post, "hardware-info", content);
        }

        public async Task<CheckUpdateResponsePayload?> CheckForUpdateAsync(string currentVersion)
        {
            string endpoint = $"check-update?current_version={Uri.EscapeDataString(currentVersion)}";
            _logger.LogInformation("Checking for updates. Current version: {CurrentVersion}, DeviceId (header): {DeviceId}", currentVersion, _deviceIdHeader);
            
            // This request returns 204 if no update, or JSON if update available.
            // MakeRequestAsync will return default (null for class) on 204.
            return await MakeRequestAsync<CheckUpdateResponsePayload>(HttpMethod.Get, endpoint);
        }

        public async Task DownloadFileAsync(string url, string savePath)
        {
            _logger.LogInformation("Downloading file from {Url} to {SavePath}", url, savePath);
            HttpClient clientToUse = _httpClient; 

            Uri downloadUri;
            bool isAbsoluteUrl = Uri.IsWellFormedUriString(url, UriKind.Absolute);

            if (isAbsoluteUrl)
            {
                downloadUri = new Uri(url);
                if (downloadUri.Host != _httpClient.BaseAddress?.Host || downloadUri.Scheme != _httpClient.BaseAddress?.Scheme)
                {
                    _logger.LogWarning("Download URL {Url} is absolute and on a different host/scheme than the API. Using a new HttpClient instance.", url);
                    clientToUse = new HttpClient(); 
                    clientToUse.DefaultRequestHeaders.UserAgent.ParseAdd("CMSAgent.NET/1.0");
                }
            }
            else 
            {
                var relativePath = url.StartsWith("/") ? url.Substring(1) : url;
                downloadUri = new Uri(_httpClient.BaseAddress!, relativePath);
                 _logger.LogDebug("Download URL is relative. Resolved to: {ResolvedUrl}. Using main HttpClient.", downloadUri);
            }

            try
            {
                var response = await clientToUse.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream);
                _logger.LogInformation("File downloaded successfully to {SavePath}", savePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download file from {Url}", url);
                if (clientToUse != _httpClient) clientToUse.Dispose();
                throw;
            }
            finally
            {
                 if (clientToUse != _httpClient) clientToUse.Dispose();
            }
        }
        
        public async Task<ReportErrorResponsePayload?> ReportErrorAsync(ReportErrorRequestPayload errorPayload)
        {
            // Ensure DeviceId in payload matches header if not already set
            if (string.IsNullOrEmpty(errorPayload.DeviceId) && !string.IsNullOrEmpty(_deviceIdHeader))
            {
                errorPayload.DeviceId = _deviceIdHeader;
            }
            var content = new StringContent(JsonConvert.SerializeObject(errorPayload), Encoding.UTF8, "application/json");
            _logger.LogInformation("Reporting error of type {ErrorType} for DeviceId (header): {DeviceId}", errorPayload.ErrorType, _deviceIdHeader);
            try
            {
                return await MakeRequestAsync<ReportErrorResponsePayload>(HttpMethod.Post, "report-error", content); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report error to backend. This error will not be re-thrown.");
                return null; // Reporting an error should not crash the agent
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
