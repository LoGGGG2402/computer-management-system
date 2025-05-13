using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CMSUpdater;

/// <summary>
/// Lớp tiện ích cấu hình logging cho Updater
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// Tạo và cấu hình logger cho Updater
    /// </summary>
    /// <param name="logDirectory">Thư mục chứa log</param>
    /// <param name="currentAgentVersion">Phiên bản agent hiện tại</param>
    /// <returns>ILogger đã cấu hình</returns>
    public static ILogger CreateUpdaterLogger(string logDirectory, string currentAgentVersion)
    {
        // Đảm bảo thư mục log tồn tại
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Tạo tên file log với timestamp và version
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string logFileName = Path.Combine(logDirectory, $"updater_{timestamp}_{currentAgentVersion}.log");

        // Cấu hình Serilog
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console(LogEventLevel.Information, 
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logFileName,
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        // Tạo logger và factory
        var serilogLogger = loggerConfiguration.CreateLogger();
        var loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
        
        // Tạo logger cho CMSUpdater
        return loggerFactory.CreateLogger("CMSUpdater");
    }
}
