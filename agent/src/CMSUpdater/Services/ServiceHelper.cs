using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.Versioning;

namespace CMSUpdater.Services;

/// <summary>
/// Lớp tiện ích tương tác với Windows Service Control Manager (SCM) cho Updater
/// </summary>
[SupportedOSPlatform("windows")]
public class ServiceHelper
{
    private readonly ILogger _logger;
    
    /// <summary>
    /// Khởi tạo ServiceHelper với logger
    /// </summary>
    /// <param name="logger">Logger để ghi log</param>
    public ServiceHelper(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Khởi động service agent
    /// </summary>
    /// <param name="serviceName">Tên service</param>
    /// <exception cref="InvalidOperationException">Thrown khi không thể khởi động service</exception>
    [SupportedOSPlatform("windows")]
    public void StartAgentService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            _logger.LogInformation("Đang khởi động service {ServiceName}...", serviceName);
            
            var timeout = TimeSpan.FromSeconds(30);
            
            if (service.Status != ServiceControllerStatus.Running)
            {
                // Bật service nếu nó không đang chạy
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    _logger.LogInformation("Service {ServiceName} đã được khởi động thành công.", serviceName);
                }
                else
                {
                    // Nếu service đang ở trạng thái khác (StartPending, PausePending, etc.), chờ nó hoàn thành
                    _logger.LogWarning("Service {ServiceName} đang ở trạng thái {Status}. Đang chờ...", 
                        serviceName, service.Status);
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    _logger.LogInformation("Service {ServiceName} đã chuyển sang trạng thái Running.", serviceName);
                }
            }
            else
            {
                _logger.LogInformation("Service {ServiceName} đã đang chạy.", serviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể khởi động service {ServiceName}.", serviceName);
            throw new InvalidOperationException($"Không thể khởi động service {serviceName}.", ex);
        }
    }
    
    /// <summary>
    /// Dừng service agent
    /// </summary>
    /// <param name="serviceName">Tên service</param>
    /// <exception cref="InvalidOperationException">Thrown khi không thể dừng service</exception>
    [SupportedOSPlatform("windows")]
    public void StopAgentService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            _logger.LogInformation("Đang dừng service {ServiceName}...", serviceName);
            
            var timeout = TimeSpan.FromSeconds(30);
            
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                // Dừng service nếu nó đang chạy
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    _logger.LogInformation("Service {ServiceName} đã được dừng thành công.", serviceName);
                }
                else
                {
                    // Nếu service đang ở trạng thái khác (StopPending, PausePending, etc.), chờ nó hoàn thành
                    _logger.LogWarning("Service {ServiceName} đang ở trạng thái {Status}. Đang chờ...", 
                        serviceName, service.Status);
                    service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    _logger.LogInformation("Service {ServiceName} đã chuyển sang trạng thái Stopped.", serviceName);
                }
            }
            else
            {
                _logger.LogInformation("Service {ServiceName} đã dừng.", serviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể dừng service {ServiceName}.", serviceName);
            throw new InvalidOperationException($"Không thể dừng service {serviceName}.", ex);
        }
    }
    
    /// <summary>
    /// Kiểm tra service agent có đang chạy hay không
    /// </summary>
    /// <param name="serviceName">Tên service</param>
    /// <returns>true nếu service đang chạy; ngược lại là false</returns>
    [SupportedOSPlatform("windows")]
    public bool IsAgentServiceRunning(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            var isRunning = service.Status == ServiceControllerStatus.Running;
            _logger.LogDebug("Service {ServiceName} có trạng thái: {Status}. Đang chạy: {IsRunning}", 
                serviceName, service.Status, isRunning);
            return isRunning;
        }
        catch (InvalidOperationException)
        {
            // Service không tồn tại
            _logger.LogWarning("Service {ServiceName} không tồn tại.", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra trạng thái của service {ServiceName}.", serviceName);
            return false;
        }
    }
    
    /// <summary>
    /// Kiểm tra một process có còn tồn tại không
    /// </summary>
    /// <param name="processId">ID của process cần kiểm tra</param>
    /// <returns>true nếu process còn tồn tại; ngược lại là false</returns>
    [SupportedOSPlatform("windows")]
    public bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process không tồn tại
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra process {ProcessId}.", processId);
            return false;
        }
    }
} 