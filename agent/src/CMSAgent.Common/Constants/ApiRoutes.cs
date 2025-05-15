namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// API endpoint paths from the central server.
    /// </summary>
    public static class ApiRoutes
    {
        /// <summary>
        /// API endpoint for registering new agent or identifying existing agent.
        /// </summary>
        public const string Identify = "/api/agent/identify";

        /// <summary>
        /// API endpoint for agent MFA verification.
        /// </summary>
        public const string VerifyMfa = "/api/agent/verify-mfa";

        /// <summary>
        /// API endpoint for sending client hardware information to server.
        /// </summary>
        public const string HardwareInfo = "/api/agent/hardware-info";

        /// <summary>
        /// API endpoint for checking new agent version availability.
        /// </summary>
        public const string CheckUpdate = "/api/agent/check-update";

        /// <summary>
        /// API endpoint for reporting agent errors to server.
        /// </summary>
        public const string ReportError = "/api/agent/report-error";
    }
}