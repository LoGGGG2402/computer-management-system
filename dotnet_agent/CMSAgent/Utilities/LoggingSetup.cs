using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;

namespace CMSAgent.Utilities
{
    /// <summary>
    /// Provides static methods for configuring Serilog logging and global exception handling.
    /// </summary>
    public static class LoggingSetup
    {
        /// <summary>
        /// Configures Serilog based on application settings and returns a configured logger factory.
        /// </summary>
        /// <param name="appConfiguration">The application's configuration, typically from appsettings.json.</param>
        /// <param name="appName">The name of the application, used for creating the log directory.</param>
        /// <returns>An <see cref="ILoggerFactory"/> configured with Serilog.</returns>
        public static ILoggerFactory ConfigureSerilog(IConfiguration appConfiguration, string appName = "CMSAgent")
        {
            var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), appName, "Logs");
            Directory.CreateDirectory(logDirectory);

            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(appConfiguration)
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                .WriteTo.File(
                    Path.Combine(logDirectory, "agent-.log"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    retainedFileCountLimit: 7,
                    shared: true
                );
            
            Log.Logger = loggerConfiguration.CreateLogger();

            Log.Information("Logging initialized for {AppName}. Log directory: {LogDirectory}", appName, logDirectory);

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog(Log.Logger);

            return loggerFactory;
        }

        /// <summary>
        /// Sets up a global handler for unhandled exceptions to ensure they are logged.
        /// </summary>
        public static void SetupGlobalExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => 
            {
                var exception = args.ExceptionObject as Exception;
                Log.Fatal(exception, "Unhandled application error: {ErrorMessage}", exception?.Message);
                Log.CloseAndFlush();
            };
        }
    }
}
