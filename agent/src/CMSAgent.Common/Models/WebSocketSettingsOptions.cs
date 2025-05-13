using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
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
} 