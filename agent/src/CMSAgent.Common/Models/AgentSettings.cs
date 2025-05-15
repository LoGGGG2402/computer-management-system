using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Main configuration of CMSAgent stored in appsettings.json
    /// </summary>
    public class CmsAgentSettingsOptions
    {
        /// <summary>
        /// Application name, used for service name and default data directory
        /// </summary>
        [Required]
        public string AppName { get; set; } = "CMSAgent";

        /// <summary>
        /// URL of the server
        /// </summary>
        [Required]
        [Url]
        public required string ServerUrl { get; set; }

        /// <summary>
        /// Version of the agent
        /// </summary>
        [Required]
        public required string Version { get; set; }

        /// <summary>
        /// Specific configuration for agent
        /// </summary>
        public AgentSpecificSettingsOptions AgentSettings { get; set; } = new();

        /// <summary>
        /// HttpClient configuration
        /// </summary>
        public HttpClientSettingsOptions HttpClientSettings { get; set; } = new();

    }

    /// <summary>
    /// Specific configuration for Agent
    /// </summary>
    public class AgentSpecificSettingsOptions
    {
        /// <summary>
        /// Interval (seconds) for sending status reports to server
        /// </summary>
        [Range(1, 3600)]
        public int StatusReportIntervalSec { get; set; } = 30;

        /// <summary>
        /// Enable/disable auto-update feature
        /// </summary>
        public bool EnableAutoUpdate { get; set; } = true;

        /// <summary>
        /// Interval (seconds) between update checks
        /// </summary>
        [Range(60, 86400 * 7)]
        public int AutoUpdateIntervalSec { get; set; } = 86400;

        /// <summary>
        /// Maximum number of retries for network errors
        /// </summary>
        [Range(1, 10)]
        public int NetworkRetryMaxAttempts { get; set; } = 5;

        /// <summary>
        /// Initial delay (seconds) before retrying network connection
        /// </summary>
        [Range(1, 60)]
        public int NetworkRetryInitialDelaySec { get; set; } = 5;

        /// <summary>
        /// Interval (seconds) between token refreshes
        /// </summary>
        [Range(3600, 86400 * 7)]
        public int TokenRefreshIntervalSec { get; set; } = 86400;
    }

    /// <summary>
    /// Configuration for command executor
    /// </summary>
    public class CommandExecutorSettingsOptions
    {
        /// <summary>
        /// Default timeout (seconds) for command execution
        /// </summary>
        [Range(30, 3600)]
        public int DefaultTimeoutSec { get; set; } = 300;

        /// <summary>
        /// Maximum number of commands that can be executed simultaneously
        /// </summary>
        [Range(1, 10)]
        public int MaxParallelCommands { get; set; } = 2;

        /// <summary>
        /// Maximum size of command queue
        /// </summary>
        [Range(10, 1000)]
        public int MaxQueueSize { get; set; } = 100;

        /// <summary>
        /// Character encoding used for console output
        /// </summary>
        [Required]
        public string ConsoleEncoding { get; set; } = "utf-8";
    }
}