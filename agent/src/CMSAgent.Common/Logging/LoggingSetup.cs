using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Serilog;
using Serilog.Events;

namespace CMSAgent.Common.Logging;

/// <summary>
/// Utility class for common logging configuration used by both CMSAgent and CMSUpdater
/// </summary>
public static class LoggingSetup
{
    // Log directory is configured in SetupScript.iss
    private static readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "CMSAgent",
        "logs");
        
    /// <summary>
    /// Creates and configures the logger
    /// </summary>
    /// <param name="configuration">Configuration from appsettings.json</param>
    /// <returns>Configured ILogger</returns>
    public static ILogger CreateLogger(IConfiguration configuration)
    {
        // Get values from configuration
        string applicationName = configuration["Application:Name"] ?? "CMSApplication";
        string currentVersion = configuration["Application:Version"] ?? "1.0.0";
        string fileTemplate = "{application}_{timestamp}_{version}.log";
        string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {Message:lj}{NewLine}{Exception}";
        
        // Get LogLevel from configuration
        string logLevel = configuration["Logging:LogLevel:Default"] ?? "Information";
        // Get log configuration from appsettings.json
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

        // Get templates from configuration
        fileTemplate = configuration["Logging:File:FileTemplate"] ?? fileTemplate;
        outputTemplate = configuration["Logging:File:OutputTemplate"] ?? outputTemplate;
        
        // Ensure log directory exists
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        // Create log filename with timestamp and version
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // Replace placeholders in template
        string logFileName = Path.Combine(_logDirectory, 
            fileTemplate.Replace("{application}", applicationName.ToLower())
                      .Replace("{timestamp}", timestamp)
                      .Replace("{version}", currentVersion));

        // Configure Serilog
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console(LogEventLevel.Information, 
                outputTemplate: outputTemplate)
            .WriteTo.File(logFileName,
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate);

        // Create logger and factory
        var serilogLogger = loggerConfiguration.CreateLogger();
        var loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
        
        // Create logger for application
        return loggerFactory.CreateLogger(applicationName);
    }
}