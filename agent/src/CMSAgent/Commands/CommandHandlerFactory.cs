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
    public class CommandHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandHandlerFactory> _logger;

        /// <summary>
        /// Khởi tạo một instance mới của CommandHandlerFactory.
        /// </summary>
        /// <param name="serviceProvider">Service provider để resolve các handler.</param>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public CommandHandlerFactory(IServiceProvider serviceProvider, ILogger<CommandHandlerFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
                    CommandType.GET_LOGS => _serviceProvider.GetRequiredService<Handlers.GetLogsCommandHandler>(),
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
