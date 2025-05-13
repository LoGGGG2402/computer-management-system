namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Thông tin trạng thái tài nguyên của máy client gửi lên server.
    /// </summary>
    public class StatusUpdatePayload
    {
        /// <summary>
        /// Phần trăm sử dụng CPU.
        /// </summary>
        public double cpuUsage { get; set; }

        /// <summary>
        /// Phần trăm sử dụng RAM.
        /// </summary>
        public double ramUsage { get; set; }

        /// <summary>
        /// Phần trăm sử dụng ổ đĩa chính.
        /// </summary>
        public double diskUsage { get; set; }
    }
}