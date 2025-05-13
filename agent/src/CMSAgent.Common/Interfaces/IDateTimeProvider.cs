using System;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface để cung cấp thời gian hiện tại, giúp dễ dàng test các thành phần phụ thuộc vào thời gian.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        /// Lấy thời gian hiện tại (UTC).
        /// </summary>
        /// <returns>Thời gian hiện tại ở định dạng UTC.</returns>
        DateTime UtcNow { get; }

        /// <summary>
        /// Lấy thời gian hiện tại (Local).
        /// </summary>
        /// <returns>Thời gian hiện tại ở múi giờ local.</returns>
        DateTime Now { get; }

        /// <summary>
        /// Lấy ngày hiện tại (Local).
        /// </summary>
        /// <returns>Ngày hiện tại ở múi giờ local.</returns>
        DateTime Today { get; }
    }
}
