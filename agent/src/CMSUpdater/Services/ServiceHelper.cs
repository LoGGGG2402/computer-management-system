using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.Versioning;

namespace CMSUpdater.Services;

/// <summary>
/// Utility class for interacting with Windows Service Control Manager (SCM) for Updater
/// </summary>
[SupportedOSPlatform("windows")]
public class ServiceHelper(ILogger logger)
{
    private readonly ILogger _logger = logger;
    
    /// <summary>
    /// Start the agent service
    /// </summary>
    /// <param name="serviceName">Service name</param>
    /// <exception cref="InvalidOperationException">Thrown when unable to start the service</exception>
    [SupportedOSPlatform("windows")]
    public void StartAgentService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            _logger.LogInformation("Starting service {ServiceName}...", serviceName);
            
            var timeout = TimeSpan.FromSeconds(30);
            
            if (service.Status != ServiceControllerStatus.Running)
            {
                // Start service if it's not running
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    _logger.LogInformation("Service {ServiceName} started successfully.", serviceName);
                }
                else
                {
                    // If service is in another state (StartPending, PausePending, etc.), wait for it to complete
                    _logger.LogWarning("Service {ServiceName} is in state {Status}. Waiting...", 
                        serviceName, service.Status);
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    _logger.LogInformation("Service {ServiceName} transitioned to Running state.", serviceName);
                }
            }
            else
            {
                _logger.LogInformation("Service {ServiceName} is already running.", serviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to start service {ServiceName}.", serviceName);
            throw new InvalidOperationException($"Unable to start service {serviceName}.", ex);
        }
    }
    
    /// <summary>
    /// Stop the agent service
    /// </summary>
    /// <param name="serviceName">Service name</param>
    /// <exception cref="InvalidOperationException">Thrown when unable to stop the service</exception>
    [SupportedOSPlatform("windows")]
    public void StopAgentService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            _logger.LogInformation("Stopping service {ServiceName}...", serviceName);
            
            var timeout = TimeSpan.FromSeconds(30);
            
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                // Stop service if it's running
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    _logger.LogInformation("Service {ServiceName} stopped successfully.", serviceName);
                }
                else
                {
                    // If service is in another state (StopPending, PausePending, etc.), wait for it to complete
                    _logger.LogWarning("Service {ServiceName} is in state {Status}. Waiting...", 
                        serviceName, service.Status);
                    service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    _logger.LogInformation("Service {ServiceName} transitioned to Stopped state.", serviceName);
                }
            }
            else
            {
                _logger.LogInformation("Service {ServiceName} is already stopped.", serviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to stop service {ServiceName}.", serviceName);
            throw new InvalidOperationException($"Unable to stop service {serviceName}.", ex);
        }
    }
    
    /// <summary>
    /// Check if the agent service is running
    /// </summary>
    /// <param name="serviceName">Service name</param>
    /// <returns>true if service is running; false otherwise</returns>
    [SupportedOSPlatform("windows")]
    public bool IsAgentServiceRunning(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            var isRunning = service.Status == ServiceControllerStatus.Running;
            _logger.LogDebug("Service {ServiceName} status: {Status}. Running: {IsRunning}", 
                serviceName, service.Status, isRunning);
            return isRunning;
        }
        catch (InvalidOperationException)
        {
            // Service doesn't exist
            _logger.LogWarning("Service {ServiceName} does not exist.", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking status of service {ServiceName}.", serviceName);
            return false;
        }
    }
    
    /// <summary>
    /// Check if a process is still running
    /// </summary>
    /// <param name="processId">ID of the process to check</param>
    /// <returns>true if process exists; false otherwise</returns>
    [SupportedOSPlatform("windows")]
    public bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking process {ProcessId}.", processId);
            return false;
        }
    }
}