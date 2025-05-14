using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Model cho các item được lưu trữ trong queue.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của item cần lưu trữ.</typeparam>
    public class QueuedItem<T> where T : class
    {
        /// <summary>
        /// Định danh duy nhất của item trong queue.
        /// </summary>
        public required string ItemId { get; set; }

        /// <summary>
        /// Dữ liệu thực tế cần lưu trữ.
        /// </summary>
        public required T Data { get; set; }

        /// <summary>
        /// Thời điểm item được thêm vào queue (UTC).
        /// </summary>
        public DateTime EnqueuedTimestampUtc { get; set; }

        /// <summary>
        /// Số lần đã thử gửi item và thất bại.
        /// </summary>
        public int RetryAttempts { get; set; } = 0;
    }

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
        public required string BasePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Thư mục lưu trữ hàng đợi
        /// </summary>
        public required string QueueDirectory { get; set; } = string.Empty;
        
        /// <summary>
        /// Số lượng tối đa các mục trong hàng đợi
        /// </summary>
        [Range(10, 10000)]
        public int MaxCount { get; set; } = 1000;
    }
} 