using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Cấu hình đặc biệt cho Agent
    /// </summary>
    public class AgentSpecificSettingsOptions
    {
        /// <summary>
        /// Khoảng thời gian (giây) gửi báo cáo trạng thái lên server
        /// </summary>
        [Range(1, 3600)]
        public int StatusReportIntervalSec { get; set; } = 30;

        /// <summary>
        /// Bật/tắt tính năng tự động cập nhật
        /// </summary>
        public bool EnableAutoUpdate { get; set; } = true;

        /// <summary>
        /// Khoảng thời gian (giây) giữa các lần kiểm tra cập nhật
        /// </summary>
        [Range(60, 86400 * 7)]
        public int AutoUpdateIntervalSec { get; set; } = 86400;

        /// <summary>
        /// Số lần thử lại tối đa khi kết nối mạng bị lỗi
        /// </summary>
        [Range(1, 10)]
        public int NetworkRetryMaxAttempts { get; set; } = 5;

        /// <summary>
        /// Thời gian chờ ban đầu (giây) trước khi thử lại kết nối mạng
        /// </summary>
        [Range(1, 60)]
        public int NetworkRetryInitialDelaySec { get; set; } = 5;

        /// <summary>
        /// Khoảng thời gian (giây) giữa các lần làm mới token
        /// </summary>
        [Range(3600, 86400 * 7)]
        public int TokenRefreshIntervalSec { get; set; } = 86400;

        /// <summary>
        /// Cấu hình cho hàng đợi offline
        /// </summary>
        public OfflineQueueSettingsOptions OfflineQueue { get; set; } = new();
    }
} 