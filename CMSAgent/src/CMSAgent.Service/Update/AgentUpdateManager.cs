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
using CMSAgent.Service.Configuration.Manager; // For IRuntimeConfigManager (to get AgentProgramDataPath)


namespace CMSAgent.Service.Update
{
    public class AgentUpdateManager : IAgentUpdateManager
    {
        private readonly ILogger<AgentUpdateManager> _logger;
        private readonly AppSettings _appSettings;
        private readonly IAgentApiClient _apiClient;
        private readonly IAgentSocketClient _socketClient; // To send agent:update_status
        private readonly IVersionIgnoreManager _versionIgnoreManager;
        private readonly IRuntimeConfigManager _runtimeConfigManager; // To get AgentProgramDataPath
        private readonly Func<Task> _requestServiceShutdown; // Action to request service shutdown

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
                var errorMsg = "Cannot determine AgentProgramDataPath from RuntimeConfigManager.";
                _logger.LogCritical(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
        }

        public async Task UpdateAndInitiateAsync(string currentAgentVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currentAgentVersion))
            {
                _logger.LogError("Current Agent version not provided. Cannot check for updates.");
                return;
            }

            _logger.LogInformation("Checking for updates for Agent version: {CurrentVersion}", currentAgentVersion);
            UpdateNotification? updateInfo = await _apiClient.CheckForUpdatesAsync(currentAgentVersion, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Update check cancelled.");
                return;
            }

