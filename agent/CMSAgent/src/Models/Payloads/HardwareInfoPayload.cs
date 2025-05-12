namespace CMSAgent.Models.Payloads
{
    /// <summary>
    /// Hardware information payload sent to the server
    /// </summary>
    public class HardwareInfoPayload
    {
        /// <summary>
        /// Operating system information
        /// </summary>
        [JsonPropertyName("os_info")]
        public string? OsInfo { get; set; }

        /// <summary>
        /// CPU information
        /// </summary>
        [JsonPropertyName("cpu_info")]
        public string? CpuInfo { get; set; }

        /// <summary>
        /// GPU information
        /// </summary>
        [JsonPropertyName("gpu_info")]
        public string? GpuInfo { get; set; }

        /// <summary>
        /// Total RAM in bytes
        /// </summary>
        [JsonPropertyName("total_ram")]
        public ulong? TotalRam { get; set; }

        /// <summary>
        /// Total disk space in bytes
        /// </summary>
        [JsonPropertyName("total_disk_space")]
        public ulong TotalDiskSpace { get; set; }
    }

    /// <summary>
    /// Error report payload sent to the server
    /// </summary>
    public class ErrorReport
    {
        /// <summary>
        /// Type of error
        /// </summary>
        public string error_type { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string error_message { get; set; } = string.Empty;

        /// <summary>
        /// Error details (stack trace etc.)
        /// </summary>
        public string error_details { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the error
        /// </summary>
        public DateTime timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Context information related to the error
        /// </summary>
        public Dictionary<string, string> context { get; set; } = new Dictionary<string, string>();
    }
}