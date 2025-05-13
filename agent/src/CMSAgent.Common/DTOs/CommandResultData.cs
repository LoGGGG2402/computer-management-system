namespace CMSAgent.Common.DTOs
{
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
