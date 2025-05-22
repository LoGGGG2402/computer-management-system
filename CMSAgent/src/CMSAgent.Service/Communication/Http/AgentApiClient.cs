// CMSAgent.Service/Communication/Http/AgentApiClient.cs
using CMSAgent.Service.Configuration.Models; // For AppSettings, PositionInfo
using CMSAgent.Service.Models; // For HardwareInfo, UpdateNotification
using CMSAgent.Shared.Constants; // For AgentConstants
using CMSAgent.Shared.Models; // For AgentErrorReport
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json; // For ReadFromJsonAsync, PostAsJsonAsync
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Communication.Http
{
    public class AgentApiClient : IAgentApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AgentApiClient> _logger;
        private readonly AppSettings _appSettings;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        // Current authentication information
        private string? _currentAgentId;
        private string? _currentAgentToken;

        public AgentApiClient(
            HttpClient httpClient,
            IOptions<AppSettings> appSettingsOptions,
            ILogger<AgentApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            // Configure HttpClient base address and default headers
            if (string.IsNullOrWhiteSpace(_appSettings.ServerUrl))
            {
                throw new InvalidOperationException("ServerUrl is not configured in appsettings.");
            }
            _httpClient.BaseAddress = new Uri(_appSettings.ServerUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Important for deserialization
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Updates authentication information to be used for subsequent requests.
        /// </summary>
        public void SetAuthenticationCredentials(string agentId, string agentToken)
        {
            _currentAgentId = agentId;
            _currentAgentToken = agentToken;
            _logger.LogInformation("API client authentication information has been updated for AgentId: {AgentId}", agentId);
        }

        private void AddAuthHeadersToRequest(HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(_currentAgentId) && !string.IsNullOrEmpty(_currentAgentToken))
            {
                request.Headers.Add("X-Agent-ID", _currentAgentId);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentAgentToken);
            }
            else
            {
                _logger.LogWarning("Attempting to make an authenticated API call without AgentId or AgentToken.");
            }
        }

        public async Task<(string Status, string? AgentToken, string? ErrorMessage)> IdentifyAgentAsync(
            string agentId, PositionInfo positionInfo, CancellationToken cancellationToken = default)
        {
            var requestPayload = new
            {
                agentId = agentId,
                positionInfo = positionInfo,
            };

            string apiUrl = $"{_appSettings.ApiPath}/agent/identify";
            _logger.LogInformation("Sending IdentifyAgent request to {ApiUrl} for AgentId: {AgentId}", apiUrl, agentId);

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(apiUrl, requestPayload, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var identifyResponse = await response.Content.ReadFromJsonAsync<IdentifyResponsePayload>(_jsonSerializerOptions, cancellationToken);
                    if (identifyResponse != null)
                    {
                        _logger.LogInformation("IdentifyAgent successful. Status: {Status}, AgentToken returned: {HasToken}",
                            identifyResponse.Status, !string.IsNullOrEmpty(identifyResponse.AgentToken));
                        return (identifyResponse.Status, identifyResponse.AgentToken, identifyResponse.Message);
                    }
                    _logger.LogError("IdentifyAgent: Successful response but could not parse JSON content.");
                    return ("error", null, "Failed to parse server response.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("IdentifyAgent: Invalid token. Please run configure command again.");
                    return ("unauthorized", null, "Token is invalid. Please run configure command again.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("IdentifyAgent failed. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponsePayload>(errorContent, _jsonSerializerOptions);
                        return (errorResponse?.Status ?? "error", null, errorResponse?.Message ?? $"HTTP Error: {response.StatusCode}");
                    }
                    catch (JsonException)
                    {
                        return ("error", null, $"HTTP Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "IdentifyAgent: HttpRequestException when calling API.");
                return ("error", null, $"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "IdentifyAgent: Request was canceled.");
                return ("error", null, "Request canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IdentifyAgent: Unexpected error.");
                return ("error", null, $"Unexpected error: {ex.Message}");
            }
        }

        public async Task<(string Status, string? AgentToken, string? ErrorMessage)> VerifyMfaAsync(
            string agentId, string mfaCode, CancellationToken cancellationToken = default)
        {
            var requestPayload = new { agentId, mfaCode };
            string apiUrl = $"{_appSettings.ApiPath}/agent/verify-mfa";
            _logger.LogInformation("Sending VerifyMfa request to {ApiUrl} for AgentId: {AgentId}", apiUrl, agentId);

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(apiUrl, requestPayload, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var mfaResponse = await response.Content.ReadFromJsonAsync<MfaResponsePayload>(_jsonSerializerOptions, cancellationToken);
                    if (mfaResponse != null && mfaResponse.Status == "success" && !string.IsNullOrEmpty(mfaResponse.AgentToken))
                    {
                        _logger.LogInformation("VerifyMfa successful for AgentId: {AgentId}", agentId);
                        return (mfaResponse.Status, mfaResponse.AgentToken, null);
                    }
                    _logger.LogError("VerifyMfa: Successful response but invalid content. Status: {Status}, Token: {Token}",
                        mfaResponse?.Status, mfaResponse?.AgentToken);
                    return (mfaResponse?.Status ?? "error", null, mfaResponse?.Message ?? "Invalid MFA response content.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("VerifyMfa failed. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponsePayload>(errorContent, _jsonSerializerOptions);
                        return (errorResponse?.Status ?? "error", null, errorResponse?.Message ?? $"HTTP Error: {response.StatusCode}");
                    }
                    catch (JsonException)
                    {
                        return ("error", null, $"HTTP Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "VerifyMfa: HttpRequestException.");
                return ("error", null, $"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "VerifyMfa: Request was canceled.");
                return ("error", null, "Request canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyMfa: Unexpected error.");
                return ("error", null, $"Unexpected error: {ex.Message}");
            }
        }

        public async Task<bool> ReportHardwareInfoAsync(HardwareInfo hardwareInfo, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/hardware-info";
            _logger.LogInformation("Sending hardware information to {ApiUrl}", apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = JsonContent.Create(hardwareInfo, options: _jsonSerializerOptions)
            };
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) // API returns 204 No Content
                {
                    _logger.LogInformation("Hardware information sent successfully.");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send hardware information. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending hardware information.");
                return false;
            }
        }

        public async Task<bool> ReportErrorAsync(AgentErrorReport errorReport, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/report-error";
            _logger.LogInformation("Sending error report of type '{ErrorType}' to {ApiUrl}", errorReport.Type, apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = JsonContent.Create(errorReport, options: _jsonSerializerOptions)
            };
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) // API returns 204 No Content
                {
                    _logger.LogInformation("Error report sent successfully.");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send error report. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending error report.");
                return false;
            }
        }

        public async Task<UpdateNotification?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/check-update?current_version={Uri.EscapeDataString(currentVersion)}";
            _logger.LogInformation("Checking for updates at {ApiUrl}", apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var updateInfo = await response.Content.ReadFromJsonAsync<UpdateNotification>(_jsonSerializerOptions, cancellationToken);
                    if (updateInfo != null)
                    {
                        _logger.LogInformation("Update check successful. New version available: {HasUpdate}", updateInfo.HasUpdate);
                        return updateInfo;
                    }
                    _logger.LogError("Update check: Successful response but could not parse JSON content.");
                    return null;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No updates available for current version: {CurrentVersion}", currentVersion);
                    return null;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Update check failed. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates.");
                return null;
            }
        }

        public async Task<bool> DownloadAgentPackageAsync(string filename, string destinationPath, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/agent-packages/{Uri.EscapeDataString(filename)}";
            _logger.LogInformation("Downloading agent package from {ApiUrl} to {DestinationPath}", apiUrl, destinationPath);

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await contentStream.CopyToAsync(fileStream, cancellationToken);
                    _logger.LogInformation("Agent package downloaded successfully to {DestinationPath}", destinationPath);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to download agent package. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading agent package.");
                return false;
            }
        }

        private class IdentifyResponsePayload
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("agentToken")]
            public string? AgentToken { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }
        }

        private class MfaResponsePayload
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("agentToken")]
            public string? AgentToken { get; set; }

            [JsonPropertyName("message")] // May have message if error
            public string? Message { get; set; }
        }

        private class ErrorResponsePayload // Common for API error responses in JSON
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("message")]
            public string? Message { get; set; }
        }
    }
}
