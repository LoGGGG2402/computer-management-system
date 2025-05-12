namespace CMSAgent.Communication
{
    /// <summary>
    /// Contains all API endpoints for server communication
    /// </summary>
    public static class ApiEndpoints
    {
        // Base API endpoint is /api/agent
        private const string Base = "/api/agent";

        // Agent identification
        public static string Identify => $"{Base}/identify";
        
        // MFA verification
        public static string VerifyMfa => $"{Base}/verify-mfa";
        
        // Hardware information
        public static string HardwareInfo => $"{Base}/hardware-info";
        
        // Check for updates
        public static string CheckUpdate => $"{Base}/check-update";
        
        // Report errors
        public static string ReportError => $"{Base}/report-error";
        
        // Download update packages format: {DownloadUpdatePackage}/{filename}
        public static string DownloadUpdatePackage => $"{Base}/download/agent-packages";
    }
}