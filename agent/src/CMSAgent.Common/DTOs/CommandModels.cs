using System.Collections.Generic;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Command information sent from server to agent.
    /// </summary>
    public class CommandPayload
    {
        /// <summary>
        /// Unique command ID.
        /// </summary>
        public required string commandId { get; set; }

        /// <summary>
        /// Command content to execute.
        /// </summary>
        public required string command { get; set; }

        /// <summary>
        /// Command type (console, system_action, get_logs).
        /// </summary>
        public CommandType commandType { get; set; }

        /// <summary>
        /// Additional parameters for the command (optional).
        /// </summary>
        public Dictionary<string, object> parameters { get; set; } = new();
    }

    /// <summary>
    /// Command execution result payload sent from agent to server.
    /// </summary>
    public class CommandResultPayload
    {
        /// <summary>
        /// ID of the executed command.
        /// </summary>
        public required string commandId { get; set; }

        /// <summary>
        /// Command execution status: true if successful, false if failed.
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// Type of executed command.
        /// </summary>
        public CommandType type { get; set; }

        /// <summary>
        /// Execution result data.
        /// </summary>
        public required CommandResultData result { get; set; } = new()
        {
            stdout = string.Empty,
            stderr = string.Empty,
            errorMessage = string.Empty,
            errorCode = string.Empty
        };
    }

    /// <summary>
    /// Command execution result data.
    /// </summary>
    public class CommandResultData
    {
        /// <summary>
        /// Standard output of the command.
        /// </summary>
        public required string stdout { get; set; } = string.Empty;

        /// <summary>
        /// Standard error output of the command.
        /// </summary>
        public required string stderr { get; set; } = string.Empty;

        /// <summary>
        /// Command exit code (if any).
        /// </summary>
        public int? exitCode { get; set; }

        /// <summary>
        /// Error message (if any).
        /// </summary>
        public required string errorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Error code (if any).
        /// </summary>
        public required string errorCode { get; set; } = string.Empty;
    }
}