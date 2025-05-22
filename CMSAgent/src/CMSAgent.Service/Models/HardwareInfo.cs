// CMSAgent.Service/Models/HardwareInfo.cs
using System.Text.Json.Serialization;
// No need for using System.Collections.Generic; if no List is used

namespace CMSAgent.Service.Models
{
    /// <summary>
    /// Model containing hardware information of the client machine, adjusted to match the API.
    /// Sent to Server via API /api/agent/hardware-info.
    /// Reference: CMSAgent_Doc.md section 5.1 and agent_api.md.
    /// </summary>
    public class HardwareInfo
    {
        /// <summary>
        /// Operating system information as a summary string.
        /// Example: "Windows 10 Pro 64-bit, Version 22H2, Build 19045.2006"
        /// </summary>
        [JsonPropertyName("os_info")]
        public string? OsInfo { get; set; } // API spec says it's "string"

        /// <summary>
        /// CPU information as a summary string.
        /// Example: "Intel(R) Core(TM) i7-8700 CPU @ 3.20GHz, 6 Cores, 12 Threads, 3192 MHz"
        /// </summary>
        [JsonPropertyName("cpu_info")]
        public string? CpuInfo { get; set; } // API spec says it's "string"

        /// <summary>
        /// GPU information as a summary string.
        /// Example: "NVIDIA GeForce RTX 3070, VRAM: 8192 MB, Driver: 30.0.15.1234"
        /// If multiple GPUs exist, can concatenate strings or only take main GPU info.
        /// </summary>
        [JsonPropertyName("gpu_info")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GpuInfo { get; set; } // API spec says it's "string"

        /// <summary>
        /// Total physical RAM capacity (MB).
        /// API spec: "total_ram": "integer"
        /// </summary>
        [JsonPropertyName("total_ram")] // Changed JSON property name
        public long TotalRamMb { get; set; }

        /// <summary>
        /// Total disk space (usually C: drive or main system drive) in MB.
        /// API spec: "total_disk_space": "integer (required)"
        /// </summary>
        [JsonPropertyName("total_disk_space")]
        public long TotalDiskSpaceMb { get; set; } // Changed from List<DiskDriveInfo> to a single long value

        // OsInfo, CpuInfo, GpuInfo, DiskDriveInfo classes have been removed
        // because API only requires summary strings or simple values.
        // Logic to create these summary strings will be in HardwareCollector.cs.
    }
}
