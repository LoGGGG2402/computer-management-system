namespace CMSAgent.Core
{
    /// <summary>
    /// Represents the current state of the agent
    /// </summary>
    public enum AgentState
    {
        /// <summary>
        /// Agent has not been started yet
        /// </summary>
        NotStarted,
        
        /// <summary>
        /// Agent is in the process of starting
        /// </summary>
        Starting,
        
        /// <summary>
        /// Agent is running normally
        /// </summary>
        Running,
        
        /// <summary>
        /// Agent is in the process of stopping
        /// </summary>
        Stopping,
        
        /// <summary>
        /// Agent has stopped
        /// </summary>
        Stopped,
        
        /// <summary>
        /// Agent encountered an error
        /// </summary>
        Error
    }
}