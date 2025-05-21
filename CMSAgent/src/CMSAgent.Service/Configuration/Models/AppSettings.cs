 // CMSAgent.Service/Configuration/Models/AppSettings.cs
using System.ComponentModel.DataAnnotations;

namespace CMSAgent.Service.Configuration.Models
{
    /// <summary>
    /// Model đại diện cho các cài đặt trong file appsettings.json.
    /// Các thuộc tính ở đây sẽ được binding từ file cấu hình.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Cấu hình Serilog (thường là một section riêng trong JSON).
        /// Ở đây ta có thể để kiểu 'object' hoặc một lớp cụ thể nếu muốn định nghĩa chi tiết.
        /// </summary>
        public SerilogSettings Serilog { get; set; } = new SerilogSettings();

        /// <summary>
        /// URL của Management Server (ví dụ: "https://cms.example.com").
        /// </summary>
        [Required(ErrorMessage = "ServerUrl is required.")]
        [Url(ErrorMessage = "ServerUrl must be a valid URL.")]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Đường dẫn gốc cho các HTTP API trên Server (ví dụ: "/api").
        /// </summary>
        public string ApiPath { get; set; } = "/api"; // Giá trị mặc định

        /// <summary>
        /// Phiên bản hiện tại của Agent.
        /// Giá trị này có thể được cập nhật tự động bởi CMSUpdater.
        /// </summary>
        public string Version { get; set; } = "0.0.0";

        /// <summary>
        /// Khoảng thời gian (giây) gửi báo cáo trạng thái tài nguyên.
        /// Mặc định: 60 giây.
        /// </summary>
        [Range(10, 3600, ErrorMessage = "StatusReportIntervalSec must be between 10 and 3600.")]
        public int StatusReportIntervalSec { get; set; } = 60;

        /// <summary>
        /// Khoảng thời gian (giây) kiểm tra cập nhật tự động.
        /// Mặc định: 3600 giây (1 giờ).
        /// </summary>
        [Range(300, 86400, ErrorMessage = "AutoUpdateIntervalSec must be between 300 and 86400.")]
        public int AutoUpdateIntervalSec { get; set; } = 3600;

        /// <summary>
        /// Bật/tắt tính năng tự động kiểm tra cập nhật.
        /// Mặc định: true.
        /// </summary>
        public bool EnableAutoUpdate { get; set; } = true;


        /// <summary>
        /// Khoảng thời gian (giây) làm mới token định kỳ.
        /// Mặc định: 43200 giây (12 giờ).
        /// </summary>
        [Range(600, 86400, ErrorMessage = "TokenRefreshIntervalSec must be between 600 and 86400.")]
        public int TokenRefreshIntervalSec { get; set; } = 43200;

        /// <summary>
        /// Cấu hình cho chính sách retry HTTP (Polly).
        /// </summary>
        public HttpRetryPolicySettings HttpRetryPolicy { get; set; } = new HttpRetryPolicySettings();

        /// <summary>
        /// Cấu hình cho kết nối WebSocket.
        /// </summary>
        public WebSocketSettings WebSocketPolicy { get; set; } = new WebSocketSettings();

        /// <summary>
        /// Cấu hình cho việc thực thi lệnh.
        /// </summary>
        public CommandExecutionSettings CommandExecution { get; set; } = new CommandExecutionSettings();

        /// <summary>
        /// Giới hạn tài nguyên, ví dụ kích thước tối đa của hàng đợi dữ liệu offline.
        /// </summary>
        public ResourceLimitSettings ResourceLimits { get; set; } = new ResourceLimitSettings();

        /// <summary>
        /// GUID duy nhất của Agent, được sử dụng để tạo tên Mutex.
        /// Giá trị này nên được sinh ngẫu nhiên và duy nhất cho mỗi bản cài đặt.
        /// Nó có thể được ghi vào appsettings.json trong quá trình cài đặt.
        /// </summary>
        public string AgentInstanceGuid { get; set; } = string.Empty; // Sẽ được tạo khi cài đặt
    }

    /// <summary>
    /// Cấu hình chi tiết cho Serilog.
    /// </summary>
    public class SerilogSettings
    {
        public MinimumLevelSettings MinimumLevel { get; set; } = new MinimumLevelSettings();
        // Các cấu hình khác của Serilog có thể thêm vào đây nếu cần đọc từ appsettings.json
        // Ví dụ: public List<SerilogSinkSettings> WriteTo { get; set; }
    }

    public class MinimumLevelSettings
    {
        public string Default { get; set; } = "Information";
        public Dictionary<string, string> Override { get; set; } = new Dictionary<string, string>();
    }


    /// <summary>
    /// Cấu hình cho chính sách retry HTTP.
    /// </summary>
    public class HttpRetryPolicySettings
    {
        /// <summary>
        /// Số lần thử lại tối đa.
        /// </summary>
        [Range(0, 10)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Thời gian chờ ban đầu (giây) trước khi thử lại.
        /// Sẽ tăng theo cấp số nhân cho các lần thử lại sau.
        /// </summary>
        [Range(1, 60)]
        public int InitialDelaySeconds { get; set; } = 2;

        /// <summary>
        /// Thời gian chờ tối đa (giây) giữa các lần thử lại.
        /// </summary>
        [Range(5, 300)]
        public int MaxDelaySeconds { get; set; } = 30;
    }

    /// <summary>
    /// Cấu hình cho kết nối WebSocket.
    /// </summary>
    public class WebSocketSettings
    {
        /// <summary>
        /// Thời gian timeout (giây) khi cố gắng kết nối WebSocket.
        /// </summary>
        [Range(5, 120)]
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Thời gian chờ tối thiểu (giây) trong chiến lược exponential backoff khi kết nối lại.
        /// </summary>
        [Range(1, 60)]
        public int ReconnectMinBackoffSeconds { get; set; } = 5;

        /// <summary>
        /// Thời gian chờ tối đa (giây) trong chiến lược exponential backoff.
        /// </summary>
        [Range(60, 600)]
        public int ReconnectMaxBackoffSeconds { get; set; } = 300;

        /// <summary>
        /// Số lần thử kết nối lại tối đa (-1 nghĩa là vô hạn).
        /// </summary>
        [Range(-1, 100)]
        public int MaxReconnectAttempts { get; set; } = -1; // Vô hạn
    }

    /// <summary>
    /// Cấu hình cho việc thực thi lệnh.
    /// </summary>
    public class CommandExecutionSettings
    {
        /// <summary>
        /// Kích thước tối đa của hàng đợi lệnh.
        /// </summary>
        [Range(1, 100)]
        public int MaxQueueSize { get; set; } = 10;

        /// <summary>
        /// Số lượng lệnh tối đa có thể chạy song song.
        /// </summary>
        [Range(1, 10)]
        public int MaxParallelCommands { get; set; } = 1; // Mặc định chạy tuần tự

        /// <summary>
        /// Thời gian timeout mặc định (giây) cho một lệnh.
        /// </summary>
        [Range(10, 3600)]
        public int DefaultCommandTimeoutSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Cấu hình giới hạn tài nguyên.
    /// </summary>
    public class ResourceLimitSettings
    {
        /// <summary>
        /// Kích thước tối đa (số lượng item) của hàng đợi dữ liệu offline.
        /// </summary>
        [Range(10, 1000)]
        public int MaxOfflineQueueSize { get; set; } = 100;
    }
}
