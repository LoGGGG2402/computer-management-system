using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CMSAgent.Models;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Monitoring
{
    public class SystemMonitor
    {
        private readonly ILogger<SystemMonitor> _logger;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;

        public SystemMonitor(ILogger<SystemMonitor> logger)
        {
            _logger = logger;
            InitializePerformanceCounters();
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total", true);
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes", true);
                _logger.LogInformation("Performance counters initialized.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters. CPU/RAM usage might not be available. Ensure the application has permissions or the counters exist. For CPU, try 'Processor Information' category if 'Processor' fails.");
                _cpuCounter = null;
                _ramCounter = null;
            }
        }

        public async Task<SystemUsageStats> GetUsageStatsAsync()
        {
            var stats = new SystemUsageStats();

            try
            {
                if (_cpuCounter != null)
                {
                    _cpuCounter.NextValue();
                    await Task.Delay(100);
                    stats.CpuUsagePercentage = Math.Round(_cpuCounter.NextValue(), 2);
                }
                else
                {
                    stats.CpuUsagePercentage = -1;
                    _logger.LogDebug("CPU counter not available for GetUsageStatsAsync.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting CPU usage.");
                stats.CpuUsagePercentage = -1;
            }

            try
            {
                if (_ramCounter != null)
                {
                    float availableRamMB = _ramCounter.NextValue();
                    long totalMemoryMB = GetTotalPhysicalMemoryMB();
                    if (totalMemoryMB > 0)
                    {
                        var usedRamMB = totalMemoryMB - availableRamMB;
                        stats.RamUsagePercentage = Math.Round((double)usedRamMB / totalMemoryMB * 100, 2);
                    }
                    else
                    {
                        stats.RamUsagePercentage = -1;
                        _logger.LogWarning("Total memory reported as 0, cannot calculate RAM usage percentage.");
                    }
                }
                else
                {
                    stats.RamUsagePercentage = -1;
                    _logger.LogDebug("RAM counter not available for GetUsageStatsAsync.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting RAM usage.");
                stats.RamUsagePercentage = -1;
            }

            stats.DiskUsagePercentage = -1;
            try
            {
                string? systemDriveLetter = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.IsNullOrEmpty(systemDriveLetter))
                {
                    _logger.LogWarning("Could not determine system drive letter. Disk usage will be unavailable.");
                }
                else
                {
                    DriveInfo? systemDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.Equals(systemDriveLetter, StringComparison.OrdinalIgnoreCase));

                    if (systemDrive != null)
                    {
                        long totalBytes = systemDrive.TotalSize;
                        long freeBytes = systemDrive.TotalFreeSpace;
                        long usedBytes = totalBytes - freeBytes;
                        if (totalBytes > 0)
                        {
                            stats.DiskUsagePercentage = Math.Round((double)usedBytes / totalBytes * 100, 2);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("System drive (expected: {SystemDrivePath}) not found or not ready. Disk usage for primary drive unavailable.", systemDriveLetter);
                        DriveInfo? firstFixedDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
                        if (firstFixedDrive != null)
                        {
                            _logger.LogInformation("Using first available fixed drive '{DriveName}' for disk usage statistics.", firstFixedDrive.Name);
                            long totalBytes = firstFixedDrive.TotalSize;
                            long freeBytes = firstFixedDrive.TotalFreeSpace;
                            long usedBytes = totalBytes - freeBytes;
                            if (totalBytes > 0)
                            {
                                stats.DiskUsagePercentage = Math.Round((double)usedBytes / totalBytes * 100, 2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred while retrieving disk usage information for the primary drive.");
            }

            _logger.LogDebug("System usage stats (internal): CPU: {CpuUsagePercentage}%, RAM: {RamUsagePercentage}%, Disk: {DiskUsagePercentage}%",
                             stats.CpuUsagePercentage, stats.RamUsagePercentage, stats.DiskUsagePercentage);
            return stats;
        }

        private long GetTotalPhysicalMemoryMB()
        {
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (var mo in mos.Get())
                    {
                        if (mo["TotalVisibleMemorySize"] != null && ulong.TryParse(mo["TotalVisibleMemorySize"].ToString(), out ulong totalVisibleMemoryKilobytes))
                        {
                            return (long)(totalVisibleMemoryKilobytes / 1024);
                        }
                    }
                }
                _logger.LogWarning("Could not retrieve TotalVisibleMemorySize from WMI for GetTotalPhysicalMemoryMB.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve total physical memory using WMI for GetTotalPhysicalMemoryMB.");
            }
            return 0;
        }

        private long GetTotalPhysicalMemoryBytes()
        {
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (var mo in mos.Get())
                    {
                        if (mo["TotalVisibleMemorySize"] != null && ulong.TryParse(mo["TotalVisibleMemorySize"].ToString(), out ulong totalVisibleMemoryKilobytes))
                        {
                            return (long)(totalVisibleMemoryKilobytes * 1024);
                        }
                    }
                }
                _logger.LogWarning("Could not retrieve TotalVisibleMemorySize from WMI for GetTotalPhysicalMemoryBytes.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve total physical memory using WMI for GetTotalPhysicalMemoryBytes.");
            }
            return 0;
        }

        public async Task<Models.HardwareInfo> GetHardwareInfoAsync()
        {
            var hwInfo = new Models.HardwareInfo();
            _logger.LogDebug("Collecting hardware information...");

            try
            {
                using (var osSearcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem"))
                {
                    var os = osSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (os != null)
                    {
                        string caption = os["Caption"]?.ToString() ?? "N/A";
                        string version = os["Version"]?.ToString() ?? "N/A";
                        hwInfo.OsInfo = $"{caption},Version {version}".Trim();
                    }
                    else { hwInfo.OsInfo = "N/A"; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting OS info via WMI.");
                hwInfo.OsInfo = RuntimeInformation.OSDescription;
            }

            try
            {
                using (var cpuSearcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
                {
                    var cpu = cpuSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    hwInfo.CpuInfo = cpu?["Name"]?.ToString()?.Trim() ?? "N/A";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting CPU info via WMI.");
                hwInfo.CpuInfo = "N/A";
            }

            try
            {
                using (var gpuSearcher = new ManagementObjectSearcher("select Name from Win32_VideoController WHERE Availability=3 OR ConfigManagerErrorCode=0"))
                {
                    var gpu = gpuSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    hwInfo.GpuInfo = gpu?["Name"]?.ToString()?.Trim() ?? "N/A";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting GPU info via WMI.");
                hwInfo.GpuInfo = "N/A";
            }

            hwInfo.TotalRamBytes = GetTotalPhysicalMemoryBytes();
            if (hwInfo.TotalRamBytes == 0)
            {
                _logger.LogWarning("Total RAM reported as 0 bytes.");
            }

            try
            {
                string? systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrEmpty(systemDrivePath))
                {
                    DriveInfo systemDrive = new DriveInfo(systemDrivePath);
                    if (systemDrive.IsReady)
                    {
                        hwInfo.TotalDiskSpaceBytes = systemDrive.TotalSize;
                    }
                    else
                    {
                        _logger.LogWarning("System drive {SystemDrivePath} is not ready. Cannot get total disk space.", systemDrivePath);
                        hwInfo.TotalDiskSpaceBytes = 0;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not determine system drive path. Total disk space will be 0.");
                    hwInfo.TotalDiskSpaceBytes = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting total disk space for system drive.");
                hwInfo.TotalDiskSpaceBytes = 0;
            }

            try
            {
                hwInfo.IpAddress = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?
                    .Address.ToString() ?? "N/A";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting IP address.");
                hwInfo.IpAddress = "N/A";
            }

            _logger.LogDebug("Hardware Info collected: OS='{OsInfo}', CPU='{CpuInfo}', GPU='{GpuInfo}', RAM={TotalRamBytes}B, Disk={TotalDiskSpaceBytes}B, IP='{IpAddress}'",
                hwInfo.OsInfo, hwInfo.CpuInfo, hwInfo.GpuInfo, hwInfo.TotalRamBytes, hwInfo.TotalDiskSpaceBytes, hwInfo.IpAddress);

            return hwInfo;
        }

        public async Task<string> GetOsInfoAsync()
        {
            try
            {
                using (var osSearcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem"))
                {
                    var os = osSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (os != null)
                    {
                        string caption = os["Caption"]?.ToString() ?? "N/A";
                        string version = os["Version"]?.ToString() ?? "N/A";
                        return $"{caption},Version {version}".Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting OS info via WMI for GetOsInfoAsync. Using fallback.");
            }
            return RuntimeInformation.OSDescription;
        }
    }
}
