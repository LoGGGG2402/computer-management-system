using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CMSUpdater.Utilities;

namespace CMSUpdater
{
    /// <summary>
    /// Parameter class for containing update command-line arguments
    /// </summary>
    public class UpdateParameters
    {
        /// <summary>
        /// PID of the old agent process to watch for termination
        /// </summary>
        public int PidToWatch { get; set; }
        
        /// <summary>
        /// Path to the new agent files
        /// </summary>
        public string NewAgentPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Directory where the current agent is installed
        /// </summary>
        public string CurrentAgentInstallDir { get; set; } = string.Empty;
        
        /// <summary>
        /// Directory to store updater logs
        /// </summary>
        public string UpdaterLogDir { get; set; } = string.Empty;
        
        /// <summary>
        /// Root directory for backup files
        /// </summary>
        public string BackupDirRoot { get; set; } = string.Empty;
        
        /// <summary>
        /// Current version of the agent
        /// </summary>
        public string CurrentAgentVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Core logic for the CMS Update process
    /// </summary>
    public class UpdaterLogic
    {
        private readonly ILogger<UpdaterLogic> _logger;
        private readonly UpdateParameters _params;
        private const string SERVICE_NAME = "CMSAgentService";
        private readonly string[] _filesToPreserve = new[] { "agent_config.json", "runtime_config.json", "logs" };

        /// <summary>
        /// Constructor for UpdaterLogic
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="updateParameters">Update parameters</param>
        public UpdaterLogic(ILogger<UpdaterLogic> logger, UpdateParameters updateParameters)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _params = updateParameters ?? throw new ArgumentNullException(nameof(updateParameters));
            
            // Validate parameters
            if (_params.PidToWatch <= 0)
                throw new ArgumentException("Invalid PID specified", nameof(updateParameters.PidToWatch));
            
            if (string.IsNullOrEmpty(_params.NewAgentPath) || !Directory.Exists(_params.NewAgentPath))
                throw new ArgumentException("Invalid new agent path", nameof(updateParameters.NewAgentPath));
            
            if (string.IsNullOrEmpty(_params.CurrentAgentInstallDir) || !Directory.Exists(_params.CurrentAgentInstallDir))
                throw new ArgumentException("Invalid current agent installation directory", nameof(updateParameters.CurrentAgentInstallDir));
            
            if (string.IsNullOrEmpty(_params.UpdaterLogDir))
                throw new ArgumentException("Invalid updater log directory", nameof(updateParameters.UpdaterLogDir));
            
            if (string.IsNullOrEmpty(_params.BackupDirRoot))
                throw new ArgumentException("Invalid backup directory root", nameof(updateParameters.BackupDirRoot));
            
            if (string.IsNullOrEmpty(_params.CurrentAgentVersion))
                throw new ArgumentException("Invalid current agent version", nameof(updateParameters.CurrentAgentVersion));
        }        /// <summary>
        /// Executes the update process
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if update was successful, false otherwise</returns>
        [SupportedOSPlatform("windows")]
        public async Task<bool> ExecuteUpdateAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting update process. New agent: {NewAgentPath}, Current install: {CurrentInstall}, Current version: {CurrentVersion}",
                _params.NewAgentPath, _params.CurrentAgentInstallDir, _params.CurrentAgentVersion);
            
            try
            {                // 1. Wait for the old agent process to exit
                _logger.LogInformation("Waiting for old agent (PID: {Pid}) to exit", _params.PidToWatch);
                bool exited = ProcessHelper.WaitForProcessToExit(_logger, _params.PidToWatch, TimeSpan.FromMinutes(2));
                if (!exited)
                {
                    _logger.LogError("Timeout waiting for old agent to exit");
                    return false;
                }
                
                // 2. Create a backup of the current agent
                string backupDir = Path.Combine(_params.BackupDirRoot, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
                _logger.LogInformation("Creating backup of current agent to {BackupDir}", backupDir);
                await FileOperations.BackupFilesAsync(_logger, _params.CurrentAgentInstallDir, backupDir, _params.CurrentAgentVersion, cancellationToken);
                
                // 3. Replace the agent files
                _logger.LogInformation("Replacing agent files");
                await FileOperations.ReplaceFilesAsync(_logger, _params.NewAgentPath, _params.CurrentAgentInstallDir, _filesToPreserve, cancellationToken);
                
                // 4. Start the new agent service
                _logger.LogInformation("Starting new agent service");
                bool serviceStarted = ServiceHelper.TryStartService(_logger, SERVICE_NAME);
                if (!serviceStarted)
                {
                    _logger.LogError("Failed to start new agent service, initiating rollback");
                    await RollbackAsync(backupDir, _params.CurrentAgentInstallDir, SERVICE_NAME, cancellationToken);
                    return false;
                }
                
                // 5. Clean up
                _logger.LogInformation("Cleaning up extracted update files");
                FileOperations.CleanupDirectory(_logger, _params.NewAgentPath);
                
                _logger.LogInformation("Update completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update process");                try
                {
                    string backupDir = Path.Combine(_params.BackupDirRoot, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
                    await RollbackAsync(backupDir, _params.CurrentAgentInstallDir, SERVICE_NAME, cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error during rollback process");
                }
                return false;
            }
        }        // Methods previously defined here have been moved to the ProcessHelper and FileOperations utility classes        /// <summary>
        /// Rolls back the update by restoring from backup
        /// </summary>
        /// <param name="backupDir">Backup directory</param>
        /// <param name="targetInstallDir">Target installation directory</param>
        /// <param name="serviceName">Service name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [SupportedOSPlatform("windows")]
        private async Task RollbackAsync(string backupDir, string targetInstallDir, string serviceName, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Rolling back update to previous version");
            
            try
            {
                // Stop the service if it's running
                ServiceHelper.TryStopService(_logger, serviceName);
                
                // Delete all files in the target directory except those to preserve (using utility method)
                await FileOperations.ReplaceFilesAsync(_logger, backupDir, targetInstallDir, _filesToPreserve, cancellationToken);
                
                // Try to start the service again
                ServiceHelper.TryStartService(_logger, serviceName);
                
                _logger.LogInformation("Rollback completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rollback process");
                throw;
            }
        }
    }
}