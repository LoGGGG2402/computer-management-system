using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Configuration for HttpClient
    /// </summary>
    public class HttpClientSettingsOptions
    {
        /// <summary>
        /// Maximum timeout for HTTP requests (seconds)
        /// </summary>
        [Range(5, 120)]
        public int RequestTimeoutSec { get; set; } = 15;
    }

    /// <summary>
    /// Configuration for WebSocket connection
    /// </summary>
    public class WebSocketSettingsOptions
    {
        /// <summary>
        /// Initial delay (seconds) before attempting to reconnect WebSocket
        /// </summary>
        [Range(1, 60)]
        public int ReconnectDelayInitialSec { get; set; } = 5;

        /// <summary>
        /// Maximum delay (seconds) before attempting to reconnect WebSocket
        /// </summary>
        [Range(5, 300)]
        public int ReconnectDelayMaxSec { get; set; } = 60;

        /// <summary>
        /// Maximum number of reconnection attempts (null = unlimited)
        /// </summary>
        public int? ReconnectAttemptsMax { get; set; }
    }
}