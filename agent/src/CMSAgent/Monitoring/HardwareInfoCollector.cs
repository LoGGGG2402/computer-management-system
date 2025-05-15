using System;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Monitoring
{
    /// <summary>
    /// Collects system hardware information.
    /// </summary>
    /// <param name="logger">Logger for logging.</param>
    public class HardwareInfoCollector(ILogger<HardwareInfoCollector> logger)
    {
        private readonly ILogger<HardwareInfoCollector> _logger = logger;

        /// <summary>
        /// Collects system hardware information.
        /// </summary>
        /// <returns>Collected hardware information.</returns>
        public async Task<HardwareInfoPayload> CollectHardwareInfoAsync()
        {
            var hwInfo = new HardwareInfoPayload
            {
                os_info = string.Empty,
                cpu_info = string.Empty,
                gpu_info = string.Empty,
                total_ram = 0,
                total_disk_space = 0
            };

            try
            {
                hwInfo.os_info = GetOsInfo();
                hwInfo.cpu_info = GetCpuInfo();
                hwInfo.gpu_info = GetGpuInfo();
                hwInfo.total_ram = GetTotalRam();
                hwInfo.total_disk_space = GetTotalDiskSpace();

                _logger.LogInformation("Hardware information collected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting hardware information");
                
                // If there's an error, still return object with collected information
                if (string.IsNullOrEmpty(hwInfo.os_info))
                    hwInfo.os_info = "Unable to collect operating system information";
                
                if (string.IsNullOrEmpty(hwInfo.cpu_info))
                    hwInfo.cpu_info = "Unable to collect CPU information";
                
                if (string.IsNullOrEmpty(hwInfo.gpu_info))
                    hwInfo.gpu_info = "Unable to collect GPU information";
                
                if (hwInfo.total_ram <= 0)
                    hwInfo.total_ram = 0;
                
                if (hwInfo.total_disk_space <= 0)
                    hwInfo.total_disk_space = 0;
            }

            return await Task.FromResult(hwInfo);
        }

        /// <summary>
        /// Collects operating system information.
        /// </summary>
        private string GetOsInfo()
        {
            try
            {
                string osInfo = string.Empty;

                using var searcher = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem");
                {
                    foreach (var os in searcher.Get())
                    {
                        string caption = os["Caption"]?.ToString() ?? "Unknown Windows";
                        string version = os["Version"]?.ToString() ?? "Unknown Version";
                        string architecture = os["OSArchitecture"]?.ToString() ?? "Unknown Architecture";
                        
                        osInfo = $"{caption} {version} {architecture}";
                        break;
                    }
                }

                return osInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting operating system information");
                return "Unable to collect operating system information";
            }
        }

        /// <summary>
        /// Collects CPU information.
        /// </summary>
        private string GetCpuInfo()
        {
            try
            {
                string cpuInfo = string.Empty;

                using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                {
                    foreach (var cpu in searcher.Get())
                    {
                        string name = cpu["Name"]?.ToString() ?? "Unknown CPU";
                        string cores = cpu["NumberOfCores"]?.ToString() ?? "?";
                        string logicalProcessors = cpu["NumberOfLogicalProcessors"]?.ToString() ?? "?";
                        
                        cpuInfo = $"{name} ({cores} cores, {logicalProcessors} logical processors)";
                        break;
                    }
                }

                return cpuInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting CPU information");
                return "Unable to collect CPU information";
            }
        }

        /// <summary>
        /// Collects GPU information.
        /// </summary>
        private string GetGpuInfo()
        {
            try
            {
                string gpuInfo = string.Empty;

                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                {
                    foreach (var gpu in searcher.Get())
                    {
                        string name = gpu["Name"]?.ToString() ?? "Unknown GPU";
                        
                        if (gpu["AdapterRAM"] != null)
                        {
                            try
                            {
                                uint ramBytes = Convert.ToUInt32(gpu["AdapterRAM"]);
                                double ramGB = Math.Round(ramBytes / (1024.0 * 1024.0 * 1024.0), 2);
                                gpuInfo += $"{name} ({ramGB} GB VRAM); ";
                            }
                            catch
                            {
                                gpuInfo += $"{name}; ";
                            }
                        }
                        else
                        {
                            gpuInfo += $"{name}; ";
                        }
                    }

                    // Remove the last semicolon if present
                    if (gpuInfo.EndsWith("; "))
                    {
                        gpuInfo = gpuInfo[..^2];
                    }
                }

                return string.IsNullOrEmpty(gpuInfo) ? "Unable to collect GPU information" : gpuInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting GPU information");
                return "Unable to collect GPU information";
            }
        }

        /// <summary>
        /// Collects total RAM information.
        /// </summary>
        private long GetTotalRam()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                {
                    foreach (var ram in searcher.Get())
                    {
                        if (ram["TotalPhysicalMemory"] != null)
                        {
                            return Convert.ToInt64(ram["TotalPhysicalMemory"]);
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting total RAM information");
                return 0;
            }
        }

        /// <summary>
        /// Collects information about the main disk drive's total space.
        /// </summary>
        private long GetTotalDiskSpace()
        {
            try
            {
                DriveInfo driveC = new("C:");
                return driveC.TotalSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting disk information");
                return 0;
            }
        }
    }
}
