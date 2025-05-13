using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CMSUpdater.Helpers;

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
    /// <param name="configuration">Cấu hình từ appsettings.json</param>
    /// <returns>ILogger đã cấu hình</returns>
    public static ILogger CreateUpdaterLogger(string logDirectory, string currentAgentVersion, IConfiguration? configuration = null)
    {
        // Lấy cấu hình log từ appsettings.json
        LogEventLevel minimumLevel = LogEventLevel.Debug;
        string basePath = "Logs";
        string fileTemplate = "updater_{timestamp}_{version}.log";
        string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {Message:lj}{NewLine}{Exception}";
        
        if (configuration != null)
        {
            // Lấy LogLevel từ cấu hình
            string logLevel = configuration["Logging:LogLevel:Default"] ?? "Information";
            minimumLevel = logLevel.ToLower() switch
            {
                "trace" => LogEventLevel.Verbose,
                "debug" => LogEventLevel.Debug,
                "information" => LogEventLevel.Information,
                "warning" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                "critical" => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
            
            // Lấy BasePath và các template từ cấu hình
            basePath = configuration["Logging:File:BasePath"] ?? basePath;
            fileTemplate = configuration["Logging:File:FileTemplate"] ?? fileTemplate;
            outputTemplate = configuration["Logging:File:OutputTemplate"] ?? outputTemplate;
        }
        
        // Kết hợp logDirectory với BasePath từ cấu hình nếu BasePath là đường dẫn tương đối
        string effectiveLogDirectory = logDirectory;
        if (!string.IsNullOrEmpty(basePath) && !Path.IsPathRooted(basePath))
        {
            effectiveLogDirectory = Path.Combine(logDirectory, basePath);
        }
        
        // Đảm bảo thư mục log tồn tại
        if (!Directory.Exists(effectiveLogDirectory))
        {
            Directory.CreateDirectory(effectiveLogDirectory);
        }

        // Tạo tên file log với timestamp và version
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // Thay thế các placeholder trong template
        string logFileName = Path.Combine(effectiveLogDirectory, 
            fileTemplate.Replace("{timestamp}", timestamp).Replace("{version}", currentAgentVersion));

        // Cấu hình Serilog
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console(LogEventLevel.Information, 
                outputTemplate: outputTemplate)
            .WriteTo.File(logFileName,
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate);

        // Tạo logger và factory
        var serilogLogger = loggerConfiguration.CreateLogger();
        var loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
        
        // Tạo logger cho CMSUpdater
        return loggerFactory.CreateLogger("CMSUpdater");
    }
} 