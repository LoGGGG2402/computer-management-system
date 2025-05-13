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
        /// Thêm bản ghi trạng thái vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa thông tin cập nhật trạng thái.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        Task EnqueueStatusReportAsync(StatusUpdatePayload payload);

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
        /// Thêm trạng thái cập nhật vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa thông tin trạng thái cập nhật.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        Task EnqueueUpdateStatusAsync(UpdateStatusPayload payload);

        /// <summary>
        /// Xử lý tất cả các hàng đợi offline và gửi dữ liệu lên server.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho thao tác xử lý hàng đợi.</returns>
        Task ProcessQueuesAsync(CancellationToken cancellationToken);
    }
}
