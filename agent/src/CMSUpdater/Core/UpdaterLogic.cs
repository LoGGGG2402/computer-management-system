using Microsoft.Extensions.Logging;
using System.Diagnostics;
using CMSUpdater.Services;
using CMSAgent.Common.Enums;
using System.Runtime.Versioning;

namespace CMSUpdater.Core;

/// <summary>
/// Lớp chịu trách nhiệm thực hiện logic cập nhật agent
/// </summary>
[SupportedOSPlatform("windows")]
public class UpdaterLogic
{
    private readonly ILogger _logger;
    private readonly RollbackManager _rollbackManager;
    private readonly ServiceHelper _serviceHelper;
    private readonly int _agentProcessIdToWait;
    private readonly string _newAgentPath;
    private readonly string _currentAgentInstallDir;
    private readonly string _updaterLogDir;
    private readonly string _currentAgentVersion;
    private readonly string _agentServiceName = "CMSAgentService";
    
    // Các thời gian timeout và chờ
    private readonly TimeSpan _processExitTimeout = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _serviceWatchdogTime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _serviceCheckInterval = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Khởi tạo UpdaterLogic
    /// </summary>
    /// <param name="logger">Logger để ghi log</param>
    /// <param name="rollbackManager">Quản lý để xử lý rollback nếu cập nhật thất bại</param>
    /// <param name="serviceHelper">Helper để tương tác với Windows Service</param>
    /// <param name="agentProcessIdToWait">PID của tiến trình agent cũ cần dừng</param>
    /// <param name="newAgentPath">Đường dẫn đến thư mục chứa file agent mới đã giải nén</param>
    /// <param name="currentAgentInstallDir">Đường dẫn thư mục cài đặt hiện tại</param>
    /// <param name="updaterLogDir">Nơi ghi file log của updater</param>
    /// <param name="currentAgentVersion">Phiên bản agent hiện tại</param>
    public UpdaterLogic(
        ILogger logger,
        RollbackManager rollbackManager,
        ServiceHelper serviceHelper,
        int agentProcessIdToWait,
        string newAgentPath,
        string currentAgentInstallDir,
        string updaterLogDir,
        string currentAgentVersion)
    {
        _logger = logger;
        _rollbackManager = rollbackManager;
        _serviceHelper = serviceHelper;
        _agentProcessIdToWait = agentProcessIdToWait;
        _newAgentPath = newAgentPath;
        _currentAgentInstallDir = currentAgentInstallDir;
        _updaterLogDir = updaterLogDir;
        _currentAgentVersion = currentAgentVersion;
    }
    
