using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho việc quản lý hàng đợi và thực thi lệnh.
    /// </summary>
    public interface ICommandExecutor
    {
        /// <summary>
        /// Thử thêm một lệnh vào hàng đợi.
        /// </summary>
        /// <param name="command">Lệnh cần thêm vào hàng đợi.</param>
        /// <returns>True nếu thêm thành công, False nếu hàng đợi đã đầy.</returns>
        bool TryEnqueueCommand(CommandPayload command);

        /// <summary>
        /// Bắt đầu xử lý các lệnh trong hàng đợi.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho quá trình xử lý lệnh.</returns>
        Task StartProcessingAsync(CancellationToken cancellationToken);
    }
}
