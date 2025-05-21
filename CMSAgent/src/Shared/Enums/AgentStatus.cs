namespace CMSAgent.Shared.Enums
{
    /// <summary>
    /// Defines possible operational statuses of the Agent.
    /// These statuses can be used to monitor and report the Agent's state.
    /// </summary>
    public enum AgentStatus
    {
        /// <summary>
        /// Agent is initializing.
        /// </summary>
        Initializing,

        /// <summary>
        /// Agent is attempting to connect to the Server (WebSocket).
        /// </summary>
        Connecting,

        /// <summary>
        /// Agent has successfully connected and authenticated with the Server.
        /// This is the normal operational state.
        /// </summary>
        Connected,

        /// <summary>
        /// Agent lost connection to the Server and is attempting to reconnect.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Agent is performing a self-update.
        /// </summary>
        Updating,

        /// <summary>
        /// Agent is in the process of shutting down gracefully.
        /// </summary>
        Stopping,

        /// <summary>
        /// Agent has stopped operating.
        /// </summary>
        Stopped,

        /// <summary>
        /// Agent encountered a critical unrecoverable error and has stopped.
        /// </summary>
        Error,

        /// <summary>
        /// Agent is in the process of initial configuration (e.g., running 'configure' command).
        /// </summary>
        Configuring
    }
}
