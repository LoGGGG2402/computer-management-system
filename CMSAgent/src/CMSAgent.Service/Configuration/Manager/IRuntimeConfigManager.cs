 // CMSAgent.Service/Configuration/Manager/IRuntimeConfigManager.cs
using CMSAgent.Service.Configuration.Models; // For RuntimeConfig
using System.Threading.Tasks;

namespace CMSAgent.Service.Configuration.Manager
{
    /// <summary>
    /// Interface định nghĩa các phương thức để quản lý cấu hình runtime của Agent (runtime_config.json).
    /// </summary>
    public interface IRuntimeConfigManager
    {
        /// <summary>
        /// Tải cấu hình runtime từ file.
        /// Nếu file không tồn tại hoặc có lỗi, một cấu hình mặc định (hoặc rỗng) có thể được trả về.
        /// </summary>
        /// <returns>Đối tượng RuntimeConfig đã được tải, hoặc một instance mới nếu không tải được.</returns>
        Task<RuntimeConfig> LoadConfigAsync();

        /// <summary>
        /// Lưu đối tượng RuntimeConfig hiện tại vào file.
        /// </summary>
        /// <param name="config">Đối tượng RuntimeConfig cần lưu.</param>
        /// <returns>True nếu lưu thành công, ngược lại False.</returns>
        Task<bool> SaveConfigAsync(RuntimeConfig config);

        /// <summary>
        /// Lấy Agent ID hiện tại từ cấu hình.
        /// </summary>
        /// <returns>Agent ID hoặc null nếu chưa được cấu hình.</returns>
        Task<string?> GetAgentIdAsync();

        /// <summary>
        /// Lấy token đã mã hóa của Agent từ cấu hình.
        /// </summary>
        /// <returns>Token đã mã hóa hoặc null nếu chưa có.</returns>
        Task<string?> GetEncryptedAgentTokenAsync();

        /// <summary>
        /// Lấy thông tin vị trí (RoomConfig) của Agent.
        /// </summary>
        /// <returns>Đối tượng PositionInfo hoặc null nếu chưa được cấu hình.</returns>
        Task<PositionInfo?> GetPositionInfoAsync();

        /// <summary>
        /// Cập nhật Agent ID trong cấu hình và lưu lại.
        /// </summary>
        /// <param name="agentId">Agent ID mới.</param>
        Task UpdateAgentIdAsync(string agentId);

        /// <summary>
        /// Cập nhật token đã mã hóa và lưu lại.
        /// </summary>
        /// <param name="encryptedToken">Token đã mã hóa mới.</param>
        Task UpdateEncryptedAgentTokenAsync(string encryptedToken);

        /// <summary>
        /// Cập nhật thông tin vị trí và lưu lại.
        /// </summary>
        /// <param name="positionInfo">Thông tin vị trí mới.</param>
        Task UpdatePositionInfoAsync(PositionInfo positionInfo);

        /// <summary>
        /// Lấy đường dẫn đầy đủ đến thư mục gốc lưu trữ dữ liệu của Agent trong ProgramData
        /// (ví dụ: C:\ProgramData\CMSAgent).
        /// </summary>
        /// <returns>Đường dẫn đến thư mục ProgramData của Agent.</returns>
        string GetAgentProgramDataPath();

        /// <summary>
        /// Lấy đường dẫn đầy đủ đến file runtime_config.json.
        /// </summary>
        /// <returns>Đường dẫn đến file runtime_config.json.</returns>
        string GetRuntimeConfigFilePath();
    }
}
