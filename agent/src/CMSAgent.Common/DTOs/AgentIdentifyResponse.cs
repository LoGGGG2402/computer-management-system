namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Phản hồi từ server khi agent định danh.
    /// </summary>
    public class AgentIdentifyResponse
    {
        /// <summary>
        /// Trạng thái của yêu cầu: "success", "mfa_required", "position_error", "error".
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// ID của agent (chỉ được trả về khi thành công).
        /// </summary>
        public string agentId { get; set; }

        /// <summary>
        /// Token xác thực (chỉ được trả về khi thành công và token được làm mới).
        /// </summary>
        public string agentToken { get; set; }

        /// <summary>
        /// Thông báo lỗi hoặc thông tin bổ sung.
        /// </summary>
        public string message { get; set; }
    }
}
