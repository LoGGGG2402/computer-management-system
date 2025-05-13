using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface để quản lý các hàng đợi offline khi mất kết nối với server.
    /// </summary>
    public interface IOfflineQueueManager
    {

        /// <summary>
        /// Thêm kết quả command vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa kết quả của command.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        Task EnqueueCommandResultAsync(CommandResultPayload payload);

        /// <summary>
        /// Thêm báo cáo lỗi vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa thông tin lỗi.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        Task EnqueueErrorReportAsync(ErrorReportPayload payload);

        /// <summary>
        /// Xử lý tất cả các hàng đợi offline và gửi dữ liệu lên server.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho thao tác xử lý hàng đợi.</returns>
        Task ProcessQueuesAsync(CancellationToken cancellationToken);
    }
}
