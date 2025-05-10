// Filepath: c:\Users\longpph\Desktop\computer-management-system\dotnet_agent\CMSAgent\SystemOperations\WindowsServiceInstaller.cs
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Manages the installation, uninstallation, and control of a Windows service.
    /// Uses sc.exe for service operations to avoid dependencies on System.Configuration.Install.
    /// </summary>
    public class WindowsServiceInstaller
    {
        private readonly ILogger<WindowsServiceInstaller> _logger;
        private readonly string _serviceName;
        private readonly string _serviceDisplayName;
        private readonly string _serviceDescription;
        private readonly string _serviceExecutablePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsServiceInstaller"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="serviceDisplayName">The display name of the service.</param>
        /// <param name="serviceDescription">The description of the service.</param>
        /// <param name="serviceExecutablePath">The full path to the service executable. If null, defaults to the current process executable path.</param>
        /// <exception cref="InvalidOperationException">Thrown if the service executable path cannot be determined.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the service executable path does not exist.</exception>
        public WindowsServiceInstaller(ILogger<WindowsServiceInstaller> logger, 
                                     string serviceName, 
                                     string serviceDisplayName, 
                                     string serviceDescription,
                                     string? serviceExecutablePath = null)
        {
            _logger = logger;
            _serviceName = serviceName;
            _serviceDisplayName = serviceDisplayName;
            _serviceDescription = serviceDescription;
            _serviceExecutablePath = serviceExecutablePath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                                     ?? throw new InvalidOperationException("Could not determine service executable path.");

            if (!File.Exists(_serviceExecutablePath))
            {
                throw new FileNotFoundException($"Service executable not found at specified path: {_serviceExecutablePath}");
            }
        }

        /// <summary>
        /// Asynchronously installs the service with the specified name, display name, and description.
        /// This is a static helper method.
        /// </summary>
        /// <param name="serviceName">The name of the service to install.</param>
        /// <param name="displayName">The display name for the service.</param>
        /// <param name="description">The description for the service.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task InstallServiceAsync(string serviceName, string displayName, string description)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<WindowsServiceInstaller>();
            
            var installer = new WindowsServiceInstaller(logger, serviceName, displayName, description);
            await installer.InstallServiceAsync();
        }

        /// <summary>
        /// Asynchronously uninstalls the service with the specified name.
        /// This is a static helper method.
        /// </summary>
        /// <param name="serviceName">The name of the service to uninstall.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task UninstallServiceAsync(string serviceName)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<WindowsServiceInstaller>();
            
            var installer = new WindowsServiceInstaller(
                logger, 
                serviceName, 
                "Service to uninstall",
                "Service to uninstall"
            );
            await installer.UninstallServiceAsync();
        }

        /// <summary>
        /// Checks if the service is installed.
        /// </summary>
        /// <returns>True if the service is installed; otherwise, false.</returns>
        public bool IsServiceInstalled()
        {
            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    var status = sc.Status;
                    _logger.LogInformation("Service '{ServiceName}' is installed. Status: {Status}", _serviceName, status);
                    return true;
                }
            }
            catch (InvalidOperationException ex) 
            { 
                _logger.LogInformation(ex, "Service '{ServiceName}' is not installed or an error occurred checking status.", _serviceName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while checking if service '{ServiceName}' is installed.", _serviceName);
                return false;
            }
        }
        
        /// <summary>
        /// Asynchronously installs the Windows service.
        /// If the service is already installed, this method logs the information and returns.
        /// Configures service recovery options upon successful installation.
        /// </summary>
        /// <returns>A task that represents the asynchronous installation operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the 'sc.exe create' command fails.</exception>
        /// <exception cref="Exception">Rethrows exceptions that occur during the installation process.</exception>
        public async Task InstallServiceAsync()
        {
            if (IsServiceInstalled())
            {
                _logger.LogInformation("Service '{ServiceName}' is already installed.", _serviceName);
                return;
            }

            _logger.LogInformation("Installing service '{ServiceName}'... Executable: {ExecutablePath}", _serviceName, _serviceExecutablePath);
            try
            {
                var arguments = $"create \"{_serviceName}\" binPath= \"{_serviceExecutablePath}\" DisplayName= \"{_serviceDisplayName}\" start= auto";
                var processStartInfo = new System.Diagnostics.ProcessStartInfo("sc.exe", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Service '{ServiceName}' installed successfully. Output: {Result}", _serviceName, result);
                    }
                    else
                    {
                        _logger.LogError("Failed to install service '{ServiceName}'. Exit Code: {ExitCode}, Output: {Result}", _serviceName, process.ExitCode, result);
                        throw new InvalidOperationException($"Failed to install service. Exit code: {process.ExitCode}");
                    }
                }
                
                arguments = $"description \"{_serviceName}\" \"{_serviceDescription}\"";
                processStartInfo = new System.Diagnostics.ProcessStartInfo("sc.exe", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    process.WaitForExit();
                }
                
                await Task.Delay(2000);
                if (IsServiceInstalled())
                {
                    _logger.LogInformation("Service '{ServiceName}' successfully installed.", _serviceName);
                    ConfigureServiceRecovery();
                }
                else
                {
                    _logger.LogWarning("Service '{ServiceName}' installation might have failed or is delayed. Please check system logs.", _serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install service '{ServiceName}'.", _serviceName);
                throw;
            }
        }
        
        /// <summary>
        /// Asynchronously uninstalls the Windows service.
        /// If the service is not installed, this method logs the information and returns.
        /// Stops the service before attempting to uninstall it.
        /// </summary>
        /// <returns>A task that represents the asynchronous uninstallation operation.</returns>
        /// <exception cref="Exception">Rethrows exceptions that occur during the uninstallation process.</exception>
        public async Task UninstallServiceAsync()
        {
            if (!IsServiceInstalled())
            {
                _logger.LogInformation("Service '{ServiceName}' is not installed. No action taken.", _serviceName);
                return;
            }

            _logger.LogInformation("Uninstalling service '{ServiceName}'...", _serviceName);
            try
            {
                await StopServiceAsync();

                var arguments = $"delete \"{_serviceName}\"";
                var processStartInfo = new System.Diagnostics.ProcessStartInfo("sc.exe", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Service '{ServiceName}' uninstalled successfully. Output: {Result}", _serviceName, result);
                    }
                    else
                    {
                        _logger.LogError("Failed to uninstall service '{ServiceName}'. Exit Code: {ExitCode}, Output: {Result}", _serviceName, process.ExitCode, result);
                    }
                }

                await Task.Delay(2000);
                if (!IsServiceInstalled())
                {
                    _logger.LogInformation("Service '{ServiceName}' successfully uninstalled.", _serviceName);
                }
                else
                {
                    _logger.LogWarning("Service '{ServiceName}' uninstallation might have failed or is delayed. Please check system logs.", _serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to uninstall service '{ServiceName}'.", _serviceName);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously starts the Windows service.
        /// If the service is not installed or already running, this method logs the information and returns.
        /// </summary>
        /// <param name="timeout">Optional timeout for waiting for the service to start. Defaults to 30 seconds.</param>
        /// <returns>A task that represents the asynchronous start operation.</returns>
        /// <exception cref="System.ServiceProcess.TimeoutException">Thrown if the service does not start within the specified timeout.</exception>
        /// <exception cref="Exception">Rethrows other exceptions that occur during the start process.</exception>
        public async Task StartServiceAsync(TimeSpan? timeout = null)
        {
            if (!IsServiceInstalled())
            {
                _logger.LogWarning("Service '{ServiceName}' is not installed. Cannot start.", _serviceName);
                return;
            }

            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        _logger.LogInformation("Service '{ServiceName}' is already running.", _serviceName);
                        return;
                    }
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.Paused)
                    {
                        _logger.LogWarning("Service '{ServiceName}' is in an intermediate state ({Status}). Attempting to start anyway.", _serviceName, sc.Status);
                    }

                    _logger.LogInformation("Starting service '{ServiceName}'...", _serviceName);
                    sc.Start();
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, timeout ?? TimeSpan.FromSeconds(30)));
                    _logger.LogInformation("Service '{ServiceName}' started successfully.", _serviceName);
                }
            }
            catch (System.ServiceProcess.TimeoutException tex)
            {
                _logger.LogError(tex, "Timeout waiting for service '{ServiceName}' to start.", _serviceName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start service '{ServiceName}'.", _serviceName);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously stops the Windows service.
        /// If the service is not installed or already stopped, this method logs the information and returns.
        /// </summary>
        /// <param name="timeout">Optional timeout for waiting for the service to stop. Defaults to 30 seconds.</param>
        /// <returns>A task that represents the asynchronous stop operation.</returns>
        /// <exception cref="System.ServiceProcess.TimeoutException">Thrown if the service does not stop within the specified timeout.</exception>
        /// <exception cref="Exception">Rethrows other exceptions that occur during the stop process.</exception>
        public async Task StopServiceAsync(TimeSpan? timeout = null)
        {
            if (!IsServiceInstalled())
            {
                _logger.LogWarning("Service '{ServiceName}' is not installed. Cannot stop.", _serviceName);
                return;
            }

            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        _logger.LogInformation("Service '{ServiceName}' is already stopped.", _serviceName);
                        return;
                    }
                    if (!sc.CanStop)
                    {
                        _logger.LogWarning("Service '{ServiceName}' cannot be stopped in its current state ({Status}).", _serviceName, sc.Status);
                        return;
                    }

                    _logger.LogInformation("Stopping service '{ServiceName}'...", _serviceName);
                    sc.Stop();
                    await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout ?? TimeSpan.FromSeconds(30)));
                    _logger.LogInformation("Service '{ServiceName}' stopped successfully.", _serviceName);
                }
            }
            catch (System.ServiceProcess.TimeoutException tex)
            {
                _logger.LogError(tex, "Timeout waiting for service '{ServiceName}' to stop.", _serviceName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop service '{ServiceName}'.", _serviceName);
                throw;
            }
        }

        /// <summary>
        /// Gets the current status of the Windows service.
        /// </summary>
        /// <returns>The <see cref="ServiceControllerStatus"/> of the service. Returns <see cref="ServiceControllerStatus.Stopped"/> if the service is not installed.</returns>
        /// <exception cref="Exception">Rethrows exceptions that occur while getting the service status, other than service not found.</exception>
        public ServiceControllerStatus GetServiceStatus()
        {            
            if (!IsServiceInstalled())
            {
                _logger.LogWarning("Service '{ServiceName}' is not installed. Returning {Status}.", _serviceName, ServiceControllerStatus.Stopped);
                return ServiceControllerStatus.Stopped;
            }
            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    return sc.Status;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get status for service '{ServiceName}'.", _serviceName);
                throw;
            }
        }

        /// <summary>
        /// Configures the service recovery options using sc.exe.
        /// Sets the service to restart on failure after 60 seconds, for up to 3 attempts, with a reset period of 1 day (86400 seconds).
        /// This method requires administrative privileges.
        /// </summary>
        private void ConfigureServiceRecovery()
        {
            _logger.LogInformation("Configuring service recovery options for '{ServiceName}'...", _serviceName);            
            try
            {
                string arguments = $"failure \"{_serviceName}\" reset= 86400 actions= restart/60000/restart/60000/restart/60000";
                System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo("sc.exe", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (System.Diagnostics.Process process = new System.Diagnostics.Process() { StartInfo = procStartInfo })
                {
                    process.Start();
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Service recovery options configured successfully for '{ServiceName}'. Output: {Result}", _serviceName, result);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to configure service recovery for '{ServiceName}'. Exit Code: {ExitCode}, Output: {Result}", _serviceName, process.ExitCode, result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring service recovery for '{ServiceName}'. This usually requires admin privileges.", _serviceName);
            }        
        }
    }
}
