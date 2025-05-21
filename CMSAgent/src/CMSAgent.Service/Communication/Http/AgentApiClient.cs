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

        // Thông tin xác thực hiện tại
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

            // Cấu hình HttpClient base address và headers mặc định
            if (string.IsNullOrWhiteSpace(_appSettings.ServerUrl))
            {
                throw new InvalidOperationException("ServerUrl không được cấu hình trong appsettings.");
            }
            _httpClient.BaseAddress = new Uri(_appSettings.ServerUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Quan trọng khi deserialize
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Cập nhật thông tin xác thực được sử dụng cho các request sau này.
        /// </summary>
        public void SetAuthenticationCredentials(string agentId, string agentToken)
        {
            _currentAgentId = agentId;
            _currentAgentToken = agentToken;
            _logger.LogInformation("Thông tin xác thực API client đã được cập nhật cho AgentId: {AgentId}", agentId);
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
            string agentId, PositionInfo positionInfo, bool forceRenewToken = false, CancellationToken cancellationToken = default)
        {
            var requestPayload = new
            {
                agentId = agentId,
                positionInfo = positionInfo,
            };

            string apiUrl = $"{_appSettings.ApiPath}/agent/identify";
            _logger.LogInformation("Đang gửi yêu cầu IdentifyAgent đến {ApiUrl} cho AgentId: {AgentId}", apiUrl, agentId);

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(apiUrl, requestPayload, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var identifyResponse = await response.Content.ReadFromJsonAsync<IdentifyResponsePayload>(_jsonSerializerOptions, cancellationToken);
                    if (identifyResponse != null)
                    {
                        _logger.LogInformation("IdentifyAgent thành công. Status: {Status}, AgentToken được trả về: {HasToken}",
                            identifyResponse.Status, !string.IsNullOrEmpty(identifyResponse.AgentToken));
                        return (identifyResponse.Status, identifyResponse.AgentToken, identifyResponse.Message);
                    }
                    _logger.LogError("IdentifyAgent: Phản hồi thành công nhưng không thể parse nội dung JSON.");
                    return ("error", null, "Failed to parse server response.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("IdentifyAgent thất bại. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
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
                _logger.LogError(ex, "IdentifyAgent: Lỗi HttpRequestException khi gọi API.");
                return ("error", null, $"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "IdentifyAgent: Yêu cầu bị hủy.");
                return ("error", null, "Request canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IdentifyAgent: Lỗi không xác định.");
                return ("error", null, $"Unexpected error: {ex.Message}");
            }
        }


        public async Task<(string Status, string? AgentToken, string? ErrorMessage)> VerifyMfaAsync(
            string agentId, string mfaCode, CancellationToken cancellationToken = default)
        {
            var requestPayload = new { agentId, mfaCode };
            string apiUrl = $"{_appSettings.ApiPath}/agent/verify-mfa";
            _logger.LogInformation("Đang gửi yêu cầu VerifyMfa đến {ApiUrl} cho AgentId: {AgentId}", apiUrl, agentId);

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(apiUrl, requestPayload, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var mfaResponse = await response.Content.ReadFromJsonAsync<MfaResponsePayload>(_jsonSerializerOptions, cancellationToken);
                    if (mfaResponse != null && mfaResponse.Status == "success" && !string.IsNullOrEmpty(mfaResponse.AgentToken))
                    {
                        _logger.LogInformation("VerifyMfa thành công cho AgentId: {AgentId}", agentId);
                        return (mfaResponse.Status, mfaResponse.AgentToken, null);
                    }
                    _logger.LogError("VerifyMfa: Phản hồi thành công nhưng nội dung không hợp lệ. Status: {Status}, Token: {Token}",
                        mfaResponse?.Status, mfaResponse?.AgentToken);
                    return (mfaResponse?.Status ?? "error", null, mfaResponse?.Message ?? "Invalid MFA response content.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("VerifyMfa thất bại. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
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
                _logger.LogError(ex, "VerifyMfa: Lỗi HttpRequestException.");
                return ("error", null, $"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "VerifyMfa: Yêu cầu bị hủy.");
                return ("error", null, "Request canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyMfa: Lỗi không xác định.");
                return ("error", null, $"Unexpected error: {ex.Message}");
            }
        }

        public async Task<bool> ReportHardwareInfoAsync(HardwareInfo hardwareInfo, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/hardware-info";
            _logger.LogInformation("Đang gửi thông tin phần cứng đến {ApiUrl}", apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = JsonContent.Create(hardwareInfo, options: _jsonSerializerOptions)
            };
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) // API trả về 204 No Content
                {
                    _logger.LogInformation("Gửi thông tin phần cứng thành công.");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Gửi thông tin phần cứng thất bại. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi thông tin phần cứng.");
                return false;
            }
        }

        public async Task<bool> ReportErrorAsync(AgentErrorReport errorReport, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/report-error";
            _logger.LogInformation("Đang gửi báo cáo lỗi loại '{ErrorType}' đến {ApiUrl}", errorReport.Type, apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = JsonContent.Create(errorReport, options: _jsonSerializerOptions)
            };
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) // API trả về 204 No Content
                {
                    _logger.LogInformation("Gửi báo cáo lỗi thành công.");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Gửi báo cáo lỗi thất bại. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi báo cáo lỗi.");
                return false;
            }
        }

        public async Task<UpdateNotification?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/check-update?current_version={Uri.EscapeDataString(currentVersion)}";
            _logger.LogInformation("Đang kiểm tra cập nhật từ {ApiUrl} cho phiên bản {CurrentVersion}", apiUrl, currentVersion);

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        _logger.LogInformation("Không có cập nhật mới.");
                        return null; // Không có cập nhật
                    }
                    // API trả về 200 OK với payload nếu có cập nhật
                    var updateInfo = await response.Content.ReadFromJsonAsync<UpdateNotification>(_jsonSerializerOptions, cancellationToken);
                    if (updateInfo != null && updateInfo.UpdateAvailable)
                    {
                        _logger.LogInformation("Có cập nhật mới: Version {NewVersion}, URL: {DownloadUrl}", updateInfo.Version, updateInfo.DownloadUrl);
                        return updateInfo;
                    }
                    _logger.LogWarning("Kiểm tra cập nhật: Phản hồi thành công nhưng nội dung không hợp lệ hoặc không có update_available=true.");
                    return null;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Kiểm tra cập nhật thất bại. StatusCode: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra cập nhật.");
                return null;
            }
        }

        public async Task<bool> DownloadAgentPackageAsync(string filename, string destinationPath, CancellationToken cancellationToken = default)
        {
            string apiUrl = $"{_appSettings.ApiPath}/agent/agent-packages/{Uri.EscapeDataString(filename)}";
            _logger.LogInformation("Đang tải gói cập nhật {Filename} từ {ApiUrl} về {DestinationPath}", filename, apiUrl, destinationPath);

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            AddAuthHeadersToRequest(request);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode(); // Ném lỗi nếu status code không thành công

                await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await contentStream.CopyToAsync(fileStream, cancellationToken);
                }
                _logger.LogInformation("Tải gói cập nhật {Filename} thành công.", filename);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải gói cập nhật {Filename}.", filename);
                // Cân nhắc xóa file đích nếu tải thất bại
                if (File.Exists(destinationPath))
                {
                    try { File.Delete(destinationPath); } catch (Exception deleteEx) { _logger.LogError(deleteEx, "Không thể xóa file tải lỗi: {DestinationPath}", destinationPath); }
                }
                return false;
            }
        }

        // Lớp nội bộ để deserialize phản hồi từ /identify và /verify-mfa
        private class IdentifyResponsePayload
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
            [JsonPropertyName("agentToken")]
            public string? AgentToken { get; set; }
            [JsonPropertyName("message")]
            public string? Message { get; set; } // Cho position_error
        }

        private class MfaResponsePayload
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
            [JsonPropertyName("agentToken")]
            public string? AgentToken { get; set; }
            [JsonPropertyName("message")] // Có thể có message nếu lỗi
            public string? Message { get; set; }
        }
        private class ErrorResponsePayload // Dùng chung cho các lỗi API trả về JSON
        {
            [JsonPropertyName("status")]
            public string? Status { get; set; }
            [JsonPropertyName("message")]
            public string? Message { get; set; }
        }
    }
}
