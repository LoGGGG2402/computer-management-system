using CMSAgent.Service.Commands.Models;

namespace CMSAgent.Service.Commands.Handlers
{
    public class SoftwareUninstallCommandHandler : CommandHandlerBase
    {
        public SoftwareUninstallCommandHandler(ILogger<SoftwareUninstallCommandHandler> logger) : base(logger)
        {
        }

        protected override Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandOutputResult.CreateError(
                ErrorCode.SOFTWARE_UNINSTALL_ERROR,
                "This command handler is currently disabled.",
                null,
                -1
            ));
        }
    }
}
