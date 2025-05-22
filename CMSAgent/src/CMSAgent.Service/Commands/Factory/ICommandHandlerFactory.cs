// CMSAgent.Service/Commands/Factory/ICommandHandlerFactory.cs
using CMSAgent.Service.Commands.Handlers;
using CMSAgent.Service.Commands.Models;

namespace CMSAgent.Service.Commands.Factory
{
    /// <summary>
    /// Interface for factory that creates command handlers.
    /// </summary>
    public interface ICommandHandlerFactory
    {
        /// <summary>
        /// Creates an ICommandHandler based on the command type.
        /// </summary>
        /// <param name="commandType">Command type (e.g., "CONSOLE", "SYSTEM_ACTION").</param>
        /// <returns>An instance of the appropriate ICommandHandler, or null if the command type is not supported.</returns>
        ICommandHandler? CreateHandler(string commandType);
    }
}
