using System;

namespace CMSAgent.Persistence.Models
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
        public string ItemId { get; set; }

        /// <summary>
        /// Dữ liệu thực tế cần lưu trữ.
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Thời điểm item được thêm vào queue (UTC).
        /// </summary>
        public DateTime EnqueuedTimestampUtc { get; set; }

        /// <summary>
        /// Số lần đã thử gửi item và thất bại.
        /// </summary>
        public int RetryAttempts { get; set; } = 0;
    }
}
