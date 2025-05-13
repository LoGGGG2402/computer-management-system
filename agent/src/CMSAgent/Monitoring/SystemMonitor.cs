using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Monitoring
{
    /// <summary>
    /// Thu thập thông tin tài nguyên hệ thống.
    /// </summary>
    public class SystemMonitor
    {
        private readonly ILogger<SystemMonitor> _logger;
        private PerformanceCounter _cpuCounter;
        private bool _isInitialized = false;
        private long _ramTotal = 0;
        private string _systemDrive;

        /// <summary>
        /// Khởi tạo một instance mới của SystemMonitor.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public SystemMonitor(ILogger<SystemMonitor> logger)
        {
            _logger = logger;
            _systemDrive = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:" : "/";
        }

        /// <summary>
        /// Khởi tạo các bộ đếm hiệu suất và thành phần giám sát.
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    // Đọc giá trị đầu tiên (luôn trả về 0)
                    _cpuCounter.NextValue();
                }

                // Lấy tổng RAM
                _ramTotal = GetTotalRam();

                _isInitialized = true;
                _logger.LogInformation("Đã khởi tạo thành công SystemMonitor");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể khởi tạo SystemMonitor");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Lấy trạng thái sử dụng tài nguyên hiện tại.
        /// </summary>
        /// <returns>Dữ liệu về mức sử dụng CPU, RAM và disk.</returns>
        public async Task<StatusUpdatePayload> GetCurrentStatusAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("SystemMonitor chưa được khởi tạo. Thử khởi tạo lại...");
                Initialize();
                
                if (!_isInitialized)
                {
                    _logger.LogError("Không thể lấy thông tin tài nguyên vì SystemMonitor không được khởi tạo");
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
                // Lấy CPU usage
                status.cpuUsage = await GetCpuUsageAsync();
                
                // Lấy RAM usage
                status.ramUsage = GetRamUsage();
                
                // Lấy Disk usage
                status.diskUsage = GetDiskUsage();

                _logger.LogDebug("Thông tin tài nguyên: CPU {CpuUsage:F1}%, RAM {RamUsage:F1}%, Disk {DiskUsage:F1}%", 
                    status.cpuUsage, status.ramUsage, status.diskUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin tài nguyên");
            }

            return status;
        }

        /// <summary>
        /// Lấy phần trăm sử dụng CPU.
        /// </summary>
        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (_cpuCounter != null)
                    {
                        return Math.Round(_cpuCounter.NextValue(), 1);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Lấy thông tin CPU qua /proc/stat
                    var firstMeasurement = await GetLinuxCpuMeasurementAsync();
                    await Task.Delay(500); // Chờ 0.5 giây để lấy mẫu thứ hai
                    var secondMeasurement = await GetLinuxCpuMeasurementAsync();

                    if (firstMeasurement != null && secondMeasurement != null)
                    {
                        double totalDiff = secondMeasurement.Total - firstMeasurement.Total;
                        double idleDiff = secondMeasurement.Idle - firstMeasurement.Idle;

                        if (totalDiff > 0)
                        {
                            double usage = 100.0 * (1.0 - idleDiff / totalDiff);
                            return Math.Round(usage, 1);
                        }
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "top",
                            Arguments = "-l 1 | grep \"CPU usage\"",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            FileName = "/bin/bash",
                            Arguments = "-c \"top -l 1 | grep \\\"CPU usage\\\"\""
                        }
                    };

                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    // Parse output: "CPU usage: 7.63% user, 14.35% sys, 78.1% idle"
                    if (!string.IsNullOrEmpty(output))
                    {
                        string[] parts = output.Split(' ');
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (parts[i] == "idle")
                            {
                                string idlePct = parts[i - 1].TrimEnd('%');
                                if (double.TryParse(idlePct, out double idle))
                                {
                                    return Math.Round(100 - idle, 1);
                                }
                            }
                        }
                    }
                }

                // Fallback
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin CPU");
                return 0;
            }
        }

        /// <summary>
        /// Lấy phần trăm sử dụng RAM.
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

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
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

                    process.Start();
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
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cat",
                            Arguments = "/proc/meminfo",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    long memTotal = 0;
                    long memAvailable = 0;

                    foreach (string line in output.Split('\n'))
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                long.TryParse(parts[1], out memTotal);
                        }
                        else if (line.StartsWith("MemAvailable:"))
                        {
                            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                long.TryParse(parts[1], out memAvailable);
                        }
                    }

                    if (memTotal > 0 && memAvailable > 0)
                    {
                        double ramUsage = 100.0 * (1.0 - (double)memAvailable / memTotal);
                        return Math.Round(ramUsage, 1);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "vm_stat",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    long pageSize = 4096; // 4KB is default page size on macOS
                    long freePages = 0;
                    long activePages = 0;
                    long inactivePages = 0;
                    long wiredPages = 0;

                    foreach (string line in output.Split('\n'))
                    {
                        if (line.Contains("Pages free:"))
                        {
                            string value = line.Split(':')[1].Trim().TrimEnd('.');
                            long.TryParse(value, out freePages);
                        }
                        else if (line.Contains("Pages active:"))
                        {
                            string value = line.Split(':')[1].Trim().TrimEnd('.');
                            long.TryParse(value, out activePages);
                        }
                        else if (line.Contains("Pages inactive:"))
                        {
                            string value = line.Split(':')[1].Trim().TrimEnd('.');
                            long.TryParse(value, out inactivePages);
                        }
                        else if (line.Contains("Pages wired down:"))
                        {
                            string value = line.Split(':')[1].Trim().TrimEnd('.');
                            long.TryParse(value, out wiredPages);
                        }
                    }

                    long usedPages = activePages + wiredPages;
                    long totalPages = freePages + activePages + inactivePages + wiredPages;

                    if (totalPages > 0)
                    {
                        double ramUsage = 100.0 * (double)usedPages / totalPages;
                        return Math.Round(ramUsage, 1);
                    }
                }

                // Fallback
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin RAM");
                return 0;
            }
        }

        /// <summary>
        /// Lấy phần trăm sử dụng ổ đĩa.
        /// </summary>
        private double GetDiskUsage()
        {
            try
            {
                DriveInfo driveInfo = new DriveInfo(_systemDrive);
                double totalSize = driveInfo.TotalSize;
                double freeSpace = driveInfo.AvailableFreeSpace;
                double usedPercentage = 100.0 * (1.0 - freeSpace / totalSize);
                
                return Math.Round(usedPercentage, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin ổ đĩa");
                return 0;
            }
        }

        /// <summary>
        /// Lấy tổng dung lượng RAM của hệ thống (bytes).
        /// </summary>
        private long GetTotalRam()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "wmic",
                            Arguments = "ComputerSystem get TotalPhysicalMemory",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
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
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cat",
                            Arguments = "/proc/meminfo",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    foreach (string line in output.Split('\n'))
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                            {
                                return kb * 1024; // KB to bytes
                            }
                        }
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sysctl",
                            Arguments = "-n hw.memsize",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (long.TryParse(output, out long bytes))
                    {
                        return bytes;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tổng RAM");
                return 0;
            }
        }

        /// <summary>
        /// Đối tượng lưu trữ thông tin CPU trên Linux.
        /// </summary>
        private class LinuxCpuMeasurement
        {
            public long User { get; set; }
            public long Nice { get; set; }
            public long System { get; set; }
            public long Idle { get; set; }
            public long IoWait { get; set; }
            public long Irq { get; set; }
            public long SoftIrq { get; set; }
            public long Steal { get; set; }
            public long Guest { get; set; }
            public long GuestNice { get; set; }

            public long Total => User + Nice + System + Idle + IoWait + Irq + SoftIrq + Steal + Guest + GuestNice;
        }

        /// <summary>
        /// Lấy thông tin CPU trên Linux từ /proc/stat.
        /// </summary>
        private async Task<LinuxCpuMeasurement> GetLinuxCpuMeasurementAsync()
        {
            try
            {
                string[] lines = await File.ReadAllLinesAsync("/proc/stat");
                foreach (string line in lines)
                {
                    if (line.StartsWith("cpu "))
                    {
                        string[] values = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (values.Length >= 11)
                        {
                            var measurement = new LinuxCpuMeasurement
                            {
                                User = long.Parse(values[1]),
                                Nice = long.Parse(values[2]),
                                System = long.Parse(values[3]),
                                Idle = long.Parse(values[4]),
                                IoWait = long.Parse(values[5]),
                                Irq = long.Parse(values[6]),
                                SoftIrq = long.Parse(values[7]),
                                Steal = long.Parse(values[8]),
                                Guest = long.Parse(values[9]),
                                GuestNice = long.Parse(values[10])
                            };
                            return measurement;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin CPU từ /proc/stat");
                return null;
            }
        }
    }
}
