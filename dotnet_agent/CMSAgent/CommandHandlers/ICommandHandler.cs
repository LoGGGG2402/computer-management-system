using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.CommandHandlers
{
    public interface ICommandHandler
    {
        Task<CommandResult> ExecuteCommandAsync(string commandId, string commandPayload, CancellationToken cancellationToken);
    }
}
