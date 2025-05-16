namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// WebSocket (Socket.IO) event names used in Agent-Server communication.
    /// </summary>
    public static class WebSocketEvents
    {
        /// <summary>
        /// Event sent by server to agent to notify successful WebSocket authentication.
        /// </summary>
        public const string AgentWsAuthSuccess = "agent:ws_auth_success";

        /// <summary>
        /// Event sent by server to agent to notify failed WebSocket authentication.
        /// </summary>
        public const string AgentWsAuthFailed = "agent:ws_auth_failed";

        /// <summary>
        /// Event sent by server to agent to request command execution.
        /// </summary>
        public const string CommandExecute = "command:execute";

        /// <summary>
        /// Event sent by agent to server to report command execution results.
        /// </summary>
        public const string AgentCommandResult = "agent:command_result";

        /// <summary>
        /// Event sent by agent to server to report resource status (CPU, RAM, Disk).
        /// </summary>
        public const string AgentStatusUpdate = "agent:status_update";

        /// <summary>
        /// Event sent by server to agent to notify about new agent version availability.
        /// </summary>
        public const string AgentNewVersionAvailable = "agent:new_version_available";

        /// <summary>
        /// Event sent by agent to server to report update process status.
        /// </summary>
        public const string AgentUpdateStatus = "agent:update_status";

        /// <summary>
        /// Event sent by agent to server to notify connection status.
        /// </summary>
        public const string AgentConnect = "agent:connect";

        /// <summary>
        /// Event sent by agent to server to notify disconnection.
        /// </summary>
        public const string AgentDisconnect = "agent:disconnect";
    }
}