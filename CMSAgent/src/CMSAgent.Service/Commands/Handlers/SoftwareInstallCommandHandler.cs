using CMSAgent.Service.Commands.Models;

namespace CMSAgent.Service.Commands.Handlers
{
    public class SoftwareInstallCommandHandler : CommandHandlerBase
    {
        public SoftwareInstallCommandHandler(ILogger<SoftwareInstallCommandHandler> logger) : base(logger)
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
