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
        public string status { get; set; }

        /// <summary>
        /// Có phiên bản mới hay không.
        /// </summary>
        public bool update_available { get; set; }

        /// <summary>
        /// Phiên bản mới (nếu có).
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// URL để tải gói cập nhật.
        /// </summary>
        public string download_url { get; set; }

        /// <summary>
        /// Checksum SHA256 của gói cập nhật.
        /// </summary>
        public string checksum_sha256 { get; set; }

        /// <summary>
        /// Ghi chú về phiên bản mới.
        /// </summary>
        public string notes { get; set; }
    }
}