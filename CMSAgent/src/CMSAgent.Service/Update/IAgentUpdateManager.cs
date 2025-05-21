// CMSAgent.Service/Update/IAgentUpdateManager.cs
using CMSAgent.Service.Models; // For UpdateNotification
using System.Threading.Tasks;
using System.Threading; // For CancellationToken

namespace CMSAgent.Service.Update
{
    /// <summary>
    /// Interface định nghĩa các phương thức quản lý quy trình cập nhật của Agent.
    /// </summary>
    public interface IAgentUpdateManager
    {
        /// <summary>
        /// Kiểm tra xem có bản cập nhật mới từ server hay không và nếu có thì bắt đầu quá trình cập nhật.
        /// Phương thức này có thể được gọi định kỳ.
        /// </summary>
        /// <param name="currentAgentVersion">Phiên bản hiện tại của Agent.</param>
        /// <param name="cancellationToken">Token để hủy bỏ quá trình.</param>
        Task UpdateAndInitiateAsync(string currentAgentVersion, CancellationToken cancellationToken = default);

        /// <summary>
        /// Xử lý thông báo có phiên bản mới nhận được từ server (ví dụ: qua WebSocket).
        /// </summary>
        /// <param name="updateNotification">Thông tin chi tiết về bản cập nhật.</param>
        /// <param name="cancellationToken">Token để hủy bỏ quá trình.</param>
        Task ProcessUpdateNotificationAsync(UpdateNotification updateNotification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra xem có đang trong quá trình cập nhật hay không.
        /// </summary>
        bool IsUpdateInProgress { get; }
    }
}
