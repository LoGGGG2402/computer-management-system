 // CMSAgent.Service/Commands/Factory/CommandHandlerFactory.cs
using CMSAgent.Service.Commands.Handlers;
using Microsoft.Extensions.DependencyInjection; // For IServiceProvider
using Microsoft.Extensions.Logging;
using System;

namespace CMSAgent.Service.Commands.Factory
{
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
                _logger.LogWarning("CommandType không được cung cấp hoặc rỗng.");
                return null;
            }

            _logger.LogDebug("Đang tạo handler cho CommandType: {CommandType}", commandType);

            // Sử dụng IServiceProvider để resolve các handler.
            // Điều này yêu cầu các handler phải được đăng ký trong DI container.
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
                    // Thêm các case khác cho các loại lệnh mới ở đây
                    default:
                        _logger.LogWarning("Không tìm thấy handler nào cho CommandType: {CommandType}", commandType);
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo handler cho CommandType: {CommandType}", commandType);
                return null; // Hoặc ném lỗi tùy theo chính sách xử lý
            }
        }
    }
}
