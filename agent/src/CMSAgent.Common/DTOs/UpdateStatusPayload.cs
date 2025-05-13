using System;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Payload trạng thái cập nhật gửi từ agent lên server.
    /// </summary>
    public class UpdateStatusPayload
    {
        /// <summary>
        /// Trạng thái của quá trình cập nhật.
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// Thông điệp mô tả trạng thái cập nhật.
        /// </summary>
        public string message { get; set; }

        /// <summary>
        /// Thời gian tạo trạng thái cập nhật.
        /// </summary>
        public DateTime timestamp { get; set; }
    }
}
