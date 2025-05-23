using CMSAgent.Service.Models;
using System.Management; 
using System.Runtime.InteropServices;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Collect detailed hardware information of the client machine.
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
            _logger.LogInformation("Starting hardware information collection.");
            try
            {
                var hardwareInfo = new HardwareInfo
                {
                    OsInfo = await GetOsInfoAsync(),
                    CpuInfo = await GetCpuInfoAsync(),
                    GpuInfo = await GetGpuInfoStringAsync(),
                    TotalRam = GetTotalRamBytes(),
                    TotalDiskSpace = GetTotalDiskSpaceBytes()
                };

                _logger.LogInformation("Hardware information collection completed.");
                return hardwareInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error while collecting hardware information.");
                return null;
            }
        }

        private Task<string?> GetOsInfoAsync()
        {
            try
            {
                // Use Environment and RuntimeInformation for basic information
                string osDescription = RuntimeInformation.OSDescription; // Example: Microsoft Windows 10.0.19045
                string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
                string frameworkDescription = RuntimeInformation.FrameworkDescription; // .NET version

                // To get more detailed information like Edition (Pro, Home), Version (22H2), Build
                // you may need to query WMI or Registry.
                // Example WMI (requires System.Management package):
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
                // API requires a string for os_info
                return Task.FromResult<string?>($"{caption}, Arch: {osArchitecture}, Version: {version}, Build: {buildNumber}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting operating system information.");
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
                            maxClockSpeed = (uint)(mo["MaxClockSpeed"] ?? 0u); // Usually in MHz
                            break; // Assume only 1 CPU
                        }
                    }
                }
                else // Fallback for other OS if needed
                {
                    cpuName = Environment.ProcessorCount > 0 ? $"Generic CPU ({Environment.ProcessorCount} cores)" : "Generic CPU";
                    numberOfCores = (uint)Environment.ProcessorCount; // This is number of logical processors
                    numberOfLogicalProcessors = (uint)Environment.ProcessorCount;
                }
                // API requires a string for cpu_info
                return Task.FromResult<string?>($"{cpuName}, Cores: {numberOfCores}, Threads: {numberOfLogicalProcessors}, Speed: {maxClockSpeed}MHz");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU information.");
                return Task.FromResult<string?>("Error retrieving CPU Info");
            }
        }

        private Task<string?> GetGpuInfoStringAsync()
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
                            ulong? adapterRamBytes = mo["AdapterRAM"] != null ? Convert.ToUInt64(mo["AdapterRAM"]) : (ulong?)null;
                            string driverVersion = mo["DriverVersion"]?.ToString()?.Trim() ?? "N/A";
                            string videoProcessor = mo["VideoProcessor"]?.ToString()?.Trim() ?? "";

                            string displayName = name;
                            if (!string.IsNullOrWhiteSpace(videoProcessor) && !videoProcessor.Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                displayName = videoProcessor.Contains(name, StringComparison.OrdinalIgnoreCase) ? videoProcessor : $"{videoProcessor} ({name})";
                            }

                            gpuInfos.Add($"{displayName}, VRAM: {(adapterRamBytes.HasValue ? adapterRamBytes.Value.ToString() + " bytes" : "N/A")}, Driver: {driverVersion}");
                        }
                    }
                }
                if (!gpuInfos.Any())
                {
                    return Task.FromResult<string?>(null);
                }
                return Task.FromResult<string?>(string.Join(" | ", gpuInfos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GPU information.");
                return Task.FromResult<string?>("Error retrieving GPU Info");
            }
        }

        private long GetTotalRamBytes()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    {
                        foreach (var mo in mos.Get().Cast<ManagementObject>())
                        {
                            // TotalVisibleMemorySize is in KB, convert to bytes
                            return Convert.ToInt64(mo["TotalVisibleMemorySize"]) * 1024;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total RAM capacity.");
            }
            return 0;
        }

        private long GetTotalDiskSpaceBytes()
        {
            try
            {
                DriveInfo? cDrive = DriveInfo.GetDrives().FirstOrDefault(d =>
                    d.DriveType == DriveType.Fixed &&
                    (d.Name.Equals("C:\\", StringComparison.OrdinalIgnoreCase) ||
                     (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && d.Name.Equals(Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase)))
                );

                if (cDrive != null && cDrive.IsReady)
                {
                    return cDrive.TotalSize; // Already in bytes
                }
                else
                {
                    _logger.LogWarning("C: drive or system drive not found, or drive not ready.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total disk space.");
            }
            return 0;
        }
    }
}
