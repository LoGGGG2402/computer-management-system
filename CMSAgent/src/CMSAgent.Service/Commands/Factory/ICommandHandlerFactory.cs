 // CMSAgent.Service/Commands/Factory/ICommandHandlerFactory.cs
using CMSAgent.Service.Commands.Handlers;
using CMSAgent.Service.Commands.Models;

namespace CMSAgent.Service.Commands.Factory
{
    /// <summary>
    /// Interface cho factory tạo ra các command handler.
    /// </summary>
    public interface ICommandHandlerFactory
    {
        /// <summary>
        /// Tạo một ICommandHandler dựa trên loại lệnh.
        /// </summary>
        /// <param name="commandType">Loại lệnh (ví dụ: "CONSOLE", "SYSTEM_ACTION").</param>
        /// <returns>Một instance của ICommandHandler phù hợp, hoặc null nếu loại lệnh không được hỗ trợ.</returns>
        ICommandHandler? CreateHandler(string commandType);
    }
}
