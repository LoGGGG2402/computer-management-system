namespace CMSAgent.Models.Payloads
{
    /// <summary>
    /// Command request payload received from the server, aligned with Standard.md
    /// </summary>
    public class CommandRequest
    {
        /// <summary>
        /// Unique ID of the command
        /// </summary>
        public string commandId { get; set; } = string.Empty;

        /// <summary>
        /// The command string to be executed (for console type) or a descriptor for system type.
        /// </summary>
        public string command { get; set; } = string.Empty;

        /// <summary>
        /// Type of command to execute (e.g., "console", "system_restart", "system_info")
        /// </summary>
        public string commandType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Command result payload sent to the server, aligned with Standard.md
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// ID of the agent processing the command
        /// </summary>
        public string agentId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the command that was executed
        /// </summary>
        public string commandId { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the command execution was successful
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// The type of command that was executed (mirrors CommandRequest.commandType)
        /// </summary>
        public string type { get; set; } = string.Empty;

        /// <summary>
        /// Result data, structure depends on the command type
        /// </summary>
        public Dictionary<string, object> result { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Private constructor to enforce usage of factory methods.
        /// </summary>
        private CommandResult() { }

        /// <summary>
        /// Creates a result for a console command.
        /// </summary>
        public static CommandResult CreateConsoleResult(string agentId, string commandId, string stdout, string stderr, int exitCode)
        {
            return new CommandResult
            {
                agentId = agentId,
                commandId = commandId,
                success = exitCode == 0, // Success is typically determined by exit code 0
                type = "console",
                result = new Dictionary<string, object>
                {
                    { "stdout", stdout },
                    { "stderr", stderr },
                    { "exitCode", exitCode }
                }
            };
        }

        /// <summary>
        /// Creates a success result for a system command.
        /// </summary>
        public static CommandResult CreateSystemSuccessResult(string agentId, string commandId, string systemCommandType, Dictionary<string, object> data)
        {
            return new CommandResult
            {
                agentId = agentId,
                commandId = commandId,
                success = true,
                type = systemCommandType,
                result = data ?? new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Creates a failure result for a system command.
        /// </summary>
        public static CommandResult CreateSystemFailureResult(string agentId, string commandId, string systemCommandType, string errorMessage, Dictionary<string, object>? additionalData = null)
        {
            var resultData = additionalData ?? new Dictionary<string, object>();
            if (!resultData.ContainsKey("error_message")) // Ensure error_message is added if not present
            {
                resultData["error_message"] = errorMessage;
            }
            
            return new CommandResult
            {
                agentId = agentId,
                commandId = commandId,
                success = false,
                type = systemCommandType,
                result = resultData
            };
        }

        /// <summary>
        /// Creates a timeout result for any command type.
        /// </summary>
        public static CommandResult CreateTimeoutResult(string agentId, string commandId, string commandType)
        {
            return new CommandResult
            {
                agentId = agentId,
                commandId = commandId,
                success = false,
                type = commandType,
                result = new Dictionary<string, object> { { "error_message", "Command execution timed out" } }
            };
        }
    }

    // Received from server via WebSocket event "command:execute"
    public class CommandToExecute
    {
        [JsonPropertyName("commandId")]
        public string CommandId { get; set; } = string.Empty;

        [JsonPropertyName("commandType")] // As per Standard.md VI.B
        public string CommandType { get; set; } = "console"; // Default to console as per typical use

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public List<string>? Parameters { get; set; } // Optional parameters for the command

        [JsonPropertyName("timeoutSeconds")]
        public int? TimeoutSeconds { get; set; }
    }

    // Sent to server via WebSocket event "agent:command_result"
    public class CommandResultPayload
    {
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty; // device_id

        [JsonPropertyName("commandId")]
        public string CommandId { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("type")] // Should reflect the CommandType received
        public string Type { get; set; } = "console"; 

        [JsonPropertyName("result")]
        public ExecutionResult Result { get; set; } = new ExecutionResult();
    }

    public class ExecutionResult
    {
        [JsonPropertyName("stdout")]
        public string? Stdout { get; set; }

        [JsonPropertyName("stderr")]
        public string? Stderr { get; set; }

        [JsonPropertyName("exitCode")]
        public int? ExitCode { get; set; }
    }
}