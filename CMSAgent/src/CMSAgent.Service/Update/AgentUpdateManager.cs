// CMSAgent.Service/Update/AgentUpdateManager.cs
using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Communication.WebSocket;
using CMSAgent.Service.Configuration.Models; // For AppSettings
using CMSAgent.Service.Models;
using CMSAgent.Shared; // For IVersionIgnoreManager
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Utils; 
using Microsoft.Extensions.Options;
using System.Diagnostics;
using CMSAgent.Service.Configuration.Manager; // For IRuntimeConfigManager (to get AgentProgramDataPath)


namespace CMSAgent.Service.Update
{
    public class AgentUpdateManager : IAgentUpdateManager
    {
        private readonly ILogger<AgentUpdateManager> _logger;
        private readonly AppSettings _appSettings;
        private readonly IAgentApiClient _apiClient;
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
            IVersionIgnoreManager versionIgnoreManager,
            IRuntimeConfigManager runtimeConfigManager,
            Func<Task> requestServiceShutdown)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
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
            _logger.LogInformation("Update check completed. Update info: {UpdateInfo}", updateInfo);

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
                string downloadDir = Path.Combine(_agentProgramDataPath, AgentConstants.UpdatesSubFolderName, AgentConstants.UpdateDownloadSubFolderName);
                Directory.CreateDirectory(downloadDir); // Ensure directory exists
                string downloadedPackagePath = Path.Combine(downloadDir, Path.GetFileName(updateNotification.DownloadUrl));

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
                FileUtils.TryDeleteFile(downloadedPackagePath, _logger);

                // 4. Verify manifest.json
                string manifestPath = Path.Combine(extractDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeInvalidPackage, "manifest.json not found in update package.", updateNotification.Version);
                    return;
                }

                // Read and verify manifest
                string manifestContent = await File.ReadAllTextAsync(manifestPath);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<UpdateManifest>(manifestContent);
                if (manifest == null || manifest.version != updateNotification.Version)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeInvalidPackage, "Invalid manifest.json or version mismatch.", updateNotification.Version);
                    return;
                }

                // Verify all files in manifest exist and have correct checksums
                foreach (var file in manifest.files)
                {
                    string filePath = Path.Combine(extractDir, file.path);
                    if (!File.Exists(filePath))
                    {
                        await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeInvalidPackage, $"File {file.path} not found in update package.", updateNotification.Version);
                        return;
                    }

                    string? fileChecksum = await FileUtils.CalculateSha256ChecksumAsync(filePath);
                    if (string.IsNullOrEmpty(fileChecksum))
                    {
                        await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeChecksumMismatch, $"Failed to calculate checksum for file {file.path}", updateNotification.Version);
                        return;
                    }
                    if (fileChecksum.ToLower() != file.checksum.ToLower())
                    {
                        await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeChecksumMismatch, $"Checksum mismatch for file {file.path}", updateNotification.Version);
                        return;
                    }
                }

                // 5. Launch CMSUpdater.exe
                _logger.LogInformation("Preparing to launch CMSUpdater.exe for version {NewVersion}", updateNotification.Version);
                bool updaterLaunched = await LaunchUpdaterAsync(extractDir, updateNotification.Version, cancellationToken);
                if (!updaterLaunched || cancellationToken.IsCancellationRequested)
                {
                    await HandleUpdateFailureAsync(AgentConstants.UpdateErrorTypeUpdateLaunchFailed, "Cannot launch CMSUpdater.exe.", updateNotification.Version);
                    return;
                }
                _logger.LogInformation("CMSUpdater.exe has been launched. Agent Service will stop soon.");

                // 6. Request Agent Service to stop (graceful shutdown)
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
            string updaterExeName = "CMSUpdater.exe";
            string updaterPath = Path.Combine(extractedUpdatePath, "Updater", updaterExeName);

            if (!File.Exists(updaterPath))
            {
                _logger.LogError("CMSUpdater.exe not found at '{Path}'.", updaterPath);
                return false;
            }

            string arguments = $"-new-version \"{newVersion}\" " +
                             $"-old-version \"{_appSettings.Version}\" " +
                             $"-source-path \"{extractedUpdatePath}\" " ;
            _logger.LogInformation("Launching Updater: \"{UpdaterPath}\" with arguments: {Arguments}", updaterPath, arguments);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(updaterPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Process? updaterProcess = null;
                try
                {
                    updaterProcess = Process.Start(startInfo);
                    if (updaterProcess == null)
                    {
                        _logger.LogError("Failed to start CMSUpdater.exe process");
                        return false;
                    }

                    // Read output and error streams
                    string output = await updaterProcess.StandardOutput.ReadToEndAsync();
                    string error = await updaterProcess.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        _logger.LogInformation("CMSUpdater output: {Output}", output);
                    }
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError("CMSUpdater error: {Error}", error);
                    }

                    // Wait a short time to verify process is still running
                    await Task.Delay(1000, cancellationToken);
                    
                    if (updaterProcess.HasExited)
                    {
                        _logger.LogError("CMSUpdater.exe exited immediately with exit code: {ExitCode}", updaterProcess.ExitCode);
                        return false;
                    }

                    _logger.LogInformation("CMSUpdater.exe launched successfully with PID: {UpdaterPID}", updaterProcess.Id);
                    return true;
                }
                finally
                {
                    if (updaterProcess != null && !updaterProcess.HasExited)
                    {
                        updaterProcess.EnableRaisingEvents = true;
                        updaterProcess.Exited += (sender, e) => 
                        {
                            _logger.LogInformation("CMSUpdater.exe process exited with code: {ExitCode}", updaterProcess.ExitCode);
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching CMSUpdater.exe");
                return false;
            }
        }

        private async Task ReportUpdateErrorAsync(string errorType, string errorMessage, string targetVersion)
        {
            try
            {
                var errorReport = ErrorReportingUtils.CreateErrorReport(
                    errorType,
                    errorMessage,
                    customDetails: new { TargetVersion = targetVersion }
                );
                await _apiClient.ReportErrorAsync(errorReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting update error to server.");
            }
        }

        private async Task HandleUpdateFailureAsync(string errorType, string errorMessage, string targetVersion, bool shouldIgnoreVersionOnError = true)
        {
            _logger.LogError("Update failed: {ErrorMessage}", errorMessage);
            await ReportUpdateErrorAsync(errorType, errorMessage, targetVersion);

            if (shouldIgnoreVersionOnError)
            {
                await _versionIgnoreManager.IgnoreVersionAsync(targetVersion);
                _logger.LogWarning("Version {TargetVersion} has been added to ignore list due to update failure.", targetVersion);
            }
        }
    }

    public class UpdateManifest
    {
        public string version { get; set; } = string.Empty;
        public string releaseDate { get; set; } = string.Empty;
        public List<UpdateFile> files { get; set; } = new List<UpdateFile>();
    }

    public class UpdateFile
    {
        public string path { get; set; } = string.Empty;
        public string checksum { get; set; } = string.Empty;
    }
}
