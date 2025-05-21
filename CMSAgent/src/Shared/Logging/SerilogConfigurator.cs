using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using CMSAgent.Shared.Constants;

namespace CMSAgent.Shared.Logging
{
    /// <summary>
    /// Provides centralized configuration for Serilog logging.
    /// </summary>
    public static class SerilogConfigurator
    {
        private static readonly string OutputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{ThreadId}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Configures the Serilog logger with file, console, and event log sinks.
        /// </summary>
        /// <param name="configuration">The configuration object to read settings from appsettings.json.</param>
        /// <param name="agentProgramDataPath">The path to the agent's ProgramData folder.</param>
        /// <param name="logFilePrefix">The prefix for log file names.</param>
        /// <param name="runningInDebugMode">Indicates whether the agent is running in debug mode.</param>
        public static void Configure(IConfiguration configuration, string agentProgramDataPath, string logFilePrefix, bool runningInDebugMode = false)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId();

            loggerConfiguration.ReadFrom.Configuration(configuration);

            if (runningInDebugMode)
            {
                loggerConfiguration.WriteTo.Console(
                    outputTemplate: OutputTemplate,
                    theme: AnsiConsoleTheme.Code,
                    restrictedToMinimumLevel: LogEventLevel.Debug
                );
            }

            string logDirectory = Path.Combine(agentProgramDataPath, AgentConstants.LogsSubFolderName);
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logFilePathFormat = Path.Combine(logDirectory, $"{logFilePrefix}{AgentConstants.LogFileDateFormat}.log");
                if (logFilePrefix == AgentConstants.UpdaterLogFilePrefix)
                {
                     logFilePathFormat = Path.Combine(logDirectory, $"{logFilePrefix}{DateTime.Now.ToString(AgentConstants.UpdaterLogFileDateTimeFormat)}.log");
                }

                loggerConfiguration.WriteTo.File(
                    logFilePathFormat,
                    outputTemplate: OutputTemplate,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File log configuration error: {ex.Message}");
            }

            try
            {
                string eventLogSource = AgentConstants.ServiceName;
                if (!System.Diagnostics.EventLog.SourceExists(eventLogSource))
                {
                    System.Diagnostics.EventLog.CreateEventSource(eventLogSource, "Application");
                }
                loggerConfiguration.WriteTo.EventLog(
                    source: eventLogSource,
                    manageEventSource: true,
                    outputTemplate: "{Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Warning
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Event Log configuration error: {ex.Message}");
            }

            Log.Logger = loggerConfiguration.CreateLogger();
            Log.Information("Serilog has been configured. Debug mode: {IsDebugMode}", runningInDebugMode);
        }

        /// <summary>
        /// Closes and flushes the Serilog logger. Should be called when the application ends.
        /// </summary>
        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
