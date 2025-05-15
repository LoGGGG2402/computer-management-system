using System.ComponentModel.DataAnnotations;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Information about errors occurring in the agent sent to the server.
    /// </summary>
    public class ErrorReportPayload
    {
        /// <summary>
        /// Type of error.
        /// </summary>
        public ErrorType error_type { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public required string error_message { get; set; } = string.Empty;

        /// <summary>
        /// Error details (can be string or complex object).
        /// </summary>
        public required object error_details { get; set; } = new();

        /// <summary>
        /// Timestamp when the error occurred.
        /// </summary>
        public DateTime timestamp { get; set; }
    }

    /// <summary>
    /// Hardware information of the client machine sent to the server.
    /// </summary>
    public class HardwareInfoPayload
    {
        /// <summary>
        /// Operating system information.
        /// </summary>
        public required string os_info { get; set; } = string.Empty;

        /// <summary>
        /// CPU information.
        /// </summary>
        public required string cpu_info { get; set; } = string.Empty;

        /// <summary>
        /// GPU information.
        /// </summary>
        public required string gpu_info { get; set; } = string.Empty;

        /// <summary>
        /// Total RAM (bytes).
        /// </summary>
        public long total_ram { get; set; }

        /// <summary>
        /// Total C: drive space (bytes).
        /// </summary>
        [Required]
        public long total_disk_space { get; set; }
    }
}