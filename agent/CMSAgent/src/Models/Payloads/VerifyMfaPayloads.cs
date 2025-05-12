namespace CMSAgent.Models.Payloads
{
    /// <summary>
    /// MFA verification request payload sent to the server
    /// </summary>
    public class VerifyMfaRequestPayload
    {
        /// <summary>
        /// Unique ID of the agent
        /// </summary>
        [JsonPropertyName("unique_agent_id")]
        public string UniqueAgentId { get; set; } = string.Empty;

        /// <summary>
        /// MFA verification code
        /// </summary>
        [JsonPropertyName("mfaCode")]
        public string MfaCode { get; set; } = string.Empty;

        // Standard.md VI.A.2 (POST /verify-mfa) does not list positionInfo or roomInfo as part of the request.
        // Remove if it was added based on a different interpretation.
        // public PositionInfoPayload? PositionInfo { get; set; }
    }

    /// <summary>
    /// MFA verification response payload received from the server
    /// </summary>
    public class VerifyMfaResponsePayload
    {
        /// <summary>
        /// Status of the MFA verification
        /// </summary>
        public string status { get; set; } = string.Empty;

        /// <summary>
        /// Agent authentication token
        /// </summary>
        public string? agentToken { get; set; }

        /// <summary>
        /// Message
        /// </summary>
        public string? message { get; set; }
    }
}