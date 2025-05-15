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
        public required string ServerUrl { get; set; }

        /// <summary>
        /// Phiên bản của agent
        /// </summary>
        [Required]
        public required string Version { get; set; }

        /// <summary>
        /// Cấu hình riêng của agent
        /// </summary>
        public AgentSpecificSettingsOptions AgentSettings { get; set; } = new();

        /// <summary>
        /// Cấu hình HttpClient
        /// </summary>
        public HttpClientSettingsOptions HttpClientSettings { get; set; } = new();

    }

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
    }

    /// <summary>
    /// Cấu hình cho trình thực thi lệnh
    /// </summary>
    public class CommandExecutorSettingsOptions
    {
        /// <summary>
        /// Thời gian chờ mặc định (giây) cho việc thực thi lệnh
        /// </summary>
        [Range(30, 3600)]
        public int DefaultTimeoutSec { get; set; } = 300;

        /// <summary>
        /// Số lượng lệnh tối đa được thực thi đồng thời
        /// </summary>
        [Range(1, 10)]
        public int MaxParallelCommands { get; set; } = 2;

        /// <summary>
        /// Kích thước tối đa của hàng đợi lệnh
        /// </summary>
        [Range(10, 1000)]
        public int MaxQueueSize { get; set; } = 100;

        /// <summary>
        /// Bảng mã sử dụng cho đầu ra console
        /// </summary>
        [Required]
        public string ConsoleEncoding { get; set; } = "utf-8";
    }
} 