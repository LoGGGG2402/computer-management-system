// CMSAgent.Service/Update/AgentUpdateManager.cs
using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Communication.WebSocket;
using CMSAgent.Service.Configuration.Models; // For AppSettings
using CMSAgent.Service.Models;
using CMSAgent.Shared; // For IVersionIgnoreManager
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Models; // For AgentErrorReport
using CMSAgent.Shared.Utils; // For FileUtils, ProcessUtils
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Service.Configuration.Manager; // For IRuntimeConfigManager (để lấy AgentProgramDataPath)


namespace CMSAgent.Service.Update
{
    public class AgentUpdateManager : IAgentUpdateManager
    {
        private readonly ILogger<AgentUpdateManager> _logger;
        private readonly AppSettings _appSettings;
        private readonly IAgentApiClient _apiClient;
        private readonly IAgentSocketClient _socketClient; // Để gửi agent:update_status
        private readonly IVersionIgnoreManager _versionIgnoreManager;
        private readonly IRuntimeConfigManager _runtimeConfigManager; // Để lấy AgentProgramDataPath
        private readonly Func<Task> _requestServiceShutdown; // Action để yêu cầu service tự dừng

        private static readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);
        private volatile bool _isUpdateInProgress = false;

        public bool IsUpdateInProgress => _isUpdateInProgress;

        private readonly string _agentProgramDataPath;


        public AgentUpdateManager(
            ILogger<AgentUpdateManager> logger,
            IOptions<AppSettings> appSettingsOptions,
            IAgentApiClient apiClient,
            IAgentSocketClient socketClient,
            IVersionIgnoreManager versionIgnoreManager,
            IRuntimeConfigManager runtimeConfigManager,
            Func<Task> requestServiceShutdown)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
            _versionIgnoreManager = versionIgnoreManager ?? throw new ArgumentNullException(nameof(versionIgnoreManager));
            _runtimeConfigManager = runtimeConfigManager ?? throw new ArgumentNullException(nameof(runtimeConfigManager));
            _requestServiceShutdown = requestServiceShutdown ?? throw new ArgumentNullException(nameof(requestServiceShutdown));

            _agentProgramDataPath = _runtimeConfigManager.GetAgentProgramDataPath();
            if (string.IsNullOrWhiteSpace(_agentProgramDataPath))
            {
                var errorMsg = "Không thể xác định AgentProgramDataPath từ RuntimeConfigManager.";
                _logger.LogCritical(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
        }

        public async Task UpdateAndInitiateAsync(string currentAgentVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currentAgentVersion))
            {
                _logger.LogError("Phiên bản Agent hiện tại không được cung cấp. Không thể kiểm tra cập nhật.");
                return;
            }

            _logger.LogInformation("Đang kiểm tra cập nhật cho phiên bản Agent: {CurrentVersion}", currentAgentVersion);
            UpdateNotification? updateInfo = await _apiClient.CheckForUpdatesAsync(currentAgentVersion, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Kiểm tra cập nhật bị hủy bỏ.");
                return;
            }

            if (updateInfo != null && updateInfo.UpdateAvailable && !string.IsNullOrWhiteSpace(updateInfo.Version))
            {
                _logger.LogInformation("Có phiên bản mới: {NewVersion}. Đang xử lý thông báo cập nhật.", updateInfo.Version);
                await ProcessUpdateNotificationAsync(updateInfo, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Không có bản cập nhật mới hoặc thông tin cập nhật không hợp lệ.");
            }
        }

