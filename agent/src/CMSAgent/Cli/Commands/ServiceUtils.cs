using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Các tiện ích để tương tác với Windows Service Control Manager (SCM).
    /// </summary>
    public class ServiceUtils
    {
        private readonly ILogger<ServiceUtils> _logger;
        private const string ScExe = "sc.exe";

        /// <summary>
        /// Khởi tạo một instance mới của ServiceUtils.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public ServiceUtils(ILogger<ServiceUtils> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Kiểm tra xem service có được cài đặt hay không.
        /// </summary>
        /// <param name="serviceName">Tên của service cần kiểm tra.</param>
        /// <returns>True nếu service đã được cài đặt, ngược lại là False.</returns>
        public bool IsServiceInstalled(string serviceName)
        {
            try
            {
                return ServiceController.GetServices().Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi kiểm tra trạng thái của service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Cài đặt service mới.
        /// </summary>
        /// <param name="serviceName">Tên của service.</param>
        /// <param name="displayName">Tên hiển thị của service.</param>
        /// <param name="description">Mô tả của service.</param>
        /// <param name="exePath">Đường dẫn đến file thực thi của service.</param>
        public void InstallService(string serviceName, string displayName, string description, string exePath)
        {
            if (!IsAdministrator())
            {
                throw new UnauthorizedAccessException("Cần quyền Administrator để cài đặt service.");
            }

            if (IsServiceInstalled(serviceName))
            {
                _logger.LogWarning("Service {ServiceName} đã được cài đặt. Bỏ qua bước cài đặt.", serviceName);
                return;
            }

            _logger.LogInformation("Đang cài đặt service {ServiceName}...", serviceName);

            // Tạo service mới
            ExecuteScCommand($"create {serviceName} binPath= \"{exePath}\" start= auto DisplayName= \"{displayName}\"", 
                "Không thể tạo service");

            // Thiết lập mô tả cho service
            ExecuteScCommand($"description {serviceName} \"{description}\"",
                "Không thể thiết lập mô tả cho service");

            // Cấu hình service để tự động khởi động lại nếu bị lỗi
            ExecuteScCommand($"failure {serviceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000",
                "Không thể cấu hình chính sách khởi động lại cho service");

            _logger.LogInformation("Đã cài đặt service {ServiceName} thành công", serviceName);
        }

        /// <summary>
        /// Gỡ bỏ service.
        /// </summary>
        /// <param name="serviceName">Tên của service cần gỡ bỏ.</param>
        public async void UninstallService(string serviceName)
        {
            if (!IsAdministrator())
            {
                throw new UnauthorizedAccessException("Cần quyền Administrator để gỡ bỏ service.");
            }

            if (!IsServiceInstalled(serviceName))
            {
                _logger.LogWarning("Service {ServiceName} không tồn tại. Bỏ qua bước gỡ bỏ.", serviceName);
                return;
            }

            // Dừng service trước khi gỡ bỏ
            try
            {
                await StopServiceAsync(serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể dừng service {ServiceName} trước khi gỡ bỏ", serviceName);
            }

            _logger.LogInformation("Đang gỡ bỏ service {ServiceName}...", serviceName);
            
            // Xóa service
            ExecuteScCommand($"delete {serviceName}", 
                "Không thể gỡ bỏ service");

            _logger.LogInformation("Đã gỡ bỏ service {ServiceName} thành công", serviceName);
        }

        /// <summary>
        /// Kiểm tra xem service có đang chạy hay không.
        /// </summary>
        /// <param name="serviceName">Tên của service cần kiểm tra.</param>
        /// <returns>True nếu service đang chạy, ngược lại là False.</returns>
        public bool IsServiceRunning(string serviceName)
        {
            if (!IsServiceInstalled(serviceName))
            {
                return false;
            }

            try
            {
                using var serviceController = new ServiceController(serviceName);
                return serviceController.Status == ServiceControllerStatus.Running;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra trạng thái của service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Khởi động service bất đồng bộ.
        /// </summary>
        /// <param name="serviceName">Tên của service cần khởi động.</param>
        /// <returns>Task tượng trưng cho hoạt động bất đồng bộ, trả về True nếu thành công.</returns>
        public async Task<bool> StartServiceAsync(string serviceName)
        {
            if (!IsAdministrator())
            {
                throw new UnauthorizedAccessException("Cần quyền Administrator để khởi động service.");
            }

            if (!IsServiceInstalled(serviceName))
            {
                throw new InvalidOperationException($"Service {serviceName} chưa được cài đặt.");
            }

            using var serviceController = new ServiceController(serviceName);
            
            if (serviceController.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("Service {ServiceName} đã đang chạy", serviceName);
                return true;
            }

            _logger.LogInformation("Đang khởi động service {ServiceName}...", serviceName);
            
            try
            {
                serviceController.Start();
                
                // Chờ service đạt trạng thái Running (tối đa 30 giây)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var statusTask = Task.Run(() => {
                    try
                    {
                        serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
                
                if (await Task.WhenAny(statusTask, timeoutTask) == timeoutTask)
                {
                    _logger.LogWarning("Quá thời gian chờ khởi động service {ServiceName}", serviceName);
                    return false;
                }
                
                bool result = await statusTask;
                if (result)
                {
                    _logger.LogInformation("Đã khởi động service {ServiceName} thành công", serviceName);
                }
                else
                {
                    _logger.LogWarning("Không thể khởi động service {ServiceName}", serviceName);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể khởi động service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Dừng service bất đồng bộ.
        /// </summary>
        /// <param name="serviceName">Tên của service cần dừng.</param>
        /// <returns>Task tượng trưng cho hoạt động bất đồng bộ, trả về True nếu thành công.</returns>
        public async Task<bool> StopServiceAsync(string serviceName)
        {
            if (!IsAdministrator())
            {
                throw new UnauthorizedAccessException("Cần quyền Administrator để dừng service.");
            }

            if (!IsServiceInstalled(serviceName))
            {
                throw new InvalidOperationException($"Service {serviceName} chưa được cài đặt.");
            }

            using var serviceController = new ServiceController(serviceName);
            
            if (serviceController.Status != ServiceControllerStatus.Running)
            {
                _logger.LogInformation("Service {ServiceName} không đang chạy", serviceName);
                return true;
            }

            _logger.LogInformation("Đang dừng service {ServiceName}...", serviceName);
            
            try
            {
                serviceController.Stop();
                
                // Chờ service đạt trạng thái Stopped (tối đa 30 giây)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var statusTask = Task.Run(() => {
                    try
                    {
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
                
                if (await Task.WhenAny(statusTask, timeoutTask) == timeoutTask)
                {
                    _logger.LogWarning("Quá thời gian chờ dừng service {ServiceName}", serviceName);
                    return false;
                }
                
                bool result = await statusTask;
                if (result)
                {
                    _logger.LogInformation("Đã dừng service {ServiceName} thành công", serviceName);
                }
                else
                {
                    _logger.LogWarning("Không thể dừng service {ServiceName}", serviceName);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể dừng service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Lấy trạng thái hiện tại của service.
        /// </summary>
        /// <param name="serviceName">Tên của service cần lấy trạng thái.</param>
        /// <returns>Trạng thái của service.</returns>
        public ServiceControllerStatus GetServiceStatus(string serviceName)
        {
            try
            {
                using var serviceController = new ServiceController(serviceName);
                return serviceController.Status;
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Service {ServiceName} không tồn tại", serviceName);
                return ServiceControllerStatus.Stopped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy trạng thái của service {ServiceName}", serviceName);
                return ServiceControllerStatus.Stopped;
            }
        }

        /// <summary>
        /// Thực thi lệnh sc.exe với tham số.
        /// </summary>
        /// <param name="arguments">Tham số dòng lệnh.</param>
        /// <param name="errorMessagePrefix">Tiền tố của thông báo lỗi.</param>
        private void ExecuteScCommand(string arguments, string errorMessagePrefix)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = ScExe;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                _logger.LogDebug("Thực thi lệnh: {Command} {Arguments}", ScExe, arguments);
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var errorMessage = $"{errorMessagePrefix}: {errorBuilder}";
                    _logger.LogError("Lỗi khi thực thi lệnh sc.exe. Mã lỗi: {ExitCode}. Thông báo: {ErrorMessage}", 
                        process.ExitCode, errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực thi lệnh sc.exe với tham số: {Arguments}", arguments);
                throw;
            }
        }

        /// <summary>
        /// Kiểm tra xem tiến trình hiện tại có chạy với quyền Administrator hay không.
        /// </summary>
        /// <returns>True nếu đang chạy với quyền Administrator, ngược lại là False.</returns>
        private bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể kiểm tra quyền Administrator");
                return false;
            }
        }
    }
} 