using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace CMSUpdater
{
    class Program
    {
        private static Serilog.ILogger _log = null!;
        private const string DefaultBackupSubDir = "_backup";
        private const string TempExtractSubDir = "_temp_update";

        [SupportedOSPlatform("windows")]
        static async Task<int> Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _log = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            _log.Information("CMSUpdater starting up with args: {Args}", string.Join(" ", args));

            string? packagePath = null;
            string? targetDir = null;
            string? serviceName = null;
            string? backupDir = null;
            string processName = "CMSAgent"; // Default process name to look for

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--package":
                        packagePath = args[++i];
                        break;
                    case "--target-dir":
                        targetDir = args[++i];
                        break;
                    case "--service-name":
                        serviceName = args[++i];
                        break;
                    case "--backup-dir":
                        backupDir = args[++i];
                        break;
                    case "--process-name":
                        processName = args[++i];
                        break;
                }
            }

            if (string.IsNullOrEmpty(packagePath) || string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(serviceName))
            {
                _log.Error("Missing required arguments: --package, --target-dir, --service-name must be provided.");
                Console.WriteLine("Usage: CMSUpdater --package <path_to_zip> --target-dir <agent_install_dir> --service-name <agent_service_name> [--backup-dir <path_to_backup>] [--process-name <agent_process_name>]");
                return 1;
            }

            if (!File.Exists(packagePath))
            {
                _log.Error("Update package not found at: {PackagePath}", packagePath);
                return 2;
            }

            if (!Directory.Exists(targetDir))
            {
                _log.Error("Target directory not found: {TargetDir}", targetDir);
                return 3;
            }

            backupDir ??= Path.Combine(Path.GetDirectoryName(targetDir) ?? ".", $"{Path.GetFileName(targetDir)}{DefaultBackupSubDir}_{DateTime.Now:yyyyMMddHHmmss}");
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"{TempExtractSubDir}_{Guid.NewGuid()}");

            try
            {
                _log.Information("Update process started.");
                _log.Information("Package: {PackagePath}", packagePath);
                _log.Information("Target Directory: {TargetDir}", targetDir);
                _log.Information("Service Name: {ServiceName}", serviceName);
                _log.Information("Backup Directory: {BackupDir}", backupDir);
                _log.Information("Temporary Extraction Directory: {TempExtractDir}", tempExtractDir);

                // 1. Stop the service and kill the process if running
                await StopAgentServiceAndProcess(serviceName, processName, targetDir);

                // 2. Create backup
                _log.Information("Creating backup of {TargetDir} to {BackupDir}", targetDir, backupDir);
                if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                Directory.CreateDirectory(backupDir);
                CopyDirectory(targetDir, backupDir, _log);
                _log.Information("Backup created successfully.");

                // 3. Extract update package to temp location
                _log.Information("Extracting update package {PackagePath} to {TempExtractDir}", packagePath, tempExtractDir);
                if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
                Directory.CreateDirectory(tempExtractDir);
                ZipFile.ExtractToDirectory(packagePath, tempExtractDir, true);
                _log.Information("Package extracted successfully.");

                // 4. Clear target directory (excluding backup and updater itself if it's inside)
                _log.Information("Clearing target directory: {TargetDir}", targetDir);
                string currentProcessFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
                ClearDirectory(targetDir, _log, Path.GetFileName(backupDir), currentProcessFileName);
 
                // 5. Copy new files from temp to target
                _log.Information("Copying new files from {TempExtractDir} to {TargetDir}", tempExtractDir, targetDir);
                CopyDirectory(tempExtractDir, targetDir, _log);
                _log.Information("New files copied successfully.");

                _log.Information("Update applied. Attempting to start service...");
                // 6. Start the service
                await StartAgentService(serviceName);

                _log.Information("Update process completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "An error occurred during the update process.");
                _log.Information("Attempting to restore from backup directory: {BackupDir}", backupDir);
                if (Directory.Exists(backupDir) && Directory.Exists(targetDir))
                {
                    try
                    {
                        _log.Information("Restoring files from backup...");
                        ClearDirectory(targetDir, _log);
                        CopyDirectory(backupDir, targetDir, _log);
                        _log.Information("Restore from backup completed. Attempting to restart service.");
                        await StartAgentService(serviceName); // Try to restart after restore
                    }
                    catch (Exception restoreEx)
                    {
                        _log.Error(restoreEx, "Failed to restore from backup.");
                    }
                }
                return 4;
            }
            finally
            {
                // 7. Cleanup
                try
                {
                    if (Directory.Exists(tempExtractDir))
                    {
                        _log.Information("Deleting temporary extraction directory: {TempExtractDir}", tempExtractDir);
                        Directory.Delete(tempExtractDir, true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _log.Warning(cleanupEx, "Error during temporary directory cleanup.");
                }
                _log.Information("CMSUpdater finished.");
                await Log.CloseAndFlushAsync();
            }
        }

        [SupportedOSPlatform("windows")]
        private static async Task StopAgentServiceAndProcess(string serviceName, string processName, string targetDir)
        {
            _log.Information("Attempting to stop service: {ServiceName}", serviceName);
            try
            {
                ServiceController sc = new ServiceController(serviceName);
                if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                {
                    _log.Information("Service status: {Status}. Stopping...", sc.Status);
                    sc.Stop();
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60)));
                    _log.Information("Service stopped successfully.");
                }
                else
                {
                    _log.Information("Service already stopped or stop pending.");
                }
            }
            catch (InvalidOperationException ex) // Service not found
            {
                _log.Warning(ex, "Service {ServiceName} not found or could not be controlled. It might not be installed.", serviceName);
            }
            catch (System.ServiceProcess.TimeoutException ex)
            {
                _log.Error(ex, "Timeout waiting for service {ServiceName} to stop.", serviceName);
                throw; // Re-throw to indicate critical failure
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error stopping service {ServiceName}. Manual intervention might be needed.", serviceName);
                // Decide if this is critical enough to stop the update
            }

            _log.Information("Ensuring agent process '{ProcessName}' is not running from target directory.", processName);
            var processes = Process.GetProcessesByName(processName).Where(p => 
                {
                    try { return Path.GetDirectoryName(p.MainModule?.FileName)?.Equals(targetDir, StringComparison.OrdinalIgnoreCase) ?? false; }
                    catch { return false; } // Access denied etc.
                }).ToList();

            foreach (var process in processes)
            {
                _log.Information("Found running agent process: {ProcessId}. Attempting to kill.", process.Id);
                try
                {
                    process.Kill();
                    process.WaitForExit(10000); // Wait 10 seconds
                    _log.Information("Process {ProcessId} killed.", process.Id);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Failed to kill process {ProcessId}. Update may fail.", process.Id);
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static async Task StartAgentService(string serviceName)
        {
            _log.Information("Attempting to start service: {ServiceName}", serviceName);
            try
            {
                ServiceController sc = new ServiceController(serviceName);
                if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                {
                    _log.Information("Service status: {Status}. Starting...", sc.Status);
                    sc.Start();
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60)));
                    _log.Information("Service started successfully.");
                }
                else
                {
                    _log.Information("Service already running or start pending.");
                }
            }
            catch (InvalidOperationException ex) // Service not found
            {
                _log.Error(ex, "Service {ServiceName} not found. Cannot start.", serviceName);
            }
             catch (System.ServiceProcess.TimeoutException ex)
            {
                _log.Error(ex, "Timeout waiting for service {ServiceName} to start.", serviceName);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error starting service {ServiceName}. Manual intervention might be needed.", serviceName);
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, Serilog.ILogger log, bool recursive = true)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir); // Create destination if it doesn't exist

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                try
                {
                    file.CopyTo(targetFilePath, true); // Overwrite existing files
                }
                catch (IOException ex)
                {
                    log.Warning(ex, "Could not copy file {SourceFile} to {DestinationFile}. It might be in use. Retrying once...", file.FullName, targetFilePath);
                    Thread.Sleep(100); // Wait a bit
                    file.CopyTo(targetFilePath, true); // Retry
                }
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, log, true);
                }
            }
        }
        
        private static void ClearDirectory(string directoryPath, Serilog.ILogger log, params string[] excludeItems)
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            if (!dirInfo.Exists) return;

            foreach (FileInfo file in dirInfo.GetFiles())
            {
                if (excludeItems.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
                {
                    log.Information("Skipping deletion of excluded file: {FileName}", file.FullName);
                    continue;
                }
                try { file.Delete(); } catch (Exception ex) { log.Warning(ex, "Failed to delete file {FilePath}", file.FullName); }
            }

            foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
            {
                 if (excludeItems.Contains(subDir.Name, StringComparer.OrdinalIgnoreCase))
                {
                    log.Information("Skipping deletion of excluded directory: {DirectoryName}", subDir.FullName);
                    continue;
                }
                try { subDir.Delete(true); } catch (Exception ex) { log.Warning(ex, "Failed to delete directory {DirectoryPath}", subDir.FullName); }
            }
        }
    }
}
