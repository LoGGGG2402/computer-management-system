using CMSAgent.Models.Payloads;
using Microsoft.Win32;
using Serilog;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace CMSAgent.Monitoring
{
    public class SystemMonitor : ISystemMonitor
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _ramCounter;

        public SystemMonitor()
        {
            // Initialize performance counters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
        }

        public async Task<HardwareInfoPayload> GetHardwareInfoAsync()
        {
            try
            {
                var hardwareInfo = new HardwareInfoPayload();

                // Get OS information
                hardwareInfo.os_info = GetOSInfo();

                // Get CPU information
                hardwareInfo.cpu_info = GetCpuInfo();

                // Get GPU information
                hardwareInfo.gpu_info = GetGpuInfo();

                // Get RAM information
                hardwareInfo.total_ram = GetTotalRam();

                // Get disk information
                hardwareInfo.total_disk_space = GetTotalDiskSpace();

                return hardwareInfo;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting hardware information: {Message}", ex.Message);
                
                // Return a minimal payload with at least the required fields
                return new HardwareInfoPayload
                {
                    os_info = "Error getting OS info",
                    total_disk_space = GetTotalDiskSpace() // This is required
                };
            }
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            try
            {
                // First reading might be 0, so take a few samples
                _cpuCounter.NextValue();
                await Task.Delay(500);

                // Get CPU usage
                float cpuUsage = _cpuCounter.NextValue();

                // Get RAM usage
                float ramUsage = _ramCounter.NextValue();

                // Get disk usage
                float diskUsage = GetDiskUsagePercent();

                return new SystemMetrics
                {
                    CpuUsage = Math.Min(Math.Round(cpuUsage, 1), 100),
                    RamUsage = Math.Min(Math.Round(ramUsage, 1), 100),
                    DiskUsage = Math.Min(Math.Round(diskUsage, 1), 100)
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting system metrics: {Message}", ex.Message);
                return new SystemMetrics
                {
                    CpuUsage = 0,
                    RamUsage = 0,
                    DiskUsage = 0
                };
            }
        }

        private string GetOSInfo()
        {
            try
            {
                string osName = string.Empty;
                string osVersion = string.Empty;
                string osBuild = string.Empty;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject os in searcher.Get().Cast<ManagementObject>())
                    {
                        osName = os["Caption"]?.ToString() ?? "Unknown OS";
                        osVersion = os["Version"]?.ToString() ?? "Unknown Version";
                        osBuild = os["BuildNumber"]?.ToString() ?? "Unknown Build";
                        break;
                    }
                }

                return $"{osName} {osVersion} Build {osBuild}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting OS information: {Message}", ex.Message);
                return $"Windows {Environment.OSVersion.Version}";
            }
        }

        private string GetCpuInfo()
        {
            try
            {
                string cpuInfo = string.Empty;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementObject processor in searcher.Get().Cast<ManagementObject>())
                    {
                        cpuInfo = processor["Name"]?.ToString() ?? "Unknown CPU";
                        break;
                    }
                }

                return cpuInfo;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting CPU information: {Message}", ex.Message);
                return "Unknown CPU";
            }
        }

        private string GetGpuInfo()
        {
            try
            {
                string gpuInfo = string.Empty;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject gpu in searcher.Get().Cast<ManagementObject>())
                    {
                        gpuInfo = gpu["Name"]?.ToString() ?? "Unknown GPU";
                        break; // Just get the first GPU
                    }
                }

                return gpuInfo;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting GPU information: {Message}", ex.Message);
                return "Unknown GPU";
            }
        }

        private long GetTotalRam()
        {
            try
            {
                long totalRam = 0;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                    {
                        totalRam = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                        break;
                    }
                }

                return totalRam;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting total RAM: {Message}", ex.Message);
                return 0;
            }
        }

        private long GetTotalDiskSpace()
        {
            try
            {
                // Get the system drive (usually C:)
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                
                DriveInfo drive = new DriveInfo(systemDrive);
                return drive.TotalSize;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting total disk space: {Message}", ex.Message);
                
                // Return a minimum value to prevent errors
                return 1000000000; // 1 GB
            }
        }

        private float GetDiskUsagePercent()
        {
            try
            {
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                
                DriveInfo drive = new DriveInfo(systemDrive);
                long totalSize = drive.TotalSize;
                long freeSpace = drive.AvailableFreeSpace;
                
                // Calculate used space percentage
                double usedPercentage = ((double)(totalSize - freeSpace) / totalSize) * 100;
                return (float)usedPercentage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting disk usage: {Message}", ex.Message);
                return 0;
            }
        }
    }

    public class SystemMetrics
    {
        public double CpuUsage { get; set; }
        public double RamUsage { get; set; }
        public double DiskUsage { get; set; }
    }
}