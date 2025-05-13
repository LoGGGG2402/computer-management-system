using System;
using CMSAgent.Common.Interfaces;

namespace CMSAgent.UnitTests.Common
{
    /// <summary>
    /// Mock implementation của IDateTimeProvider để sử dụng trong tests,
    /// cho phép đặt trước các giá trị thời gian mong muốn.
    /// </summary>
    public class MockDateTimeProvider : IDateTimeProvider
    {
        private DateTime _utcNow;
        private DateTime _now;
        private DateTime _today;

        /// <summary>
        /// Khởi tạo MockDateTimeProvider với thời gian hiện tại.
        /// </summary>
        public MockDateTimeProvider()
        {
            _utcNow = DateTime.UtcNow;
            _now = DateTime.Now;
            _today = DateTime.Today;
        }

        /// <summary>
        /// Khởi tạo MockDateTimeProvider với thời gian được chỉ định.
        /// </summary>
        /// <param name="fixedDateTime">Thời gian cố định để sử dụng cho tất cả các thuộc tính</param>
        public MockDateTimeProvider(DateTime fixedDateTime)
        {
            SetDateTime(fixedDateTime);
        }

        /// <summary>
        /// Đặt tất cả các giá trị thời gian với một DateTime duy nhất.
        /// </summary>
        /// <param name="dateTime">Thời gian cần đặt</param>
        public void SetDateTime(DateTime dateTime)
        {
            _utcNow = dateTime.ToUniversalTime();
            _now = dateTime;
            _today = dateTime.Date;
        }

        /// <summary>
        /// Đặt giá trị cụ thể cho UtcNow.
        /// </summary>
        /// <param name="utcNow">Thời gian UTC mong muốn</param>
        public void SetUtcNow(DateTime utcNow) => _utcNow = utcNow.ToUniversalTime();

        /// <summary>
        /// Đặt giá trị cụ thể cho Now.
        /// </summary>
        /// <param name="now">Thời gian local mong muốn</param>
        public void SetNow(DateTime now) => _now = now;

        /// <summary>
        /// Đặt giá trị cụ thể cho Today.
        /// </summary>
        /// <param name="today">Ngày mong muốn (thời gian sẽ được đặt về 00:00:00)</param>
        public void SetToday(DateTime today) => _today = today.Date;

        /// <summary>
        /// Lấy thời gian hiện tại (UTC) đã được đặt.
        /// </summary>
        public DateTime UtcNow => _utcNow;

        /// <summary>
        /// Lấy thời gian hiện tại (Local) đã được đặt.
        /// </summary>
        public DateTime Now => _now;

        /// <summary>
        /// Lấy ngày hiện tại (Local) đã được đặt.
        /// </summary>
        public DateTime Today => _today;
    }
} 