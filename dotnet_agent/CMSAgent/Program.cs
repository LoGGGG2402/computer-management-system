using Serilog;
using System.ServiceProcess;
using CMSAgent.Configuration;
using CMSAgent.Core;
using CMSAgent.SystemOperations;
using CMSAgent.UserInterface;
using CMSAgent.Communication;
using CMSAgent.Monitoring;
using CMSAgent.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMSAgent
{
    /// <summary>
    /// Main entry point for the CMSAgent application.
    /// Handles command-line arguments for service installation, uninstallation, configuration, and console mode execution.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            try
            {
                Log.Information("CMSAgent starting up with arguments: {Args}", string.Join(' ', args));

                var services = new ServiceCollection();
                ConfigureServices(services, configuration);
                var serviceProvider = services.BuildServiceProvider();

                bool isConsole = args.Contains("--console", StringComparer.OrdinalIgnoreCase);
                bool installService = args.Contains("--install", StringComparer.OrdinalIgnoreCase);
                bool uninstallService = args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase);
                bool configureAgent = args.Contains("--configure", StringComparer.OrdinalIgnoreCase);
                
                if (installService)
                {
                    Log.Information("Attempting to install service.");
                    var serviceInstaller = serviceProvider.GetRequiredService<WindowsServiceInstaller>();
                    await serviceInstaller.InstallServiceAsync();
                }
                else if (uninstallService)
                {
                    Log.Information("Attempting to uninstall service.");
                    var serviceInstaller = serviceProvider.GetRequiredService<WindowsServiceInstaller>();
                    await serviceInstaller.UninstallServiceAsync();
                }
                else if (configureAgent)
                {
                    Log.Information("Starting agent configuration.");
                    var configManager = serviceProvider.GetRequiredService<ConfigManager>();
                    var stateManager = serviceProvider.GetRequiredService<StateManager>();
                    await HandleConfigurationAsync(configManager, stateManager);
                }
                else if (isConsole || Environment.UserInteractive)
                {
                    Log.Information("Starting agent in console mode.");
                    var agent = serviceProvider.GetRequiredService<Agent>();
                    
                    var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (s, e) =>
                    {
                        Log.Information("Ctrl+C pressed. Shutting down console agent...");
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    await agent.StartAsync(cts.Token);
                    Log.Information("Agent started in console mode. Press Ctrl+C to exit.");
                    // Wait indefinitely until cancelled. Using try-catch to handle OperationCanceledException if the token is cancelled while Delay is active.
                    try
                    {
                        await Task.Delay(-1, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Information("Console agent shutdown requested via CancellationToken.");
                    }
                    
                    Log.Information("Console agent shutdown initiated.");
                    await agent.GracefulShutdownAsync();
                }
                else
                {
                    Log.Information("Starting agent as a Windows Service.");
                    var agentService = serviceProvider.GetRequiredService<ServiceBase>() as AgentService;
                    if (agentService != null)
                    {
                        ServiceBase.Run(agentService);
                    }
                    else
                    {
                        Log.Error("AgentService could not be resolved or is not a ServiceBase. Cannot start service.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CMSAgent terminated unexpectedly.");
            }
            finally
            {
                Log.Information("CMSAgent shutting down.");
                await Log.CloseAndFlushAsync();
            }
        }

        /// <summary>
        /// Configures the services for the application's dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">The application configuration.</param>
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(configuration);
            services.AddSingleton<ConfigManager>();
            services.AddSingleton<StateManager>();
            services.AddSingleton<FileUtilities>(); 
            services.AddSingleton<HttpClientService>();
            services.AddSingleton<SocketIOClientWrapper>();
            services.AddSingleton<ServerConnector>();
            services.AddSingleton<SystemMonitor>();
            services.AddSingleton<CommandExecutor>();
            services.AddSingleton<UpdateHandler>();
            services.AddSingleton<Agent>();
            
            services.AddSingleton<WindowsServiceInstaller>(provider => 
                new WindowsServiceInstaller(
                    provider.GetRequiredService<ILogger<WindowsServiceInstaller>>(),
                    AgentService.ServiceNameString,
                    "CMS Monitoring Agent",
                    "Provides system monitoring and management capabilities."
                )
            );

            services.AddSingleton<ServiceBase, AgentService>();
            services.AddSingleton(provider => (AgentService)provider.GetRequiredService<ServiceBase>());

            services.AddTransient<CommandHandlers.ICommandHandler, CommandHandlers.ConsoleCommandHandler>();
            services.AddTransient<CommandHandlers.ICommandHandler, CommandHandlers.SystemCommandHandler>();
        }

        /// <summary>
        /// Handles the interactive configuration process for the agent.
        /// Prompts the user for room position and server URL.
        /// </summary>
        /// <param name="configManager">The configuration manager instance.</param>
        /// <param name="stateManager">The state manager instance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task HandleConfigurationAsync(ConfigManager configManager, StateManager stateManager)
        {
            Console.WriteLine("Agent Configuration Setup:");
            var roomPosition = await ConsoleUI.PromptForRoomPositionAsync();
            if (roomPosition != null)
            {
                stateManager.UpdateRoomPosition(roomPosition); 
                ConsoleUI.DisplaySuccess($"Room position updated to: {roomPosition.RoomName} ({roomPosition.PosX}, {roomPosition.PosY}).");
            }
            else
            {
                ConsoleUI.DisplayWarning("Room position setup skipped or failed.");
            }
            string currentServerUrl = configManager.AgentConfig.ServerUrl;
            ConsoleUI.DisplayInfo($"Current Server URL: {currentServerUrl}");
            if (await ConsoleUI.ConfirmAsync("Do you want to change the Server URL?"))
            {
                Console.Write("Enter new Server URL: ");
                string? newServerUrl = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(newServerUrl) && Uri.TryCreate(newServerUrl, UriKind.Absolute, out _))
                {
                    // This assumes ConfigManager has a method to update and persist the ServerUrl.
                    // For example: await configManager.UpdateServerUrlAsync(newServerUrl);
                    // Since such a method isn't defined in the provided context, we log and inform the user.
                    ConsoleUI.DisplaySuccess($"Server URL input: {newServerUrl}. Update logic in ConfigManager needs to be implemented to persist this change.");
                    Log.Information("Server URL to be changed to {NewUrl} during interactive configuration. ConfigManager needs to persist this.", newServerUrl);
                }
                else
                {
                    ConsoleUI.DisplayError("Invalid Server URL entered. No changes made.");
                }
            }
            
            Console.WriteLine("Configuration process finished. Restart the agent for changes to take full effect if it was running or if critical settings were changed.");
        }
    }
}
