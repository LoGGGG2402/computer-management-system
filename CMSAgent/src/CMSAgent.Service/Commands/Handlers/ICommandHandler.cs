 // CMSAgent.Service/Commands/Handlers/ICommandHandler.cs
using CMSAgent.Service.Commands.Models;
using System.Threading.Tasks;
using System.Threading; // For CancellationToken

namespace CMSAgent.Service.Commands.Handlers
{
    /// <summary>
    /// Interface chung cho tất cả các command handler.
    /// Mỗi handler sẽ chịu trách nhiệm xử lý một loại lệnh cụ thể.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Thực thi lệnh.
        /// </summary>
        /// <param name="commandRequest">Đối tượng chứa thông tin yêu cầu lệnh.</param>
        /// <param name="cancellationToken">Token để hủy bỏ quá trình thực thi lệnh.</param>
        /// <returns>Một đối tượng CommandResult chứa kết quả của việc thực thi lệnh.</returns>
        Task<CommandResult> ExecuteAsync(CommandRequest commandRequest, CancellationToken cancellationToken);
    }
}
