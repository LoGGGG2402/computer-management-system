namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Các trạng thái của quá trình cập nhật agent.
    /// </summary>
    public enum UpdateStatus
    {
        /// <summary>
        /// Quá trình cập nhật bắt đầu.
        /// </summary>
        UPDATE_STARTED,

        /// <summary>
        /// Gói cập nhật đã được tải về.
        /// </summary>
        UPDATE_DOWNLOADED,

        /// <summary>
        /// Gói cập nhật đã được giải nén thành công.
        /// </summary>
        UPDATE_EXTRACTED,

        /// <summary>
        /// Tiến trình CMSUpdater đã được khởi chạy.
        /// </summary>
        UPDATER_LAUNCHED,

        /// <summary>
        /// Cập nhật thành công.
        /// </summary>
        UPDATE_SUCCESS,

        /// <summary>
        /// Cập nhật thất bại.
        /// </summary>
        UPDATE_FAILED
    }
}