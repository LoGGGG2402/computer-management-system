// CMSAgent.Service/Communication/Http/IAgentApiClient.cs
using CMSAgent.Service.Models; // For HardwareInfo, UpdateNotification
using CMSAgent.Service.Configuration.Models; // For PositionInfo
using CMSAgent.Shared.Models; // For AgentErrorReport

namespace CMSAgent.Service.Communication.Http
{
    /// <summary>
    /// Interface defining HTTP communication methods with CMS Server.
    /// </summary>
    public interface IAgentApiClient
    {
        /// <summary>
        /// Updates authentication information to be used for subsequent requests.
        /// </summary>
        /// <param name="agentId">ID of the Agent.</param>
        /// <param name="agentToken">Authentication token of the Agent.</param>
        void SetAuthenticationCredentials(string agentId, string agentToken);

        /// <summary>
        /// Identifies Agent with Server and gets token or requests MFA.
        /// API: POST /api/agent/identify
        /// </summary>
        /// <param name="agentId">ID of the Agent.</param>
        /// <param name="positionInfo">Position information of the Agent.</param>
        /// <param name="forceRenewToken">True if requesting Server to issue a new token (for periodic token renewal).</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>A tuple containing status (e.g., "mfa_required", "success", "position_error") and agentToken (if any).</returns>
        Task<(string Status, string? AgentToken, string? ErrorMessage)> IdentifyAgentAsync(string? agentId, PositionInfo positionInfo, bool forceRenewToken = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies MFA code for Agent.
        /// API: POST /api/agent/verify-mfa
        /// </summary>
        /// <param name="agentId">ID of the Agent.</param>
        /// <param name="mfaCode">MFA code entered by user.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>A tuple containing status ("success" or error) and agentToken (if successful).</returns>
        Task<(string Status, string? AgentToken, string? ErrorMessage)> VerifyMfaAsync(string agentId, string mfaCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends Agent's hardware information to Server.
        /// API: POST /api/agent/hardware-info
        /// </summary>
        /// <param name="hardwareInfo">Object containing hardware information.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>True if sent successfully, False otherwise.</returns>
        Task<bool> ReportHardwareInfoAsync(HardwareInfo hardwareInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends error report from Agent to Server.
        /// API: POST /api/agent/report-error
        /// </summary>
        /// <param name="errorReport">Object containing error information.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>True if sent successfully, False otherwise.</returns>
        Task<bool> ReportErrorAsync(AgentErrorReport errorReport, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks for new update version for Agent.
        /// API: GET /api/agent/check-update?current_version={current_version}
        /// </summary>
        /// <param name="currentVersion">Current version of the Agent.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>UpdateNotification object containing update information if available, or null if none or error.</returns>
        Task<UpdateNotification?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads Agent update package from Server.
        /// API: GET /api/agent/agent-packages/{filename}
        /// </summary>
        /// <param name="filename">Name of the update package file.</param>
        /// <param name="destinationPath">Full path to save the downloaded file.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>True if download and save successful, False otherwise.</returns>
        Task<bool> DownloadAgentPackageAsync(string filename, string destinationPath, CancellationToken cancellationToken = default);
    }
}
