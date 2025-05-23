using Microsoft.Extensions.Options;
using Serilog;
using System.CommandLine; // For System.CommandLine
using System.CommandLine.Invocation;
using CMSAgent.Service.Workers;
using CMSAgent.Service.Orchestration;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Configuration.Manager;
using CMSAgent.Shared.Logging;
using CMSAgent.Shared.Constants;
using CMSAgent.Shared; // For IVersionIgnoreManager, VersionIgnoreManager
using CMSAgent.Service.Security;
using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Communication.WebSocket;
using CMSAgent.Service.Monitoring;
using CMSAgent.Service.Commands;
using CMSAgent.Service.Commands.Factory;
using CMSAgent.Service.Commands.Handlers;
using CMSAgent.Service.Update;
using System.Runtime.Versioning;
using System.Text.Json; // For Polly retry policies
using Serilog.Events;

namespace CMSAgent.Service
{
    [SupportedOSPlatform("windows")]
    public class Program
    {
        private static MutexManager? _mutexManager; // To release mutex on exit

        public static async Task<int> Main(string[] args)
        {
            // --- Initial configuration for Serilog (output to Console for early debugging) ---
            // Full configuration will be done after IConfiguration is loaded.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateBootstrapLogger(); // Temporary logger

            Log.Information("CMSAgent.Service is starting...");
            Log.Information("Command line arguments: {Args}", string.Join(" ", args));

            // --- Configure System.CommandLine ---
            var configureCommand = new Command("configure", "Run initial configuration process for Agent.");
            var debugCommand = new Command("debug", "Run Agent in debug mode (console) instead of Windows Service.");

            var rootCommand = new RootCommand("CMS Agent Service")
            {
                configureCommand,
                debugCommand
            };

            rootCommand.SetHandler(async (InvocationContext context) =>
            {
                // Default to running as service if no subcommand is called
                await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: false, isConfigureModeFromArg: false);
            });

            configureCommand.SetHandler(async () =>
            {
                Log.Information("Configure command handler called");
                await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: false, isConfigureModeFromArg: true);
            });

