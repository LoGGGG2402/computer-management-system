using System;
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Cấu hình chính của CMSAgent được lưu trong appsettings.json
    /// </summary>
    public class CmsAgentSettingsOptions
    {
        /// <summary>
        /// Tên ứng dụng, sử dụng cho tên service và thư mục dữ liệu mặc định
        /// </summary>
        [Required]
        public string AppName { get; set; } = "CMSAgent";

        /// <summary>
        /// URL của server
        /// </summary>
        [Required]
        [Url]
        public string ServerUrl { get; set; }

        /// <summary>
        /// Phiên bản của agent
        /// </summary>
        [Required]
        public string Version { get; set; }

        /// <summary>
        /// Cấu hình riêng của agent
        /// </summary>
        public AgentSpecificSettingsOptions AgentSettings { get; set; } = new();

        /// <summary>
        /// Cấu hình HttpClient
        /// </summary>
        public HttpClientSettingsOptions HttpClientSettings { get; set; } = new();

        /// <summary>
        /// Cấu hình WebSocket
        /// </summary>
        public WebSocketSettingsOptions WebSocketSettings { get; set; } = new();

        /// <summary>
        /// Cấu hình thực thi lệnh
        /// </summary>
        public CommandExecutorSettingsOptions CommandExecutorSettings { get; set; } = new();

        /// <summary>
        /// Giới hạn tài nguyên
        /// </summary>
        public ResourceLimitsOptions ResourceLimits { get; set; } = new();
    }
} 