using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using CMSAgent.Shared.Constants;
using System.Security.Principal;

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

            ConfigureFileLogging(loggerConfiguration, agentProgramDataPath, logFilePrefix);
            ConfigureEventLogging(loggerConfiguration);

            Log.Logger = loggerConfiguration.CreateLogger();
            Log.Information("Serilog has been configured. Debug mode: {IsDebugMode}", runningInDebugMode);
        }

        private static void ConfigureFileLogging(LoggerConfiguration loggerConfiguration, string agentProgramDataPath, string logFilePrefix)
        {
            string logDirectory = Path.Combine(agentProgramDataPath, AgentConstants.LogsSubFolderName);
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logFilePathFormat = GetLogFilePath(logDirectory, logFilePrefix);
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
                Log.Error(ex, "Failed to configure file logging. Log directory: {LogDirectory}", logDirectory);
                throw; // Rethrow to ensure logging configuration is properly handled
            }
        }

        private static string GetLogFilePath(string logDirectory, string logFilePrefix)
        {
            if (logFilePrefix == AgentConstants.UpdaterLogFilePrefix)
            {
                return Path.Combine(logDirectory, $"{logFilePrefix}{DateTime.Now.ToString(AgentConstants.UpdaterLogFileDateTimeFormat)}.log");
            }
            return Path.Combine(logDirectory, $"{logFilePrefix}{AgentConstants.LogFileDateFormat}.log");
        }

        private static void ConfigureEventLogging(LoggerConfiguration loggerConfiguration)
        {
            if (!OperatingSystem.IsWindows())
            {
                Log.Debug("Event logging is only supported on Windows");
                return;
            }

            try
            {
                string eventLogSource = AgentConstants.ServiceName;
                if (!System.Diagnostics.EventLog.SourceExists(eventLogSource))
                {
                    if (!IsAdministrator())
                    {
                        Log.Warning("Cannot create Event Log source {EventLogSource} - requires administrator privileges", eventLogSource);
                        return;
                    }

                    try
                    {
                        System.Diagnostics.EventLog.CreateEventSource(eventLogSource, "Application");
                        Log.Information("Created Event Log source: {EventLogSource}", eventLogSource);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to create Event Log source {EventLogSource}", eventLogSource);
                        return;
                    }
                }

                loggerConfiguration.WriteTo.EventLog(
                    source: eventLogSource,
                    manageEventSource: false, // Set to false since we manage it ourselves
                    outputTemplate: "{Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Warning
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure Windows Event Log");
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
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