            debugCommand.SetHandler(async () =>
            {
                await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: true, isConfigureModeFromArg: false);
            });

            // Check if we have any command line arguments
            if (args.Length > 0)
            {
                Log.Information("Processing command line arguments: {Args}", string.Join(" ", args));
                // If we have args, use System.CommandLine to process them
                return await rootCommand.InvokeAsync(args);
            }
            else
            {
                // If no args, run as service
                Log.Information("No command line arguments. Running in default Windows Service mode.");
                return await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: false, isConfigureModeFromArg: false);
            }
        }

        private static async Task<int> RunAsServiceOrDebugAsync(string[] args, bool isDebugModeFromArg, bool isConfigureModeFromArg)
        {
            IHost? host = null;
            try
            {
                var hostBuilder = CreateHostBuilder(args, isDebugModeFromArg, isConfigureModeFromArg);
                Log.Information("Host builder has been created.");
                host = hostBuilder.Build();

                Log.Information("Host has been built.");

                // --- Check Mutex after ILogger and AppSettings are DI ---
                // MutexManager needs AppSettings to get AgentInstanceGuid
                _mutexManager = host.Services.GetRequiredService<MutexManager>();
                if (!_mutexManager.RequestOwnership())
                {
                    Log.Fatal("Another instance of CMSAgent.Service is already running. Exiting application.");
                    // No need to call ReleaseOwnership since we didn't acquire it
                    return 1; // Exit code for error
                }

                // --- If in configure mode ---
                if (isConfigureModeFromArg)
                {
                    Log.Information("Running in configuration mode (configure)...");
                    var orchestrator = host.Services.GetRequiredService<IAgentCoreOrchestrator>();
                    bool configSuccess = await orchestrator.RunInitialConfigurationAsync();
                    if (configSuccess)
                    {
                        Log.Information("Configuration completed successfully. Agent will need to be started (as a service) to operate.");
                        // Consider: should we automatically start the service after successful configuration? (Installer typically handles this)
                    }
                    else
                    {
                        Log.Error("Configuration process failed.");
                    }
                    // Whether successful or failed, configure mode only runs once then exits.
                    return configSuccess ? 0 : 1;
                }

                // --- Run Host (Service or Debug Console) ---
                Log.Information("Starting Host...");
                await host.RunAsync();
                Log.Information("Host has stopped.");
                return 0; // Success
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Host operation cancelled.");
                return 0; // Not a critical error
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error occurred during Host running or initialization.");
                return 1; // Exit code for error
            }
            finally
            {
                _mutexManager?.ReleaseOwnership(); // Ensure Mutex is released
                _mutexManager?.Dispose();

                if (host is IAsyncDisposable asyncDisposableHost)
                {
                    await asyncDisposableHost.DisposeAsync();
                }
                else
                {
                    host?.Dispose();
                }
                SerilogConfigurator.CloseAndFlush(); // Ensure all logs are written before exit
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, bool isDebugMode, bool isConfigureMode) =>
            Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory) // Ensure correct root path when running as service
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;
                    config.SetBasePath(AppContext.BaseDirectory); // Important for finding appsettings.json
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                    Log.Information("Configuration loaded from appsettings.json. Environment: {Environment}", env.EnvironmentName);
                })
                .ConfigureLogging((hostingContext, loggingBuilder) =>
                {
                    // Clear default logging providers if you want to use only Serilog
                    loggingBuilder.ClearProviders();
                    // Serilog will be configured in UseSerilog
                })
                .UseSerilog((hostingContext, services, loggerConfiguration) =>
                {
                    // Configure Serilog with basic settings first
                    loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration)
                        .Enrich.FromLogContext()
                        .Enrich.WithThreadId();

                    // Always enable console logging with debug level
                    loggerConfiguration.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        restrictedToMinimumLevel: LogEventLevel.Debug
                    );

                    // Get the program data path from configuration
                    var programDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        AgentConstants.AgentProgramDataFolderName
                    );

                    // Configure file logging
                    var logDirectory = Path.Combine(programDataPath, AgentConstants.LogsSubFolderName);
                    Directory.CreateDirectory(logDirectory);
                    var logFilePath = Path.Combine(logDirectory, $"{AgentConstants.AgentLogFilePrefix}{DateTime.Now:yyyyMMdd}.log");

                    loggerConfiguration.WriteTo.File(
                        logFilePath,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true
                    );

                    Log.Information("Serilog has been fully configured.");
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // --- Register Configuration ---
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));
                    // Ensure AppSettings is loaded and has AgentInstanceGuid before MutexManager is created
                    // Validate AppSettings, especially AgentInstanceGuid
                    var appSettings = hostContext.Configuration.GetSection("AppSettings").Get<AppSettings>();
                    Log.Information("AppSettings: {AppSettings}", JsonSerializer.Serialize(appSettings));
                    if (appSettings == null || string.IsNullOrWhiteSpace(appSettings.AgentInstanceGuid))
                    {
                        // Log using temporary logger if ILogger is not ready
                        Log.Warning("AgentInstanceGuid not found or empty in appsettings.json. " +
                                   "Mutex will use a default GUID (less secure) or application may not start properly.");
                        // Consider throwing exception here if AgentInstanceGuid is mandatory.
                        // If not thrown, MutexManager will throw when there's no GUID.
                        // For simplicity, we'll let MutexManager handle it.
                    }

                    services.AddSingleton<IRuntimeConfigManager, RuntimeConfigManager>();

                    // --- Register Shared Services ---
                    services.AddSingleton<IVersionIgnoreManager>(provider =>
                        new VersionIgnoreManager(
                            provider.GetRequiredService<IRuntimeConfigManager>().GetAgentProgramDataPath(),
                            provider.GetRequiredService<ILogger<VersionIgnoreManager>>()
                        )
                    );

                    // --- Register Security ---
                    services.AddSingleton<IDpapiProtector, DpapiProtector>();
                    services.AddSingleton<MutexManager>(); // Singleton because it manages global resource

                    // --- Register Communication ---
                    services.AddHttpClient(); // Register IHttpClientFactory
                    services.AddSingleton<IAgentApiClient, AgentApiClient>(); // Change to singleton
                    services.AddHttpClient<AgentApiClient>() // Add HttpClient configuration
                        .AddPolicyHandler((serviceProvider, request) =>
                        {
                            var settings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value.HttpRetryPolicy;
                            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PollyHttpRetry");
                            return RetryPolicies.GetHttpRetryPolicy(settings, logger);
                        });

                    services.AddSingleton<IAgentSocketClient, AgentSocketClient>();

                    // --- Register Monitoring ---
                    services.AddTransient<IHardwareCollector, HardwareCollector>(); // Transient because typically used only once when needed
                    services.AddSingleton<IResourceMonitor, ResourceMonitor>(); // Singleton because runs continuously in background

                    // --- Register Commands ---
                    services.AddSingleton<ICommandHandlerFactory, CommandHandlerFactory>();
                    services.AddTransient<ConsoleCommandHandler>();
                    services.AddTransient<SystemActionCommandHandler>();
                    services.AddTransient<SoftwareInstallCommandHandler>();
                    services.AddTransient<SoftwareUninstallCommandHandler>();
                    services.AddTransient<GetLogsCommandHandler>();
                    // Register other ICommandHandlers here

                    services.AddSingleton<CommandQueue>();

                    // --- Register Update ---
                    // Func<Task> requestServiceShutdown will be created and passed from AgentCoreOrchestrator or AgentWorker
                    // Currently, we'll let AgentUpdateManager receive IHostApplicationLifetime to request stop itself
                    services.AddSingleton<IAgentUpdateManager>(provider =>
                        new AgentUpdateManager(
                            provider.GetRequiredService<ILogger<AgentUpdateManager>>(),
                            provider.GetRequiredService<IOptions<AppSettings>>(),
                            provider.GetRequiredService<IAgentApiClient>(),
                            provider.GetRequiredService<IVersionIgnoreManager>(),
                            provider.GetRequiredService<IRuntimeConfigManager>(),
                            async () => // This is Func<Task> requestServiceShutdown
                            {
                                var lifetime = provider.GetRequiredService<IHostApplicationLifetime>();
                                provider.GetRequiredService<ILogger<AgentUpdateManager>>().LogInformation("Requesting service stop from AgentUpdateManager...");
                                lifetime.StopApplication(); // Request host stop
                                await Task.CompletedTask;
                            }
                        )
                    );

                    // --- Register Orchestration & Worker ---
                    services.AddSingleton<IAgentCoreOrchestrator, AgentCoreOrchestrator>();
                    services.AddHostedService<AgentWorker>(); // Register main worker

                    Log.Information("All services have been registered.");
                })
                .ConfigureHostOptions(options =>
                {
                    // Set timeout for service shutdown
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30); // Example: 30 seconds
                    Log.Information("Host options have been configured.");
                })
                .UseWindowsService(options => // Configure to run as Windows Service
                {
                    options.ServiceName = AgentConstants.ServiceName;
                    Log.Information("Windows Service options have been configured.");
                });
    }
}
