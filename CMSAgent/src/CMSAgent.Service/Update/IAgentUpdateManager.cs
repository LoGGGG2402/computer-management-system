// CMSAgent.Service/Update/IAgentUpdateManager.cs
using CMSAgent.Service.Models; // For UpdateNotification
using System.Threading.Tasks;
using System.Threading; // For CancellationToken

namespace CMSAgent.Service.Update
{
    /// <summary>
    /// Interface defining methods to manage Agent's update process.
    /// </summary>
    public interface IAgentUpdateManager
    {
        /// <summary>
        /// Check if there is a new update from the server and if so, initiate the update process.
        /// This method can be called periodically.
        /// </summary>
        /// <param name="currentAgentVersion">Current version of the Agent.</param>
        /// <param name="cancellationToken">Token to cancel the process.</param>
        Task UpdateAndInitiateAsync(string currentAgentVersion, CancellationToken cancellationToken = default);

        /// <summary>
        /// Process update notification received from server (e.g., via WebSocket).
        /// </summary>
        /// <param name="updateNotification">Detailed information about the update.</param>
        /// <param name="cancellationToken">Token to cancel the process.</param>
        Task ProcessUpdateNotificationAsync(UpdateNotification updateNotification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if an update is currently in progress.
        /// </summary>
        bool IsUpdateInProgress { get; }
    }
}
