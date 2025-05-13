namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Payload cho việc tải lên log của agent.
    /// </summary>
    public class LogUploadPayload
    {
        /// <summary>
        /// Tên file log được tải lên.
        /// </summary>
        public string log_filename { get; set; }

        /// <summary>
        /// Nội dung file log được mã hóa base64.
        /// </summary>
        public string log_content_base64 { get; set; }
    }
}
