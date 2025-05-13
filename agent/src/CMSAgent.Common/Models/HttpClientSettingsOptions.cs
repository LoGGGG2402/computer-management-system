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
} 