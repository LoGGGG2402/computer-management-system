// CMSAgent.Service/Commands/Models/CommandResult.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Commands.Models
{
    /// <summary>
    /// Standard error codes for the Agent.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// No error.
        /// </summary>
        NONE,

        /// <summary>
        /// Unknown error.
        /// </summary>
        UNKNOWN_ERROR,

        /// <summary>
        /// Error during command execution.
        /// </summary>
        COMMAND_EXECUTION_ERROR,

        /// <summary>
        /// Error accessing file.
        /// </summary>
        FILE_ACCESS_ERROR,

        /// <summary>
        /// Error accessing registry.
        /// </summary>
        REGISTRY_ACCESS_ERROR,

        /// <summary>
        /// Error during software installation.
        /// </summary>
        SOFTWARE_INSTALL_ERROR,

        /// <summary>
        /// Error during software uninstallation.
        /// </summary>
        SOFTWARE_UNINSTALL_ERROR,

        /// <summary>
        /// Error accessing logs.
        /// </summary>
        LOG_ACCESS_ERROR,

        /// <summary>
        /// Error due to insufficient permissions.
        /// </summary>
        PERMISSION_ERROR,

        /// <summary>
        /// Error due to invalid parameters.
        /// </summary>
        INVALID_PARAMETER,

        /// <summary>
        /// Error when resource not found.
        /// </summary>
        RESOURCE_NOT_FOUND,

        /// <summary>
        /// Error when resource already exists.
        /// </summary>
        RESOURCE_ALREADY_EXISTS,

        /// <summary>
        /// Error due to timeout.
        /// </summary>
        TIMEOUT_ERROR,

        /// <summary>
        /// Network connection error.
        /// </summary>
        NETWORK_ERROR
    }

    /// <summary>
    /// Model representing the result of a command sent from Agent to Server.
    /// Reference: agent_api.md, "agent:command_result" event and CMSAgent_Doc.md section 7.6.
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// ID of the original command that this result corresponds to.
        /// </summary>
        [JsonPropertyName("commandId")]
        public string CommandId { get; set; } = string.Empty;

        /// <summary>
        /// Type of the executed command.
        /// </summary>
        [JsonPropertyName("commandType")]
        public CommandType CommandType { get; set; } = CommandType.UNKNOWN;

        /// <summary>
        /// Indicates whether the command was executed successfully.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Contains the detailed result of the command.
        /// </summary>
        [JsonPropertyName("result")]
        public CommandOutputResult Result { get; set; } = new CommandOutputResult();
    }

    /// <summary>
    /// Detailed output result of a command.
    /// </summary>
    public class CommandOutputResult
    {
        /// <summary>
        /// Standard output of the command (if any).
        /// </summary>
        [JsonPropertyName("stdout")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Stdout { get; set; }

        /// <summary>
        /// Standard error of the command (if any).
        /// </summary>
        [JsonPropertyName("stderr")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Stderr { get; set; }

        /// <summary>
        /// Exit code of the command (if any).
        /// 0 typically means success.
        /// </summary>
        [JsonPropertyName("exitCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ExitCode { get; set; }

        /// <summary>
        /// Error message if success=false.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Internal error code of the Agent.
        /// </summary>
        [JsonPropertyName("errorCode")]
        public ErrorCode ErrorCode { get; set; } = ErrorCode.NONE;

        /// <summary>
        /// Creates a successful CommandOutputResult.
        /// </summary>
        public static CommandOutputResult CreateSuccess(string? stdout = null, string? stderr = null, int exitCode = 0)
        {
            return new CommandOutputResult
            {
                Stdout = stdout,
                Stderr = stderr,
                ExitCode = exitCode,
                ErrorCode = ErrorCode.NONE
            };
        }

        /// <summary>
        /// Creates a failed CommandOutputResult.
        /// </summary>
        public static CommandOutputResult CreateError(ErrorCode errorCode, string errorMessage, string? stderr = null, int exitCode = -1)
        {
            return new CommandOutputResult
            {
                Stderr = stderr,
                ExitCode = exitCode,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}
