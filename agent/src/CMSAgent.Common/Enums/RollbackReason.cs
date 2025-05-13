namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Lý do rollback cho CMSUpdater.
    /// </summary>
    public enum RollbackReason
    {
        /// <summary>
        /// Cài đặt các tệp của phiên bản mới thất bại.
        /// </summary>
        UpdateDeploymentFailed,

        /// <summary>
        /// Khởi động service của phiên bản mới thất bại.
        /// </summary>
        NewServiceStartFailed,

        /// <summary>
        /// Service của phiên bản mới không ổn định (crash liên tục).
        /// </summary>
        NewServiceUnstable
    }
}
