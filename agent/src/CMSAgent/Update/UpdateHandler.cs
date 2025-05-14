using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Update
{
    /// <summary>
    /// Xử lý logic kiểm tra, tải xuống và khởi chạy quá trình cập nhật.
    /// </summary>
    public class UpdateHandler
    {
        private readonly ILogger<UpdateHandler> _logger;
        private readonly IHttpClientWrapper _httpClient;
        private readonly IConfigLoader _configLoader;
        private readonly StateManager _stateManager;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly AgentSpecificSettingsOptions _settings;

        private bool _isUpdating = false;
        private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Khởi tạo một instance mới của UpdateHandler.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="httpClient">HttpClient để tải xuống các gói cập nhật.</param>
        /// <param name="configLoader">ConfigLoader để tải cấu hình agent.</param>
        /// <param name="stateManager">Quản lý trạng thái agent.</param>
        /// <param name="applicationLifetime">ApplicationLifetime để dừng agent sau khi cập nhật.</param>
        /// <param name="options">Cấu hình đặc biệt cho agent.</param>
        public UpdateHandler(
            ILogger<UpdateHandler> logger,
            IHttpClientWrapper httpClient,
            IConfigLoader configLoader,
            StateManager stateManager,
            IHostApplicationLifetime applicationLifetime,
            IOptions<AgentSpecificSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Kiểm tra xem có phiên bản mới của agent không.
        /// </summary>
        /// <param name="manualCheck">Cờ để xác định đây là kiểm tra thủ công hay tự động.</param>
        /// <returns>Task đại diện cho quá trình kiểm tra cập nhật.</returns>
        public async Task CheckForUpdateAsync(bool manualCheck = false)
        {
            if (!_settings.EnableAutoUpdate && !manualCheck)
            {
                _logger.LogDebug("Tự động cập nhật bị tắt trong cấu hình");
                return;
            }

            if (_isUpdating)
            {
                _logger.LogInformation("Đang cập nhật, bỏ qua yêu cầu kiểm tra cập nhật");
                return;
            }

            try
            {
                string currentVersion = _configLoader.GetAgentVersion();
                string agentId = _configLoader.GetAgentId();
                string encryptedToken = _configLoader.GetEncryptedAgentToken();

                if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogWarning("Không thể kiểm tra cập nhật: Thiếu thông tin xác thực agent");
                    return;
                }

                _logger.LogInformation("Kiểm tra cập nhật từ phiên bản hiện tại {CurrentVersion} (thủ công: {ManualCheck})",
                    currentVersion, manualCheck);

                var response = await _httpClient.GetAsync<UpdateCheckResponse>(
                    ApiRoutes.CheckUpdate,
                    agentId,
                    encryptedToken);

                if (response != null && response.status == "success")
                {
                    if (response.update_available)
                    {
                        _logger.LogInformation("Phát hiện phiên bản mới: {NewVersion}", response.version);
                        
                        // Xử lý thông tin cập nhật
                        await ProcessUpdateAsync(response);
                    }
                    else
                    {
                        _logger.LogInformation("Không có phiên bản mới (phiên bản hiện tại: {CurrentVersion})", currentVersion);
                    }
                }
                else
                {
                    _logger.LogWarning("Không thể kiểm tra cập nhật: Phản hồi từ server không hợp lệ");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra cập nhật");
            }
        }

        /// <summary>
        /// Xử lý thông tin phiên bản mới nhận được từ server.
        /// </summary>
        /// <param name="updateInfo">Thông tin về phiên bản mới.</param>
        /// <returns>Task đại diện cho quá trình cập nhật.</returns>
        public async Task ProcessUpdateAsync(UpdateCheckResponse updateInfo)
        {
            if (updateInfo == null)
                throw new ArgumentNullException(nameof(updateInfo));

            // Sử dụng semaphore để đảm bảo chỉ có một quá trình cập nhật diễn ra cùng lúc
            await _updateLock.WaitAsync();

            try
            {
                if (_isUpdating)
                {
                    _logger.LogWarning("Đã có quá trình cập nhật đang chạy, bỏ qua yêu cầu mới");
                    return;
                }

                _isUpdating = true;
                
                // Cập nhật trạng thái agent sang UPDATING
                var previousState = _stateManager.CurrentState;
                _stateManager.SetState(AgentState.UPDATING);

                string tempDirectory = Path.Combine(Path.GetTempPath(), $"cmsagent_update_{DateTime.UtcNow.Ticks}");
                string downloadPath = string.Empty;

                try
                {
                    // Tạo thư mục tạm để tải xuống
                    Directory.CreateDirectory(tempDirectory);
                    
                    // Đường dẫn tới file cập nhật
                    downloadPath = Path.Combine(tempDirectory, $"CMSUpdater_{updateInfo.version}.zip");

                    // Tải xuống gói cập nhật
                    _logger.LogInformation("Đang tải xuống gói cập nhật từ {DownloadUrl}", updateInfo.download_url);
                    
                    // Lấy thông tin đăng nhập
                    string agentId = _configLoader.GetAgentId();
                    string encryptedToken = _configLoader.GetEncryptedAgentToken();

                    using (var fileStream = File.Create(downloadPath))
                    {
                        // Tải file cập nhật
                        var downloadStream = await _httpClient.DownloadFileAsync(
                            updateInfo.download_url,
                            agentId,
                            encryptedToken);
                            
                        await downloadStream.CopyToAsync(fileStream);
                    }

                    // Kiểm tra checksum
                    _logger.LogInformation("Kiểm tra tính toàn vẹn của gói cập nhật");

                    if (!await VerifyChecksumAsync(downloadPath, updateInfo.checksum_sha256))
                    {
                        _logger.LogError("Kiểm tra tính toàn vẹn thất bại: Checksum không khớp");
                        return;
                    }

                    // Lưu đường dẫn cài đặt
                    string installDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    
                    // Lưu thông tin cập nhật vào registry hoặc file cấu hình
                    // để CMSUpdater có thể sử dụng sau khi agent được khởi động lại
                    _logger.LogInformation("Đang chuẩn bị CMSUpdater");
                    
                    // Tạo thông tin cài đặt cho CMSUpdater
                    if (!PrepareCMSUpdater(downloadPath, installDirectory, updateInfo.version))
                    {
                        _logger.LogError("Không thể chuẩn bị CMSUpdater");
                        return;
                    }

                    // Thông báo chuẩn bị khởi động lại
                    _logger.LogWarning("Agent sẽ dừng và khởi chạy CMSUpdater để hoàn tất quá trình cập nhật");

                    // Khởi chạy CMSUpdater và thoát agent
                    if (await StartCMSUpdaterAsync())
                    {
                        // Dừng host để service được khởi động lại sau khi cập nhật
                        _logger.LogInformation("Đang dừng agent để hoàn tất quá trình cập nhật");
                        _applicationLifetime.StopApplication();
                    }
                    else
                    {
                        _logger.LogError("Không thể khởi chạy CMSUpdater");
                        
                        // Khôi phục trạng thái trước đó
                        _stateManager.SetState(previousState);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong quá trình cập nhật");
                    
                    // Khôi phục trạng thái trước đó
                    _stateManager.SetState(previousState);
                }
                finally
                {
                    _isUpdating = false;

                    // Dọn dẹp thư mục tạm nếu cần
                    try
                    {
                        if (Directory.Exists(tempDirectory))
                        {
                            Directory.Delete(tempDirectory, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể xóa thư mục tạm sau khi cập nhật: {TempDirectory}", tempDirectory);
                    }
                }
            }
            finally
            {
                _updateLock.Release();
            }
        }

        /// <summary>
        /// Kiểm tra tính toàn vẹn của gói cập nhật.
        /// </summary>
        /// <param name="filePath">Đường dẫn đến file cập nhật.</param>
        /// <param name="expectedChecksum">Checksum mong đợi.</param>
        /// <returns>True nếu checksum khớp, ngược lại là False.</returns>
        private async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] hash = await sha256.ComputeHashAsync(stream);
                    string actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    
                    bool isValid = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
                    
                    _logger.LogDebug("Kiểm tra checksum: Mong đợi = {ExpectedChecksum}, Thực tế = {ActualChecksum}, Kết quả = {IsValid}",
                        expectedChecksum, actualChecksum, isValid);
                    
                    return isValid;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tính toán checksum cho file {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Chuẩn bị CMSUpdater với thông tin cần thiết.
        /// </summary>
        /// <param name="updaterPackagePath">Đường dẫn đến gói cập nhật.</param>
        /// <param name="installDirectory">Thư mục cài đặt.</param>
        /// <param name="newVersion">Phiên bản mới.</param>
        /// <returns>True nếu chuẩn bị thành công, ngược lại là False.</returns>
        private bool PrepareCMSUpdater(string updaterPackagePath, string installDirectory, string newVersion)
        {
            try
            {
                // Tìm đường dẫn đến CMSUpdater
                string updaterExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CMSUpdater", "CMSUpdater.exe");
                
                if (!File.Exists(updaterExePath))
                {
                    _logger.LogError("Không tìm thấy CMSUpdater tại {UpdaterPath}", updaterExePath);
                    return false;
                }

                // Tạo file cấu hình cho CMSUpdater
                string updaterConfigPath = Path.Combine(Path.GetDirectoryName(updaterExePath) ?? string.Empty, "update_config.json");
                
                // Tạo nội dung cấu hình
                string configContent = $@"{{
  ""package_path"": ""{updaterPackagePath.Replace("\\", "\\\\")}"",
  ""install_directory"": ""{installDirectory.Replace("\\", "\\\\")}"",
  ""new_version"": ""{newVersion}"",
  ""backup_directory"": ""{Path.Combine(Path.GetTempPath(), "CMSAgent_backup").Replace("\\", "\\\\")}"",
  ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
}}";

                // Lưu file cấu hình
                File.WriteAllText(updaterConfigPath, configContent);
                
                _logger.LogDebug("Đã tạo file cấu hình cho CMSUpdater tại {ConfigPath}", updaterConfigPath);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chuẩn bị CMSUpdater");
                return false;
            }
        }

        /// <summary>
        /// Khởi chạy CMSUpdater.
        /// </summary>
        /// <returns>True nếu khởi chạy thành công, ngược lại là False.</returns>
        private async Task<bool> StartCMSUpdaterAsync()
        {
            try
            {
                // Tìm đường dẫn đến CMSUpdater
                string updaterExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CMSUpdater", "CMSUpdater.exe");
                
                if (!File.Exists(updaterExePath))
                {
                    _logger.LogError("Không tìm thấy CMSUpdater tại {UpdaterPath}", updaterExePath);
                    return false;
                }

                // Lấy ID của process hiện tại
                int currentProcessId = Process.GetCurrentProcess().Id;

                // Khởi chạy CMSUpdater với tham số là ID của process hiện tại
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = $"--wait-for-pid {currentProcessId}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                };

                _logger.LogInformation("Khởi chạy CMSUpdater: {UpdaterPath} {Arguments}", 
                    updaterExePath, processStartInfo.Arguments);

                // Khởi chạy CMSUpdater
                Process.Start(processStartInfo);

                // Đợi một chút để đảm bảo CMSUpdater đã khởi động
                await Task.Delay(1000);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi khởi chạy CMSUpdater");
                return false;
            }
        }
    }
}
