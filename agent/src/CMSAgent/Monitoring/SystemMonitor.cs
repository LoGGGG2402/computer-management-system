using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Monitoring
{
    /// <summary>
    /// Collects system resource information.
    /// </summary>
    /// <param name="logger">Logger for logging.</param>
    public class SystemMonitor(ILogger<SystemMonitor> logger)
    {
        private readonly ILogger<SystemMonitor> _logger = logger;
        private PerformanceCounter? _cpuCounter;
        private bool _isInitialized = false;
        private long _ramTotal = 0;
        private readonly string _systemDrive = "C:";

        /// <summary>
        /// Initializes performance counters and monitoring components.
        /// </summary>
        public void Initialize()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // Read the first value (always returns 0)
                _ = _cpuCounter.NextValue();

                // Get total RAM
                _ramTotal = GetTotalRam();

                _isInitialized = true;
                _logger.LogInformation("SystemMonitor initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot initialize SystemMonitor");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Gets the current resource usage status.
        /// </summary>
        /// <returns>Data about CPU, RAM, and disk usage.</returns>
        public async Task<StatusUpdatePayload> GetCurrentStatusAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("SystemMonitor not initialized. Trying to initialize again...");
                Initialize();

                if (!_isInitialized)
                {
                    _logger.LogError("Cannot retrieve resource information because SystemMonitor is not initialized");
                    return new StatusUpdatePayload
                    {
                        cpuUsage = 0,
                        ramUsage = 0,
                        diskUsage = 0
                    };
                }
            }

            var status = new StatusUpdatePayload();

            try
            {
                // Get CPU usage
                status.cpuUsage = await GetCpuUsageAsync();

                // Get RAM usage
                status.ramUsage = GetRamUsage();

                // Get Disk usage
                status.diskUsage = GetDiskUsage();

                _logger.LogDebug("Resource information: CPU {CpuUsage:F1}%, RAM {RamUsage:F1}%, Disk {DiskUsage:F1}%",
                    status.cpuUsage, status.ramUsage, status.diskUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting resource information");
            }

            return status;
        }

        /// <summary>
        /// Gets the CPU usage percentage.
        /// </summary>
        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    float value = _cpuCounter.NextValue();
                    await Task.Delay(1);
                    return Math.Round(value, 1);
                }
                // Fallback
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting CPU information");
                return 0;
            }
        }

        /// <summary>
        /// Gets the RAM usage percentage.
        /// </summary>
        private double GetRamUsage()
        {
            try
            {
                if (_ramTotal <= 0)
                {
                    _ramTotal = GetTotalRam();
                    if (_ramTotal <= 0)
                        return 0;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "OS get FreePhysicalMemory",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _ = process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Trim().Split('\n');
                if (lines.Length > 1)
                {
                    string freeMemory = lines[1].Trim();
                    if (long.TryParse(freeMemory, out long freeRamKB))
                    {
                        long freeRamBytes = freeRamKB * 1024;
                        double ramUsage = 100.0 * (1.0 - (double)freeRamBytes / _ramTotal);
                        return Math.Round(ramUsage, 1);
                    }
                }
                
                // Fallback
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting RAM information");
                return 0;
            }
        }

        /// <summary>
        /// Gets the disk usage percentage.
        /// </summary>
        private double GetDiskUsage()
        {
            try
            {
                DriveInfo driveInfo = new(_systemDrive);
                double totalSize = driveInfo.TotalSize;
                double freeSpace = driveInfo.AvailableFreeSpace;
                double usedPercentage = 100.0 * (1.0 - freeSpace / totalSize);

                return Math.Round(usedPercentage, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting disk information");
                return 0;
            }
        }

        /// <summary>
        /// Gets the total system RAM (in bytes).
        /// </summary>
        private long GetTotalRam()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new()
                    {
                        FileName = "wmic",
                        Arguments = "ComputerSystem get TotalPhysicalMemory",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _ = process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Trim().Split('\n');
                if (lines.Length > 1)
                {
                    string totalMemory = lines[1].Trim();
                    if (long.TryParse(totalMemory, out long totalRamBytes))
                    {
                        return totalRamBytes;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total RAM");
                return 0;
            }
        }
    }
}
