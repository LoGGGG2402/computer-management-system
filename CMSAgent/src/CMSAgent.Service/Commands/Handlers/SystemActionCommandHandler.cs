using CMSAgent.Service.Commands.Models;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Commands.Handlers
{
    public class SystemActionCommandHandler : CommandHandlerBase
    {
        public SystemActionCommandHandler(ILogger<SystemActionCommandHandler> logger) : base(logger)
        {
        }

        protected override async Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            return new CommandOutputResult
            {
                ErrorMessage = "This command handler is currently disabled.",
                ExitCode = -1,
                ErrorCode = "HANDLER_DISABLED"
            };
        }
    }
}
