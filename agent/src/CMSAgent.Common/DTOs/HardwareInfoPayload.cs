using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Thông tin phần cứng của máy client gửi lên server.
    /// </summary>
    public class HardwareInfoPayload
    {
        /// <summary>
        /// Thông tin hệ điều hành.
        /// </summary>
        public string os_info { get; set; }

        /// <summary>
        /// Thông tin CPU.
        /// </summary>
        public string cpu_info { get; set; }

        /// <summary>
        /// Thông tin GPU.
        /// </summary>
        public string gpu_info { get; set; }

        /// <summary>
        /// Tổng RAM (bytes).
        /// </summary>
        public long total_ram { get; set; }

        /// <summary>
        /// Tổng dung lượng ổ C: (bytes).
        /// </summary>
        [Required]
        public long total_disk_space { get; set; }
    }
}
