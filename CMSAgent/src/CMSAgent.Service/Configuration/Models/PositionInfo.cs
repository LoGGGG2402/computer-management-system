// CMSAgent.Service/Configuration/Models/PositionInfo.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Configuration.Models
{
    /// <summary>
    /// Model containing Agent's position information.
    /// Used in runtime_config.json and in the payload of /api/agent/identify API.
    /// </summary>
    public class PositionInfo
    {
        /// <summary>
        /// Name of the room where Agent is placed.
        /// Required. (3-100 characters)
        /// </summary>
        [JsonPropertyName("roomName")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "RoomName is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "RoomName must be between 3 and 100 characters.")]
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// X coordinate of Agent in the room.
        /// Required. (Must be a non-negative integer)
        /// </summary>
        [JsonPropertyName("posX")]
        [Range(0, int.MaxValue, ErrorMessage = "PosX must be a non-negative integer.")]
        public int PosX { get; set; }

        /// <summary>
        /// Y coordinate of Agent in the room.
        /// Required. (Must be a non-negative integer)
        /// </summary>
        [JsonPropertyName("posY")]
        [Range(0, int.MaxValue, ErrorMessage = "PosY must be a non-negative integer.")]
        public int PosY { get; set; }
    }
}
