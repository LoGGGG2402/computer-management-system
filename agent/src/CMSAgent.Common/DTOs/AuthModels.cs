using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// MFA verification request for agent.
    /// </summary>
    public class VerifyMfaRequest
    {
        /// <summary>
        /// ID of the agent to verify.
        /// </summary>
        [Required]
        public required string agentId { get; set; }

        /// <summary>
        /// MFA code provided by the user.
        /// </summary>
        [Required]
        public required string mfaCode { get; set; }
    }

    /// <summary>
    /// Server response for MFA verification.
    /// </summary>
    public class VerifyMfaResponse
    {
        /// <summary>
        /// Request status: "success" or "error".
        /// </summary>
        public required string status { get; set; } = string.Empty;

        /// <summary>
        /// Authentication token (returned only on success).
        /// </summary>
        public required string agentToken { get; set; } = string.Empty;

        /// <summary>
        /// Error message or additional information.
        /// </summary>
        public required string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to register new agent or identify existing agent with server.
    /// </summary>
    public class AgentIdentifyRequest
    {
        /// <summary>
        /// Unique device ID of the agent.
        /// </summary>
        [Required]
        public required string agentId { get; set; }

        /// <summary>
        /// Position information of the agent.
        /// </summary>
        [Required]
        public required PositionInfo positionInfo { get; set; }

        /// <summary>
        /// If true, request server to issue new token even if agent has valid token.
        /// </summary>
        public bool forceRenewToken { get; set; } = false;
    }

    /// <summary>
    /// Server response for agent identification.
    /// </summary>
    public class AgentIdentifyResponse
    {
        /// <summary>
        /// Request status: "success", "mfa_required", "position_error", "error".
        /// </summary>
        public required string status { get; set; } = string.Empty;

        /// <summary>
        /// Agent ID (returned only on success).
        /// </summary>
        public required string agentId { get; set; } = string.Empty;

        /// <summary>
        /// Authentication token (returned only on success and token renewal).
        /// </summary>
        public required string agentToken { get; set; } = string.Empty;

        /// <summary>
        /// Error message or additional information.
        /// </summary>
        public required string message { get; set; } = string.Empty;
    }
}