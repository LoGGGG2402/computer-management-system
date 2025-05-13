using System;
using CMSAgent.Common.Interfaces;

namespace CMSAgent.Common
{
    /// <summary>
    /// Implementation mặc định của IDateTimeProvider.
    /// </summary>
    public class DateTimeProvider : IDateTimeProvider
    {
        /// <summary>
        /// Lấy thời gian hiện tại (UTC).
        /// </summary>
        public DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// Lấy thời gian hiện tại (Local).
        /// </summary>
        public DateTime Now => DateTime.Now;

        /// <summary>
        /// Lấy ngày hiện tại (Local).
        /// </summary>
        public DateTime Today => DateTime.Today;
    }
}
