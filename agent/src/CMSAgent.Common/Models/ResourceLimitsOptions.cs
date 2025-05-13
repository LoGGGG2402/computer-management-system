using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Cấu hình giới hạn tài nguyên
    /// </summary>
    public class ResourceLimitsOptions
    {
        /// <summary>
        /// Tỷ lệ CPU tối đa (phần trăm) mà agent được phép sử dụng
        /// </summary>
        [Range(10, 100)]
        public int MaxCpuPercentage { get; set; } = 75;

        /// <summary>
        /// Lượng RAM tối đa (MB) mà agent được phép sử dụng
        /// </summary>
        [Range(64, 2048)]
        public int MaxRamMegabytes { get; set; } = 512;
    }
} 