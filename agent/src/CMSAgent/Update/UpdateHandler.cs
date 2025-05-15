using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Text.Json;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Logging;
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
    public class UpdateHandler(
        ILogger<UpdateHandler> logger,
        IHttpClientWrapper httpClient,
        IConfigLoader configLoader,
        StateManager stateManager,
        IHostApplicationLifetime applicationLifetime,
        IOptions<AgentSpecificSettingsOptions> options)
    {
        private readonly ILogger<UpdateHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IHttpClientWrapper _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IConfigLoader _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
        private readonly StateManager _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        private readonly AgentSpecificSettingsOptions _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));

        private bool _isUpdating = false;
        private readonly SemaphoreSlim _updateLock = new(1, 1);

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
                ErrorLogs.LogException(ErrorType.UPDATE_DOWNLOAD_FAILED, ex, _logger);
            }
        }

        /// <summary>
        /// Xử lý thông tin phiên bản mới nhận được từ server.
        /// </summary>
        /// <param name="updateInfo">Thông tin về phiên bản mới.</param>
        /// <returns>Task đại diện cho quá trình cập nhật.</returns>
        public async Task ProcessUpdateAsync(UpdateCheckResponse updateInfo)
        {
            ArgumentNullException.ThrowIfNull(updateInfo);

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
                        try
                        {
                            // Tải file cập nhật
                            var downloadStream = await _httpClient.DownloadFileAsync(
                                updateInfo.download_url,
                                agentId,
                                encryptedToken);

                            await downloadStream.CopyToAsync(fileStream);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Không thể tải xuống gói cập nhật");
                            ErrorLogs.LogException(ErrorType.UPDATE_DOWNLOAD_FAILED, ex, _logger);
                            throw;
                        }
                    }

                    // Kiểm tra checksum
                    _logger.LogInformation("Kiểm tra tính toàn vẹn của gói cập nhật");

                    if (!await VerifyChecksumAsync(downloadPath, updateInfo.checksum_sha256))
                    {
                        _logger.LogError("Kiểm tra tính toàn vẹn thất bại: Checksum không khớp");
                        ErrorLogs.LogError(ErrorType.UPDATE_CHECKSUM_MISMATCH, 
                            "Kiểm tra tính toàn vẹn thất bại: Checksum không khớp", 
                            new { ExpectedChecksum = updateInfo.checksum_sha256, FilePath = downloadPath }, 
                            _logger);
                        return;
                    }
                    
                    _logger.LogInformation("Đang chuẩn bị khởi chạy CMSUpdater");

                    // Thông báo chuẩn bị khởi động lại
                    _logger.LogWarning("Agent sẽ dừng và khởi chạy CMSUpdater để hoàn tất quá trình cập nhật");

                    // Tạo thư mục giải nén tạm thời cho gói cập nhật (chứa cả agent mới và updater mới)
                    string extractedUpdateDir = Path.Combine(Path.GetTempPath(), $"cmsagent_update_extracted_{DateTime.UtcNow.Ticks}");
                    Directory.CreateDirectory(extractedUpdateDir);

                    try
                    {
                        _logger.LogInformation("Giải nén gói cập nhật {PackagePath} vào {ExtractDir}", downloadPath, extractedUpdateDir);
                        ZipFile.ExtractToDirectory(downloadPath, extractedUpdateDir, true);

                        // Khởi chạy CMSUpdater và thoát agent
                        // Truyền downloadPath (đường dẫn file zip) và extractedUpdateDir (đường dẫn thư mục đã giải nén)
                        if (await StartCMSUpdaterAsync(downloadPath, extractedUpdateDir)) // MODIFIED: Added extractedUpdateDir
                        {
                            // Dừng host để service được khởi động lại sau khi cập nhật
                            _logger.LogInformation("Đang dừng agent để hoàn tất quá trình cập nhật");
                            _applicationLifetime.StopApplication();
                        }
                        else
                        {
                            _logger.LogError("Không thể khởi chạy CMSUpdater");
                            ErrorLogs.LogError(ErrorType.UpdateFailure, "Không thể khởi chạy CMSUpdater", new { }, _logger);
                            
                            // Khôi phục trạng thái trước đó
                            _stateManager.SetState(previousState);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi trong quá trình cập nhật khi giải nén hoặc khởi chạy updater");
                        ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                        _stateManager.SetState(previousState);
                    }
                    finally
                    {
                        // Dọn dẹp thư mục giải nén extractedUpdateDir sau khi updater đã được khởi chạy (hoặc nếu có lỗi)
                        // CMSUpdater sẽ tự copy những gì nó cần từ extractedUpdateDir
                        try
                        {
                            if (Directory.Exists(extractedUpdateDir))
                            {
                                Directory.Delete(extractedUpdateDir, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Không thể xóa thư mục giải nén tạm thời: {ExtractDir}", extractedUpdateDir);
                            ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong quá trình cập nhật");
                    ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                    
                    // Khôi phục trạng thái trước đó
                    _stateManager.SetState(previousState);
                }
                finally
                {
                    _isUpdating = false;

                    // Dọn dẹp thư mục tạm downloadPath nếu cần
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
                        ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
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
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] hash = await sha256.ComputeHashAsync(stream);
                string actualChecksum = Convert.ToHexStringLower(hash);

                bool isValid = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

                _logger.LogDebug("Kiểm tra checksum: Mong đợi = {ExpectedChecksum}, Thực tế = {ActualChecksum}, Kết quả = {IsValid}",
                    expectedChecksum, actualChecksum, isValid);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tính toán checksum cho file {FilePath}", filePath);
                ErrorLogs.LogException(ErrorType.UPDATE_CHECKSUM_MISMATCH, ex, _logger);
                return false;
            }
        }

        /// <summary>
        /// Khởi chạy CMSUpdater.
        /// </summary>
        /// <param name="downloadedPackagePath">Đường dẫn đến gói cập nhật đã tải xuống (file zip).</param>
        /// <param name="extractedUpdateDir">Đường dẫn đến thư mục đã giải nén gói cập nhật.</param>
        /// <returns>True nếu khởi chạy thành công, ngược lại là False.</returns>
        private async Task<bool> StartCMSUpdaterAsync(string downloadedPackagePath, string extractedUpdateDir)
        {
            try
            {
                // Tìm đường dẫn đến CMSUpdater trong thư mục đã giải nén
                string updaterExePath = Path.Combine(extractedUpdateDir, "CMSUpdater", "CMSUpdater.exe");

                if (!File.Exists(updaterExePath))
                {
                    _logger.LogError("Không tìm thấy CMSUpdater.exe trong gói cập nhật đã giải nén tại {UpdaterPath}", updaterExePath);
                    ErrorLogs.LogError(ErrorType.UpdateFailure, 
                        $"Không tìm thấy CMSUpdater.exe trong gói cập nhật đã giải nén", 
                        new { UpdaterPath = updaterExePath }, 
                        _logger);
                    return false;
                }

                // Lấy ID của process hiện tại
                int currentProcessId = Environment.ProcessId;
                
                // Thư mục cài đặt hiện tại của agent
                string installDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Phiên bản hiện tại của agent
                string currentVersion = _configLoader.GetAgentVersion();
                
                // Gói cập nhật zip (downloadedPackagePath) vẫn được truyền cho CMSUpdater
                // CMSUpdater sẽ chịu trách nhiệm xử lý file zip này (ví dụ: lưu lại để rollback hoặc backup)
                // hoặc có thể không cần dùng đến nếu tất cả đã được giải nén ra extractedUpdateDir
                // Quan trọng là extractedUpdateDir chứa phiên bản agent mới và updater mới (nếu có).

                // Xây dựng tham số gọi CMSUpdater
                // --downloaded-package-path: đường dẫn file zip gốc (có thể CMSUpdater cần để backup hoặc kiểm tra lại)
                // --new-agent-path: đường dẫn thư mục đã giải nén (nơi chứa agent mới và updater mới)
                // --current-agent-install-dir: thư mục cài đặt agent hiện tại
                // --current-agent-version: phiên bản agent hiện tại
                string arguments = $"--pid {currentProcessId} " +
                                  $"--downloaded-package-path \"{downloadedPackagePath}\" " +
                                  $"--new-agent-path \"{extractedUpdateDir}\" " +
                                  $"--current-agent-install-dir \"{installDirectory}\" " +
                                  $"--current-agent-version \"{currentVersion}\"";

                // Khởi chạy CMSUpdater với các tham số đầy đủ
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = arguments,
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
                ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                return false;
            }
        }

        /// <summary>
        /// Sao chép thư mục và tất cả các thư mục con và tệp tin.
        /// </summary>
        /// <param name="sourceDirName">Thư mục nguồn.</param>
        /// <param name="destDirName">Thư mục đích.</param>
        /// <param name="copySubDirs">Có sao chép thư mục con hay không.</param>
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Lấy thư mục nguồn
            DirectoryInfo dir = new(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    $"Không tìm thấy thư mục nguồn: {sourceDirName}");
            }

            // Tạo thư mục đích nếu chưa tồn tại
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Lấy tất cả các tệp tin trong thư mục
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                // Tạo đường dẫn đích cho tệp tin
                string tempPath = Path.Combine(destDirName, file.Name);

                // Sao chép tệp tin
                file.CopyTo(tempPath, true);
            }

            // Nếu bao gồm thư mục con
            if (copySubDirs)
            {
                // Lấy tất cả thư mục con
                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    // Tạo thư mục đích mới
                    string newDestDirName = Path.Combine(destDirName, subDir.Name);

                    // Gọi đệ quy để sao chép thư mục con
                    DirectoryCopy(subDir.FullName, newDestDirName, copySubDirs);
                }
            }
        }
    }
}
