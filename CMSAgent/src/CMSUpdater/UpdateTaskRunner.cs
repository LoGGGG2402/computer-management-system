using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.Versioning; 
using CMSAgent.Shared; 
using CMSAgent.Shared.Utils; 
using CMSAgent.Shared.Constants; 

namespace CMSUpdater
{
    /// <summary>
    /// Handles the update process for the CMS Agent.
    /// </summary>
    public class UpdateTaskRunner
    {
        private readonly UpdaterConfig _config;
        private readonly ILogger<UpdateTaskRunner> _logger;
        private readonly IVersionIgnoreManager _versionIgnoreManager;

        private string ActualServiceName => AgentConstants.ServiceName ?? AgentConstants.ServiceName;
        private int ActualServiceWaitTimeout => _config.ServiceWaitTimeoutSeconds ?? AgentConstants.DefaultProcessWaitForExitTimeoutSeconds;
        private int ActualWatchdogPeriod => _config.NewAgentWatchdogPeriodSeconds ?? 120; // TODO: Consider adding to AgentConstants

        /// <summary>
        /// Initializes a new instance of the UpdateTaskRunner class.
        /// </summary>
        /// <param name="config">The updater configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="versionIgnoreManager">The version ignore manager instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public UpdateTaskRunner(UpdaterConfig config, ILogger<UpdateTaskRunner> logger, IVersionIgnoreManager versionIgnoreManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionIgnoreManager = versionIgnoreManager ?? throw new ArgumentNullException(nameof(versionIgnoreManager));
        }

        /// <summary>
        /// Executes the update process for the Agent.
        /// </summary>
        /// <returns>True if the update was successful, false otherwise.</returns>
        public async Task<bool> RunUpdateAsync()
        {
            _logger.LogInformation("===== Starting Agent Update Process =====");
            _logger.LogInformation("New Version: {NewVersion}, Old Version: {OldVersion}", _config.NewAgentVersion, _config.OldAgentVersion);
            _logger.LogInformation("Agent Installation Directory: {InstallDir}", _config.AgentInstallDirectory);
            _logger.LogInformation("New Version Source Path: {SourcePath}", _config.NewAgentExtractedPath);
            _logger.LogInformation("Old Agent PID: {AgentPid}", _config.CurrentAgentPid);

            try
            {
                if (!await WaitForOldAgentToStopAsync()) return false;
                if (!await BackupOldAgentAsync()) return false;
                if (!await ReplaceAgentFilesAsync()) return false;

                if (!await StartNewAgentServiceAsync())
                {
                    _logger.LogError("Cannot start new Agent Service. Performing Rollback...");
                    await PerformRollbackAsync(markVersionAsIgnored: true);
                    return false;
                }

                if (!await MonitorNewAgentAsync())
                {
                    _logger.LogError("New Agent is unstable. Performing Rollback...");
                    await PerformRollbackAsync(markVersionAsIgnored: true);
                    return false;
                }

                await CleanupAsync();

                _logger.LogInformation("===== Agent Update Process Completed Successfully! =====");
                return true;
            }
            catch (Exception ex)
            {
                var errorReport = ErrorReportingUtils.CreateErrorReport(
                    AgentConstants.UpdateErrorTypeUpdateGeneralFailure,
                    "Unexpected critical error during update.",
                    ex,
                    new { Step = "RunUpdateAsync", NewVersion = _config.NewAgentVersion, OldVersion = _config.OldAgentVersion }
                );
                _logger.LogCritical("AgentErrorReport: {@ErrorReport}", errorReport);
                await PerformRollbackAsync(markVersionAsIgnored: true);
                return false;
            }
        }

