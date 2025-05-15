using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Linq;
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
using System.Text;

namespace CMSAgent.Update
{
    /// <summary>
    /// Handles the logic for checking, downloading, and initiating the update process.
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
        /// Checks if there is a new version of the agent available.
        /// </summary>
        /// <param name="manualCheck">Flag to determine if this is a manual or automatic check.</param>
        /// <returns>Task representing the update check process.</returns>
        public async Task CheckForUpdateAsync(bool manualCheck = false)
        {
            if (!_settings.EnableAutoUpdate && !manualCheck)
            {
                _logger.LogDebug("Auto-update is disabled in configuration");
                return;
            }

            if (_isUpdating)
            {
                _logger.LogInformation("Update in progress, skipping update check request");
                return;
            }

            try
            {
                string agentId = _configLoader.GetAgentId();
                string encryptedToken = _configLoader.GetEncryptedAgentToken();

                if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogWarning("Cannot check for updates: Missing agent authentication information");
                    return;
                }

                _logger.LogInformation("Checking for updates (manual: {ManualCheck})", manualCheck);

                var response = await _httpClient.GetAsync<UpdateCheckResponse>(
                    ApiRoutes.CheckUpdate,
                    agentId,
                    encryptedToken);

                if (response != null && response.status == "success")
                {
                    if (response.update_available)
                    {
                        _logger.LogInformation("New version detected: {NewVersion}", response.version);

                        // Process update information
                        await ProcessUpdateAsync(response);
                    }
                    else
                    {
                        string currentVersion = _configLoader.GetAgentVersion();
                        _logger.LogInformation("No new version available (current version: {CurrentVersion})", currentVersion);
                    }
                }
                else
                {
                    _logger.LogWarning("Unable to check for updates: Invalid response from server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when checking for updates");
                ErrorLogs.LogException(ErrorType.UPDATE_DOWNLOAD_FAILED, ex, _logger);
            }
        }