        public async Task ProcessUpdateNotificationAsync(UpdateNotification updateNotification, CancellationToken cancellationToken = default)
        {
            if (updateNotification == null || string.IsNullOrWhiteSpace(updateNotification.Version) || string.IsNullOrWhiteSpace(updateNotification.DownloadUrl) || string.IsNullOrWhiteSpace(updateNotification.ChecksumSha256))
            {
                _logger.LogError("Thông báo cập nhật không hợp lệ hoặc thiếu thông tin.");
                return;
            }

            _logger.LogInformation("Đang xử lý thông báo cập nhật cho phiên bản: {NewVersion}", updateNotification.Version);

            if (_versionIgnoreManager.IsVersionIgnored(updateNotification.Version))
            {
                _logger.LogWarning("Phiên bản {NewVersion} đang nằm trong danh sách bỏ qua. Hủy quá trình cập nhật.", updateNotification.Version);
                return;
            }

            if (!await _updateLock.WaitAsync(TimeSpan.Zero, cancellationToken)) // Thử lock ngay lập tức
            {
                _logger.LogWarning("Một quá trình cập nhật khác đang diễn ra. Bỏ qua yêu cầu cập nhật cho phiên bản {NewVersion}.", updateNotification.Version);
                return;
            }

            _isUpdateInProgress = true;
            try
            {
                await NotifyUpdateStatusAsync("update_started", updateNotification.Version, null);

                string downloadDir = Path.Combine(_agentProgramDataPath, AgentConstants.UpdatesSubFolderName, AgentConstants.UpdateDownloadSubFolderName);
                Directory.CreateDirectory(downloadDir); // Đảm bảo thư mục tồn tại
                string downloadedPackagePath = Path.Combine(downloadDir, $"CMSAgent_v{updateNotification.Version}.zip"); // Tên file ví dụ

                // 1. Tải gói cập nhật
                _logger.LogInformation("Đang tải gói cập nhật từ: {DownloadUrl}", updateNotification.DownloadUrl);
                bool downloadSuccess = await _apiClient.DownloadAgentPackageAsync(Path.GetFileName(updateNotification.DownloadUrl), downloadedPackagePath, cancellationToken);
                if (!downloadSuccess || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeDownloadFailed, "Không thể tải gói cập nhật.", updateNotification.Version);
                    return;
                }
                _logger.LogInformation("Tải gói cập nhật thành công: {FilePath}", downloadedPackagePath);

                // 2. Xác minh Checksum
                _logger.LogInformation("Đang xác minh checksum cho: {FilePath}", downloadedPackagePath);
                string? calculatedChecksum = await FileUtils.CalculateSha256ChecksumAsync(downloadedPackagePath);
                if (string.IsNullOrWhiteSpace(calculatedChecksum) || !calculatedChecksum.Equals(updateNotification.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeChecksumMismatch, $"Checksum không khớp. Expected: {updateNotification.ChecksumSha256}, Calculated: {calculatedChecksum}", updateNotification.Version);
                    FileUtils.TryDeleteFile(downloadedPackagePath, _logger); // Xóa file lỗi
                    return;
                }
                _logger.LogInformation("Xác minh checksum thành công.");

                // 3. Giải nén gói cập nhật
                string extractDir = Path.Combine(_agentProgramDataPath, AgentConstants.UpdatesSubFolderName, AgentConstants.UpdateExtractedSubFolderName, updateNotification.Version);
                if (Directory.Exists(extractDir)) // Xóa thư mục giải nén cũ nếu có
                {
                    _logger.LogInformation("Xóa thư mục giải nén cũ: {ExtractDir}", extractDir);
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);
                _logger.LogInformation("Đang giải nén gói cập nhật vào: {ExtractDir}", extractDir);
                bool extractSuccess = await FileUtils.DecompressZipFileAsync(downloadedPackagePath, extractDir);
                if (!extractSuccess || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeExtractionFailed, "Không thể giải nén gói cập nhật.", updateNotification.Version);
                    FileUtils.TryDeleteFile(downloadedPackagePath, _logger);
                    return;
                }
                _logger.LogInformation("Giải nén gói cập nhật thành công.");
                FileUtils.TryDeleteFile(downloadedPackagePath, _logger); // Xóa file zip sau khi giải nén

