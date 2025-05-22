namespace CMSAgent.Shared.Constants
{
    /// <summary>
    /// Contains constants used throughout the Agent solution.
    /// </summary>
    public static class AgentConstants
    {
        // --- Mutex Name ---
        /// <summary>
        /// Global Mutex name to ensure only one instance of the Agent Service runs.
        /// A unique GUID should be appended to this name during actual deployment.
        /// Example: "Global\\CMSAgentSingletonMutex_A1B2C3D4E5F6"
        /// </summary>
        public const string AgentServiceMutexNamePrefix = "Global\\CMSAgentSingletonMutex_";
        // Note: The actual GUID should be generated and appended at build or installation time.
        // Example: public static readonly string AgentServiceMutexName = $"{AgentServiceMutexNamePrefix}{GetAssemblyGuid()}";
        // Or a fixed GUID defined in advance.

        // --- Service Name ---
        /// <summary>
        /// The service name of CMSAgent to be registered in Windows Services.
        /// This name is used by both the Agent Service and the Updater to identify and control the service.
        /// </summary>
        public const string ServiceName = "CMSAgent";

        /// <summary>
        /// The display name of CMSAgent Service in Windows Services.
        /// </summary>
        public const string ServiceDisplayName = "Computer Management System Agent";

        /// <summary>
        /// The description of CMSAgent Service.
        /// </summary>
        public const string ServiceDescription = "Agent collects system information and executes tasks for the Computer Management System.";

        // --- Configuration Folders and Files ---
        /// <summary>
        /// The root folder name for Agent data storage in ProgramData.
        /// </summary>
        public const string AgentProgramDataFolderName = "CMSAgent";

        /// <summary>
        /// Subfolder name for agent and updater logs.
        /// </summary>
        public const string LogsSubFolderName = "logs";

        /// <summary>
        /// Subfolder name for runtime configuration file (runtime_config.json).
        /// </summary>
        public const string RuntimeConfigSubFolderName = "runtime_config";

        /// <summary>
        /// Runtime configuration file name.
        /// </summary>
        public const string RuntimeConfigFileName = "runtime_config.json";

        /// <summary>
        /// Subfolder name for update-related files.
        /// </summary>
        public const string UpdatesSubFolderName = "updates";
        public const string UpdateDownloadSubFolderName = "download";
        public const string UpdateExtractedSubFolderName = "extracted";
        public const string UpdateBackupSubFolderName = "backup";

        /// <summary>
        /// Subfolder name for detailed error reports.
        /// </summary>
        public const string ErrorReportsSubFolderName = "error_reports";

        /// <summary>
        /// File name for storing skipped update versions.
        /// </summary>
        public const string IgnoredVersionsFileName = "ignored_versions.json";

        // --- Client Info for HTTP Header ---
        /// <summary>
        /// Value for X-Client-Type header when Agent communicates with Server.
        /// </summary>
        public const string HttpClientTypeAgent = "agent";

        // --- Standard error types for update reports ---
        // (Refer to agent_api.md and CMSAgent_Doc.md)
        public const string UpdateErrorTypeDownloadFailed = "DownloadFailed";
        public const string UpdateErrorTypeChecksumMismatch = "ChecksumMismatch";
        public const string UpdateErrorTypeExtractionFailed = "ExtractionFailed";
        public const string UpdateErrorTypeUpdateLaunchFailed = "UpdateLaunchFailed";
        public const string UpdateErrorTypeStartAgentFailed = "StartAgentFailed";
        public const string UpdateErrorTypeUpdateGeneralFailure = "UpdateGeneralFailure";

        // --- Log file date format ---
        public const string LogFileDateFormat = "yyyyMMdd";
        public const string UpdaterLogFileDateTimeFormat = "yyyyMMdd_HHmmss";
        public const string AgentLogFilePrefix = "agent_";
        public const string UpdaterLogFilePrefix = "updater_";

        // --- Other constants ---
        /// <summary>
        /// Default timeout (seconds) for waiting for a process to exit.
        /// </summary>
        public const int DefaultProcessWaitForExitTimeoutSeconds = 60;

        /// <summary>
        /// Default timeout (seconds) for waiting for process output/error streams to close.
        /// </summary>
        public const int DefaultProcessStreamCloseTimeoutSeconds = 5;

        /// <summary>
        /// Default period (seconds) for monitoring new agent service stability after update.
        /// </summary>
        public const int DefaultNewAgentWatchdogPeriodSeconds = 120;

        /// <summary>
        /// Exit codes for command execution results.
        /// </summary>
        public static class CommandExitCodes
        {
            /// <summary>
            /// Command execution timed out
            /// </summary>
            public const int Timeout = -1;

            /// <summary>
            /// Command execution was cancelled
            /// </summary>
            public const int Cancelled = -2;

            /// <summary>
            /// General unexpected error during command execution
            /// </summary>
            public const int GeneralError = -99;
        }
    }
}
