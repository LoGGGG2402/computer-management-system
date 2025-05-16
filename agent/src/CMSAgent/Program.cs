using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;
using System.Xml.Linq;
using System.Collections.Generic;
using CMSAgent.Cli;
using CMSAgent.Cli.Commands;
using CMSAgent.Commands;
using CMSAgent.Commands.Handlers;
using CMSAgent.Common.Models;
using CMSAgent.Common.Logging;
using CMSAgent.Common.Interfaces;
using CMSAgent.Configuration;
using CMSAgent.Core;
using CMSAgent.Monitoring;
using CMSAgent.Security;
using CMSAgent.Update;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Registry;
using Polly.Timeout;
using Serilog;
using CMSAgent.Communication;

namespace CMSAgent
{
    public class Program
    {
        private static bool _shouldRunAsWindowsService = true;
        private static string _applicationName = "CMSAgent";
        private static string _version = "1.0.0";
        private static readonly CancellationTokenSource _shutdownCts = new();

        public static async Task<int> Main(string[] args)
        {
            // Read information from csproj file
            LoadProjectInfo();

            // Configure Serilog before initializing the host
            ConfigureLogging();

            try
            {
                // Đăng ký xử lý tín hiệu shutdown
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _shutdownCts.Cancel();
                };

                IHost host = CreateHostBuilder(args).Build();
                
                // Process CLI commands
                var cliHandler = host.Services.GetRequiredService<CliHandler>();
                int cliResult = await cliHandler.HandleAsync(args);
                if (cliHandler.IsCliCommandExecuted)
                {
                    return cliResult; // CLI command has been processed
                }

                // Check if running in debug mode
                if (args.Length > 0 && args[0].Equals("debug", StringComparison.OrdinalIgnoreCase))
                {
                    return await RunInDebugMode(host);
                }
                
                // Run the host (either as a Windows Service or console app)
                await host.RunAsync(_shutdownCts.Token);
                
                return 0;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Application shutdown requested");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application encountered a critical error and must terminate");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void LoadProjectInfo()
        {
            try
            {
                string projectFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CMSAgent.csproj");
                
                // If the file doesn't exist in the current directory, try to find it in the parent directory
                if (!File.Exists(projectFilePath))
                {
                    string sourcePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                    
                    // Check if sourcePath is not null before calling Directory.GetParent
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        string? solutionDir = Directory.GetParent(sourcePath)?.Parent?.FullName;
                        
                        if (solutionDir != null)
                        {
                            string srcPath = Path.Combine(solutionDir, "src", "CMSAgent");
                            projectFilePath = Path.Combine(srcPath, "CMSAgent.csproj");
                        }
                    }
                }

                if (File.Exists(projectFilePath))
                {
                    XDocument doc = XDocument.Load(projectFilePath);
                    var propertyGroups = doc.Descendants("PropertyGroup");
                    
                    foreach (var propertyGroup in propertyGroups)
                    {
                        var description = propertyGroup.Element("Description");
                        if (description != null && !string.IsNullOrWhiteSpace(description.Value))
                        {
                            _applicationName = description.Value;
                        }
                        
                        var version = propertyGroup.Element("Version");
                        if (version != null && !string.IsNullOrWhiteSpace(version.Value))
                        {
                            _version = version.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Use default values if unable to read from file
                Console.WriteLine($"Unable to read information from project file: {ex.Message}");
            }
        }

        private static void ConfigureLogging()
        {
            // Read configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Get information from the assembly
            var assembly = Assembly.GetExecutingAssembly();
            
            // Add assembly information to configuration
            var configDictionary = new Dictionary<string, string?>
            {
                { "Application:Name", assembly.GetName().Name ?? _applicationName },
                { "Application:Version", assembly.GetName().Version?.ToString() ?? _version }
            };

            // Create a new configuration combining current config and assembly information
            var combinedConfig = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddInMemoryCollection(configDictionary)
                .Build();

            // Initialize logger with combined configuration
            var logger = LoggingSetup.CreateLogger(combinedConfig);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            logger.LogInformation("Application {ApplicationName} v{Version} is starting...", _applicationName, _version);
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    _ = config.SetBasePath(env.ContentRootPath)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables("CMSAGENT_");

                    // Add project information to configuration
                    _ = config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"Application:Name", _applicationName},
                        {"Application:Version", _version},
                        {"CMSAgent:AppName", _applicationName},
                        {"CMSAgent:Version", _version}
                    });

