using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Agent's position information in the room.
    /// </summary>
    public class PositionInfo
    {
        /// <summary>
        /// Room name (location) of the agent.
        /// </summary>
        [Required]
        public required string roomName { get; set; }

        /// <summary>
        /// X coordinate of the agent in the room.
        /// </summary>
        [Required]
        public int posX { get; set; }

        /// <summary>
        /// Y coordinate of the agent in the room.
        /// </summary>
        [Required]
        public int posY { get; set; }
    }
}
