// CMSAgent.Service/Commands/Handlers/ConsoleCommandHandler.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Shared.Utils;
using Microsoft.Extensions.Options; 
using CMSAgent.Service.Configuration.Models; // For AppSettings

namespace CMSAgent.Service.Commands.Handlers
{
    public class ConsoleCommandHandler : CommandHandlerBase
    {
        private readonly AppSettings _appSettings;

        public ConsoleCommandHandler(IOptions<AppSettings> appSettingsOptions, ILogger<ConsoleCommandHandler> logger)
            : base(logger)
        {
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));
        }

        protected override async Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            string commandToExecute = commandRequest.Command;
            bool usePowerShell = false;
            if (commandRequest.Parameters != null && commandRequest.Parameters.TryGetValue("use_powershell", out var usePowerShellObj))
            {
                usePowerShell = Convert.ToBoolean(usePowerShellObj);
            }

            string fileName = usePowerShell ? "powershell.exe" : "cmd.exe";
            string arguments = usePowerShell ? $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{commandToExecute.Replace("\"", "\\\"")}\""
                                             : $"/c \"{commandToExecute}\"";

            Logger.LogInformation("Executing console command: {FileName} {Arguments}", fileName, arguments);

            var (stdout, stderr, exitCode) = await ProcessUtils.ExecuteCommandAsync(
                fileName,
                arguments,
                cancellationToken: cancellationToken // Timeout is handled by CancellationTokenSource in base class
            );

            if (exitCode != 0)
            {
                return CommandOutputResult.CreateError(
                    string.IsNullOrEmpty(stderr) ? $"Command exited with code {exitCode}." : stderr,
                    null,
                    exitCode
                );
            }

            return CommandOutputResult.CreateSuccess(stdout, stderr, exitCode);
        }

        protected override int GetDefaultCommandTimeoutSeconds(CommandRequest commandRequest)
        {
            int timeoutSec = 0;
            if (commandRequest.Parameters != null && commandRequest.Parameters.TryGetValue("timeout_sec", out var timeoutObj))
            {
                timeoutSec = Convert.ToInt32(timeoutObj);
            }
            return timeoutSec > 0 ? timeoutSec : _appSettings.CommandExecution.DefaultCommandTimeoutSeconds;
        }
    }
}
