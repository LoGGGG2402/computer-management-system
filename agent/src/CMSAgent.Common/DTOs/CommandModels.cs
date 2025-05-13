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

    /// <summary>
    /// Payload kết quả thực thi lệnh gửi từ agent lên server.
    /// </summary>
    public class CommandResultPayload
    {
        /// <summary>
        /// ID của lệnh đã thực thi.
        /// </summary>
        public string commandId { get; set; }

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
        public CommandResultData result { get; set; }
    }

    /// <summary>
    /// Dữ liệu kết quả của việc thực thi lệnh.
    /// </summary>
    public class CommandResultData
    {
        /// <summary>
        /// Đầu ra tiêu chuẩn của lệnh.
        /// </summary>
        public string stdout { get; set; }

        /// <summary>
        /// Đầu ra lỗi tiêu chuẩn của lệnh.
        /// </summary>
        public string stderr { get; set; }

        /// <summary>
        /// Mã thoát của lệnh (nếu có).
        /// </summary>
        public int? exitCode { get; set; }

        /// <summary>
        /// Thông báo lỗi (nếu có).
        /// </summary>
        public string errorMessage { get; set; }

        /// <summary>
        /// Mã lỗi (nếu có).
        /// </summary>
        public string errorCode { get; set; }
    }
} 