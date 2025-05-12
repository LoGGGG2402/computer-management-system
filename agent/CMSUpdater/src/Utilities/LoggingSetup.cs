using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace CMSUpdater.Utilities
{
    public static class LoggingSetup
    {
        public static Microsoft.Extensions.Logging.ILogger<T> ConfigureUpdaterLogger<T>(string logDirectory, string logFilePrefix)
        {
            // Ensure the log directory exists
            Directory.CreateDirectory(logDirectory);

            // Filename format: prefix_YYYYMMDD_HHMMSS.log as per Standard.md VIII.1
            var logFilePath = Path.Combine(logDirectory, $"{logFilePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose() // Capture all details during update
                .WriteTo.File(logFilePath, 
                                restrictedToMinimumLevel: LogEventLevel.Verbose, // Log everything to the file
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                                fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB limit per file
                                rollOnFileSizeLimit: true, // Create new file if limit is reached
                                retainedFileCountLimit: 5) // Keep last 5 log files
                .CreateLogger();

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(serilogLogger);
            });

            return loggerFactory.CreateLogger<T>();
        }
    }
}