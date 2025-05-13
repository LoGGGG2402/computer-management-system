using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Yêu cầu đăng ký agent mới hoặc định danh agent đã tồn tại với server.
    /// </summary>
    public class AgentIdentifyRequest
    {
        /// <summary>
        /// Device ID duy nhất của agent.
        /// </summary>
        [Required]
        public string agentId { get; set; }

        /// <summary>
        /// Thông tin vị trí của agent.
        /// </summary>
        [Required]
        public PositionInfo positionInfo { get; set; }

        /// <summary>
        /// Nếu true, yêu cầu server cấp token mới ngay cả khi agent đã có token hợp lệ.
        /// </summary>
        public bool forceRenewToken { get; set; } = false;
    }
}
