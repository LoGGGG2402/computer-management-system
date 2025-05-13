using CMSAgent.Common.Enums;

namespace CMSUpdater;

/// <summary>
/// Class chứa các tham số dòng lệnh cho Updater
/// </summary>
public class UpdateParameters 
{
    /// <summary>
    /// Process ID của tiến trình CMSAgent.exe cũ cần dừng
    /// </summary>
    public int AgentProcessIdToWait { get; set; }
    
    /// <summary>
    /// Đường dẫn đến thư mục chứa file agent mới đã giải nén
    /// </summary>
    public string NewAgentPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Đường dẫn thư mục cài đặt hiện tại
    /// </summary>
    public string CurrentAgentInstallDir { get; set; } = string.Empty;
    
    /// <summary>
    /// Nơi ghi file log của updater
    /// </summary>
    public string UpdaterLogDir { get; set; } = string.Empty;
    
    /// <summary>
    /// Phiên bản agent hiện tại (dùng cho tên backup)
    /// </summary>
    public string CurrentAgentVersion { get; set; } = string.Empty;
}
