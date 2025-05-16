using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using CMSAgent.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Cli
{
    /// <summary>
    /// Handles and orchestrates the CLI commands of the application.
    /// </summary>
    public class CliHandler
    {
        private static readonly string[] RemoveDataAliases = ["--remove-data", "-r"];

        /// <summary>
        /// Flag indicating whether a CLI command has been executed.
        /// </summary>
        public bool IsCliCommandExecuted { get; private set; } = false;

        private readonly ILogger<CliHandler> _logger;
        private readonly RootCommand _rootCommand;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of CliHandler.
        /// </summary>
        /// <param name="serviceProvider">Service provider to resolve command handler classes.</param>
        /// <param name="logger">Logger for logging events.</param>
        public CliHandler(IServiceProvider serviceProvider, ILogger<CliHandler> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Create root command
            _rootCommand = new RootCommand("CMSAgent - Computer Management System");
            
            // Register subcommands
            RegisterCommands();
        }

        /// <summary>
        /// Handles commands from the command line.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Command error code.</returns>
        public async Task<int> HandleAsync(string[] args)
        {
            try
            {
                return await _rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error occurred while executing CLI command");
                return -1;
            }
        }

        /// <summary>
        /// Registers subcommands to the root command.
        /// </summary>
        private void RegisterCommands()
        {
            // Configure command
            var configureCmd = new Command("configure", "Initial configuration for the agent (interactive)");
            configureCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<ConfigureCommand>();
                context.ExitCode = await cmd.ExecuteAsync(context.GetCancellationToken());
            });
            _rootCommand.AddCommand(configureCmd);

            // Install command
            var installCmd = new Command("install", "Cài đặt CMSAgent như một Windows Service");
            installCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<InstallCommand>();
                Environment.ExitCode = await cmd.ExecuteAsync();
            });
            _rootCommand.AddCommand(installCmd);

            // Start command
            var startCmd = new Command("start", "Start the CMSAgent Windows service");
            startCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<StartCommand>();
                context.ExitCode = await cmd.ExecuteAsync();
            });
            _rootCommand.AddCommand(startCmd);

            // Stop command
            var stopCmd = new Command("stop", "Stop the CMSAgent Windows service");
            stopCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<StopCommand>();
                context.ExitCode = await cmd.ExecuteAsync();
            });
            _rootCommand.AddCommand(stopCmd);

            // Uninstall command
            var uninstallCmd = new Command("uninstall", "Uninstall the CMSAgent Windows service");
            var removeDataOption = new Option<bool>(
                aliases: RemoveDataAliases,
                description: "Remove all agent data from ProgramData"
            );
            uninstallCmd.AddOption(removeDataOption);
            uninstallCmd.SetHandler(async (bool removeData) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<UninstallCommand>();
                Environment.ExitCode = await cmd.ExecuteAsync(removeData);
            }, removeDataOption);
            _rootCommand.AddCommand(uninstallCmd);
        }
    }
}
