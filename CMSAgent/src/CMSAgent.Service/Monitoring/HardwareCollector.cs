 // CMSAgent.Service/Monitoring/HardwareCollector.cs
using CMSAgent.Service.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management; // Cần thêm package System.Management
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Thu thập thông tin phần cứng chi tiết của máy client.
    /// </summary>
    public class HardwareCollector : IHardwareCollector
    {
        private readonly ILogger<HardwareCollector> _logger;

        public HardwareCollector(ILogger<HardwareCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HardwareInfo?> CollectHardwareInfoAsync()
        {
            _logger.LogInformation("Bắt đầu thu thập thông tin phần cứng.");
            try
            {
                var hardwareInfo = new HardwareInfo
                {
                    OsInfo = await GetOsInfoAsync(),
                    CpuInfo = await GetCpuInfoAsync(),
                    GpuInfo = await GetGpuInfoStringAsync(), // API yêu cầu string
                    TotalRamMb = GetTotalRamMb(),
                    TotalDiskSpaceMb = GetTotalDiskSpaceMb() // API yêu cầu một giá trị tổng
                };

                _logger.LogInformation("Thu thập thông tin phần cứng hoàn tất.");
                return hardwareInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi thu thập thông tin phần cứng.");
                return null;
            }
        }

        private Task<string?> GetOsInfoAsync()
        {
            try
            {
                // Sử dụng Environment và RuntimeInformation để có thông tin cơ bản
                string osDescription = RuntimeInformation.OSDescription; // Ví dụ: Microsoft Windows 10.0.19045
                string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
                string frameworkDescription = RuntimeInformation.FrameworkDescription; // .NET version

                // Để lấy thông tin chi tiết hơn như Edition (Pro, Home), Version (22H2), Build
                // bạn có thể cần truy vấn WMI hoặc Registry.
                // Ví dụ WMI (cần package System.Management):
                string caption = "N/A";
                string version = "N/A"; // Windows version (e.g., 10.0.19045)
                string buildNumber = "N/A";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var mos = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
                    {
                        foreach (var mo in mos.Get().Cast<ManagementObject>())
                        {
                            caption = mo["Caption"]?.ToString()?.Trim() ?? "N/A"; // e.g., Microsoft Windows 10 Pro
                            version = mo["Version"]?.ToString()?.Trim() ?? "N/A";
                            buildNumber = mo["BuildNumber"]?.ToString()?.Trim() ?? "N/A";
                            break;
                        }
                    }
                }
                // API yêu cầu một chuỗi os_info
                return Task.FromResult<string?>($"{caption}, Arch: {osArchitecture}, Version: {version}, Build: {buildNumber}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin hệ điều hành.");
                return Task.FromResult<string?>("Error retrieving OS Info");
            }
        }

        private Task<string?> GetCpuInfoAsync()
        {
            try
            {
                string cpuName = "N/A";
                uint numberOfCores = 0;
                uint numberOfLogicalProcessors = 0;
                uint maxClockSpeed = 0; // MHz

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var mos = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
                    {
                        foreach (var mo in mos.Get().Cast<ManagementObject>())
                        {
                            cpuName = mo["Name"]?.ToString()?.Trim() ?? "N/A";
                            numberOfCores = (uint)(mo["NumberOfCores"] ?? 0u);
                            numberOfLogicalProcessors = (uint)(mo["NumberOfLogicalProcessors"] ?? 0u);
                            maxClockSpeed = (uint)(mo["MaxClockSpeed"] ?? 0u); // Thường là MHz
                            break; // Giả sử chỉ có 1 CPU
                        }
                    }
                }
                else // Fallback cho các OS khác nếu cần
                {
                    cpuName = Environment.ProcessorCount > 0 ? $"Generic CPU ({Environment.ProcessorCount} cores)" : "Generic CPU";
                    numberOfCores = (uint)Environment.ProcessorCount; // Đây là số logical processors
                    numberOfLogicalProcessors = (uint)Environment.ProcessorCount;
                }
                // API yêu cầu một chuỗi cpu_info
                return Task.FromResult<string?>($"{cpuName}, Cores: {numberOfCores}, Threads: {numberOfLogicalProcessors}, Speed: {maxClockSpeed}MHz");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin CPU.");
                return Task.FromResult<string?>("Error retrieving CPU Info");
            }
        }

        private async Task<string?> GetGpuInfoStringAsync()
        {
            var gpuInfos = new List<string>();
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var mos = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion, VideoProcessor FROM Win32_VideoController"))
                    {
                        foreach (var mo in mos.Get().Cast<ManagementObject>())
                        {
                            string name = mo["Name"]?.ToString()?.Trim() ?? "N/A";
                            // AdapterRAM trả về bytes, cần chuyển sang MB. Có thể null.
                            uint? adapterRamMb = mo["AdapterRAM"] != null ? Convert.ToUInt32(mo["AdapterRAM"]) / (1024 * 1024) : (uint?)null;
                            string driverVersion = mo["DriverVersion"]?.ToString()?.Trim() ?? "N/A";
                            string videoProcessor = mo["VideoProcessor"]?.ToString()?.Trim() ?? ""; // Thường chứa tên GPU

                            // Ưu tiên VideoProcessor nếu Name chung chung (vd: "Microsoft Basic Display Adapter")
                            string displayName = name;
                            if (!string.IsNullOrWhiteSpace(videoProcessor) && !videoProcessor.Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                displayName = videoProcessor.Contains(name, StringComparison.OrdinalIgnoreCase) ? videoProcessor : $"{videoProcessor} ({name})";
                            }

                            gpuInfos.Add($"{displayName}, VRAM: {(adapterRamMb.HasValue ? adapterRamMb.Value.ToString() + "MB" : "N/A")}, Driver: {driverVersion}");
                        }
                    }
                }
                if (!gpuInfos.Any())
                {
                    return null; // Hoặc "N/A" nếu API yêu cầu string không null
                }
                // API yêu cầu một chuỗi gpu_info, nếu có nhiều GPU, nối chúng lại
                return string.Join(" | ", gpuInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin GPU.");
                return "Error retrieving GPU Info";
            }
        }


        private long GetTotalRamMb()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    {
                        foreach (var mo in mos.Get().Cast<ManagementObject>())
                        {
                            // TotalVisibleMemorySize là KB, chuyển sang MB
                            return Convert.ToInt64(mo["TotalVisibleMemorySize"]) / 1024;
                        }
                    }
                }
                // Fallback (ít chính xác hơn)
                // Process.GetCurrentProcess().WorkingSet64 có thể không phải là tổng RAM
                // PerformanceCounter có thể dùng nhưng phức tạp hơn cho việc lấy một lần
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tổng dung lượng RAM.");
            }
            return 0; // Hoặc giá trị mặc định khác
        }

        private long GetTotalDiskSpaceMb()
        {
            // API yêu cầu "total_disk_space" là một integer.
            // Tài liệu nói "usually C: drive". Ta sẽ lấy thông tin ổ C:.
            try
            {
                DriveInfo cDrive = DriveInfo.GetDrives().FirstOrDefault(d =>
                    d.DriveType == DriveType.Fixed &&
                    (d.Name.Equals("C:\\", StringComparison.OrdinalIgnoreCase) ||
                     (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && d.Name.Equals(Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase)))
                );

                if (cDrive != null && cDrive.IsReady)
                {
                    return cDrive.TotalSize / (1024 * 1024); // Bytes to MB
                }
                else
                {
                    _logger.LogWarning("Không tìm thấy ổ C: hoặc ổ đĩa hệ thống, hoặc ổ đĩa chưa sẵn sàng.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tổng dung lượng ổ đĩa.");
            }
            return 0; // Hoặc giá trị mặc định khác
        }
    }
}
