using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Cấu hình cho HttpClient
    /// </summary>
    public class HttpClientSettingsOptions
    {
        /// <summary>
        /// Thời gian chờ tối đa cho request HTTP (giây)
        /// </summary>
        [Range(5, 120)]
        public int RequestTimeoutSec { get; set; } = 15;
    }

    /// <summary>
    /// Cấu hình cho kết nối WebSocket
    /// </summary>
    public class WebSocketSettingsOptions
    {
        /// <summary>
        /// Thời gian chờ ban đầu (giây) trước khi thử kết nối lại WebSocket
        /// </summary>
        [Range(1, 60)]
        public int ReconnectDelayInitialSec { get; set; } = 5;

        /// <summary>
        /// Thời gian chờ tối đa (giây) trước khi thử kết nối lại WebSocket
        /// </summary>
        [Range(5, 300)]
        public int ReconnectDelayMaxSec { get; set; } = 60;

        /// <summary>
        /// Số lần thử kết nối lại tối đa (null = không giới hạn)
        /// </summary>
        public int? ReconnectAttemptsMax { get; set; }
    }

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