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
        public required string commandId { get; set; }

        /// <summary>
        /// Nội dung lệnh cần thực thi.
        /// </summary>
        public required string command { get; set; }

        /// <summary>
        /// Loại lệnh (console, system_action, get_logs).
        /// </summary>
        public CommandType commandType { get; set; }

        /// <summary>
        /// Các tham số bổ sung cho lệnh (tùy chọn).
        /// </summary>
        public required Dictionary<string, object> parameters { get; set; } = new();
    }

    /// <summary>
    /// Payload kết quả thực thi lệnh gửi từ agent lên server.
    /// </summary>
    public class CommandResultPayload
    {
        /// <summary>
        /// ID của lệnh đã thực thi.
        /// </summary>
        public required string commandId { get; set; }

        /// <summary>
        /// Trạng thái thực thi lệnh: true nếu thành công, false nếu thất bại.
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// Loại lệnh đã thực thi.
        /// </summary>
        public CommandType type { get; set; }

        /// <summary>
        /// Dữ liệu kết quả thực thi.
        /// </summary>
        public required CommandResultData result { get; set; } = new()
        {
            stdout = string.Empty,
            stderr = string.Empty,
            errorMessage = string.Empty,
            errorCode = string.Empty
        };
    }

    /// <summary>
    /// Dữ liệu kết quả của việc thực thi lệnh.
    /// </summary>
    public class CommandResultData
    {
        /// <summary>
        /// Đầu ra tiêu chuẩn của lệnh.
        /// </summary>
        public required string stdout { get; set; } = string.Empty;

        /// <summary>
        /// Đầu ra lỗi tiêu chuẩn của lệnh.
        /// </summary>
        public required string stderr { get; set; } = string.Empty;

        /// <summary>
        /// Mã thoát của lệnh (nếu có).
        /// </summary>
        public int? exitCode { get; set; }

        /// <summary>
        /// Thông báo lỗi (nếu có).
        /// </summary>
        public required string errorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Mã lỗi (nếu có).
        /// </summary>
        public required string errorCode { get; set; } = string.Empty;
    }
} 