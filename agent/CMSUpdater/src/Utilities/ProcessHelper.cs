using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CMSUpdater.Utilities
{
    /// <summary>
    /// Helper class for process-related operations
    /// </summary>
    public static class ProcessHelper
    {
        /// <summary>
        /// Waits for a process to exit within the given timeout
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="pid">Process ID</param>
        /// <param name="timeout">Maximum wait time</param>
        /// <returns>True if process exited, false if timeout occurred</returns>
        public static bool WaitForProcessToExit(ILogger logger, int pid, TimeSpan timeout)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                if (process == null || process.HasExited)
                {
                    logger.LogInformation("Process with PID {Pid} has already exited", pid);
                    return true;
                }

                logger.LogInformation("Waiting for process with PID {Pid} to exit (timeout: {Timeout} seconds)", 
                    pid, timeout.TotalSeconds);
                
                return process.WaitForExit((int)timeout.TotalMilliseconds);
            }
            catch (ArgumentException)
            {
                // Process is not running
                logger.LogInformation("Process with PID {Pid} is not running", pid);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error waiting for process exit");
                return false;
            }
        }
    }
}
