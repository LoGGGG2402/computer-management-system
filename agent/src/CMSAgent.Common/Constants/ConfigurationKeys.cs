namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// Tên các key trong file cấu hình của CMSAgent.
    /// </summary>
    public static class ConfigurationKeys
    {
        /// <summary>
        /// Section chính cho cấu hình CMSAgent trong appsettings.json.
        /// </summary>
        public const string CmsAgentSettingsSection = "CMSAgentSettings";

        /// <summary>
        /// Key để lấy URL server từ cấu hình.
        /// </summary>
        public const string ServerUrlKey = $"{CmsAgentSettingsSection}:ServerUrl";

        /// <summary>
        /// Section con chứa cấu hình agent cụ thể.
        /// </summary>
        public const string AgentSettingsSection = $"{CmsAgentSettingsSection}:AgentSettings";

        /// <summary>
        /// Key để lấy khoảng thời gian gửi báo cáo trạng thái.
        /// </summary>
        public const string StatusReportIntervalKey = $"{AgentSettingsSection}:StatusReportIntervalSec";

        /// <summary>
        /// Key để lấy cài đặt kích hoạt tự động cập nhật.
        /// </summary>
        public const string EnableAutoUpdateKey = $"{AgentSettingsSection}:EnableAutoUpdate";

        /// <summary>
        /// Key để lấy khoảng thời gian kiểm tra cập nhật.
        /// </summary>
        public const string AutoUpdateIntervalKey = $"{AgentSettingsSection}:AutoUpdateIntervalSec";

        /// <summary>
        /// Key để lấy số lần thử lại kết nối mạng tối đa.
        /// </summary>
        public const string NetworkRetryMaxAttemptsKey = $"{AgentSettingsSection}:NetworkRetryMaxAttempts";

        /// <summary>
        /// Key để lấy thời gian chờ ban đầu khi thử lại kết nối mạng.
        /// </summary>
        public const string NetworkRetryInitialDelayKey = $"{AgentSettingsSection}:NetworkRetryInitialDelaySec";

        /// <summary>
        /// Key để lấy khoảng thời gian làm mới token.
        /// </summary>
        public const string TokenRefreshIntervalKey = $"{AgentSettingsSection}:TokenRefreshIntervalSec";

        /// <summary>
        /// Section cho cấu hình HttpClient.
        /// </summary>
        public const string HttpClientSettingsSection = $"{CmsAgentSettingsSection}:HttpClientSettings";

        /// <summary>
        /// Section cho cấu hình WebSocket.
        /// </summary>
        public const string WebSocketSettingsSection = $"{CmsAgentSettingsSection}:WebSocketSettings";

        /// <summary>
        /// Section cho cấu hình CommandExecutor.
        /// </summary>
        public const string CommandExecutorSettingsSection = $"{CmsAgentSettingsSection}:CommandExecutorSettings";

        /// <summary>
        /// Section cho cấu hình giới hạn tài nguyên.
        /// </summary>
        public const string ResourceLimitsSection = $"{CmsAgentSettingsSection}:ResourceLimits";

        /// <summary>
        /// Section cho cấu hình queue offline.
        /// </summary>
        public const string OfflineQueueSection = $"{AgentSettingsSection}:OfflineQueue";
    }
}
