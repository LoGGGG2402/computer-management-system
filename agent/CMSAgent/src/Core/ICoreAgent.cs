namespace CMSAgent.Core
{
    public interface ICoreAgent
    {
        /// <summary>
        /// Starts the agent and all its components
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the agent gracefully
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Returns a task that completes when the agent has fully stopped
        /// </summary>
        Task WaitForCompletionAsync();
    }
}