        /// <summary>
        /// Waits for the old Agent process to stop completely.
        /// </summary>
        /// <returns>True if the old Agent stopped successfully, false otherwise.</returns>
        private async Task<bool> WaitForOldAgentToStopAsync()
        {
            _logger.LogInformation("Waiting for old Agent process (PID: {AgentPid}) to stop...", _config.CurrentAgentPid);
            if (_config.CurrentAgentPid <= 0)
            {
                _logger.LogWarning("Old Agent PID is invalid ({AgentPid}). Skipping direct PID wait.", _config.CurrentAgentPid);
            }
            else
            {
                try
                {
                    bool exited = ProcessUtils.WaitForProcessExit(_config.CurrentAgentPid, ActualServiceWaitTimeout * 1000);
                    if (!exited)
                    {
                        _logger.LogWarning("Old Agent (PID: {AgentPid}) did not stop after {Timeout} seconds. Attempting to kill process.", _config.CurrentAgentPid, ActualServiceWaitTimeout);
                        try
                        {
                            var oldAgentProcess = Process.GetProcessById(_config.CurrentAgentPid);
                            oldAgentProcess.Kill(true);
                            _logger.LogInformation("Successfully killed old Agent (PID: {AgentPid}).", _config.CurrentAgentPid);
                        }
                        catch (Exception killEx)
                        {
                            _logger.LogError(killEx, "Cannot kill old Agent (PID: {AgentPid}). Update process may fail.", _config.CurrentAgentPid);
                            return false;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Old Agent (PID: {AgentPid}) has stopped.", _config.CurrentAgentPid);
                    }
                }
                catch (ArgumentException)
                {
                    _logger.LogInformation("Old Agent (PID: {AgentPid}) not found (may have already stopped).", _config.CurrentAgentPid);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while waiting for old Agent (PID: {AgentPid}) to stop.", _config.CurrentAgentPid);
                }
            }
            
            if (OperatingSystem.IsWindows() && IsServiceRunning(ActualServiceName))
            {
                 _logger.LogWarning("Service {ServiceName} is still running. Attempting to stop service...", ActualServiceName);
                 if (!await StopServiceAsync(ActualServiceName)) return false;
            }

            _logger.LogInformation("Confirmed old Agent has stopped.");
            return true;
        }

        /// <summary>
        /// Creates a backup of the old Agent version.
        /// </summary>
        /// <returns>True if backup was successful, false otherwise.</returns>
        private async Task<bool> BackupOldAgentAsync()
        {
            _logger.LogInformation("Backing up old Agent version ({OldVersion}) to: {BackupDir}", _config.OldAgentVersion, _config.BackupDirectoryForOldVersion);
            try
            {
                if (Directory.Exists(_config.BackupDirectoryForOldVersion))
                {
                    _logger.LogWarning("Old backup directory exists, will be deleted: {BackupDir}", _config.BackupDirectoryForOldVersion);
                    Directory.Delete(_config.BackupDirectoryForOldVersion, true);
                }
                var parentDir = Path.GetDirectoryName(_config.BackupDirectoryForOldVersion);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                if (!Directory.Exists(_config.AgentInstallDirectory))
                {
                    _logger.LogWarning("Old Agent installation directory does not exist: {InstallDir}. Skipping backup.", _config.AgentInstallDirectory);
                    return true;
                }

                await Task.Run(() => FileUtils.CopyDirectory(_config.AgentInstallDirectory, _config.BackupDirectoryForOldVersion, true));
                _logger.LogInformation("Old Agent backup completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                var errorReport = ErrorReportingUtils.CreateErrorReport(
                    AgentConstants.UpdateErrorTypeUpdateGeneralFailure,
                    "Error while backing up old Agent.",
                    ex,
                    new { Step = "BackupOldAgentAsync", OldVersion = _config.OldAgentVersion, BackupDir = _config.BackupDirectoryForOldVersion }
                );
                _logger.LogError("AgentErrorReport: {@ErrorReport}", errorReport);
                return false;
            }
        }

        /// <summary>
        /// Replaces the old Agent files with the new version.
        /// </summary>
        /// <returns>True if file replacement was successful, false otherwise.</returns>
        private async Task<bool> ReplaceAgentFilesAsync()
        {
            _logger.LogInformation("Replacing Agent files from: {SourcePath} to: {InstallDir}", _config.NewAgentExtractedPath, _config.AgentInstallDirectory);
            try
            {
                if (!Directory.Exists(_config.NewAgentExtractedPath))
                {
                    _logger.LogError("New version source directory does not exist: {SourcePath}", _config.NewAgentExtractedPath);
                    return false;
                }

                if (Directory.Exists(_config.AgentInstallDirectory))
                {
                    _logger.LogInformation("Deleting old files in installation directory: {InstallDir}", _config.AgentInstallDirectory);
                    var installDirInfo = new DirectoryInfo(_config.AgentInstallDirectory);
                    foreach (var file in installDirInfo.GetFiles())
                    {
                        try { file.Delete(); } catch (Exception ex) { _logger.LogWarning(ex, "Cannot delete file: {FilePath}", file.FullName); }
                    }
                    foreach (var dir in installDirInfo.GetDirectories())
                    {
                        try { dir.Delete(true); } catch (Exception ex) { _logger.LogWarning(ex, "Cannot delete directory: {DirPath}", dir.FullName); }
                    }
                }
                else
                {
                    Directory.CreateDirectory(_config.AgentInstallDirectory);
                }

                await Task.Run(() => FileUtils.CopyDirectory(_config.NewAgentExtractedPath, _config.AgentInstallDirectory, true));
                _logger.LogInformation("Agent file replacement completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                var errorReport = ErrorReportingUtils.CreateErrorReport(
                    AgentConstants.UpdateErrorTypeUpdateGeneralFailure,
                    "Error while replacing Agent files.",
                    ex,
                    new { Step = "ReplaceAgentFilesAsync", SourcePath = _config.NewAgentExtractedPath, InstallDir = _config.AgentInstallDirectory }
                );
                _logger.LogError("AgentErrorReport: {@ErrorReport}", errorReport);
                return false;
            }
        }

        /// <summary>
        /// Starts the new Agent service.
        /// </summary>
        /// <returns>True if service started successfully, false otherwise.</returns>
        private async Task<bool> StartNewAgentServiceAsync()
        {
            _logger.LogInformation("Starting new Agent Service: {ServiceName}", AgentConstants.ServiceName);
            if (OperatingSystem.IsWindows())
            {
                return await StartServiceAsync(AgentConstants.ServiceName);
            }
            else
            {
                _logger.LogError("Service control operations are only supported on Windows.");
                return false;
            }
        }

        /// <summary>
        /// Monitors the new Agent service for stability.
        /// </summary>
        /// <returns>True if the service remains stable during the monitoring period, false otherwise.</returns>
        private async Task<bool> MonitorNewAgentAsync()
        {
            _logger.LogInformation("Starting to monitor new Agent Service for {WatchdogPeriod} seconds...", ActualWatchdogPeriod);
            await Task.Delay(TimeSpan.FromSeconds(5));

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < ActualWatchdogPeriod)
            {
                if (OperatingSystem.IsWindows() && !IsServiceRunning(ActualServiceName))
                {
                    _logger.LogError("New Agent Service {ServiceName} has stopped during monitoring.", ActualServiceName);
                    return false;
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            stopwatch.Stop();
            _logger.LogInformation("New Agent Service operated stably during monitoring period.");
            return true;
        }

        /// <summary>
        /// Cleans up temporary files and backup directories after successful update.
        /// </summary>
        private async Task CleanupAsync()
        {
            _logger.LogInformation("Cleaning up temporary files and backup directories...");
            try
            {
                if (Directory.Exists(_config.BackupDirectoryForOldVersion))
                {
                    await Task.Run(() => Directory.Delete(_config.BackupDirectoryForOldVersion, true));
                    _logger.LogInformation("Deleted backup directory: {BackupDir}", _config.BackupDirectoryForOldVersion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while deleting backup directory: {BackupDir}", _config.BackupDirectoryForOldVersion);
            }

            try
            {
                if (Directory.Exists(_config.NewAgentExtractedPath))
                {
                    await Task.Run(() => Directory.Delete(_config.NewAgentExtractedPath, true));
                    _logger.LogInformation("Deleted source extraction directory: {SourcePath}", _config.NewAgentExtractedPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while deleting source extraction directory: {SourcePath}", _config.NewAgentExtractedPath);
            }
            _logger.LogInformation("Cleanup completed.");
        }

        /// <summary>
        /// Performs rollback to the previous version in case of update failure.
        /// </summary>
        /// <param name="markVersionAsIgnored">If true, marks the failed version as ignored.</param>
        private async Task PerformRollbackAsync(bool markVersionAsIgnored = false)
        {
            _logger.LogWarning("===== Starting Rollback Process =====");
            if (OperatingSystem.IsWindows() && IsServiceRunning(AgentConstants.ServiceName))
            {
                _logger.LogInformation("Stopping new Agent Service (if running) for rollback...");
                await StopServiceAsync(AgentConstants.ServiceName);
            }

            _logger.LogInformation("Restoring Agent from backup: {BackupDir}", _config.BackupDirectoryForOldVersion);
            try
            {
                if (!Directory.Exists(_config.BackupDirectoryForOldVersion))
                {
                    _logger.LogError("Backup directory not found for rollback: {BackupDir}. Rollback failed.", _config.BackupDirectoryForOldVersion);
                    if (markVersionAsIgnored)
                    {
                        _logger.LogWarning("Due to rollback failure, marking version {NewVersion} as faulty.", _config.NewAgentVersion);
                        await _versionIgnoreManager.IgnoreVersionAsync(_config.NewAgentVersion);
                    }
                    return;
                }

                if (Directory.Exists(_config.AgentInstallDirectory))
                {
                    _logger.LogInformation("Deleting current installation directory before restore: {InstallDir}", _config.AgentInstallDirectory);
                    Directory.Delete(_config.AgentInstallDirectory, true);
                }
                Directory.CreateDirectory(_config.AgentInstallDirectory);

                await Task.Run(() => FileUtils.CopyDirectory(_config.BackupDirectoryForOldVersion, _config.AgentInstallDirectory, true));
                _logger.LogInformation("Successfully restored files from backup.");

                if (markVersionAsIgnored)
                {
                    _logger.LogWarning("Marking version {NewVersion} as faulty after successful rollback.", _config.NewAgentVersion);
                    await _versionIgnoreManager.IgnoreVersionAsync(_config.NewAgentVersion);
                }
            }
            catch (Exception ex)
            {
                var errorReport = ErrorReportingUtils.CreateErrorReport(
                    AgentConstants.UpdateErrorTypeUpdateGeneralFailure,
                    "Critical error while restoring files from backup. Rollback may be incomplete.",
                    ex,
                    new { Step = "PerformRollbackAsync", BackupDir = _config.BackupDirectoryForOldVersion, InstallDir = _config.AgentInstallDirectory }
                );
                _logger.LogError("AgentErrorReport: {@ErrorReport}", errorReport);
                if (markVersionAsIgnored)
                {
                    _logger.LogWarning("Due to rollback error, marking version {NewVersion} as faulty.", _config.NewAgentVersion);
                    await _versionIgnoreManager.IgnoreVersionAsync(_config.NewAgentVersion);
                }
                return;
            }

            _logger.LogInformation("Starting old Agent Service version...");
            if (OperatingSystem.IsWindows())
            {
                if (!await StartServiceAsync(AgentConstants.ServiceName))
                {
                    _logger.LogError("Cannot start old Agent Service version after rollback.");
                }
                else
                {
                    _logger.LogInformation("Old Agent Service version has been started.");
                }
            }
            else
            {
                _logger.LogError("Service control operations are only supported on Windows.");
            }
            _logger.LogWarning("===== Rollback Process Completed =====");
        }

        #region Service Control Utilities
        /// <summary>
        /// Checks if a Windows service is currently running.
        /// </summary>
        /// <param name="serviceName">The name of the service to check.</param>
        /// <returns>True if the service is running, false otherwise.</returns>
        [SupportedOSPlatform("windows")]
        private bool IsServiceRunning(string serviceName)
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogError("Service control operations are only supported on Windows.");
                return false;
            }

            try
            {
                ServiceController sc = new ServiceController(serviceName);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Service {ServiceName} not found.", serviceName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking service status {ServiceName}.", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Starts a Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to start.</param>
        /// <returns>True if the service was started successfully, false otherwise.</returns>
        [SupportedOSPlatform("windows")]
        private async Task<bool> StartServiceAsync(string serviceName)
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogError("Service control operations are only supported on Windows.");
                return false;
            }

            try
            {
                ServiceController sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    _logger.LogInformation("Service {ServiceName} is already running.", serviceName);
                    return true;
                }
                if (sc.Status == ServiceControllerStatus.StartPending)
                {
                    _logger.LogInformation("Service {ServiceName} is in the process of starting. Waiting...", serviceName);
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(ActualServiceWaitTimeout)));
                    return sc.Status == ServiceControllerStatus.Running;
                }

                _logger.LogInformation("Starting service: {ServiceName}...", serviceName);
                sc.Start();
                await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(ActualServiceWaitTimeout)));
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    _logger.LogInformation("Service {ServiceName} started successfully.", serviceName);
                    return true;
                }
                else
                {
                    _logger.LogError("Cannot start service {ServiceName}. Current status: {Status}", serviceName, sc.Status);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting service {ServiceName}.", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Stops a Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to stop.</param>
        /// <returns>True if the service was stopped successfully, false otherwise.</returns>
        [SupportedOSPlatform("windows")]
        private async Task<bool> StopServiceAsync(string serviceName)
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogError("Service control operations are only supported on Windows.");
                return false;
            }

            try
            {
                ServiceController sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    _logger.LogInformation("Service {ServiceName} is already stopped.", serviceName);
                    return true;
                }
                if (sc.Status == ServiceControllerStatus.StopPending)
                {
                    _logger.LogInformation("Service {ServiceName} is in the process of stopping. Waiting...", serviceName);
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(ActualServiceWaitTimeout)));
                    return sc.Status == ServiceControllerStatus.Stopped;
                }

                if (sc.CanStop)
                {
                    _logger.LogInformation("Stopping service: {ServiceName}...", serviceName);
                    sc.Stop();
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(ActualServiceWaitTimeout)));
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        _logger.LogInformation("Service {ServiceName} stopped successfully.", serviceName);
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Cannot stop service {ServiceName}. Current status: {Status}", serviceName, sc.Status);
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning("Service {ServiceName} cannot be stopped (CanStop=false). Status: {Status}", serviceName, sc.Status);
                    return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "InvalidOperationException while stopping service {ServiceName} (service may not exist or has been uninstalled).", serviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping service {ServiceName}.", serviceName);
                return false;
            }
        }
        #endregion
    }
}
