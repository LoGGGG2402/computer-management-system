using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace CMSUpdater.Services;

/// <summary>
/// Class managing the rollback process when update fails
/// </summary>
[SupportedOSPlatform("windows")]
public class RollbackManager
{
    private readonly ILogger _logger;
    private readonly string _currentAgentInstallDir;
    private readonly string _currentAgentVersion;
    private readonly ServiceHelper _serviceHelper;
    private readonly string _agentServiceName = "CMSAgentService";
    private readonly string _backupFolderPath;
    
    /// <summary>
    /// Initialize RollbackManager
    /// </summary>
    /// <param name="logger">Logger for logging</param>
    /// <param name="currentAgentInstallDir">Current installation directory path</param>
    /// <param name="currentAgentVersion">Current agent version</param>
    /// <param name="serviceHelper">Helper for interacting with Windows Service</param>
    public RollbackManager(
        ILogger logger, 
        string currentAgentInstallDir, 
        string currentAgentVersion, 
        ServiceHelper serviceHelper)
    {
        _logger = logger;
        _currentAgentInstallDir = currentAgentInstallDir;
        _currentAgentVersion = currentAgentVersion;
        _serviceHelper = serviceHelper;
        _backupFolderPath = Path.Combine(_currentAgentInstallDir, "backup_" + _currentAgentVersion);
    }
    
    /// <summary>
    /// Perform rollback
    /// </summary>
    /// <param name="reason">Reason for rollback</param>
    /// <returns>Task representing the asynchronous rollback process</returns>
    public async Task RollbackAsync(string reason)
    {
        _logger.LogWarning("Starting rollback process. Reason: {Reason}", reason);
        
        try
        {
            // Stop new agent service (if started)
            if (_serviceHelper.IsAgentServiceRunning(_agentServiceName))
            {
                _logger.LogInformation("Stopping new agent service before rollback...");
                try
                {
                    _serviceHelper.StopAgentService(_agentServiceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to stop new agent service. Continuing rollback...");
                }
            }
            
            // Check if backup exists
            if (!Directory.Exists(_backupFolderPath))
            {
                _logger.LogError("Backup folder not found at: {BackupPath}. Cannot rollback.", _backupFolderPath);
                throw new DirectoryNotFoundException($"Backup directory does not exist: {_backupFolderPath}");
            }
            
            // Delete current installation directory contents (except backup)
            _logger.LogInformation("Deleting current installation directory contents (except backup)...");
            foreach (var entry in Directory.GetFileSystemEntries(_currentAgentInstallDir))
            {
                var entryName = Path.GetFileName(entry);
                var backupFolderName = Path.GetFileName(_backupFolderPath);
                
                // Skip backup folder
                if (string.Equals(entryName, backupFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                try
                {
                    if (File.Exists(entry))
                    {
                        File.Delete(entry);
                    }
                    else if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to delete {Entry} during rollback.", entry);
                }
            }
            
            // Copy content from backup to installation directory
            _logger.LogInformation("Copying content from backup to installation directory...");
            foreach (var entry in Directory.GetFileSystemEntries(_backupFolderPath))
            {
                var entryName = Path.GetFileName(entry);
                var destPath = Path.Combine(_currentAgentInstallDir, entryName);
                
                try
                {
                    if (File.Exists(entry))
                    {
                        File.Copy(entry, destPath, true);
                    }
                    else if (Directory.Exists(entry))
                    {
                        CopyDirectory(entry, destPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to copy {Entry} during rollback.", entry);
                }
            }
            
            // Restart old agent service
            _logger.LogInformation("Restarting old agent service...");
            try
            {
                _serviceHelper.StartAgentService(_agentServiceName);
                _logger.LogInformation("Old agent service restarted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to restart old agent service after rollback.");
                throw;
            }
            
            _logger.LogInformation("Rollback completed successfully.");
            
            await Task.Delay(1); // Use await to avoid CS1998 warning
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed. Reason: {Reason}", reason);
            throw;
        }
    }
    
    /// <summary>
    /// Copy entire directory and its contents
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        // Create destination directory if it does not exist
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        // Copy all subdirectories
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}