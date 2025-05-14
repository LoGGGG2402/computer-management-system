using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Lớp xử lý lệnh start để khởi động CMSAgent Windows service.
    /// </summary>
    public class StartCommand
    {
        private readonly ILogger<StartCommand> _logger;
        private readonly ServiceUtils _serviceUtils;
        private readonly string _serviceName;

        /// <summary>
        /// Khởi tạo một instance mới của StartCommand.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="serviceUtils">Tiện ích quản lý Windows Service.</param>
        /// <param name="options">Cấu hình agent.</param>
        public StartCommand(
            ILogger<StartCommand> logger,
            ServiceUtils serviceUtils,
            IOptions<CmsAgentSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
            _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";
        }

        /// <summary>
        /// Thực thi lệnh start.
        /// </summary>
        /// <returns>Mã lỗi của lệnh.</returns>
        public async Task<int> ExecuteAsync()
        {
            Console.WriteLine($"Đang khởi động dịch vụ {_serviceName}...");

            try
            {
                // Kiểm tra nếu service đã được cài đặt
                if (!_serviceUtils.IsServiceInstalled(_serviceName))
                {
                    Console.WriteLine($"Dịch vụ {_serviceName} chưa được cài đặt.");
                    return (int)CliExitCodes.ServiceNotInstalled;
                }

                // Kiểm tra nếu service đang chạy
                if (_serviceUtils.IsServiceRunning(_serviceName))
                {
                    Console.WriteLine($"Dịch vụ {_serviceName} đã đang chạy.");
                    return (int)CliExitCodes.Success;
                }

                // Khởi động service
                bool startSuccess = await _serviceUtils.StartServiceAsync(_serviceName);
                if (startSuccess)
                {
                    Console.WriteLine($"Dịch vụ {_serviceName} đã khởi động thành công.");
                    return (int)CliExitCodes.Success;
                }
                else
                {
                    Console.Error.WriteLine($"Không thể khởi động dịch vụ {_serviceName}.");
                    return (int)CliExitCodes.ServiceOperationFailed;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Lỗi: Cần quyền Administrator để khởi động dịch vụ. Vui lòng chạy lại lệnh với quyền Administrator.");
                return (int)CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Lỗi khi khởi động dịch vụ: {ex.Message}");
                _logger.LogError(ex, "Lỗi khi khởi động dịch vụ {ServiceName}", _serviceName);
                return (int)CliExitCodes.GeneralError;
            }
        }
    }
}
