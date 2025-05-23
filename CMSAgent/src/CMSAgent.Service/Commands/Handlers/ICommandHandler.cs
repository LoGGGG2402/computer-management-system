using CMSAgent.Service.Commands.Models;
namespace CMSAgent.Service.Commands.Handlers
{
    /// <summary>
    /// Common interface for all command handlers.
    /// Each handler is responsible for processing a specific type of command.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="commandRequest">Object containing command request information.</param>
        /// <param name="cancellationToken">Token to cancel the command execution process.</param>
        /// <returns>A CommandResult object containing the result of command execution.</returns>
        Task<CommandResult> ExecuteAsync(CommandRequest commandRequest, CancellationToken cancellationToken);
    }
}
