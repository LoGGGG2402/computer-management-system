using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace CMSAgent.Logging
{
    /// <summary>
    /// Cấu hình logging cho ứng dụng.
    /// </summary>
    public static class LoggingSetup
    {
        /// <summary>
        /// Thiết lập Serilog cho ứng dụng.
        /// </summary>
        /// <param name="hostBuilder">Host builder để cấu hình.</param>
        /// <returns>Host builder đã được cấu hình.</returns>
        public static IHostBuilder ConfigureSerilog(this IHostBuilder hostBuilder)
        {
            return hostBuilder.UseSerilog((hostingContext, services, loggerConfiguration) =>
            {
                // Tạo thư mục logs nếu chưa tồn tại
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                // Đọc cấu hình từ appsettings.json
                loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext();
                
                // Thêm EventLog sink khi chạy trên Windows
                if (OperatingSystem.IsWindows())
                {
                    loggerConfiguration.WriteTo.EventLog("CMSAgent",
                        restrictedToMinimumLevel: LogEventLevel.Warning,
                        manageEventSource: true);
                }
                
                // Ghi log khởi động
                Log.Information("=== CMSAgent starting up ===");
                
                // Xử lý các lỗi không được bắt tại mức toàn cục
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    Log.Fatal(ex, "Unhandled exception: {ExceptionMessage}", ex?.Message);
                };
            });
        }
    }
}