            if (updateInfo != null && updateInfo.UpdateAvailable && !string.IsNullOrWhiteSpace(updateInfo.Version))
            {
                _logger.LogInformation("New version available: {NewVersion}. Processing update notification.", updateInfo.Version);
                await ProcessUpdateNotificationAsync(updateInfo, cancellationToken);
            }
            else
            {
                _logger.LogInformation("No new updates available or update information is invalid.");
            }
        }

        public async Task ProcessUpdateNotificationAsync(UpdateNotification updateNotification, CancellationToken cancellationToken = default)
        {
            if (updateNotification == null || string.IsNullOrWhiteSpace(updateNotification.Version) || string.IsNullOrWhiteSpace(updateNotification.DownloadUrl) || string.IsNullOrWhiteSpace(updateNotification.ChecksumSha256))
            {
                _logger.LogError("Invalid update notification or missing information.");
                return;
            }

            _logger.LogInformation("Processing update notification for version: {NewVersion}", updateNotification.Version);

            if (_versionIgnoreManager.IsVersionIgnored(updateNotification.Version))
            {
                _logger.LogWarning("Version {NewVersion} is in the ignore list. Cancelling update process.", updateNotification.Version);
                return;
            }

            if (!await _updateLock.WaitAsync(TimeSpan.Zero, cancellationToken)) // Try to lock immediately
            {
                _logger.LogWarning("Another update process is in progress. Ignoring update request for version {NewVersion}.", updateNotification.Version);
                return;
            }

            _isUpdateInProgress = true;
            try
            {
                await NotifyUpdateStatusAsync("update_started", updateNotification.Version, null);

                string downloadDir = Path.Combine(_agentProgramDataPath, AgentConstants.UpdatesSubFolderName, AgentConstants.UpdateDownloadSubFolderName);
                Directory.CreateDirectory(downloadDir); // Ensure directory exists
                string downloadedPackagePath = Path.Combine(downloadDir, $"CMSAgent_v{updateNotification.Version}.zip"); // Example filename

                // 1. Download update package
                _logger.LogInformation("Downloading update package from: {DownloadUrl}", updateNotification.DownloadUrl);
                bool downloadSuccess = await _apiClient.DownloadAgentPackageAsync(Path.GetFileName(updateNotification.DownloadUrl), downloadedPackagePath, cancellationToken);
                if (!downloadSuccess || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeDownloadFailed, "Cannot download update package.", updateNotification.Version);
                    return;
                }
                _logger.LogInformation("Update package downloaded successfully: {FilePath}", downloadedPackagePath);

                // 2. Verify Checksum
                _logger.LogInformation("Verifying checksum for: {FilePath}", downloadedPackagePath);
                string? calculatedChecksum = await FileUtils.CalculateSha256ChecksumAsync(downloadedPackagePath);
                if (string.IsNullOrWhiteSpace(calculatedChecksum) || !calculatedChecksum.Equals(updateNotification.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeChecksumMismatch, $"Checksum mismatch. Expected: {updateNotification.ChecksumSha256}, Calculated: {calculatedChecksum}", updateNotification.Version);
                    FileUtils.TryDeleteFile(downloadedPackagePath, _logger); // Delete error file
                    return;
                }
                _logger.LogInformation("Checksum verification successful.");

                // 3. Extract update package
                string extractDir = Path.Combine(_agentProgramDataPath, AgentConstants.UpdatesSubFolderName, AgentConstants.UpdateExtractedSubFolderName, updateNotification.Version);
                if (Directory.Exists(extractDir)) // Delete old extraction directory if exists
                {
                    _logger.LogInformation("Deleting old extraction directory: {ExtractDir}", extractDir);
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);
                _logger.LogInformation("Extracting update package to: {ExtractDir}", extractDir);
                bool extractSuccess = await FileUtils.DecompressZipFileAsync(downloadedPackagePath, extractDir);
                if (!extractSuccess || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeExtractionFailed, "Cannot extract update package.", updateNotification.Version);
                    FileUtils.TryDeleteFile(downloadedPackagePath, _logger);
                    return;
                }
                _logger.LogInformation("Update package extracted successfully.");
                FileUtils.TryDeleteFile(downloadedPackagePath, _logger); // Delete zip file after extraction

                // 4. Launch CMSUpdater.exe
                _logger.LogInformation("Preparing to launch CMSUpdater.exe for version {NewVersion}", updateNotification.Version);
                bool updaterLaunched = await LaunchUpdaterAsync(extractDir, updateNotification.Version, cancellationToken);
                if (!updaterLaunched || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateLaunchFailed, "Cannot launch CMSUpdater.exe.", updateNotification.Version);
                    return;
                }
                _logger.LogInformation("CMSUpdater.exe has been launched. Agent Service will stop soon.");

                // 5. Request Agent Service to stop (graceful shutdown)
                // AgentCoreOrchestrator will handle this
                await _requestServiceShutdown();

            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Update process cancelled for version {NewVersion}.", updateNotification.Version);
                await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateGeneralFailure, "Update process cancelled.", updateNotification.Version, false); // Don't ignore version if cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during update process for version {NewVersion}.", updateNotification.Version);
                await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateGeneralFailure, $"Unknown error: {ex.Message}", updateNotification.Version);
            }
            finally
            {
                _isUpdateInProgress = false;
                _updateLock.Release();
            }
        }

        private async Task<bool> LaunchUpdaterAsync(string extractedUpdatePath, string newVersion, CancellationToken cancellationToken)
        {
            string updaterExeName = "CMSUpdater.exe"; // Updater executable filename
            string updaterPathInNewPackage = Path.Combine(extractedUpdatePath, "Updater", updaterExeName); // Prefer updater in new package
            string updaterPathInCurrentInstall = Path.Combine(AppContext.BaseDirectory, "Updater", updaterExeName); // Current updater

            string updaterToLaunch = File.Exists(updaterPathInNewPackage) ? updaterPathInNewPackage : updaterPathInCurrentInstall;

            if (!File.Exists(updaterToLaunch))
            {
                _logger.LogError("CMSUpdater.exe not found at '{Path1}' or '{Path2}'.", updaterPathInNewPackage, updaterPathInCurrentInstall);
                return false;
            }

            string arguments = $"-new-version \"{newVersion}\" " +
                             $"-old-version \"{_appSettings.AgentVersion}\" " +
                             $"-source-path \"{extractedUpdatePath}\" " +
                             $"-service-wait-timeout {_appSettings.ServiceWaitTimeoutSeconds} " +
                             $"-watchdog-period {_appSettings.NewAgentWatchdogPeriodSeconds}";

            _logger.LogInformation("Launching Updater: \"{UpdaterPath}\" with arguments: {Arguments}", updaterToLaunch, arguments);

            try
            {
                // Launch Updater as a separate process, don't wait for it to finish
                Process? updaterProcess = ProcessUtils.StartProcess(updaterToLaunch, arguments, Path.GetDirectoryName(updaterToLaunch), createNoWindow: true, useShellExecute: false);

                if (updaterProcess == null || updaterProcess.HasExited) // Checking HasExited immediately may not be accurate if process just started
                {
                    _logger.LogError("Cannot launch or CMSUpdater.exe exited immediately. PID: {PID}", updaterProcess?.Id);
                    return false;
                }
                _logger.LogInformation("CMSUpdater.exe launched successfully with PID: {UpdaterPID}", updaterProcess.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching CMSUpdater.exe.");
                return false;
            }
        }

        private async Task NotifyUpdateStatusAsync(string status, string targetVersion, string? message)
        {
            if (!_socketClient.IsConnected)
            {
                _logger.LogWarning("Cannot send update status '{Status}' for version {TargetVersion}: WebSocket not connected.", status, targetVersion);
                return;
            }
            try
            {
                var payload = new
                {
                    status = status,
                    target_version = targetVersion,
                    message = message
                };
                await _socketClient.SendEventAsync("agent:update_status", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending update status '{Status}' for version {TargetVersion}.", status, targetVersion);
            }
        }

        private async Task ReportUpdateErrorAsync(string errorType, string errorMessage, string targetVersion)
        {
            try
            {
                var errorReport = new AgentErrorReport
                {
                    ErrorType = errorType,
                    ErrorMessage = errorMessage,
                    TargetVersion = targetVersion,
                    Timestamp = DateTime.UtcNow
                };
                await _apiClient.ReportUpdateErrorAsync(errorReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting update error to server.");
            }
        }

        private async Task HandleUpdateFailureAsync(string errorType, string errorMessage, string targetVersion, bool shouldIgnoreVersionOnError = true)
        {
            _logger.LogError("Update failed: {ErrorMessage}", errorMessage);
            await NotifyUpdateStatusAsync("update_failed", targetVersion, errorMessage);
            await ReportUpdateErrorAsync(errorType, errorMessage, targetVersion);

            if (shouldIgnoreVersionOnError)
            {
                _versionIgnoreManager.IgnoreVersion(targetVersion);
                _logger.LogWarning("Version {TargetVersion} has been added to ignore list due to update failure.", targetVersion);
            }
        }
    }
}
