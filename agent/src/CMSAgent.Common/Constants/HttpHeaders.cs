namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// HTTP header constants used in agent-server communication.
    /// </summary>
    public static class HttpHeaders
    {
        /// <summary>
        /// Header containing the agent's identifier.
        /// </summary>
        public const string AgentIdHeader = "X-Agent-Id";

        /// <summary>
        /// Header defining the client type.
        /// </summary>
        public const string ClientTypeHeader = "X-Client-Type";

        /// <summary>
        /// Value for ClientTypeHeader when sent from agent.
        /// </summary>
        public const string ClientTypeValue = "agent";

        /// <summary>
        /// Authentication header.
        /// </summary>
        public const string AuthorizationHeader = "Authorization";

        /// <summary>
        /// Prefix for Bearer token in Authorization header.
        /// </summary>
        public const string BearerPrefix = "Bearer ";
    }
}
