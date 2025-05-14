using System;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Lớp xử lý lệnh uninstall để gỡ bỏ CMSAgent service.
    /// </summary>
    public class UninstallCommand
    {
        private readonly ILogger<UninstallCommand> _logger;
        private readonly ServiceUtils _serviceUtils;
        private readonly IConfigLoader _configLoader;
        private readonly string _serviceName;
        private readonly string _dataDirectory;

        /// <summary>
        /// Khởi tạo một instance mới của UninstallCommand.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="serviceUtils">Tiện ích quản lý Windows Service.</param>
        /// <param name="configLoader">ConfigLoader để truy cập vào đường dẫn cấu hình.</param>
        /// <param name="options">Cấu hình agent.</param>
        public UninstallCommand(
            ILogger<UninstallCommand> logger,
            ServiceUtils serviceUtils,
            IConfigLoader configLoader,
            IOptions<CmsAgentSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";
            _dataDirectory = _configLoader.GetDataPath();
        }

        /// <summary>
        /// Thực thi lệnh uninstall.
        /// </summary>
        /// <param name="removeData">Cờ xác định có xóa dữ liệu hay không.</param>
        /// <returns>Mã lỗi của lệnh.</returns>
        public async Task<int> ExecuteAsync(bool removeData)
        {
            Console.WriteLine($"Đang gỡ bỏ service {_serviceName}...");

            try
            {
                // Kiểm tra tồn tại service
                if (!_serviceUtils.IsServiceInstalled(_serviceName))
                {
                    Console.WriteLine($"Service {_serviceName} không tồn tại hoặc đã được gỡ bỏ trước đó.");
                    
                    // Xóa dữ liệu nếu yêu cầu
                    if (removeData)
                    {
                        await RemoveDataDirectoriesAsync();
                    }
                    
                    return (int)CliExitCodes.Success;
                }

                // Gỡ bỏ service
                _serviceUtils.UninstallService(_serviceName);
                
                Console.WriteLine($"Service {_serviceName} đã được gỡ bỏ thành công.");

                // Xóa dữ liệu nếu yêu cầu
                if (removeData)
                {
                    await RemoveDataDirectoriesAsync();
                }
                
                return (int)CliExitCodes.Success;
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Lỗi: Cần quyền Administrator để gỡ bỏ service. Vui lòng chạy lại lệnh với quyền Administrator.");
                return (int)CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Lỗi khi gỡ bỏ service: {ex.Message}");
                _logger.LogError(ex, "Lỗi khi gỡ bỏ service {ServiceName}", _serviceName);
                return (int)CliExitCodes.ServiceOperationFailed;
            }
        }

        /// <summary>
        /// Xóa các thư mục dữ liệu của agent.
        /// </summary>
        /// <returns>Task đại diện cho tác vụ xóa dữ liệu.</returns>
        private async Task RemoveDataDirectoriesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_dataDirectory) || !Directory.Exists(_dataDirectory))
                {
                    Console.WriteLine("Không tìm thấy thư mục dữ liệu của agent.");
                    return;
                }

                Console.WriteLine($"Đang xóa dữ liệu agent từ: {_dataDirectory}");
                
                // Xóa các file cấu hình
                string configFile = Path.Combine(_dataDirectory, "runtime_config.json");
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                // Xóa các thư mục hàng đợi
                string queuesDirectory = Path.Combine(_dataDirectory, "queues");
                if (Directory.Exists(queuesDirectory))
                {
                    Directory.Delete(queuesDirectory, true);
                }

                // Xóa các thư mục logs
                string logsDirectory = Path.Combine(_dataDirectory, "logs");
                if (Directory.Exists(logsDirectory))
                {
                    Directory.Delete(logsDirectory, true);
                }

                // Thử xóa thư mục gốc (có thể thất bại nếu có các tiến trình đang giữ lock)
                try
                {
                    Directory.Delete(_dataDirectory, true);
                    Console.WriteLine("Đã xóa toàn bộ dữ liệu của agent.");
                }
                catch (IOException)
                {
                    Console.WriteLine("Đã xóa một phần dữ liệu của agent. Một số file hoặc thư mục có thể đang được sử dụng.");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Đã xóa một phần dữ liệu của agent. Không đủ quyền để xóa một số file hoặc thư mục.");
                }
                
                // Thêm một await để loại bỏ cảnh báo
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Lỗi khi xóa dữ liệu: {ex.Message}");
                _logger.LogError(ex, "Lỗi khi xóa dữ liệu agent tại {DataDirectory}", _dataDirectory);
            }
        }
    }
}
