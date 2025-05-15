namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Response from server when checking for updates.
    /// </summary>
    public class UpdateCheckResponse
    {
        /// <summary>
        /// Request status: "success".
        /// </summary>
        public required string status { get; set; } = string.Empty;

        /// <summary>
        /// Whether a new version is available.
        /// </summary>
        public bool update_available { get; set; }

        /// <summary>
        /// New version (if available).
        /// </summary>
        public required string version { get; set; } = string.Empty;

        /// <summary>
        /// URL to download the update package.
        /// </summary>
        public required string download_url { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 checksum of the update package.
        /// </summary>
        public required string checksum_sha256 { get; set; } = string.Empty;
    }
}