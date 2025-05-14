using System;
using System.ComponentModel.DataAnnotations;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Thông tin về các lỗi phát sinh trong agent gửi lên server.
    /// </summary>
    public class ErrorReportPayload
    {
        /// <summary>
        /// Phân loại lỗi.
        /// </summary>
        public ErrorType error_type { get; set; }

        /// <summary>
        /// Thông điệp lỗi.
        /// </summary>
        public required string error_message { get; set; } = string.Empty;

        /// <summary>
        /// Chi tiết lỗi (có thể là string hoặc object phức tạp).
        /// </summary>
        public required object error_details { get; set; } = new();

        /// <summary>
        /// Thời điểm xảy ra lỗi.
        /// </summary>
        public DateTime timestamp { get; set; }
    }

    /// <summary>
    /// Thông tin phần cứng của máy client gửi lên server.
    /// </summary>
    public class HardwareInfoPayload
    {
        /// <summary>
        /// Thông tin hệ điều hành.
        /// </summary>
        public required string os_info { get; set; } = string.Empty;

        /// <summary>
        /// Thông tin CPU.
        /// </summary>
        public required string cpu_info { get; set; } = string.Empty;

        /// <summary>
        /// Thông tin GPU.
        /// </summary>
        public required string gpu_info { get; set; } = string.Empty;

        /// <summary>
        /// Tổng RAM (bytes).
        /// </summary>
        public long total_ram { get; set; }

        /// <summary>
        /// Tổng dung lượng ổ C: (bytes).
        /// </summary>
        [Required]
        public long total_disk_space { get; set; }
    }

    /// <summary>
    /// Payload cho việc tải lên log của agent.
    /// </summary>
    public class LogUploadPayload
    {
        /// <summary>
        /// Tên file log được tải lên.
        /// </summary>
        public required string log_filename { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung file log được mã hóa base64.
        /// </summary>
        public required string log_content_base64 { get; set; } = string.Empty;
    }
} 