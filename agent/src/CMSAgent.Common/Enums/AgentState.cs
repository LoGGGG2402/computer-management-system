namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Định nghĩa các trạng thái của agent.
    /// </summary>
    public enum AgentState
    {
        /// <summary>
        /// Agent khởi động, đang tải cấu hình và các module ban đầu.
        /// </summary>
        INITIALIZING,

        /// <summary>
        /// Đang trong quá trình kết nối và xác thực WebSocket với server.
        /// </summary>
        AUTHENTICATING,

        /// <summary>
        /// Đã kết nối và xác thực thành công với server, hoạt động bình thường.
        /// </summary>
        CONNECTED,

        /// <summary>
        /// Mất kết nối với server, đang trong quá trình thử kết nối lại tự động.
        /// </summary>
        DISCONNECTED,

        /// <summary>
        /// Đang trong quá trình tải xuống và chuẩn bị cho việc cập nhật phiên bản mới.
        /// </summary>
        UPDATING,

        /// <summary>
        /// Gặp lỗi nghiêm trọng không thể phục hồi (ví dụ: cấu hình hỏng), không thể hoạt động.
        /// </summary>
        ERROR,

        /// <summary>
        /// Đang trong quá trình dừng hoạt động một cách an toàn (ví dụ: khi SCM yêu cầu).
        /// </summary>
        STOPPING
    }
}