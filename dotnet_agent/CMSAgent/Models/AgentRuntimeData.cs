using System;
using Newtonsoft.Json; // Switched to Newtonsoft.Json for consistency with ApiPayloads

namespace CMSAgent.Models
{
    /// <summary>
    /// Represents runtime data for the agent, such as device ID and authentication tokens.
    /// This data is typically acquired or generated during the agent's initialization and operation.
    /// Corresponds to data elements used in agent_standard.md, particularly for identification and communication.
    /// </summary>
    public class AgentRuntimeData
    {
        /// <summary>
        /// Gets or sets the unique identifier for the device where the agent is running.
        /// Corresponds to "device_id" in agent_standard.md.
        /// </summary>
        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the authentication token used by the agent to connect to the server (e.g., for WebSocket).
        /// Corresponds to "agent_token" in agent_standard.md.
        /// </summary>
        [JsonProperty("agent_token")]
        public string? AgentToken { get; set; }

        /// <summary>
        /// Gets or sets the room configuration for the agent, if applicable.
        /// Corresponds to "room_config" (which maps to "positionInfo" in /identify request) in agent_standard.md.
        /// </summary>
        [JsonProperty("room_config")]
        public RoomPosition? RoomConfig { get; set; }

        // Removed other dynamic state properties if they are not strictly part of agent_standard.md
        // or if their persistence is handled differently (e.g., in-memory or derived).
        // For example, agent_version is typically from assembly, not dynamic state to be persisted here.
        // LastConnectionTime, CurrentAgentState (enum) are runtime concerns, not persisted state as per standard.
    }

    /// <summary>
    /// Represents the position or configuration of an agent within a room or logical grouping.
    /// Used as part of the <see cref="AgentRuntimeData"/>.
    /// Structure should align with "positionInfo" in the /identify request.
    /// </summary>
    public class RoomPosition
    {
        /// <summary>
        /// Gets or sets the name of the room.
        /// Corresponds to "roomName" in agent_standard.md.
        /// </summary>
        [JsonProperty("roomName")]
        public string? RoomName { get; set; }

        /// <summary>
        /// Gets or sets the X-coordinate position within the room.
        /// Corresponds to "posX" in agent_standard.md.
        /// </summary>
        [JsonProperty("posX")]
        public string? PosX { get; set; } // Kept as string to match standard's example

        /// <summary>
        /// Gets or sets the Y-coordinate position within the room.
        /// Corresponds to "posY" in agent_standard.md.
        /// </summary>
        [JsonProperty("posY")]
        public string? PosY { get; set; } // Kept as string to match standard's example
    }
}
