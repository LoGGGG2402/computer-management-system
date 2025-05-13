using System.Threading.Tasks;
using CMSAgent.Common.Models;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho việc tải và lưu cấu hình agent.
    /// </summary>
    public interface IConfigLoader
    {
        /// <summary>
        /// Cấu hình chính của agent từ appsettings.json.
        /// </summary>
        CmsAgentSettingsOptions Settings { get; }

        /// <summary>
        /// Cấu hình đặc thù cho agent từ appsettings.json.
        /// </summary>
        AgentSpecificSettingsOptions AgentSettings { get; }

        /// <summary>
        /// Tải cấu hình runtime từ file.
        /// </summary>
        /// <param name="forceReload">Bắt buộc tải lại từ đĩa thay vì dùng bộ nhớ đệm.</param>
        /// <returns>Cấu hình runtime hoặc null nếu không thể tải.</returns>
        Task<RuntimeConfig> LoadRuntimeConfigAsync(bool forceReload = false);

        /// <summary>
        /// Lưu cấu hình runtime xuống file.
        /// </summary>
        /// <param name="config">Cấu hình runtime cần lưu.</param>
        /// <returns>Task đại diện cho việc lưu cấu hình.</returns>
        Task SaveRuntimeConfigAsync(RuntimeConfig config);

        /// <summary>
        /// Lấy ID của agent từ cấu hình runtime đã tải.
        /// </summary>
        /// <returns>ID của agent hoặc null nếu chưa tải cấu hình.</returns>
        string GetAgentId();

        /// <summary>
        /// Lấy token đã mã hóa của agent từ cấu hình runtime đã tải.
        /// </summary>
        /// <returns>Token đã mã hóa hoặc null nếu chưa tải cấu hình.</returns>
        string GetEncryptedAgentToken();

        /// <summary>
        /// Lấy đường dẫn cài đặt của agent.
        /// </summary>
        /// <returns>Đường dẫn thư mục cài đặt.</returns>
        string GetInstallPath();

        /// <summary>
        /// Lấy đường dẫn thư mục dữ liệu của agent.
        /// </summary>
        /// <returns>Đường dẫn thư mục dữ liệu.</returns>
        string GetDataPath();
    }
}
