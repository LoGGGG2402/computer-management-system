namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// Tên các sự kiện WebSocket (Socket.IO) được sử dụng trong giao tiếp Agent-Server.
    /// </summary>
    public static class WebSocketEvents
    {
        /// <summary>
        /// Sự kiện server gửi cho agent để thông báo xác thực WebSocket thành công.
        /// </summary>
        public const string AgentWsAuthSuccess = "agent:ws_auth_success";

        /// <summary>
        /// Sự kiện server gửi cho agent để thông báo xác thực WebSocket thất bại.
        /// </summary>
        public const string AgentWsAuthFailed = "agent:ws_auth_failed";

        /// <summary>
        /// Sự kiện server gửi cho agent để yêu cầu thực thi lệnh.
        /// </summary>
        public const string CommandExecute = "command:execute";

        /// <summary>
        /// Sự kiện server gửi cho agent để thông báo có phiên bản agent mới.
        /// </summary>
        public const string AgentNewVersionAvailable = "agent:new_version_available";

        /// <summary>
        /// Sự kiện agent gửi lên server để xác thực WebSocket (dự phòng nếu không dùng header).
        /// </summary>
        public const string AgentAuthenticate = "agent:authenticate";

        /// <summary>
        /// Sự kiện agent gửi lên server để báo cáo trạng thái tài nguyên (CPU, RAM, Disk).
        /// </summary>
        public const string AgentStatusUpdate = "agent:status_update";

        /// <summary>
        /// Sự kiện agent gửi lên server để báo cáo kết quả thực thi lệnh.
        /// </summary>
        public const string AgentCommandResult = "agent:command_result";

        /// <summary>
        /// Sự kiện agent gửi lên server để báo cáo trạng thái quá trình cập nhật.
        /// </summary>
        public const string AgentUpdateStatus = "agent:update_status";
    }
}