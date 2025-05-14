using System.Text.Json.Serialization;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Đại diện cho cấu trúc dữ liệu của file runtime_config.json.
    /// Chứa thông tin định danh agent và thông tin vị trí được lưu trữ giữa các lần chạy.
    /// </summary>
    public class RuntimeConfig
    {
        /// <summary>
        /// Định danh duy nhất của agent.
        /// </summary>
        [JsonPropertyName("agentId")]
        public required string AgentId { get; set; }

        /// <summary>
        /// Cấu hình vị trí của agent trong phòng.
        /// </summary>
        [JsonPropertyName("room_config")]
        public required RoomConfig RoomConfig { get; set; }

        /// <summary>
        /// Token đã được mã hóa bằng DPAPI để xác thực với server.
        /// </summary>
        [JsonPropertyName("agent_token_encrypted")]
        public required string AgentTokenEncrypted { get; set; }
    }

    /// <summary>
    /// Đại diện cho cấu hình vị trí của agent trong phòng.
    /// </summary>
    public class RoomConfig
    {
        /// <summary>
        /// Tên phòng mà agent được đặt trong đó.
        /// </summary>
        [JsonPropertyName("roomName")]
        public required string RoomName { get; set; }

        /// <summary>
        /// Tọa độ X trong bản đồ phòng.
        /// </summary>
        [JsonPropertyName("posX")]
        public int PosX { get; set; }

        /// <summary>
        /// Tọa độ Y trong bản đồ phòng.
        /// </summary>
        [JsonPropertyName("posY")]
        public int PosY { get; set; }
    }
} 