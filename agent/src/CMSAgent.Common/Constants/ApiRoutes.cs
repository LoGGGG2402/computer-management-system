namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// Đường dẫn các API endpoint từ server trung tâm.
    /// </summary>
    public static class ApiRoutes
    {
        /// <summary>
        /// Đường dẫn API để đăng ký agent mới hoặc định danh agent đã tồn tại.
        /// </summary>
        public const string Identify = "/api/agent/identify";

        /// <summary>
        /// Đường dẫn API để xác thực MFA cho agent.
        /// </summary>
        public const string VerifyMfa = "/api/agent/verify-mfa";

        /// <summary>
        /// Đường dẫn API để gửi thông tin phần cứng của máy client lên server.
        /// </summary>
        public const string HardwareInfo = "/api/agent/hardware-info";

        /// <summary>
        /// Đường dẫn API để kiểm tra có phiên bản agent mới hay không.
        /// </summary>
        public const string CheckUpdate = "/api/agent/check-update";

        /// <summary>
        /// Đường dẫn API để báo cáo lỗi phát sinh trong agent lên server.
        /// </summary>
        public const string ReportError = "/api/agent/report-error";
    }
}