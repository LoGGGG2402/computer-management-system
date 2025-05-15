namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Exit codes specific to CMSUpdater.exe.
    /// </summary>
    public enum UpdaterExitCodes
    {
        /// <summary>
        /// Update completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Error: Cannot stop the old agent.
        /// </summary>
        StopAgentFailed = 10,

        /// <summary>
        /// Error: Failed to backup the old agent.
        /// </summary>
        BackupFailed = 11,

        /// <summary>
        /// Error: Failed to deploy the new agent.
        /// </summary>
        DeployFailed = 12,

        /// <summary>
        /// Error: Failed to start the new agent service.
        /// </summary>
        NewServiceStartFailed = 13,

        /// <summary>
        /// Error: Rollback failed.
        /// </summary>
        RollbackFailed = 14,

        /// <summary>
        /// Command line argument error.
        /// </summary>
        InvalidArguments = 15,

        /// <summary>
        /// Error: Timeout waiting for old agent to stop.
        /// </summary>
        AgentStopTimeout = 16,

        /// <summary>
        /// Watchdog detected new agent instability and triggered rollback.
        /// </summary>
        WatchdogTriggeredRollback = 17,

        /// <summary>
        /// General unspecified Updater error.
        /// </summary>
        GeneralError = 99
    }
}
