using Microsoft.Extensions.Logging;
using System.Diagnostics;
using CMSUpdater.Services;
using CMSAgent.Common.Enums;
using System.Runtime.Versioning;
using CMSAgent.Common.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CMSUpdater.Core;

/// <summary>
/// Class responsible for executing agent update logic
/// </summary>
[SupportedOSPlatform("windows")]
public class UpdaterLogic
{
    private readonly ILogger _logger;
    private readonly RollbackManager _rollbackManager;
    private readonly ServiceHelper _serviceHelper;
    private readonly int _agentProcessIdToWait;
    private readonly string _newAgentPath;
    private readonly string _currentAgentInstallDir;
    private readonly string _currentAgentVersion;
    private readonly string _agentServiceName = "CMSAgentService";
    
    // Timeout and wait durations
    private readonly TimeSpan _processExitTimeout = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _serviceWatchdogTime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _serviceCheckInterval = TimeSpan.FromSeconds(30);
    
    private readonly IConfiguration _configuration;
    
    /// <summary>
    /// Initialize UpdaterLogic
    /// </summary>
    /// <param name="logger">Logger for logging</param>
    /// <param name="rollbackManager">Manager to handle rollback if update fails</param>
    /// <param name="serviceHelper">Helper to interact with Windows Service</param>
    /// <param name="agentProcessIdToWait">PID of old agent process to stop</param>
    /// <param name="newAgentPath">Path to extracted new agent files</param>
    /// <param name="currentAgentInstallDir">Current installation directory path</param>
    /// <param name="updaterLogDir">Updater log directory</param>
    /// <param name="currentAgentVersion">Current agent version</param>
    /// <param name="configuration">Configuration to read settings</param>
    public UpdaterLogic(
        ILogger logger,
        RollbackManager rollbackManager,
        ServiceHelper serviceHelper,
        int agentProcessIdToWait,
        string newAgentPath,
        string currentAgentInstallDir,
        string updaterLogDir,
        string currentAgentVersion,
        IConfiguration configuration)
    {
        _logger = logger;
        _rollbackManager = rollbackManager;
        _serviceHelper = serviceHelper;
        _agentProcessIdToWait = agentProcessIdToWait;
        _newAgentPath = newAgentPath;
        _currentAgentInstallDir = currentAgentInstallDir;
        _currentAgentVersion = currentAgentVersion;
        _configuration = configuration;
    }
    
