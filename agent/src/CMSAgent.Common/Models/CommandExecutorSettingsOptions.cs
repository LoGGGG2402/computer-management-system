using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Cấu hình cho trình thực thi lệnh
    /// </summary>
    public class CommandExecutorSettingsOptions
    {
        /// <summary>
        /// Thời gian chờ mặc định (giây) cho việc thực thi lệnh
        /// </summary>
        [Range(30, 3600)]
        public int DefaultTimeoutSec { get; set; } = 300;

        /// <summary>
        /// Số lượng lệnh tối đa được thực thi đồng thời
        /// </summary>
        [Range(1, 10)]
        public int MaxParallelCommands { get; set; } = 2;

        /// <summary>
        /// Kích thước tối đa của hàng đợi lệnh
        /// </summary>
        [Range(10, 1000)]
        public int MaxQueueSize { get; set; } = 100;

        /// <summary>
        /// Bảng mã sử dụng cho đầu ra console
        /// </summary>
        [Required]
        public string ConsoleEncoding { get; set; } = "utf-8";
    }
} 