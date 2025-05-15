using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using CMSAgent.Common.Enums;
using CMSUpdater.Core;
using CMSUpdater.Services;
using System.Runtime.Versioning;
using System.Reflection;
using Serilog;
using CMSAgent.Common.Logging;
using System.Text.Json;

/// <summary>
/// Lớp Program chính của CMSUpdater
/// </summary>
[SupportedOSPlatform("windows")]
public class Program
{
    /// <summary>
    /// Logger tĩnh cho Updater
    /// </summary>
    private static Microsoft.Extensions.Logging.ILogger _logger = NullLogger.Instance;
    
    /// <summary>
    /// Cấu hình ứng dụng
    /// </summary>
    private static IConfiguration _configuration = null!;

    /// <summary>
    /// Đường dẫn đến file update_info.json
    /// </summary>
    private static readonly string UpdateInfoPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "update_info.json");

    /// <summary>
    /// Điểm vào chính của ứng dụng
    /// </summary>
    /// <param name="args">Tham số dòng lệnh</param>
    /// <returns>Mã trạng thái (exit code)</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Đọc cấu hình từ appsettings.json
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
            
            // Cập nhật thông tin version từ assembly
            var assembly = Assembly.GetExecutingAssembly();
            
            // Thêm thông tin từ assembly vào configuration
            var configDictionary = new Dictionary<string, string?>
            {
                { "Application:Name", assembly.GetName().Name },
                { "Application:Version", assembly.GetName().Version?.ToString() }
            };

            // Kết hợp cấu hình hiện tại với thông tin từ assembly
            _configuration = new ConfigurationBuilder()
                .AddConfiguration(_configuration)
                .AddInMemoryCollection(configDictionary)
                .Build();
            
            // Cấu hình logging sử dụng LoggingSetup
            _logger = LoggingSetup.CreateLogger(_configuration);
            
            // Phân tích tham số dòng lệnh
            var (isValid, agentProcessIdToWait, newAgentPath, currentAgentInstallDir, updaterLogDir, currentAgentVersion) = ParseArguments(args);
            
            // Nếu tham số dòng lệnh không đủ, thử đọc từ file update_info.json
            if (!isValid && File.Exists(UpdateInfoPath))
            {
                _logger.LogInformation("Đọc thông tin cập nhật từ file {UpdateInfoPath}", UpdateInfoPath);
                
                try 
                {
                    var updateInfoJson = File.ReadAllText(UpdateInfoPath);
                    var updateInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(updateInfoJson);
                    
                    if (updateInfo != null)
                    {
                        // Lấy thông tin từ file update_info.json
                        if (updateInfo.TryGetValue("package_path", out var packagePath) &&
                            updateInfo.TryGetValue("install_directory", out var installDir) &&
                            updateInfo.TryGetValue("new_version", out var newVersion))
                        {
                            // PID hiện tại có thể không được lưu trong file, lấy pid từ tham số dòng lệnh
                            int pid = 0;
                            if (args.Length > 1 && args[0].Contains("pid") && int.TryParse(args[1], out pid))
                            {
                                agentProcessIdToWait = pid;
                            }
                            
                            // Sử dụng thông tin từ file update_info.json
                            newAgentPath = packagePath;
                            currentAgentInstallDir = installDir;
                            currentAgentVersion = newVersion;
                            updaterLogDir = Path.Combine(currentAgentInstallDir, "logs");
                            
                            isValid = !string.IsNullOrEmpty(newAgentPath) && 
                                     !string.IsNullOrEmpty(currentAgentInstallDir) && 
                                     !string.IsNullOrEmpty(currentAgentVersion);
                            
                            _logger.LogInformation("Đã đọc thông tin từ file update_info.json: " +
                                "PID={PID}, NewPath={NewPath}, InstallDir={InstallDir}, Version={Version}",
                                agentProcessIdToWait, newAgentPath, currentAgentInstallDir, currentAgentVersion);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi đọc file update_info.json");
                }
            }
            
            if (!isValid)
            {
                _logger.LogError("Tham số không hợp lệ cho CMSUpdater.");
                PrintUsage();
                return (int)UpdaterExitCodes.InvalidArguments;
            }
            
            _logger.LogInformation("CMSUpdater đã khởi động với PID: {PID}, NewPath: {NewPath}, CurrentDir: {CurrentDir}, LogDir: {LogDir}, CurrentVersion: {Version}", 
                agentProcessIdToWait, newAgentPath, currentAgentInstallDir, updaterLogDir, currentAgentVersion);
            
            // Đọc các cấu hình từ appsettings.json 
            int retryAttempts = _configuration.GetValue<int>("Updater:RetryAttempts", 3);
            int retryDelayMs = _configuration.GetValue<int>("Updater:RetryDelayMilliseconds", 1000);
            int processTimeoutSec = _configuration.GetValue<int>("Updater:WaitForProcessTimeoutSeconds", 30);
            var filesToExclude = _configuration.GetSection("Updater:FilesToExcludeFromUpdate").Get<string[]>() ?? Array.Empty<string>();
            
            _logger.LogInformation("Cấu hình từ appsettings.json: RetryAttempts={Attempts}, RetryDelay={Delay}ms, ProcessTimeout={Timeout}s, FilesToExclude={ExcludeCount} mục", 
                retryAttempts, retryDelayMs, processTimeoutSec, filesToExclude.Length);
            
            var serviceHelper = new ServiceHelper(_logger);
            var rollbackManager = new RollbackManager(_logger, currentAgentInstallDir, currentAgentVersion, serviceHelper);
            var updaterLogic = new UpdaterLogic(
                _logger, 
                rollbackManager, 
                serviceHelper, 
                agentProcessIdToWait, 
                newAgentPath, 
                currentAgentInstallDir, 
                updaterLogDir, 
                currentAgentVersion,
                _configuration);
            
            return await updaterLogic.ExecuteUpdateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xử lý được trong CMSUpdater");
            return (int)UpdaterExitCodes.GeneralError;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    /// <summary>
    /// Phân tích tham số dòng lệnh
    /// </summary>
    /// <param name="args">Mảng tham số dòng lệnh</param>
    /// <returns>Tuple chứa tính hợp lệ và các giá trị tham số</returns>
    private static (bool isValid, int agentProcessIdToWait, string newAgentPath, string currentAgentInstallDir, string updaterLogDir, string currentAgentVersion) ParseArguments(string[] args)
    {
        if (args.Length < 5)
        {
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        int agentProcessIdToWait = 0;
        string newAgentPath = string.Empty;
        string currentAgentInstallDir = string.Empty;
        string updaterLogDir = string.Empty;
        string currentAgentVersion = string.Empty;
        
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
                        agentProcessIdToWait = pid;
                        hasPid = true;
                    }
                    break;
                
                case "new-agent-path":
                case "--new-agent-path":
                    newAgentPath = args[i + 1].Trim('"');
                    hasNewAgentPath = true;
                    break;
                
                case "current-agent-install-dir":
                case "--current-agent-install-dir":
                    currentAgentInstallDir = args[i + 1].Trim('"');
                    hasCurrentInstallDir = true;
                    break;
                
                case "updater-log-dir":
                case "--updater-log-dir":
                    updaterLogDir = args[i + 1].Trim('"');
                    hasUpdaterLogDir = true;
                    break;
                
                case "current-agent-version":
                case "--current-agent-version":
                    currentAgentVersion = args[i + 1].Trim('"');
                    hasCurrentVersion = true;
                    break;
            }
        }
        
        // Kiểm tra xem tất cả tham số bắt buộc đã có hay chưa
        if (!hasPid || !hasNewAgentPath || !hasCurrentInstallDir || !hasUpdaterLogDir || !hasCurrentVersion)
        {
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        // Kiểm tra tính hợp lệ của đường dẫn
        if (!Directory.Exists(newAgentPath))
        {
            _logger.LogError($"Lỗi: Thư mục agent mới không tồn tại: {newAgentPath}");
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        if (!Directory.Exists(currentAgentInstallDir))
        {
            _logger.LogError($"Lỗi: Thư mục cài đặt hiện tại không tồn tại: {currentAgentInstallDir}");
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        return (true, agentProcessIdToWait, newAgentPath, currentAgentInstallDir, updaterLogDir, currentAgentVersion);
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
        Console.WriteLine("Lưu ý: Có thể sử dụng file update_info.json trong thư mục CMSUpdater thay cho tham số dòng lệnh");
        Console.WriteLine("Ví dụ:");
        Console.WriteLine("  CMSUpdater.exe --pid 1234 --new-agent-path \"C:\\ProgramData\\CMSAgent\\updates\\extracted\\v1.1.0\" --current-agent-install-dir \"C:\\Program Files\\CMSAgent\" --updater-log-dir \"C:\\ProgramData\\CMSAgent\\logs\" --current-agent-version \"1.0.2\"");
    }
} 