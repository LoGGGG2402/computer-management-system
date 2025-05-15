using System.Text.Json.Serialization;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Represents the data structure of the runtime_config.json file.
    /// Contains agent identification and location information stored between runs.
    /// </summary>
    public class RuntimeConfig
    {
        /// <summary>
        /// Unique identifier of the agent.
        /// </summary>
        [JsonPropertyName("agentId")]
        public required string AgentId { get; set; }

        /// <summary>
        /// Location configuration of the agent in the room.
        /// </summary>
        [JsonPropertyName("room_config")]
        public required RoomConfig RoomConfig { get; set; }

        /// <summary>
        /// DPAPI encrypted token for server authentication.
        /// </summary>
        [JsonPropertyName("agent_token_encrypted")]
        public required string AgentTokenEncrypted { get; set; }
    }

    /// <summary>
    /// Represents the location configuration of the agent in the room.
    /// </summary>
    public class RoomConfig
    {
        /// <summary>
        /// Name of the room where the agent is located.
        /// </summary>
        [JsonPropertyName("roomName")]
        public required string RoomName { get; set; }

        /// <summary>
        /// X coordinate in the room map.
        /// </summary>
        [JsonPropertyName("posX")]
        public int PosX { get; set; }

        /// <summary>
        /// Y coordinate in the room map.
        /// </summary>
        [JsonPropertyName("posY")]
        public int PosY { get; set; }
    }
}