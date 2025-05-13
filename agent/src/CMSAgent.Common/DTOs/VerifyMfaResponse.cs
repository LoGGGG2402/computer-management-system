namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Phản hồi từ server khi xác thực MFA.
    /// </summary>
    public class VerifyMfaResponse
    {
        /// <summary>
        /// Trạng thái của yêu cầu: "success" hoặc "error".
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// ID của agent (chỉ được trả về khi thành công).
        /// </summary>
        public string agentId { get; set; }

        /// <summary>
        /// Token xác thực (chỉ được trả về khi thành công).
        /// </summary>
        public string agentToken { get; set; }

        /// <summary>
        /// Thông báo lỗi hoặc thông tin bổ sung.
        /// </summary>
        public string message { get; set; }
    }
}
