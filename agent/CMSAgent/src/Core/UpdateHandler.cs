using CMSAgent.Configuration;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace CMSAgent.Core
{
    /// <summary>
    /// Handles agent self-update operations
    /// </summary>
    public class UpdateHandler : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly StaticConfigProvider _configProvider;
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly string _updateDirectory;
        private readonly string _backupDirectory;
        private readonly string _installationDirectory;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new instance of the UpdateHandler class
        /// </summary>
        public UpdateHandler(
            StaticConfigProvider configProvider,
            RuntimeStateManager runtimeStateManager)
        {
            _configProvider = configProvider;
            _runtimeStateManager = runtimeStateManager;

            // Create HttpClient for downloads
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10) // Long timeout for large downloads
            };

            // Set up directories
            _installationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _updateDirectory = Path.Combine(_installationDirectory, "Updates");
            _backupDirectory = Path.Combine(_installationDirectory, "Backups");

            // Create directories if they don't exist
            Directory.CreateDirectory(_updateDirectory);
            Directory.CreateDirectory(_backupDirectory);
        }

        /// <summary>
        /// Checks if an update is available
        /// </summary>
        public async Task<(bool updateAvailable, string availableVersion, string downloadUrl)> CheckForUpdateAsync()
        {
            try
            {
                Log.Information("Checking for updates...");

                // Get current version
                string currentVersion = _runtimeStateManager.GetCurrentVersion();

                // Get update info URL from config
                string updateCheckUrl = _configProvider.Config.server_config.api_endpoints.update_check;

                // Add current version and device ID as query parameters
                string url = $"{updateCheckUrl}?version={currentVersion}&deviceId={_runtimeStateManager.DeviceId}";

                // Make request to check for updates
                var response = await _httpClient.GetStringAsync(url);

                // Parse response
                var updateInfo = System.Text.Json.JsonSerializer.Deserialize<UpdateInfo>(response);

                if (updateInfo == null)
                {
                    Log.Warning("Failed to parse update information");
                    return (false, string.Empty, string.Empty);
                }

                if (updateInfo.UpdateAvailable && !string.IsNullOrEmpty(updateInfo.AvailableVersion))
                {
                    Log.Information("Update available: {Version}", updateInfo.AvailableVersion);
                    return (true, updateInfo.AvailableVersion, updateInfo.DownloadUrl);
                }
                else
                {
                    Log.Information("No updates available");
                    return (false, string.Empty, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates: {Message}", ex.Message);
                return (false, string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Downloads an update package
        /// </summary>
        public async Task<bool> DownloadUpdateAsync(string downloadUrl, string version)
        {
            try
            {
                Log.Information("Downloading update from {Url}", downloadUrl);

                // Create update package filename
                string updatePackagePath = Path.Combine(_updateDirectory, $"update-{version}.zip");

                // Delete existing file if it exists
                if (File.Exists(updatePackagePath))
                {
                    File.Delete(updatePackagePath);
                }

                // Download the update package
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(updatePackagePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                // Validate the download
                if (!File.Exists(updatePackagePath))
                {
                    Log.Error("Downloaded update package not found at {Path}", updatePackagePath);
                    return false;
                }

                Log.Information("Update downloaded successfully to {Path}", updatePackagePath);

                // Update the runtime config with the update information
                var runtimeConfig = await _runtimeStateManager.GetRuntimeConfigAsync();
                runtimeConfig.update_info.update_available = true;
                runtimeConfig.update_info.available_version = version;
                runtimeConfig.update_info.download_url = downloadUrl;
                runtimeConfig.update_info.update_downloaded = true;
                runtimeConfig.update_info.update_package_path = updatePackagePath;
                await _runtimeStateManager.SaveRuntimeConfigAsync(runtimeConfig);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error downloading update: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Installs an update package
        /// </summary>
        public async Task<bool> InstallUpdateAsync(string updatePackagePath, string newVersion)
        {
            try
            {
                Log.Information("Installing update from {Path}", updatePackagePath);

                // Verify update package exists
                if (!File.Exists(updatePackagePath))
                {
                    Log.Error("Update package not found at {Path}", updatePackagePath);
                    return false;
                }

                // Create a unique ID for this update
                string updateId = Guid.NewGuid().ToString();
                string extractPath = Path.Combine(_updateDirectory, updateId);
                string backupPath = Path.Combine(_backupDirectory, $"backup-{_runtimeStateManager.GetCurrentVersion()}-{updateId}.zip");

                // Create extraction directory
                Directory.CreateDirectory(extractPath);

                // Extract the update package
                Log.Debug("Extracting update package to {Path}", extractPath);
                ZipFile.ExtractToDirectory(updatePackagePath, extractPath);

                // Create backup of current installation
                Log.Debug("Creating backup of current installation to {Path}", backupPath);
                await CreateBackupAsync(backupPath);

                // Create updater script
                string updaterScript = CreateUpdaterScript(extractPath, backupPath, newVersion);
                string updaterScriptPath = Path.Combine(_updateDirectory, $"updater-{updateId}.bat");
                File.WriteAllText(updaterScriptPath, updaterScript);

                // Update the runtime config
                var runtimeConfig = await _runtimeStateManager.GetRuntimeConfigAsync();
                runtimeConfig.update_info.update_status = "installing";
                runtimeConfig.update_info.update_time = DateTime.UtcNow;
                await _runtimeStateManager.SaveRuntimeConfigAsync(runtimeConfig);

                // Execute the updater script
                Log.Information("Starting update installation process...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error installing update: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates a backup of the current installation
        /// </summary>
        private async Task CreateBackupAsync(string backupPath)
        {
            try
            {
                Log.Debug("Creating backup of current installation");

                // List of directories and files to exclude from backup
                string[] excludeDirs = { "Logs", "Updates", "Backups", "Data" };

                // Create a memory stream to create the zip file
                using (var zipArchive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Add all files in the installation directory
                    foreach (var file in Directory.GetFiles(_installationDirectory, "*", SearchOption.AllDirectories))
                    {
                        // Skip files in excluded directories
                        bool excludeFile = false;
                        foreach (var excludeDir in excludeDirs)
                        {
                            if (file.Contains(Path.Combine(_installationDirectory, excludeDir)))
                            {
                                excludeFile = true;
                                break;
                            }
                        }

                        if (excludeFile)
                        {
                            continue;
                        }

                        // Get relative path for the archive
                        string relativePath = file.Substring(_installationDirectory.Length).TrimStart('\\', '/');
                        zipArchive.CreateEntryFromFile(file, relativePath);
                    }
                }

                Log.Debug("Backup created successfully at {Path}", backupPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating backup: {Message}", ex.Message);
                throw; // Rethrow to stop the update process
            }
        }

        /// <summary>
        /// Creates a script to perform the update
        /// </summary>
        private string CreateUpdaterScript(string extractPath, string backupPath, string newVersion)
        {
            // Windows batch script to perform the update
            return @$"
@echo off
echo Starting CMS Agent update process...

REM Wait for the agent service to stop
timeout /t 5 /nobreak > nul

REM Stopping the agent service if it's still running
sc stop CMSAgentService > nul 2>&1
timeout /t 2 /nobreak > nul

REM Copy new files to installation directory
echo Copying new files...
xcopy ""{extractPath}\*"" ""{_installationDirectory}"" /E /Y /H /R > nul

REM Update completed successfully
echo Update to version {newVersion} completed successfully.

REM Start the agent service
echo Starting the agent service...
sc start CMSAgentService > nul 2>&1

REM Clean up
echo Cleaning up...
rmdir /S /Q ""{extractPath}"" > nul 2>&1
del ""{updatePackagePath}"" > nul 2>&1
timeout /t 2 /nobreak > nul

REM Delete this script
del ""%~f0""
";
        }

        /// <summary>
        /// Rolls back an update in case of failure
        /// </summary>
        public async Task<bool> RollbackUpdateAsync(string backupPath)
        {
            try
            {
                Log.Information("Rolling back failed update using backup {Path}", backupPath);

                if (!File.Exists(backupPath))
                {
                    Log.Error("Backup file not found at {Path}", backupPath);
                    return false;
                }

                // Create rollback ID
                string rollbackId = Guid.NewGuid().ToString();
                string extractPath = Path.Combine(_backupDirectory, rollbackId);
                
                // Create extraction directory
                Directory.CreateDirectory(extractPath);

                // Extract the backup
                ZipFile.ExtractToDirectory(backupPath, extractPath);

                // Create rollback script
                string rollbackScript = @$"
@echo off
echo Starting CMS Agent rollback process...

REM Wait for the agent service to stop
timeout /t 5 /nobreak > nul

REM Stopping the agent service if it's still running
sc stop CMSAgentService > nul 2>&1
timeout /t 2 /nobreak > nul

REM Copy backup files to installation directory
echo Restoring backup files...
xcopy ""{extractPath}\*"" ""{_installationDirectory}"" /E /Y /H /R > nul

REM Rollback completed successfully
echo Rollback completed successfully.

REM Start the agent service
echo Starting the agent service...
sc start CMSAgentService > nul 2>&1

REM Clean up
echo Cleaning up...
rmdir /S /Q ""{extractPath}"" > nul 2>&1
timeout /t 2 /nobreak > nul

REM Delete this script
del ""%~f0""
";

                string rollbackScriptPath = Path.Combine(_backupDirectory, $"rollback-{rollbackId}.bat");
                File.WriteAllText(rollbackScriptPath, rollbackScript);

                // Update the runtime config
                var runtimeConfig = await _runtimeStateManager.GetRuntimeConfigAsync();
                runtimeConfig.update_info.update_status = "rollback";
                await _runtimeStateManager.SaveRuntimeConfigAsync(runtimeConfig);

                // Execute the rollback script
                Log.Information("Starting rollback process...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = rollbackScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error rolling back update: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Clean up old update files
        /// </summary>
        public void CleanupOldUpdates(int maxBackupsToKeep = 3)
        {
            try
            {
                Log.Debug("Cleaning up old update files");

                // Clean up old update packages
                var updateFiles = Directory.GetFiles(_updateDirectory, "update-*.zip")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .Skip(maxBackupsToKeep);

                foreach (var file in updateFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Log.Debug("Deleted old update package: {Path}", file);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Could not delete old update package: {Path}", file);
                    }
                }

                // Clean up old backup files
                var backupFiles = Directory.GetFiles(_backupDirectory, "backup-*.zip")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .Skip(maxBackupsToKeep);

                foreach (var file in backupFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Log.Debug("Deleted old backup: {Path}", file);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Could not delete old backup: {Path}", file);
                    }
                }

                // Clean up old script files
                var scriptFiles = Directory.GetFiles(_updateDirectory, "updater-*.bat")
                    .Concat(Directory.GetFiles(_backupDirectory, "rollback-*.bat"))
                    .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1));

                foreach (var file in scriptFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Log.Debug("Deleted old script: {Path}", file);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Could not delete old script: {Path}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up old updates: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Disposes the update handler
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the update handler
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Update information model for deserialization
        /// </summary>
        private class UpdateInfo
        {
            public bool UpdateAvailable { get; set; }
            public string? AvailableVersion { get; set; }
            public string? DownloadUrl { get; set; }
            public string? ReleaseNotes { get; set; }
        }
    }
}