    /// <summary>
    /// Execute the update process
    /// </summary>
    /// <returns>Update process status code (exit code)</returns>
    public async Task<int> ExecuteUpdateAsync()
    {
        _logger.LogInformation("Starting agent update from version {CurrentVersion}", _currentAgentVersion);
        
        try
        {
            // Step 1: Wait for old agent process to stop
            _logger.LogInformation("Waiting for old agent process (PID: {PID}) to stop...", _agentProcessIdToWait);
            if (!await WaitForProcessExitAsync(_agentProcessIdToWait, _processExitTimeout))
            {
                _logger.LogError("Timeout while waiting for old agent process (PID: {PID}) to stop", _agentProcessIdToWait);
                ErrorLogs.LogError(ErrorType.UpdateFailure, 
                    $"Timeout while waiting for old agent process (PID: {_agentProcessIdToWait}) to stop", 
                    new { ProcessId = _agentProcessIdToWait, Timeout = _processExitTimeout }, 
                    _logger);
                return (int)UpdaterExitCodes.AgentStopTimeout;
            }
            
            // Ensure service is stopped
            if (_serviceHelper.IsAgentServiceRunning(_agentServiceName))
            {
                _logger.LogWarning("Service {ServiceName} is still running. Stopping service...", _agentServiceName);
                try
                {
                    _serviceHelper.StopAgentService(_agentServiceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to stop service {ServiceName}", _agentServiceName);
                    ErrorLogs.LogException(ErrorType.ServiceOperationFailure, ex, _logger);
                    return (int)UpdaterExitCodes.StopAgentFailed;
                }
            }
            
            // Step 2: Backup old agent
            _logger.LogInformation("Backing up old agent...");
            try
            {
                await BackupAgentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to backup old agent");
                ErrorLogs.LogException(ErrorType.BackupFailure, ex, _logger);
                return (int)UpdaterExitCodes.BackupFailed;
            }
            
            // Step 3: Deploy new agent
            _logger.LogInformation("Deploying new agent from {NewAgentPath}...", _newAgentPath);
            try
            {
                await DeployNewAgentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deploy new agent");
                ErrorLogs.LogException(ErrorType.DeploymentFailure, ex, _logger);
                
                try
                {
                    await _rollbackManager.RollbackAsync("UpdateDeploymentFailed");
                    return (int)UpdaterExitCodes.DeployFailed;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback after deployment failure failed");
                    ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Step 4: Start new agent service
            _logger.LogInformation("Starting new agent service...");
            try
            {
                _serviceHelper.StartAgentService(_agentServiceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to start new agent service");
                ErrorLogs.LogException(ErrorType.ServiceOperationFailure, ex, _logger);
                
                try
                {
                    await _rollbackManager.RollbackAsync("NewServiceStartFailed");
                    return (int)UpdaterExitCodes.NewServiceStartFailed;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback after service start failure failed");
                    ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Step 5: Watchdog to ensure new agent stability
            _logger.LogInformation("Monitoring new service for {WatchdogTime} (crash detection)...", _serviceWatchdogTime);
            if (!await WatchdogServiceAsync(_serviceWatchdogTime, _serviceCheckInterval))
            {
                _logger.LogError("New agent service is unstable, performing rollback...");
                ErrorLogs.LogError(ErrorType.ServiceInstability, 
                    "New agent service is unstable after update", 
                    new { ServiceName = _agentServiceName, WatchdogTime = _serviceWatchdogTime }, 
                    _logger);
                
                try
                {
                    await _rollbackManager.RollbackAsync("NewServiceUnstable");
                    return (int)UpdaterExitCodes.WatchdogTriggeredRollback;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback after service crash failed");
                    ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Step 6: Cleanup after successful update
            _logger.LogInformation("Update successful. Cleaning up...");
            await CleanUpAsync();
            
            _logger.LogInformation("Update completed successfully");
            return (int)UpdaterExitCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during update process");
            ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
            
            try
            {
                await _rollbackManager.RollbackAsync("UpdateDeploymentFailed");
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback after unhandled error failed");
                ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
                return (int)UpdaterExitCodes.RollbackFailed;
            }
            
            return (int)UpdaterExitCodes.GeneralError;
        }
    }
    
    /// <summary>
    /// Wait for process to exit
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="timeout">Timeout duration</param>
    /// <returns>true if process exited; false if timeout</returns>
    private async Task<bool> WaitForProcessExitAsync(int pid, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (!_serviceHelper.IsProcessRunning(pid))
                {
                    _logger.LogInformation("Process {PID} has exited", pid);
                    return true;
                }
                
                _logger.LogDebug("Process {PID} is still running. Waiting for 1 second...", pid);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking process {PID}", pid);
                ErrorLogs.LogException(ErrorType.ServiceOperationFailure, ex, _logger);
                // Assume process has exited if error occurs
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Backup current agent
    /// </summary>
    private async Task BackupAgentAsync()
    {
        string backupFolderPath = Path.Combine(_currentAgentInstallDir, $"backup_{_currentAgentVersion}");
        
        // Delete old backup if exists
        if (Directory.Exists(backupFolderPath))
        {
            _logger.LogInformation("Deleting old backup at {BackupPath}...", backupFolderPath);
            Directory.Delete(backupFolderPath, true);
        }
        
        // Create backup directory
        _logger.LogInformation("Creating backup directory at {BackupPath}...", backupFolderPath);
        Directory.CreateDirectory(backupFolderPath);
        
        // Copy files and directories (excluding backup and update folders)
        foreach (var entry in Directory.GetFileSystemEntries(_currentAgentInstallDir))
        {
            var entryName = Path.GetFileName(entry);
            
            // Skip folders not to be backed up
            if (entryName.StartsWith("backup_", StringComparison.OrdinalIgnoreCase) ||
                entryName.Equals("updates", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var destPath = Path.Combine(backupFolderPath, entryName);
            
            try
            {
                if (File.Exists(entry))
                {
                    _logger.LogDebug("Backing up file: {FilePath}", entry);
                    File.Copy(entry, destPath, true);
                }
                else if (Directory.Exists(entry))
                {
                    _logger.LogDebug("Backing up directory: {DirPath}", entry);
                    CopyDirectory(entry, destPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to backup {Entry}", entry);
                ErrorLogs.LogException(ErrorType.BackupFailure, ex, _logger);
                throw;
            }
        }
        
        await Task.CompletedTask; // To keep method async
    }
    
    /// <summary>
    /// Deploy new agent
    /// </summary>
    /// <returns>Task representing the deployment process</returns>
    private async Task DeployNewAgentAsync()
    {
        await Task.Yield(); // Ensure method is async
        
        _logger.LogInformation("Deploying new agent from {SourceDir} to {TargetDir}", _newAgentPath, _currentAgentInstallDir);
        
        // Check if new directory exists
        if (!Directory.Exists(_newAgentPath))
        {
            _logger.LogError("New agent directory does not exist: {Path}", _newAgentPath);
            throw new DirectoryNotFoundException($"New agent directory not found: {_newAgentPath}");
        }
        
        // Read excluded files list from configuration
        var filesToExclude = _configuration.GetSection("Updater:FilesToExcludeFromUpdate").Get<string[]>() ?? Array.Empty<string>();
        _logger.LogInformation("Files to be excluded from update: {Files}", string.Join(", ", filesToExclude));

        // Collect all files in source directory (new agent)
        var sourceFiles = Directory.GetFiles(_newAgentPath, "*", SearchOption.AllDirectories);
        _logger.LogDebug("Found {Count} files in source directory", sourceFiles.Length);
        
        int filesProcessed = 0;
        int filesCopied = 0;
        int filesSkipped = 0;
        
        foreach (var sourceFile in sourceFiles)
        {
            filesProcessed++;
            
            // Relative path from source directory
            string relativePath = Path.GetRelativePath(_newAgentPath, sourceFile);
            
            // Check if file is in exclude list
            bool shouldExclude = false;
            foreach (var pattern in filesToExclude)
            {
                if (IsFileMatchPattern(relativePath, pattern))
                {
                    shouldExclude = true;
                    _logger.LogDebug("Skipping file matching exclude pattern: {File} (pattern: {Pattern})", relativePath, pattern);
                    break;
                }
            }
            
            if (shouldExclude)
            {
                filesSkipped++;
                continue;
            }
            
            // Destination path for file
            string targetFile = Path.Combine(_currentAgentInstallDir, relativePath);
            
            try
            {
                // Create destination directory if not exists
                string targetDir = Path.GetDirectoryName(targetFile) ?? string.Empty;
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                
                // Copy file, overwrite if exists
                File.Copy(sourceFile, targetFile, true);
                filesCopied++;
                
                // Log every 100 files to avoid excessive logging
                if (filesProcessed % 100 == 0 || filesProcessed == sourceFiles.Length)
                {
                    _logger.LogInformation("Copy progress: {Processed}/{Total} files (copied: {Copied}, skipped: {Skipped})",
                        filesProcessed, sourceFiles.Length, filesCopied, filesSkipped);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying file {Source} to {Target}", sourceFile, targetFile);
                throw new IOException($"Unable to copy file {sourceFile}: {ex.Message}", ex);
            }
        }
        
        _logger.LogInformation("Deployment successful: Total {Total} files, copied {Copied}, skipped {Skipped}",
            filesProcessed, filesCopied, filesSkipped);
    }
    
    /// <summary>
    /// Check if a file matches a glob pattern
    /// </summary>
    /// <param name="filePath">File path to check</param>
    /// <param name="pattern">Glob pattern (e.g., *.json, logs/**)</param>
    /// <returns>True if file matches the pattern</returns>
    private bool IsFileMatchPattern(string filePath, string pattern)
    {
        // Handle special characters in glob pattern
        if (pattern.StartsWith("*"))
        {
            string extension = pattern.TrimStart('*');
            return filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }
        
        // Check if filePath matches pattern exactly
        if (string.Equals(filePath, pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // Check if filePath is a specific directory
        if (pattern.EndsWith("/") && filePath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // Check if filePath is a file in the specified directory
        string directory = pattern;
        if (directory.EndsWith("/*") || directory.EndsWith("/**"))
        {
            directory = directory.Substring(0, directory.LastIndexOf('/'));
            return filePath.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }
    
    /// <summary>
    /// Monitor service for a duration
    /// </summary>
    /// <param name="watchTime">Monitoring duration</param>
    /// <param name="checkInterval">Interval between checks</param>
    /// <returns>true if service is stable during monitoring; false if service is unstable</returns>
    private async Task<bool> WatchdogServiceAsync(TimeSpan watchTime, TimeSpan checkInterval)
    {
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < watchTime)
        {
            if (!_serviceHelper.IsAgentServiceRunning(_agentServiceName))
            {
                _logger.LogError("Service {ServiceName} stopped during monitoring!", _agentServiceName);
                return false;
            }
            
            _logger.LogDebug("Service {ServiceName} is running normally. Remaining time to monitor: {TimeLeft}.", 
                _agentServiceName, watchTime - stopwatch.Elapsed);
            
            await Task.Delay(checkInterval);
        }
        
        return true;
    }
    
    /// <summary>
    /// Cleanup after successful update
    /// </summary>
    private async Task CleanUpAsync()
    {
        try
        {
            // Cleanup temporary files if needed
            
            // Optionally delete extracted new agent directory
            if (Directory.Exists(_newAgentPath))
            {
                _logger.LogInformation("Deleting extracted new agent directory: {Path}", _newAgentPath);
                Directory.Delete(_newAgentPath, true);
            }
            
            // Perform other cleanup tasks here if needed
        }
        catch (Exception ex)
        {
            // Log error but do not throw exception as this is just cleanup
            _logger.LogWarning(ex, "Error during cleanup after update");
            ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
        }
        
        await Task.CompletedTask; // To keep method async
    }
    
    /// <summary>
    /// Copy entire directory and its contents
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        // Create destination directory if not exists
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