using System.Text.Json.Serialization;

namespace CMSAgent.Models.Payloads
{
    /// <summary>
    /// Update check response received from the server when calling GET /check-update
    /// </summary>
    public class UpdateInfoFromServer
    {
        /// <summary>
        /// Status of the update check
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Whether an update is available
        /// </summary>
        [JsonPropertyName("update_available")]
        public bool UpdateAvailable { get; set; } = false;

        /// <summary>
        /// New version available
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// URL to download the update
        /// </summary>
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the update package
        /// </summary>
        [JsonPropertyName("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = string.Empty;

        /// <summary>
        /// Release notes
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payload for when server sends a WebSocket event agent:new_version_available
    /// </summary>
    public class NewVersionEventPayload
    {
        /// <summary>
        /// New version available
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// URL to download the update
        /// </summary>
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the update package
        /// </summary>
        [JsonPropertyName("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = string.Empty;
    }
}
}