using CMSAgent.Common.Enums;

namespace CMSAgent.Common.DTOs
{
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
}