 // CMSAgent.Service/Configuration/Models/PositionInfo.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Configuration.Models
{
    /// <summary>
    /// Model chứa thông tin vị trí của Agent.
    /// Được sử dụng trong runtime_config.json và trong payload của API /api/agent/identify.
    /// </summary>
    public class PositionInfo
    {
        /// <summary>
        /// Tên phòng nơi Agent được đặt.
        /// Bắt buộc. (3-100 ký tự)
        /// </summary>
        [JsonPropertyName("roomName")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "RoomName is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "RoomName must be between 3 and 100 characters.")]
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// Tọa độ X của Agent trong phòng.
        /// Bắt buộc. (Phải là số nguyên không âm)
        /// </summary>
        [JsonPropertyName("posX")]
        [Range(0, int.MaxValue, ErrorMessage = "PosX must be a non-negative integer.")]
        public int PosX { get; set; }

        /// <summary>
        /// Tọa độ Y của Agent trong phòng.
        /// Bắt buộc. (Phải là số nguyên không âm)
        /// </summary>
        [JsonPropertyName("posY")]
        [Range(0, int.MaxValue, ErrorMessage = "PosY must be a non-negative integer.")]
        public int PosY { get; set; }
    }
}
