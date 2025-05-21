 // CMSAgent.Service/Configuration/Models/RuntimeConfig.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Configuration.Models
{
    /// <summary>
    /// Model đại diện cho các cài đặt trong file runtime_config.json.
    /// File này chứa các cấu hình động, có thể thay đổi trong quá trình Agent hoạt động.
    /// </summary>
    public class RuntimeConfig
    {
        /// <summary>
        /// ID duy nhất của Agent (GUID string).
        /// </summary>
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// Thông tin vị trí của máy (phòng, tọa độ).
        /// </summary>
        [JsonPropertyName("room_config")]
        public PositionInfo? RoomConfig { get; set; } // Sử dụng nullable nếu có thể chưa được cấu hình

        /// <summary>
        /// Token xác thực của Agent, đã được mã hóa bằng DPAPI.
        /// </summary>
        [JsonPropertyName("agent_token_encrypted")]
        public string? AgentTokenEncrypted { get; set; } // Token có thể null nếu agent chưa được đăng ký
    }
}
