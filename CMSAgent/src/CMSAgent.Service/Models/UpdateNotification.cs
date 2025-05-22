// CMSAgent.Service/Models/UpdateNotification.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Models
{
    /// <summary>
    /// Model representing new version notification from Server via WebSocket.
    /// WebSocket event: "agent:new_version_available"
    /// Reference: agent_api.md and CMSAgent_Doc.md section 8.1.2.
    /// This structure is similar to UpdateCheckResponse when an update is available.
    /// </summary>
    public class UpdateNotification
    {
        /// <summary>
        /// Status (usually "success").
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "success"; // Default according to API doc

        /// <summary>
        /// Always true for this event.
        /// </summary>
        [JsonPropertyName("update_available")]
        public bool UpdateAvailable { get; set; } = true; // Default according to API doc

        /// <summary>
        /// New version of Agent.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// URL to download the update package.
        /// </summary>
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 checksum of the update package.
        /// </summary>
        [JsonPropertyName("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = string.Empty;

        /// <summary>
        /// Release notes for the new version (optional).
        /// </summary>
        [JsonPropertyName("notes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Notes { get; set; }
    }
}
