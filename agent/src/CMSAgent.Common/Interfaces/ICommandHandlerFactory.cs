using CMSAgent.Common.Enums;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho factory tạo ra các command handler phù hợp với từng loại command.
    /// </summary>
    public interface ICommandHandlerFactory
    {
        /// <summary>
        /// Tạo và trả về handler phù hợp với loại command.
        /// </summary>
        /// <param name="commandType">Loại command cần xử lý.</param>
        /// <returns>Command handler phù hợp để xử lý command.</returns>
        ICommandHandler GetHandler(CommandType commandType);
    }
}
