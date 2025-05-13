using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface định nghĩa một command handler có khả năng xử lý một loại command cụ thể.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Thực thi một command và trả về kết quả.
        /// </summary>
        /// <param name="command">Command cần thực thi.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Kết quả của việc thực thi command.</returns>
        Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken);
    }
}
