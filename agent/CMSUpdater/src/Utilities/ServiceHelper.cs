using System;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace CMSUpdater.Utilities
{
    /// <summary>
    /// Helper class for Windows service operations
    /// </summary>
    public static class ServiceHelper
    {
        /// <summary>
        /// Attempts to start a Windows service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="serviceName">Name of the service</param>
        /// <returns>True if service started successfully, false otherwise</returns>
        [SupportedOSPlatform("windows")]
        public static bool TryStartService(ILogger logger, string serviceName)
        {
            try
            {
                logger.LogInformation("Starting service: {ServiceName}", serviceName);
                
#if WINDOWS
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        logger.LogInformation("Service is already running");
                        return true;
                    }
                    
                    // If service is stopped, try to start it
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        logger.LogInformation("Service started successfully");
                        return true;
                    }
                    
                    logger.LogWarning("Service is in {Status} state, cannot start it", sc.Status);
                    return false;
                }
#else
                // On non-Windows platforms, assume success
                logger.LogWarning("Service control is only supported on Windows. Assuming service started successfully.");
                return true;
#endif
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error starting service");
                return false;
            }
        }

        /// <summary>
        /// Attempts to stop a Windows service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="serviceName">Name of the service</param>
        /// <returns>True if service stopped successfully, false otherwise</returns>
        [SupportedOSPlatform("windows")]
        public static bool TryStopService(ILogger logger, string serviceName)
        {
            try
            {
#if WINDOWS
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        logger.LogInformation("Service is already stopped");
                        return true;
                    }
                    
                    logger.LogInformation("Stopping service: {ServiceName}", serviceName);
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    logger.LogInformation("Service stopped successfully");
                    return true;
                }
#else
                // On non-Windows platforms, assume success
                logger.LogWarning("Service control is only supported on Windows. Assuming service stopped successfully.");
                return true;
#endif
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping service");
                return false;
            }
        }
    }
}
