using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Shared.Enums;

namespace CMSAgent.Service.Orchestration
{
    /// <summary>
    /// Interface defining main methods to orchestrate Agent's activities.
    /// </summary>
    public interface IAgentCoreOrchestrator
    {
        /// <summary>
        /// Start main Agent activities, including server connection,
        /// launching monitoring modules, command processing, and update checks.
        /// This method will run until a stop signal is received.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel activities.</param>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Safely stop all Agent activities.
        /// </summary>
        /// <param name="cancellationToken">Token to limit stop time.</param>
        Task StopAsync(CancellationToken cancellationToken);
    }
}