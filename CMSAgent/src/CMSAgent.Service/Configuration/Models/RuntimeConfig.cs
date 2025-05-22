// CMSAgent.Service/Configuration/Models/RuntimeConfig.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Configuration.Models
{
    /// <summary>
    /// Model representing settings in runtime_config.json file.
    /// This file contains dynamic configurations that can change during Agent operation.
    /// </summary>
    public class RuntimeConfig
    {
        /// <summary>
        /// Unique ID of Agent (GUID string).
        /// </summary>
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// Machine position information (room, coordinates).
        /// </summary>
        [JsonPropertyName("room_config")]
        public PositionInfo? RoomConfig { get; set; } // Use nullable if it may not be configured yet

        /// <summary>
        /// Agent's authentication token, encrypted using DPAPI.
        /// </summary>
        [JsonPropertyName("agent_token_encrypted")]
        public string? AgentTokenEncrypted { get; set; } // Token can be null if agent is not registered yet
    }
}
