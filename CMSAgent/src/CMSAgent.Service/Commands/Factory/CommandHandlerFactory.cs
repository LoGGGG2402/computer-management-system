using CMSAgent.Service.Commands.Handlers;
using Microsoft.Extensions.DependencyInjection; // For IServiceProvider
using Microsoft.Extensions.Logging;
using System;

namespace CMSAgent.Service.Commands.Factory
{
    /// <summary>
    /// Factory class for creating command handlers
    /// </summary>
    public class CommandHandlerFactory : ICommandHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandHandlerFactory> _logger;

        public CommandHandlerFactory(IServiceProvider serviceProvider, ILogger<CommandHandlerFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ICommandHandler? CreateHandler(string commandType)
        {
            if (string.IsNullOrWhiteSpace(commandType))
            {
                _logger.LogWarning("CommandType is not provided or empty.");
                return null;
            }

            _logger.LogDebug("Creating handler for CommandType: {CommandType}", commandType);

            try
            {
                switch (commandType.ToUpperInvariant())
                {
                    case "CONSOLE":
                        return _serviceProvider.GetRequiredService<ConsoleCommandHandler>();
                    case "SYSTEM_ACTION":
                        return _serviceProvider.GetRequiredService<SystemActionCommandHandler>();
                    case "SOFTWARE_INSTALL":
                        return _serviceProvider.GetRequiredService<SoftwareInstallCommandHandler>();
                    case "SOFTWARE_UNINSTALL":
                        return _serviceProvider.GetRequiredService<SoftwareUninstallCommandHandler>();
                    case "GET_LOGS":
                        return _serviceProvider.GetRequiredService<GetLogsCommandHandler>();
                    default:
                        _logger.LogWarning("No handler found for CommandType: {CommandType}", commandType);
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating handler for CommandType: {CommandType}", commandType);
                return null;    
            }
        }
    }
}
