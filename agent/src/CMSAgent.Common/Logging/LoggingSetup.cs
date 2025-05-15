using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Serilog;
using Serilog.Events;

namespace CMSAgent.Common.Logging;

/// <summary>
/// Lớp tiện ích cấu hình logging chung cho cả CMSAgent và CMSUpdater
/// </summary>
public static class LoggingSetup
{
    // Thư mục logs được thiết lập trong SetupScript.iss
    private static readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "CMSAgent",
        "logs");
        
    /// <summary>
    /// Tạo và cấu hình logger
    /// </summary>
    /// <param name="configuration">Cấu hình từ appsettings.json</param>
    /// <returns>ILogger đã cấu hình</returns>
    public static ILogger CreateLogger(IConfiguration configuration)
    {
        // Lấy các giá trị từ configuration
        string applicationName = configuration["Application:Name"] ?? "CMSApplication";
        string currentVersion = configuration["Application:Version"] ?? "1.0.0";
        string fileTemplate = "{application}_{timestamp}_{version}.log";
        string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {Message:lj}{NewLine}{Exception}";
        
        // Lấy LogLevel từ cấu hình
        string logLevel = configuration["Logging:LogLevel:Default"] ?? "Information";
        // Lấy cấu hình log từ appsettings.json
        LogEventLevel minimumLevel = logLevel.ToLower() switch
        {
            "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };

        // Lấy các template từ cấu hình
        fileTemplate = configuration["Logging:File:FileTemplate"] ?? fileTemplate;
        outputTemplate = configuration["Logging:File:OutputTemplate"] ?? outputTemplate;
        
        // Đảm bảo thư mục log tồn tại
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        // Tạo tên file log với timestamp và version
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // Thay thế các placeholder trong template
        string logFileName = Path.Combine(_logDirectory, 
            fileTemplate.Replace("{application}", applicationName.ToLower())
                      .Replace("{timestamp}", timestamp)
                      .Replace("{version}", currentVersion));

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
        
        // Tạo logger cho ứng dụng
        return loggerFactory.CreateLogger(applicationName);
    }
} 