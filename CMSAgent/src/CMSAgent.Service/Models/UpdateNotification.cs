 // CMSAgent.Service/Models/UpdateNotification.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Models
{
    /// <summary>
    /// Model đại diện cho thông báo có phiên bản mới từ Server qua WebSocket.
    /// Sự kiện WebSocket: "agent:new_version_available"
    /// Tham khảo: agent_api.md và CMSAgent_Doc.md mục 8.1.2.
    /// Cấu trúc này tương tự như UpdateCheckResponse khi có cập nhật.
    /// </summary>
    public class UpdateNotification
    {
        /// <summary>
        /// Trạng thái (thường là "success").
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "success"; // Mặc định theo API doc

        /// <summary>
        /// Luôn là true cho sự kiện này.
        /// </summary>
        [JsonPropertyName("update_available")]
        public bool UpdateAvailable { get; set; } = true; // Mặc định theo API doc

        /// <summary>
        /// Phiên bản mới của Agent.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// URL để tải gói cập nhật.
        /// </summary>
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Checksum SHA256 của gói cập nhật.
        /// </summary>
        [JsonPropertyName("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = string.Empty;

        /// <summary>
        /// Ghi chú phát hành cho phiên bản mới (tùy chọn).
        /// </summary>
        [JsonPropertyName("notes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Notes { get; set; }
    }
}
