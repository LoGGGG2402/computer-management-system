using Serilog;
using Serilog.Events;
using System.Reflection;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace CMSAgent.Utilities
{
    public static class LoggingSetup
    {
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Initializes the logging system
        /// </summary>
        public static void Initialize(bool isDebugMode = false)
        {
            if (_isInitialized)
            {
                return;
            }

            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    return;
                }

                try
                {
                    // Create logs directory if it doesn't exist
                    string logDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "CMSAgent",
                        "logs");

                    Directory.CreateDirectory(logDirectory);

                    // Load configuration from appsettings.json
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();

                    // Get the current assembly version
                    string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

                    // Configure Serilog
                    var loggerConfiguration = new LoggerConfiguration()
                        .ReadFrom.Configuration(configuration)
                        .Enrich.WithProperty("Version", version);

                    if (isDebugMode)
                    {
                        loggerConfiguration.MinimumLevel.Debug();
                    }

                    Log.Logger = loggerConfiguration.CreateLogger();

                    Log.Information("Logging initialized. Version: {Version}, Debug Mode: {DebugMode}", 
                        version, isDebugMode);
                    
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    // Create a fallback logger if the main logger configuration fails
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Error()
                        .WriteTo.Console()
                        .CreateLogger();

                    Log.Error(ex, "Error initializing logging: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Gracefully shuts down logging system
        /// </summary>
        public static void Shutdown()
        {
            Log.Information("Shutting down logging system");
            Log.CloseAndFlush();
            _isInitialized = false;
        }
        
        /// <summary>
        /// Changes the minimum log level
        /// </summary>
        public static void SetLogLevel(LogEventLevel level)
        {
            try
            {
                // Load configuration from appsettings.json
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Get the current assembly version
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

                // Configure Serilog
                var loggerConfiguration = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .Enrich.WithProperty("Version", version)
                    .MinimumLevel.Is(level);

                Log.Logger = loggerConfiguration.CreateLogger();

                Log.Information("Log level changed to {Level}", level);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error changing log level: {Message}", ex.Message);
            }
        }
    }
}