    /// <summary>
    /// Thực thi quá trình cập nhật
    /// </summary>
    /// <returns>Mã trạng thái của quá trình cập nhật (exit code)</returns>
    public async Task<int> ExecuteUpdateAsync()
    {
        _logger.LogInformation("Bắt đầu quá trình cập nhật agent từ phiên bản {CurrentVersion}", _currentAgentVersion);
        
        try
        {
            // Bước 1: Chờ process agent cũ dừng
            _logger.LogInformation("Chờ process agent cũ (PID: {PID}) dừng...", _agentProcessIdToWait);
            if (!await WaitForProcessExitAsync(_agentProcessIdToWait, _processExitTimeout))
            {
                _logger.LogError("Timeout khi chờ process agent cũ (PID: {PID}) dừng", _agentProcessIdToWait);
                return (int)UpdaterExitCodes.AgentStopTimeout;
            }
            
            // Đảm bảo service đã dừng
            if (_serviceHelper.IsAgentServiceRunning(_agentServiceName))
            {
                _logger.LogWarning("Service {ServiceName} vẫn đang chạy. Dừng service...", _agentServiceName);
                try
                {
                    _serviceHelper.StopAgentService(_agentServiceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể dừng service {ServiceName}", _agentServiceName);
                    return (int)UpdaterExitCodes.StopAgentFailed;
                }
            }
            
            // Bước 2: Sao lưu agent cũ
            _logger.LogInformation("Sao lưu agent cũ...");
            try
            {
                await BackupAgentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể sao lưu agent cũ");
                return (int)UpdaterExitCodes.BackupFailed;
            }
            
            // Bước 3: Triển khai agent mới
            _logger.LogInformation("Triển khai agent mới từ {NewAgentPath}...", _newAgentPath);
            try
            {
                await DeployNewAgentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể triển khai agent mới");
                
                try
                {
                    await _rollbackManager.RollbackAsync("UpdateDeploymentFailed");
                    return (int)UpdaterExitCodes.DeployFailed;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback sau lỗi triển khai thất bại");
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Bước 4: Khởi động service agent mới
            _logger.LogInformation("Khởi động service agent mới...");
            try
            {
                _serviceHelper.StartAgentService(_agentServiceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể khởi động service agent mới");
                
                try
                {
                    await _rollbackManager.RollbackAsync("NewServiceStartFailed");
                    return (int)UpdaterExitCodes.NewServiceStartFailed;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback sau lỗi khởi động service thất bại");
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Bước 5: Watchdog để đảm bảo agent mới hoạt động ổn định
            _logger.LogInformation("Theo dõi service mới trong {WatchdogTime} (theo dõi crash)...", _serviceWatchdogTime);
            if (!await WatchdogServiceAsync(_serviceWatchdogTime, _serviceCheckInterval))
            {
                _logger.LogError("Service agent mới không ổn định, đang thực hiện rollback...");
                
                try
                {
                    await _rollbackManager.RollbackAsync("NewServiceUnstable");
                    return (int)UpdaterExitCodes.WatchdogTriggeredRollback;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback sau crash service thất bại");
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Bước 6: Dọn dẹp sau khi cập nhật thành công
            _logger.LogInformation("Cập nhật thành công. Đang dọn dẹp...");
            await CleanUpAsync();
            
            _logger.LogInformation("Cập nhật hoàn tất thành công");
            return (int)UpdaterExitCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định trong quá trình cập nhật");
            
            try
            {
                await _rollbackManager.RollbackAsync("UpdateDeploymentFailed");
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback sau lỗi không xác định thất bại");
                return (int)UpdaterExitCodes.RollbackFailed;
            }
            
            return (int)UpdaterExitCodes.GeneralError;
        }
    }
    
    /// <summary>
    /// Chờ process thoát
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="timeout">Thời gian timeout</param>
    /// <returns>true nếu process đã thoát; false nếu timeout</returns>
    private async Task<bool> WaitForProcessExitAsync(int pid, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (!_serviceHelper.IsProcessRunning(pid))
                {
                    _logger.LogInformation("Process {PID} đã thoát", pid);
                    return true;
                }
                
                _logger.LogDebug("Process {PID} vẫn đang chạy. Chờ thêm 1 giây...", pid);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra process {PID}", pid);
                // Nếu có lỗi khi kiểm tra, giả định process đã thoát
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Sao lưu agent hiện tại
    /// </summary>
    private async Task BackupAgentAsync()
    {
        string backupFolderPath = Path.Combine(_currentAgentInstallDir, $"backup_{_currentAgentVersion}");
        
        // Xóa backup cũ nếu tồn tại
        if (Directory.Exists(backupFolderPath))
        {
            _logger.LogInformation("Xóa backup cũ tại {BackupPath}...", backupFolderPath);
            Directory.Delete(backupFolderPath, true);
        }
        
        // Tạo thư mục backup
        _logger.LogInformation("Tạo thư mục backup tại {BackupPath}...", backupFolderPath);
        Directory.CreateDirectory(backupFolderPath);
        
        // Sao chép file và thư mục (trừ thư mục backup và update)
        foreach (var entry in Directory.GetFileSystemEntries(_currentAgentInstallDir))
        {
            var entryName = Path.GetFileName(entry);
            
            // Bỏ qua các thư mục không cần sao lưu
            if (entryName.StartsWith("backup_", StringComparison.OrdinalIgnoreCase) ||
                entryName.Equals("updates", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var destPath = Path.Combine(backupFolderPath, entryName);
            
            try
            {
                if (File.Exists(entry))
                {
                    _logger.LogDebug("Sao lưu file: {FilePath}", entry);
                    File.Copy(entry, destPath, true);
                }
                else if (Directory.Exists(entry))
                {
                    _logger.LogDebug("Sao lưu thư mục: {DirPath}", entry);
                    CopyDirectory(entry, destPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể sao lưu {Entry}", entry);
                throw;
            }
        }
        
        await Task.CompletedTask; // Để method là async
    }
    
    /// <summary>
    /// Triển khai agent mới
    /// </summary>
    private async Task DeployNewAgentAsync()
    {
        // Xóa các file hiện tại (trừ backup và updates)
        _logger.LogInformation("Xóa các file agent cũ...");
        foreach (var entry in Directory.GetFileSystemEntries(_currentAgentInstallDir))
        {
            var entryName = Path.GetFileName(entry);
            
            // Bỏ qua thư mục backup và updates
            if (entryName.StartsWith("backup_", StringComparison.OrdinalIgnoreCase) ||
                entryName.Equals("updates", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            try
            {
                if (File.Exists(entry))
                {
                    _logger.LogDebug("Xóa file: {FilePath}", entry);
                    File.Delete(entry);
                }
                else if (Directory.Exists(entry))
                {
                    _logger.LogDebug("Xóa thư mục: {DirPath}", entry);
                    Directory.Delete(entry, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể xóa {Entry}", entry);
                throw;
            }
        }
        
        // Sao chép file mới
        _logger.LogInformation("Sao chép các file agent mới...");
        foreach (var entry in Directory.GetFileSystemEntries(_newAgentPath))
        {
            var entryName = Path.GetFileName(entry);
            var destPath = Path.Combine(_currentAgentInstallDir, entryName);
            
            try
            {
                if (File.Exists(entry))
                {
                    _logger.LogDebug("Sao chép file: {FilePath} -> {DestPath}", entry, destPath);
                    File.Copy(entry, destPath, true);
                }
                else if (Directory.Exists(entry))
                {
                    _logger.LogDebug("Sao chép thư mục: {DirPath} -> {DestPath}", entry, destPath);
                    CopyDirectory(entry, destPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể sao chép {Entry}", entry);
                throw;
            }
        }
        
        await Task.CompletedTask; // Để method là async
    }
    
    /// <summary>
    /// Theo dõi service trong một khoảng thời gian
    /// </summary>
    /// <param name="watchTime">Thời gian theo dõi</param>
    /// <param name="checkInterval">Khoảng thời gian giữa các lần kiểm tra</param>
    /// <returns>true nếu service ổn định trong thời gian theo dõi; false nếu service không ổn định</returns>
    private async Task<bool> WatchdogServiceAsync(TimeSpan watchTime, TimeSpan checkInterval)
    {
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < watchTime)
        {
            if (!_serviceHelper.IsAgentServiceRunning(_agentServiceName))
            {
                _logger.LogError("Service {ServiceName} đã dừng trong thời gian theo dõi!", _agentServiceName);
                return false;
            }
            
            _logger.LogDebug("Service {ServiceName} đang chạy bình thường. Còn lại {TimeLeft} để theo dõi.", 
                _agentServiceName, watchTime - stopwatch.Elapsed);
            
            await Task.Delay(checkInterval);
        }
        
        return true;
    }
    
    /// <summary>
    /// Dọn dẹp sau khi cập nhật thành công
    /// </summary>
    private async Task CleanUpAsync()
    {
        try
        {
            // Dọn dẹp các file tạm nếu cần
            
            // Có thể xóa thư mục agent mới đã giải nén nếu cần
            if (Directory.Exists(_newAgentPath))
            {
                _logger.LogInformation("Xóa thư mục agent mới đã giải nén: {Path}", _newAgentPath);
                Directory.Delete(_newAgentPath, true);
            }
            
            // Có thể thực hiện các công việc dọn dẹp khác ở đây
        }
        catch (Exception ex)
        {
            // Log lỗi nhưng không ném ngoại lệ vì đây chỉ là dọn dẹp
            _logger.LogWarning(ex, "Có lỗi trong quá trình dọn dẹp sau khi cập nhật");
        }
        
        await Task.CompletedTask; // Để method là async
    }
    
    /// <summary>
    /// Sao chép toàn bộ thư mục và nội dung bên trong
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        // Tạo thư mục đích nếu chưa tồn tại
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        // Sao chép tất cả file
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        // Sao chép tất cả thư mục con
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
} 