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

        /// <summary>
        /// Run initial configuration process for Agent.
        /// Called when Agent runs with "configure" parameter.
        /// </summary>
        /// <returns>True if configuration is successful, False otherwise.</returns>
        Task<bool> RunInitialConfigurationAsync(CancellationToken cancellationToken = default);
    }
}