        /// <summary>
        /// Processes the new version information received from the server.
        /// </summary>
        /// <param name="updateInfo">Information about the new version.</param>
        /// <returns>Task representing the update process.</returns>
        public async Task ProcessUpdateAsync(UpdateCheckResponse updateInfo)
        {
            ArgumentNullException.ThrowIfNull(updateInfo);

            // Kiểm tra phiên bản hiện tại của agent
            string currentVersion = _configLoader.GetAgentVersion();
            _logger.LogInformation("Processing update from current version {CurrentVersion} to new version {NewVersion}", 
                currentVersion, updateInfo.version);

            // Kiểm tra xem phiên bản mới có trong danh sách bị ignore hay không
            if (await VersionIgnoreManager.IsVersionIgnoredAsync(updateInfo.version))
            {
                _logger.LogWarning("Bỏ qua cập nhật: Phiên bản {Version} nằm trong danh sách phiên bản bị ignore",
                    updateInfo.version);
                return;
            }

            // So sánh phiên bản - dừng cập nhật nếu phiên bản mới không lớn hơn phiên bản hiện tại
            if (!IsNewVersionGreater(updateInfo.version, currentVersion))
            {
                _logger.LogWarning("Update aborted: New version {NewVersion} is not greater than current version {CurrentVersion}",
                    updateInfo.version, currentVersion);
                return;
            }

            // Validate that necessary information is available
            if (string.IsNullOrEmpty(updateInfo.version) || 
                string.IsNullOrEmpty(updateInfo.download_url) || 
                string.IsNullOrEmpty(updateInfo.checksum_sha256))
            {
                _logger.LogError("Received incomplete update information: Version={Version}, HasDownloadUrl={HasUrl}, HasChecksum={HasChecksum}",
                    updateInfo.version,
                    !string.IsNullOrEmpty(updateInfo.download_url),
                    !string.IsNullOrEmpty(updateInfo.checksum_sha256));
                return;
            }

            // Use semaphore to ensure only one update process is running at a time
            await _updateLock.WaitAsync();

            try
            {
                if (_isUpdating)
                {
                    _logger.LogWarning("An update process is already running, skipping new request");
                    return;
                }

                _isUpdating = true;

                // Update agent state to UPDATING
                var previousState = _stateManager.CurrentState;
                _stateManager.SetState(AgentState.UPDATING);

                string tempDirectory = Path.Combine(Path.GetTempPath(), $"cmsagent_update_{DateTime.UtcNow.Ticks}");
                string downloadPath = string.Empty;

                try
                {
                    // Create temporary directory for download
                    Directory.CreateDirectory(tempDirectory);

                    // Path to the update file
                    downloadPath = Path.Combine(tempDirectory, $"CMSUpdater_{updateInfo.version}.zip");

                    // Download the update package
                    _logger.LogInformation("Downloading update package from {DownloadUrl}", updateInfo.download_url);

                    // Get login information
                    string agentId = _configLoader.GetAgentId();
                    string encryptedToken = _configLoader.GetEncryptedAgentToken();

                    using (var fileStream = File.Create(downloadPath))
                    {
                        try
                        {
                            // Download update file
                            var downloadStream = await _httpClient.DownloadFileAsync(
                                updateInfo.download_url,
                                agentId,
                                encryptedToken);

                            await downloadStream.CopyToAsync(fileStream);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unable to download update package");
                            ErrorLogs.LogException(ErrorType.UPDATE_DOWNLOAD_FAILED, ex, _logger);
                            
                            // Thêm phiên bản vào danh sách ignore khi tải cập nhật thất bại
                            await VersionIgnoreManager.AddVersionToIgnoreListAsync(updateInfo.version, "Error downloading update package");
                            throw;
                        }
                    }

                    // Verify checksum
                    _logger.LogInformation("Verifying integrity of update package");

                    if (!await VerifyChecksumAsync(downloadPath, updateInfo.checksum_sha256))
                    {
                        _logger.LogError("Integrity check failed: Checksum does not match");
                        ErrorLogs.LogError(ErrorType.UPDATE_CHECKSUM_MISMATCH, 
                            "Integrity check failed: Checksum does not match", 
                            new { ExpectedChecksum = updateInfo.checksum_sha256, FilePath = downloadPath }, 
                            _logger);
                            
                            // Thêm phiên bản vào danh sách ignore khi kiểm tra checksum thất bại
                            await VersionIgnoreManager.AddVersionToIgnoreListAsync(updateInfo.version, "Checksum verification failed");
                            return;
                        }
                    
                    _logger.LogInformation("Preparing to launch CMSUpdater");

                    // Notify about impending restart
                    _logger.LogWarning("Agent will stop and launch CMSUpdater to complete the update process");

                    // Create temporary extraction directory for the update package (containing both new agent and updater)
                    string extractedUpdateDir = Path.Combine(Path.GetTempPath(), $"cmsagent_update_extracted_{DateTime.UtcNow.Ticks}");
                    Directory.CreateDirectory(extractedUpdateDir);

                    try
                    {
                        _logger.LogInformation("Extracting update package {PackagePath} to {ExtractDir}", downloadPath, extractedUpdateDir);
                        ZipFile.ExtractToDirectory(downloadPath, extractedUpdateDir, true);

                        // Launch CMSUpdater and exit agent
                        // Pass downloadPath (path to zip file) and extractedUpdateDir (path to extracted directory)
                        if (await StartCMSUpdaterAsync(downloadPath, extractedUpdateDir, updateInfo)) // MODIFIED: Added updateInfo
                        {
                            // Stop host so the service restarts after the update
                            _logger.LogInformation("Stopping agent to complete the update process");
                            _applicationLifetime.StopApplication();
                        }
                        else
                        {
                            _logger.LogError("Unable to launch CMSUpdater");
                            ErrorLogs.LogError(ErrorType.UpdateFailure, "Unable to launch CMSUpdater", new { }, _logger);
                            
                            // Thêm phiên bản vào danh sách ignore khi không thể khởi chạy CMSUpdater
                            await VersionIgnoreManager.AddVersionToIgnoreListAsync(updateInfo.version, "Unable to launch CMSUpdater");
                            
                            // Restore previous state
                            _stateManager.SetState(previousState);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during update process when extracting or launching updater");
                        ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                        
                        // Thêm phiên bản vào danh sách ignore khi cập nhật thất bại
                        await VersionIgnoreManager.AddVersionToIgnoreListAsync(updateInfo.version, "Error during extraction or launching updater");
                        
                        _stateManager.SetState(previousState);
                    }
                    finally
                    {
                        // Clean up extracted directory after updater has been launched (or if there was an error)
                        // CMSUpdater will copy what it needs from extractedUpdateDir
                        try
                        {
                            if (Directory.Exists(extractedUpdateDir))
                            {
                                Directory.Delete(extractedUpdateDir, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Unable to delete temporary extraction directory: {ExtractDir}", extractedUpdateDir);
                            ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during update process");
                    ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                    
                    // Thêm phiên bản vào danh sách ignore khi cập nhật thất bại
                    await VersionIgnoreManager.AddVersionToIgnoreListAsync(updateInfo.version, "Error during update process");
                    
                    // Restore previous state
                    _stateManager.SetState(previousState);
                }
                finally
                {
                    _isUpdating = false;

                    // Clean up temporary directory downloadPath if needed
                    try
                    {
                        if (Directory.Exists(tempDirectory))
                        {
                            Directory.Delete(tempDirectory, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to delete temporary directory after update: {TempDirectory}", tempDirectory);
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
        /// Verifies the integrity of the update package.
        /// </summary>
        /// <param name="filePath">Path to the update file.</param>
        /// <param name="expectedChecksum">Expected checksum.</param>
        /// <returns>True if the checksum matches, otherwise False.</returns>
        private async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] hash = await sha256.ComputeHashAsync(stream);
                
                var hashStringBuilder = new StringBuilder();
                foreach (byte b in hash)
                {
                    hashStringBuilder.Append(b.ToString("x2"));
                }
                string actualChecksum = hashStringBuilder.ToString();

                bool isValid = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

                _logger.LogDebug("Checksum verification: Expected = {ExpectedChecksum}, Actual = {ActualChecksum}, Result = {IsValid}",
                    expectedChecksum, actualChecksum, isValid);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calculating checksum for file {FilePath}", filePath);
                ErrorLogs.LogException(ErrorType.UPDATE_CHECKSUM_MISMATCH, ex, _logger);
                return false;
            }
        }

        /// <summary>
        /// Launches CMSUpdater.
        /// </summary>
        /// <param name="downloadedPackagePath">Path to the downloaded update package (zip file).</param>
        /// <param name="extractedUpdateDir">Path to the directory where the update package has been extracted.</param>
        /// <param name="updateInfo">Information about the new version.</param>
        /// <returns>True if launch is successful, otherwise False.</returns>
        private async Task<bool> StartCMSUpdaterAsync(string downloadedPackagePath, string extractedUpdateDir, UpdateCheckResponse updateInfo)
        {
            try
            {
                // Find the path to CMSUpdater in the extracted directory
                string updaterExePath = Path.Combine(extractedUpdateDir, "CMSUpdater", "CMSUpdater.exe");

                if (!File.Exists(updaterExePath))
                {
                    _logger.LogError("CMSUpdater.exe not found in the extracted update package at {UpdaterPath}", updaterExePath);
                    ErrorLogs.LogError(ErrorType.UpdateFailure, 
                        $"CMSUpdater.exe not found in the extracted update package", 
                        new { UpdaterPath = updaterExePath }, 
                        _logger);
                    return false;
                }

                // Get current process ID
                int currentProcessId = Environment.ProcessId;
                
                // Current installation directory of the agent
                string installDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Current version of the agent
                string currentVersion = _configLoader.GetAgentVersion();
                
                // The update package zip (downloadedPackagePath) is still passed to CMSUpdater
                // CMSUpdater will be responsible for handling this zip file (e.g., saving for rollback or backup)
                // or it may not need to use it if everything has already been extracted to extractedUpdateDir
                // The important thing is that extractedUpdateDir contains the new agent version and new updater (if any).

                // Build parameters for CMSUpdater
                // --downloaded-package-path: path to the original zip file (CMSUpdater may need it for backup or verification)
                // --new-agent-path: path to the extracted directory (containing the new agent and new updater)
                // --current-agent-install-dir: current agent installation directory
                // --current-agent-version: current agent version
                // --new-agent-version: new agent version
                string arguments = $"--pid {currentProcessId} " +
                                  $"--downloaded-package-path \"{downloadedPackagePath}\" " +
                                  $"--new-agent-path \"{extractedUpdateDir}\" " +
                                  $"--current-agent-install-dir \"{installDirectory}\" " +
                                  $"--current-agent-version \"{currentVersion}\" " +
                                  $"--new-agent-version \"{updateInfo.version}\"";

                // Launch CMSUpdater with full parameters
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                };

                _logger.LogInformation("Launching CMSUpdater: {UpdaterPath} {Arguments}",
                    updaterExePath, processStartInfo.Arguments);

                // Start CMSUpdater
                Process.Start(processStartInfo);

                // Wait a moment to ensure CMSUpdater has started
                await Task.Delay(1000);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when launching CMSUpdater");
                ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
                return false;
            }
        }

        /// <summary>
        /// Copies a directory and all its subdirectories and files.
        /// </summary>
        /// <param name="sourceDirName">Source directory.</param>
        /// <param name="destDirName">Destination directory.</param>
        /// <param name="copySubDirs">Whether to copy subdirectories.</param>
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the source directory
            DirectoryInfo dir = new(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    $"Source directory not found: {sourceDirName}");
            }

            // Create destination directory if it doesn't exist
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get all files in the directory
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                // Create destination path for the file
                string tempPath = Path.Combine(destDirName, file.Name);

                // Copy the file
                file.CopyTo(tempPath, true);
            }

            // If including subdirectories
            if (copySubDirs)
            {
                // Get all subdirectories
                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    // Create new destination directory
                    string newDestDirName = Path.Combine(destDirName, subDir.Name);

                    // Call recursively to copy subdirectory
                    DirectoryCopy(subDir.FullName, newDestDirName, copySubDirs);
                }
            }
        }

        /// <summary>
        /// So sánh hai phiên bản theo định dạng semantic versioning (X.Y.Z)
        /// </summary>
        /// <param name="newVersion">Phiên bản mới</param>
        /// <param name="currentVersion">Phiên bản hiện tại</param>
        /// <returns>True nếu phiên bản mới lớn hơn phiên bản hiện tại, ngược lại False</returns>
        private bool IsNewVersionGreater(string newVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(newVersion) || string.IsNullOrEmpty(currentVersion))
            {
                return false;
            }

            try
            {
                // Phân tích phiên bản thành các phần chính, bỏ qua các thông tin sau dấu - hoặc +
                string newVerClean = newVersion.Split('-', '+')[0];
                string currentVerClean = currentVersion.Split('-', '+')[0];

                // Phân tách thành các phần major.minor.patch
                int[] newVerParts = newVerClean.Split('.').Select(int.Parse).ToArray();
                int[] currentVerParts = currentVerClean.Split('.').Select(int.Parse).ToArray();

                // So sánh từng phần: major, sau đó minor, sau đó patch
                for (int i = 0; i < Math.Min(newVerParts.Length, currentVerParts.Length); i++)
                {
                    if (newVerParts[i] > currentVerParts[i])
                        return true;
                    if (newVerParts[i] < currentVerParts[i])
                        return false;
                }

                // Nếu tất cả các phần đã so sánh bằng nhau, kiểm tra xem phiên bản nào dài hơn
                return newVerParts.Length > currentVerParts.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing versions: {NewVersion} vs {CurrentVersion}", 
                    newVersion, currentVersion);
                return false;
            }
        }
    }
}
