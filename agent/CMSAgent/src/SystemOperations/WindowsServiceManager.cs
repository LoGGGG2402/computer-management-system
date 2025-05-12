using Serilog;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Manages Windows service operations for the CMS Agent
    /// </summary>
    public class WindowsServiceManager
    {
        private const string ServiceName = "CMSAgentService";
        private const string ServiceDisplayName = "CMS Agent Service";
        private const string ServiceDescription = "Computer Management System Agent Service";

        /// <summary>
        /// Checks if the service is installed
        /// </summary>
        public bool IsServiceInstalled()
        {
            try
            {
                ServiceController[] services = ServiceController.GetServices();
                return services.Any(s => s.ServiceName == ServiceName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking if service is installed: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets the current service status
        /// </summary>
        public ServiceControllerStatus? GetServiceStatus()
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return null;
                }

                using var controller = new ServiceController(ServiceName);
                return controller.Status;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting service status: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Installs the Windows service
        /// </summary>
        public async Task<bool> InstallServiceAsync()
        {
            try
            {
                Log.Information("Installing Windows service...");

                if (IsServiceInstalled())
                {
                    Log.Warning("Service is already installed");
                    return true;
                }

                // Get the path to the current executable
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    Log.Error("Could not determine the path to the executable");
                    return false;
                }

                // Use sc.exe to create the service
                var processInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"create {ServiceName} binPath= \"{exePath} --service\" DisplayName= \"{ServiceDisplayName}\" start= auto",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        Log.Error("Failed to create service: {Error}", string.IsNullOrEmpty(error) ? output : error);
                        return false;
                    }
                }

                // Set the service description
                processInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"description {ServiceName} \"{ServiceDescription}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        Log.Warning("Failed to set service description: {Error}", string.IsNullOrEmpty(error) ? output : error);
                        // We don't consider this a critical failure
                    }
                }

                // Set the service failure actions - restart on failure
                processInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        Log.Warning("Failed to set service failure actions: {Error}", string.IsNullOrEmpty(error) ? output : error);
                        // We don't consider this a critical failure
                    }
                }

                Log.Information("Service installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error installing service: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Uninstalls the Windows service
        /// </summary>
        public async Task<bool> UninstallServiceAsync()
        {
            try
            {
                Log.Information("Uninstalling Windows service...");

                if (!IsServiceInstalled())
                {
                    Log.Warning("Service is not installed");
                    return true;
                }

                // Stop service if it's running
                if (GetServiceStatus() == ServiceControllerStatus.Running || 
                    GetServiceStatus() == ServiceControllerStatus.StartPending ||
                    GetServiceStatus() == ServiceControllerStatus.PausePending)
                {
                    await StopServiceAsync();
                }

                // Use sc.exe to delete the service
                var processInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"delete {ServiceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        Log.Error("Failed to delete service: {Error}", string.IsNullOrEmpty(error) ? output : error);
                        return false;
                    }
                }

                Log.Information("Service uninstalled successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error uninstalling service: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Starts the Windows service
        /// </summary>
        public async Task<bool> StartServiceAsync()
        {
            try
            {
                Log.Information("Starting Windows service...");

                if (!IsServiceInstalled())
                {
                    Log.Error("Service is not installed");
                    return false;
                }

                // Check if service is already running
                var status = GetServiceStatus();
                if (status == ServiceControllerStatus.Running)
                {
                    Log.Warning("Service is already running");
                    return true;
                }

                if (status == ServiceControllerStatus.StartPending)
                {
                    Log.Warning("Service is already starting");
                    return true;
                }

                // Use net.exe to start the service (more reliable than ServiceController)
                var processInfo = new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = $"start {ServiceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        Log.Error("Failed to start service: {Error}", string.IsNullOrEmpty(error) ? output : error);
                        return false;
                    }
                }

                Log.Information("Service started successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error starting service: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Stops the Windows service
        /// </summary>
        public async Task<bool> StopServiceAsync()
        {
            try
            {
                Log.Information("Stopping Windows service...");

                if (!IsServiceInstalled())
                {
                    Log.Error("Service is not installed");
                    return false;
                }

                // Check if service is already stopped
                var status = GetServiceStatus();
                if (status == ServiceControllerStatus.Stopped)
                {
                    Log.Warning("Service is already stopped");
                    return true;
                }

                if (status == ServiceControllerStatus.StopPending)
                {
                    Log.Warning("Service is already stopping");
                    return true;
                }

                // Use net.exe to stop the service (more reliable than ServiceController)
                var processInfo = new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = $"stop {ServiceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        Log.Error("Failed to stop service: {Error}", string.IsNullOrEmpty(error) ? output : error);
                        return false;
                    }
                }

                Log.Information("Service stopped successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping service: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Restarts the Windows service
        /// </summary>
        public async Task<bool> RestartServiceAsync()
        {
            try
            {
                Log.Information("Restarting Windows service...");

                if (!IsServiceInstalled())
                {
                    Log.Error("Service is not installed");
                    return false;
                }

                // Stop service if it's running
                await StopServiceAsync();

                // Wait for service to fully stop
                await Task.Delay(TimeSpan.FromSeconds(5));

                // Start service
                return await StartServiceAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error restarting service: {Message}", ex.Message);
                return false;
            }
        }
    }
}