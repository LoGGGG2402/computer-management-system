using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface defining a command handler capable of processing a specific type of command.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Executes a command and returns the result.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Result of command execution.</returns>
        Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken);
    }
}
