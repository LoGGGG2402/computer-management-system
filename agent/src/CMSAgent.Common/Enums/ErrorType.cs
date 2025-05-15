namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Error types for reporting to server.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// Failed to download update package.
        /// </summary>
        UPDATE_DOWNLOAD_FAILED,

        /// <summary>
        /// Update package checksum verification failed.
        /// </summary>
        UPDATE_CHECKSUM_MISMATCH,

        /// <summary>
        /// Overall update process failure.
        /// </summary>
        UpdateFailure,

        /// <summary>
        /// Pre-update backup failure.
        /// </summary>
        BackupFailure,

        /// <summary>
        /// New version deployment failure.
        /// </summary>
        DeploymentFailure,

        /// <summary>
        /// Rollback failure after update failure.
        /// </summary>
        RollbackFailure,

        /// <summary>
        /// Windows Service operation failure.
        /// </summary>
        ServiceOperationFailure,

        /// <summary>
        /// Service instability after update.
        /// </summary>
        ServiceInstability
    }
}