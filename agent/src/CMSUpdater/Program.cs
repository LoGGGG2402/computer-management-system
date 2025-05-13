using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CMSAgent.Common.Enums;

namespace CMSUpdater;

/// <summary>
/// Lớp Program chính của CMSUpdater
/// </summary>
public class Program
{
    /// <summary>
    /// Logger tĩnh cho Updater
    /// </summary>
    private static ILogger _logger = NullLogger.Instance;
    
    /// <summary>
    /// Điểm vào chính của ứng dụng
    /// </summary>
    /// <param name="args">Tham số dòng lệnh</param>
    /// <returns>Mã trạng thái (exit code)</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var updateParams = ParseArguments(args);
            
            if (updateParams == null)
            {
                Console.Error.WriteLine("Tham số không hợp lệ cho CMSUpdater.");
                PrintUsage();
                return (int)UpdaterExitCodes.InvalidArguments;
            }
            
            _logger = LoggingSetup.CreateUpdaterLogger(updateParams.UpdaterLogDir, updateParams.CurrentAgentVersion);
            
            _logger.LogInformation("CMSUpdater đã khởi động với PID: {PID}, NewPath: {NewPath}, CurrentDir: {CurrentDir}, LogDir: {LogDir}, CurrentVersion: {Version}", 
                updateParams.AgentProcessIdToWait, updateParams.NewAgentPath, updateParams.CurrentAgentInstallDir, 
                updateParams.UpdaterLogDir, updateParams.CurrentAgentVersion);
            
            var serviceHelper = new ServiceHelper(_logger);
            var rollbackManager = new RollbackManager(_logger, updateParams, serviceHelper);
            var updaterLogic = new UpdaterLogic(_logger, rollbackManager, serviceHelper, updateParams);
            
            return await updaterLogic.ExecuteUpdateAsync();
        }
        catch (Exception ex)
        {
            if (_logger != NullLogger.Instance)
            {
                _logger.LogError(ex, "Lỗi không xử lý được trong CMSUpdater");
            }
            else
            {
                Console.Error.WriteLine($"Lỗi không xử lý được trong CMSUpdater: {ex}");
            }
            
            return (int)UpdaterExitCodes.GeneralError;
        }
    }
    
    /// <summary>
    /// Phân tích tham số dòng lệnh
    /// </summary>
    /// <param name="args">Mảng tham số dòng lệnh</param>
    /// <returns>UpdateParameters hoặc null nếu không hợp lệ</returns>
    private static UpdateParameters? ParseArguments(string[] args)
    {
        if (args.Length < 5)
        {
            return null;
        }
        
        var parameters = new UpdateParameters();
        bool hasPid = false;
        bool hasNewAgentPath = false;
        bool hasCurrentInstallDir = false;
        bool hasUpdaterLogDir = false;
        bool hasCurrentVersion = false;
        
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLower())
            {
                case "pid":
                case "--pid":
                    if (int.TryParse(args[i + 1], out int pid))
                    {
                        parameters.AgentProcessIdToWait = pid;
                        hasPid = true;
                    }
                    break;
                
                case "new-agent-path":
                case "--new-agent-path":
                    parameters.NewAgentPath = args[i + 1].Trim('"');
                    hasNewAgentPath = true;
                    break;
                
                case "current-agent-install-dir":
                case "--current-agent-install-dir":
                    parameters.CurrentAgentInstallDir = args[i + 1].Trim('"');
                    hasCurrentInstallDir = true;
                    break;
                
                case "updater-log-dir":
                case "--updater-log-dir":
                    parameters.UpdaterLogDir = args[i + 1].Trim('"');
                    hasUpdaterLogDir = true;
                    break;
                
                case "current-agent-version":
                case "--current-agent-version":
                    parameters.CurrentAgentVersion = args[i + 1].Trim('"');
                    hasCurrentVersion = true;
                    break;
            }
        }
        
        // Kiểm tra xem tất cả tham số bắt buộc đã có hay chưa
        if (!hasPid || !hasNewAgentPath || !hasCurrentInstallDir || !hasUpdaterLogDir || !hasCurrentVersion)
        {
            return null;
        }
        
        // Kiểm tra tính hợp lệ của đường dẫn
        if (!Directory.Exists(parameters.NewAgentPath))
        {
            Console.Error.WriteLine($"Lỗi: Thư mục agent mới không tồn tại: {parameters.NewAgentPath}");
            return null;
        }
        
        if (!Directory.Exists(parameters.CurrentAgentInstallDir))
        {
            Console.Error.WriteLine($"Lỗi: Thư mục cài đặt hiện tại không tồn tại: {parameters.CurrentAgentInstallDir}");
            return null;
        }
        
        return parameters;
    }
    
    /// <summary>
    /// In thông tin sử dụng ra console
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Cách sử dụng: CMSUpdater.exe [tham số]");
        Console.WriteLine("Các tham số bắt buộc:");
        Console.WriteLine("  --pid <process_id>                        PID của tiến trình CMSAgent.exe cũ cần dừng");
        Console.WriteLine("  --new-agent-path \"<đường_dẫn>\"            Đường dẫn đến thư mục chứa file agent mới đã giải nén");
        Console.WriteLine("  --current-agent-install-dir \"<đường_dẫn>\" Đường dẫn thư mục cài đặt hiện tại");
        Console.WriteLine("  --updater-log-dir \"<đường_dẫn>\"           Nơi ghi file log của updater");
        Console.WriteLine("  --current-agent-version \"<phiên_bản>\"     Phiên bản agent hiện tại (dùng cho tên backup)");
        Console.WriteLine();
        Console.WriteLine("Ví dụ:");
        Console.WriteLine("  CMSUpdater.exe --pid 1234 --new-agent-path \"C:\\ProgramData\\CMSAgent\\updates\\extracted\\v1.1.0\" --current-agent-install-dir \"C:\\Program Files\\CMSAgent\" --updater-log-dir \"C:\\ProgramData\\CMSAgent\\logs\" --current-agent-version \"1.0.2\"");
    }
}
