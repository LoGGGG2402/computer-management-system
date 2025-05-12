using CMSAgent.Configuration;
using CMSAgent.Core;
using CMSAgent.SystemOperations;
using CMSAgent.UserInterface;
using CMSAgent.Utilities;
using CMSAgent.Communication; // Added for IServerConnector
using Serilog;
using System.CommandLine;
using System.ServiceProcess;
using System.Threading; // Added for CancellationTokenSource
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace CMSAgent
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Initialize basic logging before command processing
            LoggingSetup.Initialize(false);

            var rootCommand = new RootCommand("CMS Agent - Computer Management System monitoring agent.");

            // Add the 'configure' subcommand
            var configureCommand = new Command("configure", "Configure the agent for first use or reconfigure settings.");
            configureCommand.SetHandler(ConfigureCommand);
            rootCommand.Add(configureCommand);

            // Add the 'debug' subcommand
            var debugCommand = new Command("debug", "Run the agent in debug mode (console).");
            debugCommand.SetHandler(DebugCommand);
            rootCommand.Add(debugCommand);

            // Add the 'start' subcommand
            var startCommand = new Command("start", "Start the agent Windows service.");
            startCommand.SetHandler(StartCommand);
            rootCommand.Add(startCommand);

            // Add the 'stop' subcommand
            var stopCommand = new Command("stop", "Stop the agent Windows service.");
            stopCommand.SetHandler(StopCommand);
            rootCommand.Add(stopCommand);

            // Add the 'uninstall' subcommand
            var uninstallCommand = new Command("uninstall", "Uninstall the agent service.");
            var removeDataOption = new Option<bool>("--remove-data", "Remove all agent data from ProgramData folder.");
            uninstallCommand.AddOption(removeDataOption);
            uninstallCommand.SetHandler((bool removeData) => UninstallCommand(removeData), removeDataOption);
            rootCommand.Add(uninstallCommand);

            // Default command (service)
            rootCommand.SetHandler(() =>
            {
                ServiceBase[] servicesToRun = new ServiceBase[]
                {
                    new AgentWindowsService()
                };
                ServiceBase.Run(servicesToRun);
            });

            return await rootCommand.InvokeAsync(args);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.Sources.Clear(); // Xóa các nguồn cấu hình mặc định
                    var env = hostingContext.HostingEnvironment;

                    // Tải file cấu hình chính
                    config.AddJsonFile("agent_config.json", optional: false, reloadOnChange: true);

                    // Có thể thêm các file cấu hình theo môi trường nếu cần (ví dụ: agent_config.Development.json)
                    // config.AddJsonFile($"agent_config.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

                    config.AddEnvironmentVariables();
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                });

        private static async Task ConfigureCommand()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent process termination
                cts.Cancel();
                Log.Information("Configuration cancelled by user (Ctrl+C).");
            };

            try
            {
                Log.Information("Starting agent initial configuration (as per Standard.md II.3)...");

                // Setup dependencies
                var staticConfigProvider = new StaticConfigProvider();
                await staticConfigProvider.InitializeAsync();
                if (cts.IsCancellationRequested) { Environment.Exit(1); return; }

                var runtimeStateManager = new RuntimeStateManager();
                await runtimeStateManager.InitializeAsync(); // This ensures DeviceId is loaded/created early
                if (cts.IsCancellationRequested) { Environment.Exit(1); return; }

                // ServerConnector for HTTP communication during configuration
                var serverConnector = new ServerConnector(staticConfigProvider, runtimeStateManager);
                await serverConnector.InitializeAsync(); // Initializes HttpChannel with server_url and potentially existing token
                if (cts.IsCancellationRequested) { Environment.Exit(1); return; }

                var consoleUI = new ConsoleUI(staticConfigProvider, runtimeStateManager, serverConnector);
                
                // Call the new method for initial configuration
                bool configureResult = await consoleUI.PerformInitialConfigurationAsync(cts.Token); 
                
                if (configureResult)
                {
                    Log.Information("Agent initial configuration completed successfully.");
                    Environment.Exit(0); // Success
                }
                else
                {
                    Log.Error("Agent initial configuration failed or was cancelled.");
                    Environment.Exit(1); // Failure
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Agent configuration was cancelled.");
                Environment.Exit(1); // Failure
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during initial configuration: {Message}", ex.Message);
                Environment.Exit(1); // Failure
            }
        }

        private static async Task DebugCommand()
        {
            try
            {
                Log.Information("Starting agent in debug mode...");
                await RunAgentInDebugMode();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error in debug mode: {Message}", ex.Message);
                Environment.Exit(1);
            }
        }

        private static async Task RunAgentInDebugMode()
        {
            // Setup complete logging for debug mode
            LoggingSetup.Initialize(true);

            // Create and initialize the CoreAgent
            var agent = new CoreAgent();
            
            // Handle Ctrl+C to gracefully shut down
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate exit
                Log.Information("Ctrl+C received, initiating graceful shutdown...");
                agent.StopAsync().Wait();
            };

            // Start and run the agent
            await agent.StartAsync();

            // Keep the console alive until the agent is stopped
            await agent.WaitForCompletionAsync();
            
            Log.Information("Agent debug mode terminated.");
        }

        private static void StartCommand()
        {
            try
            {
                var serviceManager = new WindowsServiceManager();
                bool success = serviceManager.StartService("CMSAgentService");
                
                if (success)
                {
                    Log.Information("CMSAgentService started successfully.");
                    Environment.Exit(0); // Success
                }
                else
                {
                    Log.Error("Failed to start CMSAgentService.");
                    Environment.Exit(1); // Failure
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error starting service: {Message}", ex.Message);
                Environment.Exit(1); // Failure
            }
        }

        private static void StopCommand()
        {
            try
            {
                var serviceManager = new WindowsServiceManager();
                bool success = serviceManager.StopService("CMSAgentService");
                
                if (success)
                {
                    Log.Information("CMSAgentService stopped successfully.");
                    Environment.Exit(0); // Success
                }
                else
                {
                    Log.Error("Failed to stop CMSAgentService.");
                    Environment.Exit(1); // Failure
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error stopping service: {Message}", ex.Message);
                Environment.Exit(1); // Failure
            }
        }

        private static void UninstallCommand(bool removeData)
        {
            try
            {
                // First stop the service if it's running
                var serviceManager = new WindowsServiceManager();
                serviceManager.StopService("CMSAgentService");
                Log.Information("Stopped CMSAgentService (if running)");

                // Uninstall the service
                bool uninstallResult = serviceManager.UninstallService("CMSAgentService");
                if (uninstallResult)
                {
                    Log.Information("Uninstalled CMSAgentService");
                }
                else
                {
                    Log.Warning("Failed to uninstall CMSAgentService, it may have already been removed");
                }

                // Remove data if requested
                if (removeData)
                {
                    var dataPath = DirectoryUtils.GetProgramDataDirectoryPath();
                    if (Directory.Exists(dataPath))
                    {
                        Directory.Delete(dataPath, true);
                        Log.Information("Removed agent data directory: {Path}", dataPath);
                    }
                }

                Log.Information("Agent uninstallation completed successfully.");
                Environment.Exit(0); // Success
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error during uninstallation: {Message}", ex.Message);
                Environment.Exit(1); // Failure
            }
        }
    }
}