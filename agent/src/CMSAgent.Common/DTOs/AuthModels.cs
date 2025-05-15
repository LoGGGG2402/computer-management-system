using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Yêu cầu xác thực MFA cho agent.
    /// </summary>
    public class VerifyMfaRequest
    {
        /// <summary>
        /// ID của agent cần xác thực.
        /// </summary>
        [Required]
        public required string agentId { get; set; }

        /// <summary>
        /// Mã MFA do người dùng cung cấp.
        /// </summary>
        [Required]
        public required string mfaCode { get; set; }
    }

    /// <summary>
    /// Phản hồi từ server khi xác thực MFA.
    /// </summary>
    public class VerifyMfaResponse
    {
        /// <summary>
        /// Trạng thái của yêu cầu: "success" hoặc "error".
        /// </summary>
        public required string status { get; set; } = string.Empty;
        /// <summary>
        /// Token xác thực (chỉ được trả về khi thành công).
        /// </summary>
        public required string agentToken { get; set; } = string.Empty;

        /// <summary>
        /// Thông báo lỗi hoặc thông tin bổ sung.
        /// </summary>
        public required string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Yêu cầu đăng ký agent mới hoặc định danh agent đã tồn tại với server.
    /// </summary>
    public class AgentIdentifyRequest
    {
        /// <summary>
        /// Device ID duy nhất của agent.
        /// </summary>
        [Required]
        public required string agentId { get; set; }

        /// <summary>
        /// Thông tin vị trí của agent.
        /// </summary>
        [Required]
        public required PositionInfo positionInfo { get; set; }

        /// <summary>
        /// Nếu true, yêu cầu server cấp token mới ngay cả khi agent đã có token hợp lệ.
        /// </summary>
        public bool forceRenewToken { get; set; } = false;
    }

    /// <summary>
    /// Phản hồi từ server khi agent định danh.
    /// </summary>
    public class AgentIdentifyResponse
    {
        /// <summary>
        /// Trạng thái của yêu cầu: "success", "mfa_required", "position_error", "error".
        /// </summary>
        public required string status { get; set; } = string.Empty;

        /// <summary>
        /// ID của agent (chỉ được trả về khi thành công).
        /// </summary>
        public required string agentId { get; set; } = string.Empty;

        /// <summary>
        /// Token xác thực (chỉ được trả về khi thành công và token được làm mới).
        /// </summary>
        public required string agentToken { get; set; } = string.Empty;

        /// <summary>
        /// Thông báo lỗi hoặc thông tin bổ sung.
        /// </summary>
        public required string message { get; set; } = string.Empty;
    }
} 