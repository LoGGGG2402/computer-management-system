using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using CMSAgent.Shared.Constants;
using System.Security.Principal;

namespace CMSAgent.Shared.Logging
{
    /// <summary>
    /// Centralized configuration utility for Serilog logging infrastructure.
    /// Provides standardized logging setup with file output, console output, and Windows Event Log integration.
    /// Supports different logging levels and output templates for various deployment scenarios.
    /// </summary>
    /// <remarks>
    /// This class handles the complete Serilog configuration including:
    /// - File-based logging with automatic rotation and retention policies
    /// - Console logging for debug scenarios with colored output
    /// - Windows Event Log integration for production monitoring
    /// - Thread-safe logging with context enrichment
    /// </remarks>
    public static class SerilogConfigurator
    {
        /// <summary>
        /// Standard output template used across all logging sinks for consistent log formatting.
        /// Includes timestamp, log level, source context, thread ID, message, and exception details.
        /// </summary>
        private static readonly string OutputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{ThreadId}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Configures and initializes the global Serilog logger with multiple output sinks.
        /// Sets up file logging, optional console logging, and Windows Event Log integration.
        /// </summary>
        /// <param name="configuration">Application configuration containing Serilog settings from appsettings.json</param>
        /// <param name="agentProgramDataPath">Base directory path for storing log files and application data</param>
        /// <param name="logFilePrefix">Prefix string used for naming log files to distinguish different components</param>
        /// <param name="runningInDebugMode">Flag indicating whether to enable console logging for development scenarios</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration or paths are null or empty</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when insufficient permissions to create log directories</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified agent program data path does not exist</exception>
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

        /// <summary>
        /// Configures file-based logging with automatic rotation and retention policies.
        /// Creates log directory if it doesn't exist and sets up daily log file rotation.
        /// </summary>
        /// <param name="loggerConfiguration">The Serilog logger configuration to add file sink to</param>
        /// <param name="agentProgramDataPath">Base directory path where log subdirectory will be created</param>
        /// <param name="logFilePrefix">Prefix for log file names to distinguish different application components</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when unable to create or access log directory</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when insufficient permissions to write to log directory</exception>
        /// <exception cref="IOException">Thrown when file system errors occur during log file operations</exception>
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
                throw;
            }
        }

        /// <summary>
        /// Generates the appropriate log file path based on the component type and naming conventions.
        /// Handles special formatting for updater components with timestamp-based naming.
        /// </summary>
        /// <param name="logDirectory">Directory where log files will be stored</param>
        /// <param name="logFilePrefix">Component-specific prefix for the log file name</param>
        /// <returns>Complete file path for the log file including directory and formatted filename</returns>
        /// <remarks>
        /// Uses different naming patterns:
        /// - Updater logs: prefix + current datetime + .log extension
        /// - Standard logs: prefix + date format placeholder + .log extension for daily rotation
        /// </remarks>
        private static string GetLogFilePath(string logDirectory, string logFilePrefix)
        {
            if (logFilePrefix == AgentConstants.UpdaterLogFilePrefix)
            {
                return Path.Combine(logDirectory, $"{logFilePrefix}{DateTime.Now.ToString(AgentConstants.UpdaterLogFileDateTimeFormat)}.log");
            }
            return Path.Combine(logDirectory, $"{logFilePrefix}{AgentConstants.LogFileDateFormat}.log");
        }

        /// <summary>
        /// Configures Windows Event Log integration for system-level logging and monitoring.
        /// Automatically creates event source if running with administrator privileges.
        /// Only configures on Windows platforms and gracefully handles permission issues.
        /// </summary>
        /// <param name="loggerConfiguration">The Serilog logger configuration to add event log sink to</param>
        /// <remarks>
        /// Event Log configuration details:
        /// - Only available on Windows operating systems
        /// - Requires administrator privileges to create new event sources
        /// - Logs warning-level and above messages to Application event log
        /// - Uses simplified output template without timestamp (Event Log provides its own)
        /// </remarks>
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
                    manageEventSource: false,
                    outputTemplate: "{Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Warning
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure Windows Event Log");
            }
        }

        /// <summary>
        /// Determines if the current process is running with administrator privileges on Windows.
        /// Used to check if Event Log source creation is possible.
        /// </summary>
        /// <returns>True if running as administrator, false otherwise or if check fails</returns>
        /// <remarks>
        /// This method is Windows-specific and will return false on other platforms.
        /// Uses Windows identity and security principal to determine administrative privileges.
        /// </remarks>
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
        /// Properly closes and flushes the Serilog logger to ensure all pending log entries are written.
        /// Should be called during application shutdown to prevent log message loss.
        /// </summary>
        /// <remarks>
        /// This method blocks until all pending log writes are completed.
        /// Essential for ensuring log integrity during application termination.
        /// </remarks>
        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
