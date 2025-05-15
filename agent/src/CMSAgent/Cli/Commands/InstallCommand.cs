using System;
using System.CommandLine;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using CMSAgent.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Class for handling the install command to register CMSAgent as a Windows service.
    /// </summary>
    public class InstallCommand
    {
        private readonly ILogger<InstallCommand> _logger;
        private readonly ServiceUtils _serviceUtils;
        private readonly string _serviceName;
        private readonly string _displayName;
        private readonly string _description;

        /// <summary>
        /// Initializes a new instance of InstallCommand.
        /// </summary>
        /// <param name="logger">Logger for logging events.</param>
        /// <param name="serviceUtils">Utility for managing Windows Services.</param>
        /// <param name="options">Agent configuration.</param>
        public InstallCommand(
            ILogger<InstallCommand> logger,
            ServiceUtils serviceUtils,
            IOptions<CmsAgentSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
            _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";
            _displayName = "Computer Management System Agent";
            _description = "Dịch vụ quản lý máy tính từ xa";
        }

        /// <summary>
        /// Executes the install command.
        /// </summary>
        /// <param name="serviceName">Tùy chỉnh tên service (tùy chọn)</param>
        /// <param name="displayName">Tùy chỉnh tên hiển thị (tùy chọn)</param>
        /// <param name="description">Tùy chỉnh mô tả (tùy chọn)</param>
        /// <returns>Exit code of the command.</returns>
        public async Task<int> ExecuteAsync(string? serviceName = null, string? displayName = null, string? description = null)
        {
            // Sử dụng giá trị tùy chỉnh nếu được cung cấp
            string actualServiceName = string.IsNullOrEmpty(serviceName) ? _serviceName : serviceName;
            string actualDisplayName = string.IsNullOrEmpty(displayName) ? _displayName : displayName;
            string actualDescription = string.IsNullOrEmpty(description) ? _description : description;

            Console.WriteLine($"Cài đặt service {actualServiceName}...");

            try
            {
                // Kiểm tra xem có đang chạy trên Windows không
                if (!OperatingSystem.IsWindows())
                {
                    Console.Error.WriteLine("Chỉ có thể cài đặt Windows Service trên hệ điều hành Windows");
                    return (int)CliExitCodes.GeneralError;
                }

                // Kiểm tra xem service đã được cài đặt chưa
                if (_serviceUtils.IsServiceInstalled(actualServiceName))
                {
                    Console.WriteLine($"Service {actualServiceName} đã được cài đặt trước đó.");
                    return (int)CliExitCodes.Success;
                }

                // Đường dẫn đến file exe
                string executablePath = Assembly.GetExecutingAssembly().Location;
                if (executablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    // Đối với ứng dụng .NET Core, thay đổi phần mở rộng từ .dll sang .exe
                    executablePath = Path.ChangeExtension(executablePath, ".exe");
                }

                if (!File.Exists(executablePath))
                {
                    Console.Error.WriteLine($"Không tìm thấy file thực thi tại {executablePath}");
                    return (int)CliExitCodes.GeneralError;
                }

                // Cài đặt service
                _serviceUtils.InstallService(actualServiceName, actualDisplayName, actualDescription, executablePath);

                // Khởi động service
                bool startResult = await _serviceUtils.StartServiceAsync(actualServiceName);
                if (startResult)
                {
                    Console.WriteLine($"Service {actualServiceName} đã được cài đặt và khởi động thành công");
                    return (int)CliExitCodes.Success;
                }
                else
                {
                    Console.Error.WriteLine($"Service {actualServiceName} đã được cài đặt nhưng không thể khởi động");
                    return (int)CliExitCodes.ServiceOperationFailed;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Lỗi: Cần quyền Administrator để cài đặt Windows Service. Vui lòng chạy lệnh với quyền Administrator.");
                return (int)CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Lỗi khi cài đặt service: {ex.Message}");
                _logger.LogError(ex, "Lỗi khi cài đặt service {ServiceName}: {ErrorMessage}", actualServiceName, ex.Message);
                return (int)CliExitCodes.GeneralError;
            }
        }
    }
} 