namespace CMSAgent.Configuration
{
    /// <summary>
    /// Represents the static configuration of the agent
    /// This configuration is stored in agent_config.json
    /// </summary>
    public class AgentConfigPoco
    {
        /// <summary>
        /// The name of the application
        /// </summary>
        public string app_name { get; set; } = "CMSAgent";

        /// <summary>
        /// The version of the agent
        /// </summary>
        public string version { get; set; } = "1.0.0";

        /// <summary>
        /// The URL of the server
        /// </summary>
        public string server_url { get; set; } = "http://localhost:3000";

        /// <summary>
        /// Agent settings
        /// </summary>
        public AgentSettings agent_settings { get; set; }

        /// <summary>
        /// HTTP client settings
        /// </summary>
        public HttpClientSettings http_client_settings { get; set; }

        /// <summary>
        /// WebSocket settings
        /// </summary>
        public WebsocketSettings websocket_settings { get; set; }

        /// <summary>
        /// Command executor settings
        /// </summary>
        public CommandExecutorSettings command_executor_settings { get; set; }

        /// <summary>
        /// Update settings
        /// </summary>
        public UpdateSettings update_settings { get; set; }

        public AgentConfigPoco()
        {
            agent_settings = new AgentSettings();
            http_client_settings = new HttpClientSettings();
            websocket_settings = new WebsocketSettings();
            command_executor_settings = new CommandExecutorSettings();
            update_settings = new UpdateSettings();
        }
    }

    /// <summary>
    /// Agent settings
    /// </summary>
    public class AgentSettings
    {
        /// <summary>
        /// Interval between status reports in seconds
        /// </summary>
        public int status_report_interval_sec { get; set; } = 60;

        /// <summary>
        /// Whether to enable automatic updates
        /// </summary>
        public bool enable_auto_update { get; set; } = true;

        /// <summary>
        /// Interval between update checks in seconds
        /// </summary>
        public int auto_update_interval_sec { get; set; } = 3600;
    }

    /// <summary>
    /// WebSocket settings
    /// </summary>
    public class WebsocketSettings
    {
        /// <summary>
        /// Initial delay for reconnection in seconds
        /// </summary>
        public int reconnect_delay_initial_sec { get; set; } = 5;

        /// <summary>
        /// Maximum delay for reconnection in seconds
        /// </summary>
        public int reconnect_delay_max_sec { get; set; } = 120;

        /// <summary>
        /// Maximum number of reconnection attempts (null for unlimited)
        /// </summary>
        public int? reconnect_attempts_max { get; set; } = null;
    }

    /// <summary>
    /// HTTP client settings
    /// </summary>
    public class HttpClientSettings
    {
        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int request_timeout_sec { get; set; } = 30;
    }

    /// <summary>
    /// Command executor settings
    /// </summary>
    public class CommandExecutorSettings
    {
        /// <summary>
        /// Default timeout for commands in seconds
        /// </summary>
        public int default_timeout_sec { get; set; } = 300;

        /// <summary>
        /// Maximum number of parallel commands
        /// </summary>
        public int max_parallel_commands { get; set; } = 2;

        /// <summary>
        /// Maximum size of the command queue
        /// </summary>
        public int max_queue_size { get; set; } = 100;

        /// <summary>
        /// Encoding for console output
        /// </summary>
        public string console_encoding { get; set; } = "utf-8";

        /// <summary>
        /// Timeout for console commands in seconds
        /// </summary>
        public int console_command_timeout_seconds { get; set; } = 60;
    }

    /// <summary>
    /// Update settings
    /// </summary>
    public class UpdateSettings
    {
        /// <summary>
        /// Default download directory for updates
        /// </summary>
        public string download_directory { get; set; } = "C:\\ProgramData\\CMSAgent\\Updates";

        /// <summary>
        /// Optional override for update check URL
        /// </summary>
        public string update_check_url_override { get; set; } = string.Empty;
    }
}