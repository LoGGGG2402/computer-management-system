using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Thông tin vị trí của agent trong phòng.
    /// </summary>
    public class PositionInfo
    {
        /// <summary>
        /// Tên phòng (vị trí) của agent.
        /// </summary>
        [Required]
        public required string roomName { get; set; }

        /// <summary>
        /// Tọa độ X của agent trong phòng.
        /// </summary>
        [Required]
        public int posX { get; set; }

        /// <summary>
        /// Tọa độ Y của agent trong phòng.
        /// </summary>
        [Required]
        public int posY { get; set; }
    }
}
