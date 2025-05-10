using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.CommandHandlers
{
    public class ConsoleCommandHandler : ICommandHandler
    {
        private readonly ILogger<ConsoleCommandHandler> _logger;

        public ConsoleCommandHandler(ILogger<ConsoleCommandHandler> logger)
        {
            _logger = logger;
        }

        public async Task<CommandResult> ExecuteCommandAsync(string commandId, string commandPayload, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing console command (Id: {CommandId}): {CommandPayload}", commandId, commandPayload);
            var result = new CommandResult(commandId);

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "cmd.exe"; // Or powershell.exe, or sh/bash on Linux/macOS
                process.StartInfo.Arguments = $"/C {commandPayload}"; // /C for cmd, -Command for powershell
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) => 
                {
                    if (args.Data != null)
                    {
                        outputBuilder.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) => 
                {
                    if (args.Data != null)
                    {
                        errorBuilder.AppendLine(args.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit or for cancellation
                await process.WaitForExitAsync(cancellationToken);

                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString().Trim();
                result.Error = errorBuilder.ToString().Trim();
                result.Success = process.ExitCode == 0;

                if (result.Success)
                {
                    _logger.LogInformation("Console command (Id: {CommandId}) executed successfully. Exit code: {ExitCode}", commandId, result.ExitCode);
                }
                else
                {
                    _logger.LogError("Console command (Id: {CommandId}) failed. Exit code: {ExitCode}. Error: {Error}", commandId, result.ExitCode, result.Error);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Console command (Id: {CommandId}) execution was canceled.", commandId);
                result.Error = "Command execution was canceled.";
                result.Success = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during console command (Id: {CommandId}) execution: {CommandPayload}", commandId, commandPayload);
                result.Error = ex.Message;
                result.Success = false;
            }

            return result;
        }
    }
}
