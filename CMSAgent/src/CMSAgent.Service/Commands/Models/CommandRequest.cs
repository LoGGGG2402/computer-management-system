// CMSAgent.Service/Commands/Models/CommandRequest.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Commands.Models
{
    /// <summary>
    /// Supported command types by the Agent.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CommandType
    {
        /// <summary>
        /// Command to execute in console.
        /// </summary>
        CONSOLE,

        /// <summary>
        /// Command to perform system actions.
        /// </summary>
        SYSTEM_ACTION,

        /// <summary>
        /// Command to install software.
        /// </summary>
        SOFTWARE_INSTALL,

        /// <summary>
        /// Command to uninstall software.
        /// </summary>
        SOFTWARE_UNINSTALL,

        /// <summary>
        /// Command to retrieve logs.
        /// </summary>
        GET_LOGS,

        /// <summary>
        /// Unknown or unsupported command type.
        /// </summary>
        UNKNOWN
    }

    /// <summary>
    /// Model representing a command request from Server to Agent.
    /// Reference: agent_api.md, "command:execute" event and CMSAgent_Doc.md section 7.1.
    /// </summary>
    public class CommandRequest
    {
        /// <summary>
        /// Unique identifier of the command (UUID string).
        /// </summary>
        [JsonPropertyName("commandId")]
        public string CommandId { get; set; } = string.Empty;

        /// <summary>
        /// Main content of the command.
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Type of the command.
        /// </summary>
        [JsonPropertyName("commandType")]
        public CommandType CommandType { get; set; } = CommandType.CONSOLE;

        /// <summary>
        /// Additional parameters for the command, as a JSON object (dictionary).
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }

        /// <summary>
        /// Converts a string to CommandType enum.
        /// </summary>
        /// <param name="commandType">The command type string to parse.</param>
        /// <returns>The corresponding CommandType enum value, or UNKNOWN if parsing fails.</returns>
        public static CommandType ParseCommandType(string commandType)
        {
            if (string.IsNullOrWhiteSpace(commandType))
                return CommandType.UNKNOWN;

            return Enum.TryParse<CommandType>(commandType.ToUpperInvariant(), out var result)
                ? result
                : CommandType.UNKNOWN;
        }
    }
}
