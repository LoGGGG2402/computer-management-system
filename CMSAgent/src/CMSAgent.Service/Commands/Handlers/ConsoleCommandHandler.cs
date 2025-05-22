// CMSAgent.Service/Commands/Handlers/ConsoleCommandHandler.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Shared.Utils; // For ProcessUtils
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // For IOptions<AppSettings>
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
            bool usePowerShell = CommandParameterHelper.GetBool(commandRequest.Parameters, "use_powershell", false, Logger);

            string fileName = usePowerShell ? "powershell.exe" : "cmd.exe";
            string arguments = usePowerShell ? $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{commandToExecute.Replace("\"", "\\\"")}\""
                                             : $"/c \"{commandToExecute}\"";

            Logger.LogInformation("Executing console command: {FileName} {Arguments}", fileName, arguments);

            var (stdout, stderr, exitCode) = await ProcessUtils.ExecuteCommandAsync(
                fileName,
                arguments,
                cancellationToken: cancellationToken // Timeout is handled by CancellationTokenSource in base class
            );

            return new CommandOutputResult
            {
                Stdout = stdout,
                Stderr = stderr,
                ExitCode = exitCode,
                ErrorMessage = exitCode != 0 && string.IsNullOrEmpty(stderr) ? $"Command exited with code {exitCode}." : (string.IsNullOrEmpty(stderr) ? null : stderr)
            };
        }

        protected override int GetDefaultCommandTimeoutSeconds(CommandRequest commandRequest)
        {
            int timeoutSec = CommandParameterHelper.GetInt(commandRequest.Parameters, "timeout_sec", 0, Logger);
            return timeoutSec > 0 ? timeoutSec : _appSettings.CommandExecution.DefaultCommandTimeoutSeconds;
        }
    }
}
