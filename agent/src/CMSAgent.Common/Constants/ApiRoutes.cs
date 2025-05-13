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
        public const string Identify = "/identify";

        /// <summary>
        /// Đường dẫn API để xác thực MFA cho agent.
        /// </summary>
        public const string VerifyMfa = "/verify-mfa";

        /// <summary>
        /// Đường dẫn API để gửi thông tin phần cứng của máy client lên server.
        /// </summary>
        public const string HardwareInfo = "/hardware-info";

        /// <summary>
        /// Đường dẫn API để kiểm tra có phiên bản agent mới hay không.
        /// </summary>
        public const string CheckUpdate = "/check-update";

        /// <summary>
        /// Đường dẫn API để báo cáo lỗi phát sinh trong agent lên server.
        /// </summary>
        public const string ReportError = "/report-error";

        /// <summary>
        /// Đường dẫn cơ sở API cho việc tải gói cập nhật agent.
        /// </summary>
        public const string DownloadPackageBase = "/download/agent-packages/";

        /// <summary>
        /// Đường dẫn API để tải lên file log.
        /// </summary>
        public const string LogUpload = "/upload-logs";
    }
}