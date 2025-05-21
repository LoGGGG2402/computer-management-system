using CMSAgent.Shared.Constants;
namespace CMSUpdater
{
    /// <summary>
    /// Model containing configuration parameters passed to CMSUpdater.exe via command line.
    /// Reference: CMSAgent_Doc.md sections 8.2.6 and 8.3.1.
    /// </summary>
    public class UpdaterConfig
    {
        /// <summary>
        /// Process ID of the running CMSAgent.Service that needs to be stopped.
        /// </summary>
        /// <example>Command line parameter: -pid 1234</example>
        public int CurrentAgentPid { get; set; } = -1;

        /// <summary>
        /// Version string of the new Agent to be installed.
        /// </summary>
        /// <example>Command line parameter: -new-version "1.1.0"</example>
        public string NewAgentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Version string of the old Agent (version running before update).
        /// Used for naming the backup directory.
        /// </summary>
        /// <example>Command line parameter: -old-version "1.0.0"</example>
        public string OldAgentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Path to the directory containing the extracted files of the new Agent version.
        /// </summary>
        /// <example>
        /// Path example: C:\ProgramData\CMSAgent\updates\extracted\1.1.0\
        /// Command line parameter: -source-path "C:\ProgramData\CMSAgent\updates\extracted\1.1.0"
        /// </example>
        public string NewAgentExtractedPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the main installation directory of CMSAgent.Service.
        /// </summary>
        /// <example>
        /// Path example: C:\Program Files\CMSAgent
        /// Command line parameter: -install-dir "C:\Program Files\CMSAgent"
        /// </example>
        public string AgentInstallDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Path to the root directory storing Agent data in ProgramData.
        /// </summary>
        /// <example>
        /// Path example: C:\ProgramData\CMSAgent
        /// Command line parameter: -data-dir "C:\ProgramData\CMSAgent"
        /// </example>
        public string AgentProgramDataDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Timeout duration (seconds) to wait for old Agent to stop or new Agent to start.
        /// If not provided, will use default from AgentConstants.DefaultProcessWaitForExitTimeoutSeconds.
        /// </summary>
        /// <remarks>Will be null if not provided</remarks>
        public int? ServiceWaitTimeoutSeconds { get; set; }

        /// <summary>
        /// Duration (seconds) for CMSUpdater to monitor the new Agent after startup (watchdog).
        /// If not provided, will use default value of 120 seconds (or add to AgentConstants if needed).
        /// </summary>
        /// <remarks>Will be null if not provided</remarks>
        public int? NewAgentWatchdogPeriodSeconds { get; set; }

        /// <summary>
        /// Gets the backup directory path for the old version.
        /// </summary>
        public string BackupDirectoryForOldVersion => Path.Combine(AgentProgramDataDirectory,
                                                               AgentConstants.UpdatesSubFolderName,
                                                               AgentConstants.UpdateBackupSubFolderName,
                                                               OldAgentVersion);
    }
}
