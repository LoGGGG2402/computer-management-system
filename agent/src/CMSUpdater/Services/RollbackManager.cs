using Microsoft.Extensions.Logging;
namespace CMSUpdater.Services;

/// <summary>
/// Lớp quản lý quá trình rollback khi cập nhật thất bại
/// </summary>
public class RollbackManager
{
    private readonly ILogger<RollbackManager> _logger;
    private readonly int _agentProcessIdToWait;
    private readonly string _newAgentPath;
    private readonly string _currentAgentInstallDir;
    private readonly string _updaterLogDir;
    private readonly string _currentAgentVersion;
    private readonly ServiceHelper _serviceHelper;
    private readonly string _agentServiceName = "CMSAgentService";
    private readonly string _backupFolderPath;
    
    /// <summary>
    /// Khởi tạo RollbackManager
    /// </summary>
    /// <param name="logger">Logger để ghi log</param>
    /// <param name="agentProcessIdToWait">PID của tiến trình agent cũ cần dừng</param>
    /// <param name="newAgentPath">Đường dẫn đến thư mục chứa file agent mới đã giải nén</param>
    /// <param name="currentAgentInstallDir">Đường dẫn thư mục cài đặt hiện tại</param>
    /// <param name="updaterLogDir">Nơi ghi file log của updater</param>
    /// <param name="currentAgentVersion">Phiên bản agent hiện tại</param>
    /// <param name="serviceHelper">Helper để tương tác với Windows Service</param>
    public RollbackManager(
        ILogger<RollbackManager> logger, 
        int agentProcessIdToWait, 
        string newAgentPath, 
        string currentAgentInstallDir, 
        string updaterLogDir, 
        string currentAgentVersion, 
        ServiceHelper serviceHelper)
    {
        _logger = logger;
        _agentProcessIdToWait = agentProcessIdToWait;
        _newAgentPath = newAgentPath;
        _currentAgentInstallDir = currentAgentInstallDir;
        _updaterLogDir = updaterLogDir;
        _currentAgentVersion = currentAgentVersion;
        _serviceHelper = serviceHelper;
        _backupFolderPath = Path.Combine(_currentAgentInstallDir, "backup_" + _currentAgentVersion);
    }
    
    /// <summary>
    /// Thực hiện rollback
    /// </summary>
    /// <param name="reason">Lý do rollback</param>
    /// <returns>Task đại diện cho quá trình rollback bất đồng bộ</returns>
    public async Task RollbackAsync(string reason)
    {
        _logger.LogWarning("Bắt đầu quá trình rollback. Lý do: {Reason}", reason);
        
        try
        {
            // Dừng service agent mới (nếu đã được khởi động)
            if (_serviceHelper.IsAgentServiceRunning(_agentServiceName))
            {
                _logger.LogInformation("Dừng service agent mới trước khi rollback...");
                try
                {
                    _serviceHelper.StopAgentService(_agentServiceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể dừng service agent mới. Tiếp tục rollback...");
                }
            }
            
            // Kiểm tra xem backup có tồn tại không
            if (!Directory.Exists(_backupFolderPath))
            {
                _logger.LogError("Không tìm thấy thư mục backup tại: {BackupPath}. Không thể rollback.", _backupFolderPath);
                throw new DirectoryNotFoundException($"Thư mục backup không tồn tại: {_backupFolderPath}");
            }
            
            // Xóa nội dung hiện tại của thư mục cài đặt (trừ thư mục backup)
            _logger.LogInformation("Xóa nội dung thư mục cài đặt hiện tại (trừ backup)...");
            foreach (var entry in Directory.GetFileSystemEntries(_currentAgentInstallDir))
            {
                var entryName = Path.GetFileName(entry);
                var backupFolderName = Path.GetFileName(_backupFolderPath);
                
                // Bỏ qua thư mục backup
                if (string.Equals(entryName, backupFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                try
                {
                    if (File.Exists(entry))
                    {
                        File.Delete(entry);
                    }
                    else if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể xóa {Entry} trong quá trình rollback.", entry);
                }
            }
            
            // Sao chép nội dung từ backup vào thư mục cài đặt
            _logger.LogInformation("Sao chép nội dung từ backup vào thư mục cài đặt...");
            foreach (var entry in Directory.GetFileSystemEntries(_backupFolderPath))
            {
                var entryName = Path.GetFileName(entry);
                var destPath = Path.Combine(_currentAgentInstallDir, entryName);
                
                try
                {
                    if (File.Exists(entry))
                    {
                        File.Copy(entry, destPath, true);
                    }
                    else if (Directory.Exists(entry))
                    {
                        CopyDirectory(entry, destPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể sao chép {Entry} trong quá trình rollback.", entry);
                }
            }
            
            // Khởi động lại service agent cũ
            _logger.LogInformation("Khởi động lại service agent cũ...");
            try
            {
                _serviceHelper.StartAgentService(_agentServiceName);
                _logger.LogInformation("Khởi động lại service agent cũ thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể khởi động lại service agent cũ sau khi rollback.");
                throw;
            }
            
            _logger.LogInformation("Rollback hoàn tất thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback thất bại. Lý do: {Reason}", reason);
            throw;
        }
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