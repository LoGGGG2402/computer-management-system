namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Exit codes for CMSAgent.exe CLI commands.
    /// </summary>
    public enum CliExitCodes
    {
        /// <summary>
        /// Operation successful.
        /// </summary>
        Success = 0,

        /// <summary>
        /// General error.
        /// </summary>
        GeneralError = 1,

        /// <summary>
        /// Missing permissions (Administrator).
        /// </summary>
        MissingPermissions = 2,

        /// <summary>
        /// Operation cancelled by user.
        /// </summary>
        UserCancelled = 3,

        /// <summary>
        /// Server connection or authentication failed.
        /// </summary>
        ServerConnectionFailed = 4,

        /// <summary>
        /// Failed to save configuration file.
        /// </summary>
        ConfigSaveFailed = 5,

        /// <summary>
        /// Service stop/uninstall operation failed.
        /// </summary>
        ServiceOperationFailed = 6,

        /// <summary>
        /// Service is not installed.
        /// </summary>
        ServiceNotInstalled = 7,

        /// <summary>
        /// Invalid input.
        /// </summary>
        InvalidInput = 9,
    }
}
