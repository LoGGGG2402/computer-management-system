using System;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Commands
{
    /// <summary>
    /// Factory to create command handlers appropriate for each command type.
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve handlers.</param>
    /// <param name="logger">Logger to log events.</param>
    public class CommandHandlerFactory(IServiceProvider serviceProvider, ILogger<CommandHandlerFactory> logger)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly ILogger<CommandHandlerFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Create and return a handler appropriate for the command type.
        /// </summary>
        /// <param name="commandType">Command type to handle.</param>
        /// <returns>Command handler appropriate for handling the command.</returns>
        public ICommandHandler GetHandler(CommandType commandType)
        {
            _logger.LogDebug("Creating handler for command type: {CommandType}", commandType);

            try
            {
                return commandType switch
                {
                    CommandType.CONSOLE => _serviceProvider.GetRequiredService<Handlers.ConsoleCommandHandler>(),
                    CommandType.SYSTEM_ACTION => _serviceProvider.GetRequiredService<Handlers.SystemActionCommandHandler>(),
                    _ => throw new ArgumentException($"Unsupported command type: {commandType}", nameof(commandType))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating handler for command type {CommandType}", commandType);
                throw;
            }
        }
    }
}
