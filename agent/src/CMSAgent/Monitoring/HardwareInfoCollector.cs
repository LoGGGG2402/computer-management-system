using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Monitoring
{
    /// <summary>
    /// Thu thập thông tin phần cứng của hệ thống.
    /// </summary>
    public class HardwareInfoCollector
    {
        private readonly ILogger<HardwareInfoCollector> _logger;

        /// <summary>
        /// Khởi tạo một instance mới của HardwareInfoCollector.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public HardwareInfoCollector(ILogger<HardwareInfoCollector> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Thu thập thông tin phần cứng của hệ thống.
        /// </summary>
        /// <returns>Thông tin phần cứng đã thu thập.</returns>
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

                _logger.LogInformation("Đã thu thập thông tin phần cứng thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin phần cứng");
                
                // Nếu có lỗi, vẫn trả về object với các thông tin đã thu thập được
                if (string.IsNullOrEmpty(hwInfo.os_info))
                    hwInfo.os_info = "Không thể thu thập thông tin hệ điều hành";
                
                if (string.IsNullOrEmpty(hwInfo.cpu_info))
                    hwInfo.cpu_info = "Không thể thu thập thông tin CPU";
                
                if (string.IsNullOrEmpty(hwInfo.gpu_info))
                    hwInfo.gpu_info = "Không thể thu thập thông tin GPU";
                
                if (hwInfo.total_ram <= 0)
                    hwInfo.total_ram = 0;
                
                if (hwInfo.total_disk_space <= 0)
                    hwInfo.total_disk_space = 0;
            }

            return await Task.FromResult(hwInfo);
        }

        /// <summary>
        /// Thu thập thông tin hệ điều hành.
        /// </summary>
        private string GetOsInfo()
        {
            try
            {
                string osInfo = string.Empty;

                using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem"))
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
                _logger.LogError(ex, "Lỗi khi thu thập thông tin hệ điều hành");
                return "Không thể thu thập thông tin hệ điều hành";
            }
        }

        /// <summary>
        /// Thu thập thông tin CPU.
        /// </summary>
        private string GetCpuInfo()
        {
            try
            {
                string cpuInfo = string.Empty;

                using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
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
                _logger.LogError(ex, "Lỗi khi thu thập thông tin CPU");
                return "Không thể thu thập thông tin CPU";
            }
        }

        /// <summary>
        /// Thu thập thông tin GPU.
        /// </summary>
        private string GetGpuInfo()
        {
            try
            {
                string gpuInfo = string.Empty;

                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
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

                    // Xóa dấu phẩy cuối cùng nếu có
                    if (gpuInfo.EndsWith("; "))
                    {
                        gpuInfo = gpuInfo.Substring(0, gpuInfo.Length - 2);
                    }
                }

                return string.IsNullOrEmpty(gpuInfo) ? "Không thể thu thập thông tin GPU" : gpuInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin GPU");
                return "Không thể thu thập thông tin GPU";
            }
        }

        /// <summary>
        /// Thu thập thông tin tổng RAM.
        /// </summary>
        private long GetTotalRam()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
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
                _logger.LogError(ex, "Lỗi khi thu thập thông tin tổng RAM");
                return 0;
            }
        }

        /// <summary>
        /// Thu thập thông tin tổng dung lượng ổ đĩa chính.
        /// </summary>
        private long GetTotalDiskSpace()
        {
            try
            {
                DriveInfo driveC = new DriveInfo("C:");
                return driveC.TotalSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập thông tin ổ đĩa");
                return 0;
            }
        }
    }
}
