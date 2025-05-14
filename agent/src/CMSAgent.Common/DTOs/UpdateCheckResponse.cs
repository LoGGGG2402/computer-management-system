namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Phản hồi từ server khi kiểm tra cập nhật.
    /// </summary>
    public class UpdateCheckResponse
    {
        /// <summary>
        /// Trạng thái của yêu cầu: "success".
        /// </summary>
        public required string status { get; set; } = string.Empty;

        /// <summary>
        /// Có phiên bản mới hay không.
        /// </summary>
        public bool update_available { get; set; }

        /// <summary>
        /// Phiên bản mới (nếu có).
        /// </summary>
        public required string version { get; set; } = string.Empty;

        /// <summary>
        /// URL để tải gói cập nhật.
        /// </summary>
        public required string download_url { get; set; } = string.Empty;

        /// <summary>
        /// Checksum SHA256 của gói cập nhật.
        /// </summary>
        public required string checksum_sha256 { get; set; } = string.Empty;

        /// <summary>
        /// Ghi chú về phiên bản mới.
        /// </summary>
        public required string notes { get; set; } = string.Empty;
    }
}