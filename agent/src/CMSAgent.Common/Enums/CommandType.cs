using System.Text.Json.Serialization;

namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Types of commands that the agent can receive and execute.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CommandType
    {
        /// <summary>
        /// Console execution command (cmd.exe or PowerShell).
        /// </summary>
        CONSOLE,

        /// <summary>
        /// System action command (e.g., restart, shutdown).
        /// </summary>
        SYSTEM_ACTION,
    }
}