                // 4. Khởi chạy CMSUpdater.exe
                _logger.LogInformation("Đang chuẩn bị khởi chạy CMSUpdater.exe cho phiên bản {NewVersion}", updateNotification.Version);
                bool updaterLaunched = await LaunchUpdaterAsync(extractDir, updateNotification.Version, cancellationToken);
                if (!updaterLaunched || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateLaunchFailed, "Không thể khởi chạy CMSUpdater.exe.", updateNotification.Version);
                    return;
                }
                _logger.LogInformation("CMSUpdater.exe đã được khởi chạy. Agent Service sẽ sớm dừng lại.");

                // 5. Yêu cầu Agent Service tự dừng (graceful shutdown)
                // AgentCoreOrchestrator sẽ xử lý việc này
                await _requestServiceShutdown();

            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Quá trình cập nhật bị hủy bỏ cho phiên bản {NewVersion}.", updateNotification.Version);
                await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateGeneralFailure, "Quá trình cập nhật bị hủy.", updateNotification.Version, false); // Không ignore version nếu do cancel
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng trong quá trình cập nhật phiên bản {NewVersion}.", updateNotification.Version);
                await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateGeneralFailure, $"Lỗi không xác định: {ex.Message}", updateNotification.Version);
            }
            finally
            {
                _isUpdateInProgress = false;
                _updateLock.Release();
            }
        }

        private async Task<bool> LaunchUpdaterAsync(string extractedUpdatePath, string newVersion, CancellationToken cancellationToken)
        {
            string updaterExeName = "CMSUpdater.exe"; // Tên file thực thi của Updater
            string updaterPathInNewPackage = Path.Combine(extractedUpdatePath, "Updater", updaterExeName); // Ưu tiên updater trong gói mới
            string updaterPathInCurrentInstall = Path.Combine(AppContext.BaseDirectory, "Updater", updaterExeName); // Updater hiện tại

            string updaterToLaunch = File.Exists(updaterPathInNewPackage) ? updaterPathInNewPackage : updaterPathInCurrentInstall;

            if (!File.Exists(updaterToLaunch))
            {
                _logger.LogError("Không tìm thấy CMSUpdater.exe tại '{Path1}' hoặc '{Path2}'.", updaterPathInNewPackage, updaterPathInCurrentInstall);
                return false;
            }

            int currentAgentPid = Process.GetCurrentProcess().Id;
            string arguments = $"-pid {currentAgentPid} -new-agent-version \"{newVersion}\"";
            // Các tham số khác có thể được thêm vào nếu UpdaterConfig yêu cầu
            // Ví dụ: -log-dir "đường_dẫn_log" -install-dir "đường_dẫn_cài_đặt_agent" -source-path "đường_dẫn_file_cập_nhật_đã_giải_nén"

            _logger.LogInformation("Đang khởi chạy Updater: \"{UpdaterPath}\" với tham số: {Arguments}", updaterToLaunch, arguments);

            try
            {
                // Khởi chạy Updater như một tiến trình riêng biệt, không chờ nó kết thúc.
                Process? updaterProcess = ProcessUtils.StartProcess(updaterToLaunch, arguments, Path.GetDirectoryName(updaterToLaunch), createNoWindow: true, useShellExecute: false);

                if (updaterProcess == null || updaterProcess.HasExited) // Kiểm tra HasExited ngay có thể không chính xác nếu process vừa start
                {
                    _logger.LogError("Không thể khởi chạy hoặc CMSUpdater.exe kết thúc ngay lập tức. PID: {PID}", updaterProcess?.Id);
                    return false;
                }
                _logger.LogInformation("CMSUpdater.exe đã được khởi chạy thành công với PID: {UpdaterPID}", updaterProcess.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi khởi chạy CMSUpdater.exe.");
                return false;
            }
        }

        private async Task NotifyUpdateStatusAsync(string status, string targetVersion, string? message)
        {
            if (!_socketClient.IsConnected)
            {
                _logger.LogWarning("Không thể gửi trạng thái cập nhật '{Status}' cho phiên bản {TargetVersion}: WebSocket không kết nối.", status, targetVersion);
                return;
            }
            try
            {
                var payload = new
                {
                    status = status,
                    target_version = targetVersion,
                    message = message // Có thể null
                };
                await _socketClient.SendUpdateStatusAsync(payload);
                _logger.LogInformation("Đã gửi trạng thái cập nhật '{Status}' cho phiên bản {TargetVersion} lên server.", status, targetVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi trạng thái cập nhật '{Status}' cho phiên bản {TargetVersion}.", status, targetVersion);
            }
        }

        private async Task ReportUpdateErrorAsync(string errorType, string errorMessage, string targetVersion)
        {
            var errorReport = ErrorReportingUtils.CreateErrorReport(
                errorType: errorType,
                message: $"Lỗi cập nhật Agent lên phiên bản {targetVersion}: {errorMessage}",
                customDetails: new { TargetVersion = targetVersion }
            );
            await _apiClient.ReportErrorAsync(errorReport); // Không cần await lâu vì đây là báo cáo lỗi
            _logger.LogError("Đã báo cáo lỗi cập nhật loại '{ErrorType}' cho phiên bản {TargetVersion}: {ErrorMessage}", errorType, targetVersion, errorMessage);
        }

        private async Task HandleUpdateFailureAsync(string errorType, string errorMessage, string targetVersion, bool shouldIgnoreVersionOnError = true)
        {
            _logger.LogError("Cập nhật thất bại! Loại lỗi: {ErrorType}, Phiên bản đích: {TargetVersion}, Thông điệp: {ErrorMessage}", errorType, targetVersion, errorMessage);
            await NotifyUpdateStatusAsync("update_failed", targetVersion, errorMessage);
            await ReportUpdateErrorAsync(errorType, errorMessage, targetVersion);

            if (shouldIgnoreVersionOnError)
            {
                _logger.LogWarning("Thêm phiên bản {TargetVersion} vào danh sách bỏ qua do lỗi cập nhật.", targetVersion);
                await _versionIgnoreManager.IgnoreVersionAsync(targetVersion);
            }
        }
    }

    // Extension method cho FileUtils (để tránh lặp code)
    internal static class FileUtilsExtensions
    {
        public static void TryDeleteFile(string filePath, ILogger logger)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    logger.LogInformation("Đã xóa file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể xóa file: {FilePath}", filePath);
            }
        }
    }
}
