using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho việc xử lý cập nhật agent.
    /// </summary>
    public interface IUpdateHandler
    {
        /// <summary>
        /// Kiểm tra xem có phiên bản mới của agent không.
        /// </summary>
        /// <param name="manualCheck">Cờ để xác định đây là kiểm tra thủ công hay tự động.</param>
        /// <returns>Task đại diện cho quá trình kiểm tra cập nhật.</returns>
        Task CheckForUpdateAsync(bool manualCheck = false);

        /// <summary>
        /// Xử lý thông tin phiên bản mới nhận được từ server.
        /// </summary>
        /// <param name="updateInfo">Thông tin về phiên bản mới.</param>
        /// <returns>Task đại diện cho quá trình cập nhật.</returns>
        Task ProcessUpdateAsync(UpdateCheckResponse updateInfo);
    }
}
