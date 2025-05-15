using Microsoft.Extensions.Logging;
using System.Diagnostics;
using CMSUpdater.Services;
using CMSAgent.Common.Enums;
using System.Runtime.Versioning;
using CMSAgent.Common.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
    private readonly string _currentAgentVersion;
    private readonly string _agentServiceName = "CMSAgentService";
    
    // Các thời gian timeout và chờ
    private readonly TimeSpan _processExitTimeout = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _serviceWatchdogTime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _serviceCheckInterval = TimeSpan.FromSeconds(30);
    
    private readonly IConfiguration _configuration;
    
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
    /// <param name="configuration">Configuration để đọc cấu hình</param>
    public UpdaterLogic(
        ILogger logger,
        RollbackManager rollbackManager,
        ServiceHelper serviceHelper,
        int agentProcessIdToWait,
        string newAgentPath,
        string currentAgentInstallDir,
        string updaterLogDir,
        string currentAgentVersion,
        IConfiguration configuration)
    {
        _logger = logger;
        _rollbackManager = rollbackManager;
        _serviceHelper = serviceHelper;
        _agentProcessIdToWait = agentProcessIdToWait;
        _newAgentPath = newAgentPath;
        _currentAgentInstallDir = currentAgentInstallDir;
        _currentAgentVersion = currentAgentVersion;
        _configuration = configuration;
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
                ErrorLogs.LogError(ErrorType.UpdateFailure, 
                    $"Timeout khi chờ process agent cũ (PID: {_agentProcessIdToWait}) dừng", 
                    new { ProcessId = _agentProcessIdToWait, Timeout = _processExitTimeout }, 
                    _logger);
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
                    ErrorLogs.LogException(ErrorType.ServiceOperationFailure, ex, _logger);
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
                ErrorLogs.LogException(ErrorType.BackupFailure, ex, _logger);
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
                ErrorLogs.LogException(ErrorType.DeploymentFailure, ex, _logger);
                
                try
                {
                    await _rollbackManager.RollbackAsync("UpdateDeploymentFailed");
                    return (int)UpdaterExitCodes.DeployFailed;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback sau lỗi triển khai thất bại");
                    ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
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
                ErrorLogs.LogException(ErrorType.ServiceOperationFailure, ex, _logger);
                
                try
                {
                    await _rollbackManager.RollbackAsync("NewServiceStartFailed");
                    return (int)UpdaterExitCodes.NewServiceStartFailed;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback sau lỗi khởi động service thất bại");
                    ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
                    return (int)UpdaterExitCodes.RollbackFailed;
                }
            }
            
            // Bước 5: Watchdog để đảm bảo agent mới hoạt động ổn định
            _logger.LogInformation("Theo dõi service mới trong {WatchdogTime} (theo dõi crash)...", _serviceWatchdogTime);
            if (!await WatchdogServiceAsync(_serviceWatchdogTime, _serviceCheckInterval))
            {
                _logger.LogError("Service agent mới không ổn định, đang thực hiện rollback...");
                ErrorLogs.LogError(ErrorType.ServiceInstability, 
                    "Service agent mới không ổn định sau khi cập nhật", 
                    new { ServiceName = _agentServiceName, WatchdogTime = _serviceWatchdogTime }, 
                    _logger);
                
                try
                {
                    await _rollbackManager.RollbackAsync("NewServiceUnstable");
                    return (int)UpdaterExitCodes.WatchdogTriggeredRollback;
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback sau crash service thất bại");
                    ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
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
            ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
            
            try
            {
                await _rollbackManager.RollbackAsync("UpdateDeploymentFailed");
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback sau lỗi không xác định thất bại");
                ErrorLogs.LogException(ErrorType.RollbackFailure, rollbackEx, _logger);
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
                ErrorLogs.LogException(ErrorType.ServiceOperationFailure, ex, _logger);
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
                ErrorLogs.LogException(ErrorType.BackupFailure, ex, _logger);
                throw;
            }
        }
        
        await Task.CompletedTask; // Để method là async
    }
    
    /// <summary>
    /// Triển khai agent mới
    /// </summary>
    /// <returns>Task đại diện cho quá trình triển khai</returns>
    private async Task DeployNewAgentAsync()
    {
        await Task.Yield(); // Đảm bảo phương thức là async
        
        _logger.LogInformation("Đang triển khai agent mới từ {SourceDir} vào {TargetDir}", _newAgentPath, _currentAgentInstallDir);
        
        // Kiểm tra xem thư mục mới có tồn tại không
        if (!Directory.Exists(_newAgentPath))
        {
            _logger.LogError("Thư mục chứa agent mới không tồn tại: {Path}", _newAgentPath);
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục agent mới: {_newAgentPath}");
        }
        
        // Đọc danh sách file loại trừ từ cấu hình
        var filesToExclude = _configuration.GetSection("Updater:FilesToExcludeFromUpdate").Get<string[]>() ?? Array.Empty<string>();
        _logger.LogInformation("Các file sẽ được loại trừ khỏi cập nhật: {Files}", string.Join(", ", filesToExclude));

        // Thu thập tất cả các file trong thư mục nguồn (agent mới)
        var sourceFiles = Directory.GetFiles(_newAgentPath, "*", SearchOption.AllDirectories);
        _logger.LogDebug("Tìm thấy {Count} file trong thư mục nguồn", sourceFiles.Length);
        
        int filesProcessed = 0;
        int filesCopied = 0;
        int filesSkipped = 0;
        
        foreach (var sourceFile in sourceFiles)
        {
            filesProcessed++;
            
            // Đường dẫn tương đối so với thư mục nguồn
            string relativePath = Path.GetRelativePath(_newAgentPath, sourceFile);
            
            // Kiểm tra xem file có thuộc danh sách loại trừ không
            bool shouldExclude = false;
            foreach (var pattern in filesToExclude)
            {
                if (IsFileMatchPattern(relativePath, pattern))
                {
                    shouldExclude = true;
                    _logger.LogDebug("Bỏ qua file phù hợp với mẫu loại trừ: {File} (mẫu: {Pattern})", relativePath, pattern);
                    break;
                }
            }
            
            if (shouldExclude)
            {
                filesSkipped++;
                continue;
            }
            
            // Đường dẫn đích cho file
            string targetFile = Path.Combine(_currentAgentInstallDir, relativePath);
            
            try
            {
                // Tạo thư mục đích nếu chưa tồn tại
                string targetDir = Path.GetDirectoryName(targetFile) ?? string.Empty;
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                
                // Sao chép file, ghi đè nếu đã tồn tại
                File.Copy(sourceFile, targetFile, true);
                filesCopied++;
                
                // Log mỗi 100 file để tránh log quá nhiều
                if (filesProcessed % 100 == 0 || filesProcessed == sourceFiles.Length)
                {
                    _logger.LogInformation("Tiến độ sao chép: {Processed}/{Total} files (đã sao chép: {Copied}, bỏ qua: {Skipped})",
                        filesProcessed, sourceFiles.Length, filesCopied, filesSkipped);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sao chép file {Source} đến {Target}", sourceFile, targetFile);
                throw new IOException($"Không thể sao chép file {sourceFile}: {ex.Message}", ex);
            }
        }
        
        _logger.LogInformation("Triển khai thành công: Tổng số {Total} files, đã sao chép {Copied}, bỏ qua {Skipped}",
            filesProcessed, filesCopied, filesSkipped);
    }
    
    /// <summary>
    /// Kiểm tra xem một file có phù hợp với mẫu glob không
    /// </summary>
    /// <param name="filePath">Đường dẫn file cần kiểm tra</param>
    /// <param name="pattern">Mẫu glob (ví dụ: *.json, logs/**)</param>
    /// <returns>True nếu file phù hợp với mẫu</returns>
    private bool IsFileMatchPattern(string filePath, string pattern)
    {
        // Xử lý các ký tự đặc biệt trong mẫu glob
        if (pattern.StartsWith("*"))
        {
            string extension = pattern.TrimStart('*');
            return filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }
        
        // Kiểm tra nếu filePath khớp chính xác với pattern
        if (string.Equals(filePath, pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // Kiểm tra nếu filePath là thư mục cụ thể
        if (pattern.EndsWith("/") && filePath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // Kiểm tra nếu filePath là một file trong thư mục được chỉ định
        string directory = pattern;
        if (directory.EndsWith("/*") || directory.EndsWith("/**"))
        {
            directory = directory.Substring(0, directory.LastIndexOf('/'));
            return filePath.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
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
            ErrorLogs.LogException(ErrorType.UpdateFailure, ex, _logger);
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