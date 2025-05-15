using System;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Commands
{
    /// <summary>
    /// Factory tạo ra các command handler phù hợp với từng loại command.
    /// </summary>
    /// <param name="serviceProvider">Service provider để resolve các handler.</param>
    /// <param name="logger">Logger để ghi nhật ký.</param>
    public class CommandHandlerFactory(IServiceProvider serviceProvider, ILogger<CommandHandlerFactory> logger)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly ILogger<CommandHandlerFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Tạo và trả về handler phù hợp với loại command.
        /// </summary>
        /// <param name="commandType">Loại command cần xử lý.</param>
        /// <returns>Command handler phù hợp để xử lý command.</returns>
        public ICommandHandler GetHandler(CommandType commandType)
        {
            _logger.LogDebug("Đang tạo handler cho command type: {CommandType}", commandType);

            try
            {
                return commandType switch
                {
                    CommandType.CONSOLE => _serviceProvider.GetRequiredService<Handlers.ConsoleCommandHandler>(),
                    CommandType.SYSTEM_ACTION => _serviceProvider.GetRequiredService<Handlers.SystemActionCommandHandler>(),
                    _ => throw new ArgumentException($"Không hỗ trợ loại command: {commandType}", nameof(commandType))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo handler cho command type {CommandType}", commandType);
                throw;
            }
        }
    }
}
