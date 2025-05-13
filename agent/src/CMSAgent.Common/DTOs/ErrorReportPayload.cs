using System;
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
        public string error_message { get; set; }

        /// <summary>
        /// Chi tiết lỗi (có thể là string hoặc object phức tạp).
        /// </summary>
        public object error_details { get; set; }

        /// <summary>
        /// Thời điểm xảy ra lỗi.
        /// </summary>
        public DateTime timestamp { get; set; }
    }
}