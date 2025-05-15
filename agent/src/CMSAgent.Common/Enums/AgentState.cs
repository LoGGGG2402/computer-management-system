namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Defines the possible states of the agent.
    /// </summary>
    public enum AgentState
    {
        /// <summary>
        /// Agent is starting up, loading initial configuration and modules.
        /// </summary>
        INITIALIZING,

        /// <summary>
        /// Establishing connection to server.
        /// </summary>
        CONNECTING,

        /// <summary>
        /// Server authentication failed.
        /// </summary>
        AUTHENTICATION_FAILED,

        /// <summary>
        /// Successfully connected and authenticated with server, operating normally.
        /// </summary>
        CONNECTED,

        /// <summary>
        /// Lost connection to server, in automatic reconnection process.
        /// </summary>
        DISCONNECTED,

        /// <summary>
        /// Attempting to reconnect to server after connection loss.
        /// </summary>
        RECONNECTING,

        /// <summary>
        /// In offline mode, not connected to server.
        /// </summary>
        OFFLINE,

        /// <summary>
        /// Downloading and preparing for new version update.
        /// </summary>
        UPDATING,

        /// <summary>
        /// Invalid configuration error.
        /// </summary>
        CONFIGURATION_ERROR,

        /// <summary>
        /// Service is in the process of complete shutdown.
        /// </summary>
        SHUTTING_DOWN
    }
}