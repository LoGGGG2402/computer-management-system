using System.Collections.Generic;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Thông tin lệnh được gửi từ server đến agent.
    /// </summary>
    public class CommandPayload
    {
        /// <summary>
        /// ID duy nhất của lệnh.
        /// </summary>
        public string commandId { get; set; }

        /// <summary>
        /// Nội dung lệnh cần thực thi.
        /// </summary>
        public string command { get; set; }

        /// <summary>
        /// Loại lệnh (console, system_action, get_logs).
        /// </summary>
        public CommandType commandType { get; set; }

        /// <summary>
        /// Các tham số bổ sung cho lệnh (tùy chọn).
        /// </summary>
        public Dictionary<string, object> parameters { get; set; }
    }
}