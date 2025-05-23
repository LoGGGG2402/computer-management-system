using CMSAgent.Service.Commands.Models;

namespace CMSAgent.Service.Commands.Handlers
{
    public class GetLogsCommandHandler : CommandHandlerBase
    {
        public GetLogsCommandHandler(ILogger<GetLogsCommandHandler> logger) : base(logger)
        {
        }

        protected override Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandOutputResult.CreateError(
                "This command handler is currently disabled.",
                null,
                -1
            ));
        }
    }
}
