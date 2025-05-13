namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Mã lỗi cho các lệnh CLI của CMSAgent.exe.
    /// </summary>
    public enum CliExitCodes
    {
        /// <summary>
        /// Thành công.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Lỗi chung.
        /// </summary>
        GeneralError = 1,

        /// <summary>
        /// Thiếu quyền (Administrator).
        /// </summary>
        MissingPermissions = 2,

        /// <summary>
        /// Người dùng hủy thao tác.
        /// </summary>
        UserCancelled = 3,

        /// <summary>
        /// Lỗi kết nối hoặc xác thực với server.
        /// </summary>
        ServerConnectionFailed = 4,

        /// <summary>
        /// Lỗi lưu file cấu hình.
        /// </summary>
        ConfigSaveFailed = 5,

        /// <summary>
        /// Lỗi dừng/gỡ bỏ service.
        /// </summary>
        ServiceOperationFailed = 6,

        /// <summary>
        /// Service không được cài đặt.
        /// </summary>
        ServiceNotInstalled = 7,

        /// <summary>
        /// Tham số không hợp lệ.
        /// </summary>
        InvalidArguments = 8,

        /// <summary>
        /// Mã cơ sở cho lỗi của CMSUpdater.
        /// Các mã lỗi của updater sẽ được cộng thêm vào giá trị này.
        /// </summary>
        UpdaterErrorBase = 10
    }
}
