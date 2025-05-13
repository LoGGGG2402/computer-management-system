using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Cấu hình cho hàng đợi offline
    /// </summary>
    public class OfflineQueueSettingsOptions
    {
        /// <summary>
        /// Kích thước tối đa (MB) của tất cả hàng đợi
        /// </summary>
        [Range(1, 1024)]
        public int MaxSizeMb { get; set; } = 100;

        /// <summary>
        /// Thời gian tối đa (giờ) một mục có thể nằm trong hàng đợi
        /// </summary>
        [Range(1, 24 * 30)]
        public int MaxAgeHours { get; set; } = 24;

        /// <summary>
        /// Số lượng báo cáo trạng thái tối đa trong hàng đợi
        /// </summary>
        [Range(10, 10000)]
        public int StatusReportsMaxCount { get; set; } = 1000;

        /// <summary>
        /// Số lượng kết quả lệnh tối đa trong hàng đợi
        /// </summary>
        [Range(10, 1000)]
        public int CommandResultsMaxCount { get; set; } = 500;

        /// <summary>
        /// Số lượng báo cáo lỗi tối đa trong hàng đợi
        /// </summary>
        [Range(10, 1000)]
        public int ErrorReportsMaxCount { get; set; } = 200;

        /// <summary>
        /// Đường dẫn cơ sở cho hàng đợi offline (null = sử dụng đường dẫn mặc định)
        /// </summary>
        public string BasePath { get; set; }
    }
} 