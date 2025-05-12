namespace CMSAgent.Models.Payloads
{
    /// <summary>
    /// Represents the positionInfo object to be included in the Identify request
    /// </summary>
    public class PositionInfoPayload
    {
        /// <summary>
        /// Name of the room
        /// </summary>
        [JsonPropertyName("roomName")]
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// X position in the room
        /// </summary>
        [JsonPropertyName("posX")]
        public int PosX { get; set; } // Standard.md implies server expects number after CLI input

        /// <summary>
        /// Y position in the room
        /// </summary>
        [JsonPropertyName("posY")]
        public int PosY { get; set; } // Standard.md implies server expects number after CLI input
    }

    /// <summary>
    /// Agent identify request payload sent to the server
    /// </summary>
    public class IdentifyRequestPayload
    {
        /// <summary>
        /// Unique ID of the agent
        /// </summary>
        [JsonPropertyName("unique_agent_id")]
        public string UniqueAgentId { get; set; } = string.Empty;

        /// <summary>
        /// Position information
        /// </summary>
        [JsonPropertyName("positionInfo")]
        public PositionInfoPayload? PositionInfo { get; set; } // Nullable if identify can be called without position in other contexts

        /// <summary>
        /// Whether to force renew the token
        /// </summary>
        [JsonPropertyName("forceRenewToken")]
        public bool ForceRenewToken { get; set; } = false;
    }

    /// <summary>
    /// Agent identify response payload received from the server
    /// </summary>
    public class IdentifyResponse
    {
        /// <summary>
        /// Status of the identify request
        /// </summary>
        public string status { get; set; } = string.Empty;

        /// <summary>
        /// Agent authentication token
        /// </summary>
        public string? agentToken { get; set; }

        /// <summary>
        /// Agent display name
        /// </summary>
        public string? displayName { get; set; }

        /// <summary>
        /// MFA information if MFA is required
        /// </summary>
        public MfaInfo? mfaInfo { get; set; }

        /// <summary>
        /// Message
        /// </summary>
        public string? message { get; set; }
    }

    /// <summary>
    /// MFA information
    /// </summary>
    public class MfaInfo
    {
        /// <summary>
        /// MFA request ID
        /// </summary>
        public string requestId { get; set; } = string.Empty;

        /// <summary>
        /// MFA type
        /// </summary>
        public string type { get; set; } = string.Empty;

        /// <summary>
        /// MFA prompt
        /// </summary>
        public string prompt { get; set; } = string.Empty;
    }
}