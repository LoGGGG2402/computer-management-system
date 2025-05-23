// CMSAgent.Service/Configuration/Models/AppSettings.cs
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Service.Configuration.Models
{
    /// <summary>
    /// Model representing settings in appsettings.json file.
    /// Properties here will be bound from the configuration file.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Management Server URL (e.g., "https://cms.example.com").
        /// </summary>
        [Required(ErrorMessage = "ServerUrl is required.")]
        [Url(ErrorMessage = "ServerUrl must be a valid URL.")]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Root path for HTTP APIs on Server (e.g., "/api").
        /// </summary>
        public string ApiPath { get; set; } = "/api"; // Default value

        /// <summary>
        /// Current version of Agent.
        /// This value can be automatically updated by CMSUpdater.
        /// </summary>
        public string Version { get; set; } = "0.0.0";

        /// <summary>
        /// Time interval (seconds) for sending resource status reports.
        /// Default: 60 seconds.
        /// </summary>
        [Range(10, 3600, ErrorMessage = "StatusReportIntervalSec must be between 10 and 3600.")]
        public int StatusReportIntervalSec { get; set; } = 60;

        /// <summary>
        /// Time interval (seconds) for checking automatic updates.
        /// Default: 3600 seconds (1 hour).
        /// </summary>
        [Range(300, 86400, ErrorMessage = "AutoUpdateIntervalSec must be between 300 and 86400.")]
        public int AutoUpdateIntervalSec { get; set; } = 3600;

        /// <summary>
        /// Enable/disable automatic update check feature.
        /// Default: true.
        /// </summary>
        public bool EnableAutoUpdate { get; set; } = true;
        /// <summary>
        /// Configuration for HTTP retry policy (Polly).
        /// </summary>
        public HttpRetryPolicySettings HttpRetryPolicy { get; set; } = new HttpRetryPolicySettings();

        /// <summary>
        /// Configuration for WebSocket connection.
        /// </summary>
        public WebSocketSettings WebSocketPolicy { get; set; } = new WebSocketSettings();

        /// <summary>
        /// Configuration for command execution.
        /// </summary>
        public CommandExecutionSettings CommandExecution { get; set; } = new CommandExecutionSettings();

        /// <summary>
        /// Unique GUID of Agent, used to create Mutex name.
        /// This value should be randomly generated and unique for each installation.
        /// It can be written to appsettings.json during installation.
        /// </summary>
        public string AgentInstanceGuid { get; set; } = string.Empty; // Will be generated during installation
    }
    /// <summary>
    /// Configuration for HTTP retry policy.
    /// </summary>
    public class HttpRetryPolicySettings
    {
        /// <summary>
        /// Maximum number of retry attempts.
        /// </summary>
        [Range(0, 10)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Initial delay (seconds) before retry.
        /// Will increase exponentially for subsequent retries.
        /// </summary>
        [Range(1, 60)]
        public int InitialDelaySeconds { get; set; } = 2;
    }

    /// <summary>
    /// Configuration for WebSocket connection.
    /// </summary>
    public class WebSocketSettings
    {
        /// <summary>
        /// Timeout (seconds) when attempting WebSocket connection.
        /// </summary>
        [Range(5, 120)]
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Minimum wait time (seconds) in exponential backoff strategy when reconnecting.
        /// </summary>
        [Range(1, 60)]
        public int ReconnectMinBackoffSeconds { get; set; } = 5;

        /// <summary>
        /// Maximum wait time (seconds) in exponential backoff strategy.
        /// </summary>
        [Range(60, 600)]
        public int ReconnectMaxBackoffSeconds { get; set; } = 300;

        /// <summary>
        /// Maximum number of reconnection attempts (-1 means infinite).
        /// </summary>
        [Range(-1, 100)]
        public int MaxReconnectAttempts { get; set; } = -1; // Infinite
    }

    /// <summary>
    /// Configuration for command execution.
    /// </summary>
    public class CommandExecutionSettings
    {
        /// <summary>
        /// Maximum size of command queue.
        /// </summary>
        [Range(1, 100)]
        public int MaxQueueSize { get; set; } = 10;

        /// <summary>
        /// Maximum number of commands that can run in parallel.
        /// </summary>
        [Range(1, 10)]
        public int MaxParallelCommands { get; set; } = 1; // Default to sequential execution

        /// <summary>
        /// Default timeout (seconds) for a command.
        /// </summary>
        [Range(10, 3600)]
        public int DefaultCommandTimeoutSeconds { get; set; } = 60;
    }
}
