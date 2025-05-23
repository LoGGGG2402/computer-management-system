using CMSAgent.Service.Commands.Models;

namespace CMSAgent.Service.Commands.Handlers
{
    public class SystemActionCommandHandler : CommandHandlerBase
    {
        public SystemActionCommandHandler(ILogger<SystemActionCommandHandler> logger) : base(logger)
        {
        }

        protected override Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandOutputResult.CreateError(
                ErrorCode.COMMAND_EXECUTION_ERROR,
                "This command handler is currently disabled.",
                null,
                -1
            ));
        }
    }
}
