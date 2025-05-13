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
        public string agentId { get; set; }

        /// <summary>
        /// Mã MFA do người dùng cung cấp.
        /// </summary>
        [Required]
        public string mfaCode { get; set; }
    }
}