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
        private PerformanceCounter? _cpuCounter;
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
            _systemDrive = "C:";
        }

        /// <summary>
        /// Khởi tạo các bộ đếm hiệu suất và thành phần giám sát.
        /// </summary>
        public void Initialize()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // Đọc giá trị đầu tiên (luôn trả về 0)
                _cpuCounter.NextValue();

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

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tổng RAM");
                return 0;
            }
        }
    }
}