                    // Create data directory if needed
                    var configuration = config.Build();
                    var dataDir = configuration["CMSAgent:DataDirectoryPath"] ?? "AppData";
                    var dataPath = Path.Combine(env.ContentRootPath, dataDir);
                    if (!Directory.Exists(dataPath))
                    {
                        _ = Directory.CreateDirectory(dataPath);
                    }
                    
                    // Determine if should run as a Windows Service
                    _shouldRunAsWindowsService = !(args.Length > 0 && args[0].Equals("debug", StringComparison.OrdinalIgnoreCase));
                })
                .UseSerilog() // Use previously configured Serilog
                .ConfigureServices(ConfigureServices)
                .ConfigureHostOptions(options =>
                {
                    // Cấu hình timeout cho quá trình shutdown
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
                });

            // Configure Windows Service if needed
            if (_shouldRunAsWindowsService && OperatingSystem.IsWindows())
            {
                _ = hostBuilder.UseWindowsService(options =>
                {
                    options.ServiceName = _applicationName;
                });
            }

            return hostBuilder;
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Register configuration from appsettings
            _ = services.AddOptions<CmsAgentSettingsOptions>()
                .Bind(context.Configuration.GetSection("CMSAgent"))
                .ValidateDataAnnotations();

            _ = services.AddOptions<AgentSpecificSettingsOptions>()
                .Bind(context.Configuration.GetSection("CMSAgent:AgentSpecificSettings"))
                .ValidateDataAnnotations();

            _ = services.AddOptions<HttpClientSettingsOptions>()
                .Bind(context.Configuration.GetSection("CMSAgent:HttpClientSettings"))
                .ValidateDataAnnotations();

            // Register Polly policies
            var policyRegistry = new PolicyRegistry();
            
            // Retry policy
            var httpRetryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: context.Configuration.GetValue<int>("CMSAgent:HttpClientSettings:MaxRetryAttempts", 3),
                    sleepDurationProvider: (retryAttempt) => 
                        TimeSpan.FromMilliseconds(
                            Math.Pow(2, retryAttempt) * context.Configuration.GetValue<int>("CMSAgent:HttpClientSettings:RetryDelayMilliseconds", 200)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        // Log retry attempt
                        Log.Warning("Retrying HTTP request attempt {RetryAttempt} after {RetryInterval}ms",
                            retryAttempt, timespan.TotalMilliseconds);
                    });
            
            // Circuit Breaker policy
            var httpCircuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay) =>
                    {
                        Log.Warning("Circuit Breaker opened for {BreakDelay}s due to continuous errors", breakDelay.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        Log.Information("Circuit Breaker closed, connection is functioning again");
                    });
            
            // Timeout policy
            var httpTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                context.Configuration.GetValue<int>("CMSAgent:HttpClientSettings:TimeoutSeconds", 30));
            
            // Add policies to registry
            policyRegistry.Add("HttpRetryPolicy", httpRetryPolicy);
            policyRegistry.Add("HttpCircuitBreakerPolicy", httpCircuitBreakerPolicy);
            policyRegistry.Add("HttpTimeoutPolicy", httpTimeoutPolicy);

            // Add policy registry to services
            _ = services.AddSingleton<IReadOnlyPolicyRegistry<string>>(policyRegistry);

            // Register HttpClient with Polly
            _ = services.AddHttpClient(HttpClientNames.ApiClient, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<CmsAgentSettingsOptions>>();
                client.BaseAddress = new Uri(options.Value.ServerUrl);
                client.Timeout = TimeSpan.FromSeconds(options.Value.HttpClientSettings.RequestTimeoutSec);
                client.DefaultRequestHeaders.Add("User-Agent", $"CMSAgent/{options.Value.Version}");
            })
            .AddPolicyHandler(httpRetryPolicy)
            .AddPolicyHandler(httpCircuitBreakerPolicy)
            .AddPolicyHandler(httpTimeoutPolicy);

            // Register singleton services
            _ = services.AddSingleton<StateManager>();
            _ = services.AddSingleton<IConfigLoader, ConfigLoader>();
            _ = services.AddSingleton<IHttpClientWrapper, HttpClientWrapper>();
            _ = services.AddSingleton<IWebSocketConnector, WebSocketConnector>();
            _ = services.AddSingleton<SystemMonitor>();
            _ = services.AddSingleton<HardwareInfoCollector>();
            _ = services.AddSingleton<UpdateHandler>();
            _ = services.AddSingleton<CommandExecutor>();

            // Register singleton mutex
            _ = services.AddSingleton<SingletonMutex>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CmsAgentSettingsOptions>>();
                var logger = provider.GetRequiredService<ILogger<SingletonMutex>>();
                return new SingletonMutex(options.Value.AppName, logger);
            });

            // Register Command Handlers
            _ = services.AddTransient<ConsoleCommandHandler>();
            _ = services.AddTransient<SystemActionCommandHandler>();

            // Register CLI Handlers
            _ = services.AddTransient<ServiceUtils>();
            _ = services.AddTransient<ConfigureCommand>();
            _ = services.AddTransient<StartCommand>();
            _ = services.AddTransient<StopCommand>();
            _ = services.AddTransient<UninstallCommand>();
            _ = services.AddTransient<InstallCommand>();
            _ = services.AddSingleton<CliHandler>();

            // Register main service
            _ = services.AddHostedService<AgentService>();

            // Security and other services
            _ = services.AddSingleton<TokenProtector>();
            _ = services.AddSingleton<CommandHandlerFactory>();

            // Đăng ký các service cần graceful shutdown
            services.AddHostedService<GracefulShutdownService>();
        }

        private static async Task<int> RunInDebugMode(IHost host)
        {
            Console.WriteLine("--------------------------------");
            Console.WriteLine("| CMSAgent running in DEBUG mode |");
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Press CTRL+C to stop.\n");

            try
            {
                var agentOperations = new AgentOperations(
                    host.Services.GetRequiredService<ILogger<AgentService>>(),
                    host.Services.GetRequiredService<StateManager>(),
                    host.Services.GetRequiredService<IConfigLoader>(),
                    host.Services.GetRequiredService<IWebSocketConnector>(),
                    host.Services.GetRequiredService<SystemMonitor>(),
                    host.Services.GetRequiredService<CommandExecutor>(),
                    host.Services.GetRequiredService<UpdateHandler>(),
                    host.Services.GetRequiredService<SingletonMutex>(),
                    host.Services.GetRequiredService<TokenProtector>(),
                    host.Services.GetRequiredService<IOptions<AgentSpecificSettingsOptions>>().Value,
                    host.Services.GetRequiredService<HardwareInfoCollector>(),
                    host.Services.GetRequiredService<IHttpClientWrapper>()
                );

                Log.Information("CMSAgent has started in debug mode");

                // Initialize agent operations
                await agentOperations.InitializeAsync();

                // Create a cancellation token source for CTRL+C
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                // Start agent operations
                await agentOperations.StartAsync(cts.Token);

                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error running in debug mode");
                return 1;
            }
        }
    }

    /// <summary>
    /// Names of HttpClients registered in DI
    /// </summary>
    public static class HttpClientNames
    {
        /// <summary>
        /// HttpClient used for API requests
        /// </summary>
        public const string ApiClient = "ApiClient";
        
        /// <summary>
        /// HttpClient used for large files
        /// </summary>
        public const string DownloadClient = "DownloadClient";
    }

    public class GracefulShutdownService(
        ILogger<GracefulShutdownService> logger,
        IHostApplicationLifetime hostLifetime) : BackgroundService
    {
        private readonly ILogger<GracefulShutdownService> _logger = logger;
        private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Đăng ký xử lý sự kiện shutdown
                _hostLifetime.ApplicationStopping.Register(() =>
                {
                    _logger.LogInformation("Application is stopping. Starting graceful shutdown...");
                    // Thực hiện các tác vụ cleanup cần thiết
                });

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Graceful shutdown service is stopping");
            }
        }
    }
}
