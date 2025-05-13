namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Mã lỗi riêng cho CMSUpdater.exe.
    /// </summary>
    public enum UpdaterExitCodes
    {
        /// <summary>
        /// Cập nhật thành công.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Lỗi: Không thể dừng agent cũ.
        /// </summary>
        StopAgentFailed = 10,

        /// <summary>
        /// Lỗi: Sao lưu agent cũ thất bại.
        /// </summary>
        BackupFailed = 11,

        /// <summary>
        /// Lỗi: Triển khai agent mới thất bại.
        /// </summary>
        DeployFailed = 12,

        /// <summary>
        /// Lỗi: Khởi động service agent mới thất bại.
        /// </summary>
        NewServiceStartFailed = 13,

        /// <summary>
        /// Lỗi: Rollback thất bại.
        /// </summary>
        RollbackFailed = 14,

        /// <summary>
        /// Lỗi tham số dòng lệnh.
        /// </summary>
        InvalidArguments = 15,

        /// <summary>
        /// Lỗi: Timeout chờ agent cũ dừng.
        /// </summary>
        AgentStopTimeout = 16,

        /// <summary>
        /// Watchdog phát hiện agent mới không ổn định và trigger rollback.
        /// </summary>
        WatchdogTriggeredRollback = 17,

        /// <summary>Z
        /// Lỗi chung không xác định của Updater.
        /// </summary>
        GeneralError = 99
    }
}
