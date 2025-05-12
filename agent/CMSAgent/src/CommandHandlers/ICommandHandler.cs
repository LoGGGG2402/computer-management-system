using CMSAgent.Models.Payloads;
using System.Threading.Tasks;

namespace CMSAgent.CommandHandlers
{
    /// <summary>
    /// Common interface for all command handlers.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Initializes the command handler.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Handles a command based on the provided CommandRequest.
        /// The specific action is determined by CommandRequest.commandType and CommandRequest.command.
        /// </summary>
        /// <param name="commandRequest">The command request payload from the server.</param>
        /// <returns>A CommandResult payload to be sent back to the server.</returns>
        Task<CommandResult> HandleCommandAsync(CommandRequest commandRequest);
    }
}