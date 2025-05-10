using System.Text.Json.Serialization;

namespace CMSAgent.Models
{
    /// <summary>
    /// Defines settings related to the agent's core behavior.
    /// Corresponds to "agent" section in agent_config.json.
    /// </summary>
    public class AgentSettings
    {
        /// <summary>
        /// Gets or sets the interval in seconds for reporting agent status to the server.
        /// </summary>
        [JsonPropertyName("status_report_interval_sec")]
        public int StatusReportIntervalSec { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic updates for the agent are enabled.
        /// </summary>
        [JsonPropertyName("enable_auto_update")]
        public bool EnableAutoUpdate { get; set; }

        /// <summary>
        /// Gets or sets the interval in seconds for checking for agent updates.
        /// </summary>
        [JsonPropertyName("auto_update_interval_sec")]
        public int AutoUpdateIntervalSec { get; set; }
    }

    /// <summary>
    /// Defines settings for the HTTP client used by the agent.
    /// Corresponds to "http_client" section in agent_config.json.
    /// </summary>
    public class HttpClientSettings
    {
        /// <summary>
        /// Gets or sets the timeout in seconds for HTTP requests.
        /// </summary>
        [JsonPropertyName("request_timeout_sec")]
        public int RequestTimeoutSec { get; set; }
    }

    /// <summary>
    /// Defines settings for the WebSocket client used by the agent for real-time communication.
    /// Corresponds to "websocket" section in agent_config.json.
    /// </summary>
    public class WebSocketSettings
    {
        /// <summary>
        /// Gets or sets the initial delay in seconds before attempting to reconnect after a WebSocket disconnection.
        /// </summary>
        [JsonPropertyName("reconnect_delay_initial_sec")]
        public int ReconnectDelayInitialSec { get; set; }

        /// <summary>
        /// Gets or sets the maximum delay in seconds between WebSocket reconnection attempts.
        /// </summary>
        [JsonPropertyName("reconnect_delay_max_sec")]
        public int ReconnectDelayMaxSec { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of reconnection attempts for the WebSocket connection. Null for unlimited attempts.
        /// </summary>
        [JsonPropertyName("reconnect_attempts_max")]
        public int? ReconnectAttemptsMax { get; set; }
    }

    /// <summary>
    /// Defines settings for the command executor component of the agent.
    /// Corresponds to "command_executor" section in agent_config.json.
    /// </summary>
    public class CommandExecutorSettings
    {
        /// <summary>
        /// Gets or sets the default timeout in seconds for executing commands.
        /// </summary>
        [JsonPropertyName("default_timeout_sec")]
        public int DefaultTimeoutSec { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of commands that can be executed in parallel.
        /// </summary>
        [JsonPropertyName("max_parallel_commands")]
        public int MaxParallelCommands { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of the command queue.
        /// </summary>
        [JsonPropertyName("max_queue_size")]
        public int MaxQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the console encoding to be used when executing commands (e.g., "utf-8", "SHIFT-JIS").
        /// </summary>
        [JsonPropertyName("console_encoding")]
        public string ConsoleEncoding { get; set; } = "utf-8"; // Default to utf-8 as per standard
    }

    /// <summary>
    /// Represents the agent's static configuration, typically loaded from agent_config.json.
    /// This class structure is based on agent_standard.md: Phần 2, 1. Cấu Hình Tĩnh.
    /// </summary>
    public class AgentConfig
    {
        /// <summary>
        /// Gets or sets the URL of the CMS server.
        /// </summary>
        [JsonPropertyName("server_url")]
        public required string ServerUrl { get; set; }

        /// <summary>
        /// Gets or sets the agent-specific settings.
        /// </summary>
        [JsonPropertyName("agent")]
        public required AgentSettings Agent { get; set; }

        /// <summary>
        /// Gets or sets the HTTP client settings.
        /// </summary>
        [JsonPropertyName("http_client")]
        public required HttpClientSettings HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the WebSocket client settings.
        /// </summary>
        [JsonPropertyName("websocket")]
        public required WebSocketSettings Websocket { get; set; }

        /// <summary>
        /// Gets or sets the command executor settings.
        /// </summary>
        [JsonPropertyName("command_executor")]
        public required CommandExecutorSettings CommandExecutor { get; set; }
    }
}
