namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Các loại lỗi để báo cáo lên server.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// Lỗi tải gói cập nhật.
        /// </summary>
        UPDATE_DOWNLOAD_FAILED,

        /// <summary>
        /// Lỗi kiểm tra checksum gói cập nhật.
        /// </summary>
        UPDATE_CHECKSUM_MISMATCH,

        
        /// <summary>
        /// Lỗi quá trình cập nhật tổng thể.
        /// </summary>
        UpdateFailure,
        
        /// <summary>
        /// Lỗi quá trình sao lưu trước khi cập nhật.
        /// </summary>
        BackupFailure,
        
        /// <summary>
        /// Lỗi quá trình triển khai phiên bản mới.
        /// </summary>
        DeploymentFailure,
        
        /// <summary>
        /// Lỗi quá trình rollback sau khi cập nhật thất bại.
        /// </summary>
        RollbackFailure,
        
        /// <summary>
        /// Lỗi thao tác với Windows Service.
        /// </summary>
        ServiceOperationFailure,
        
        /// <summary>
        /// Lỗi do service không ổn định sau khi cập nhật.
        /// </summary>
        ServiceInstability
    }
}