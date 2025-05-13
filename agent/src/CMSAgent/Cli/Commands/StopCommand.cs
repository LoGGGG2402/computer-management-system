using System;
using System.CommandLine.IO;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Lớp xử lý lệnh stop để dừng CMSAgent service.
    /// </summary>
    public class StopCommand
    {
        private readonly ILogger<StopCommand> _logger;
        private readonly ServiceUtils _serviceUtils;
        private readonly string _serviceName;

        /// <summary>
        /// Khởi tạo một instance mới của StopCommand.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="serviceUtils">Tiện ích quản lý Windows Service.</param>
        /// <param name="options">Cấu hình agent.</param>
        public StopCommand(
            ILogger<StopCommand> logger,
            ServiceUtils serviceUtils,
            IOptions<CmsAgentSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
            _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";
        }

        /// <summary>
        /// Thực thi lệnh stop.
        /// </summary>
        /// <param name="console">Console để tương tác với người dùng.</param>
        /// <returns>Mã lỗi của lệnh.</returns>
        public async Task<int> ExecuteAsync(IConsole console)
        {
            console.Out.WriteLine($"Đang dừng service {_serviceName}...");

            try
            {
                // Kiểm tra tồn tại service
                if (!_serviceUtils.IsServiceInstalled(_serviceName))
                {
                    console.Error.WriteLine($"Lỗi: Service {_serviceName} chưa được cài đặt.");
                    return CliExitCodes.ServiceNotInstalled;
                }

                // Lấy trạng thái hiện tại
                var status = _serviceUtils.GetServiceStatus(_serviceName);
                
                if (status != System.ServiceProcess.ServiceControllerStatus.Running &&
                    status != System.ServiceProcess.ServiceControllerStatus.StartPending)
                {
                    console.Out.WriteLine($"Service {_serviceName} không đang chạy.");
                    return CliExitCodes.Success;
                }

                // Dừng service
                _serviceUtils.StopService(_serviceName);
                
                console.Out.WriteLine($"Service {_serviceName} đã được dừng thành công.");
                return CliExitCodes.Success;
            }
            catch (UnauthorizedAccessException)
            {
                console.Error.WriteLine("Lỗi: Cần quyền Administrator để dừng service. Vui lòng chạy lại lệnh với quyền Administrator.");
                return CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine($"Lỗi khi dừng service: {ex.Message}");
                _logger.LogError(ex, "Lỗi khi dừng service {ServiceName}", _serviceName);
                return CliExitCodes.ServiceOperationFailed;
            }
        }
    }
}
