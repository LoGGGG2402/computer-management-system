using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands.Handlers
{
    /// <summary>
    /// Handler to execute console commands (CMD or PowerShell).
    /// </summary>
    public class ConsoleCommandHandler(ILogger<ConsoleCommandHandler> logger, IOptions<CommandExecutorSettingsOptions> options) : ICommandHandler
    {
        private readonly ILogger<ConsoleCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly CommandExecutorSettingsOptions _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));

        /// <summary>
        /// Execute a console command.
        /// </summary>
        /// <param name="command">Command information to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Command execution result.</returns>
        public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            _logger.LogInformation("Starting console command execution: {CommandId}", command.commandId);

            // Default to CMD, but can be changed based on parameters
            bool usePowerShell = false;
            int timeoutSeconds = _settings.DefaultTimeoutSec;

            // Determine shell type and timeout from parameters
            if (command.parameters != null)
            {
                if (command.parameters.TryGetValue("use_powershell", out var pshellParam) && pshellParam is bool usePsParam)
                {
                    usePowerShell = usePsParam;
                }
                
                if (command.parameters.TryGetValue("timeout_sec", out var timeoutParam) && timeoutParam is int timeoutValue)
                {
                    timeoutSeconds = Math.Max(30, Math.Min(timeoutValue, 3600)); // limit from 30s to 1h
                }
            }

            var result = new CommandResultPayload
            {
                commandId = command.commandId,
                type = command.commandType,
                success = false,
                result = new CommandResultData
                {
                    stdout = string.Empty,
                    stderr = string.Empty,
                    errorMessage = string.Empty,
                    errorCode = string.Empty
                }
            };

            try
            {
                using var process = new Process();
                
                if (usePowerShell)
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.command}\"";
                }
                else
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c {command.command}";
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                
                // Set encoding for output
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(_settings.ConsoleEncoding);
                process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding(_settings.ConsoleEncoding);

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        _ = outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        _ = errorBuilder.AppendLine(e.Data);
                    }
                };

                _logger.LogDebug("Starting process {FileName} with parameters: {Arguments}", 
                    process.StartInfo.FileName, process.StartInfo.Arguments);

                _ = process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process to complete with timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
                var processExitTask = process.WaitForExitAsync(cancellationToken);
                
                // Which task completes first?
                if (await Task.WhenAny(processExitTask, timeoutTask) == timeoutTask)
                {
                    // Timeout occurred, kill process
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning("Command exceeded wait time of {Timeout} seconds, cancelling", timeoutSeconds);
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error when killing process");
                    }

                    result.success = false;
                    result.result.exitCode = -1;
                    result.result.errorMessage = $"Command exceeded timeout of {timeoutSeconds} seconds and was cancelled";
                    result.result.stdout = outputBuilder.ToString();
                    result.result.stderr = errorBuilder.ToString();
                    return result;
                }

                // Process exited normally
                result.success = process.ExitCode == 0;
                result.result.exitCode = process.ExitCode;
                result.result.stdout = outputBuilder.ToString();
                result.result.stderr = errorBuilder.ToString();

                _logger.LogInformation("Console command {CommandId} completed with exit code {ExitCode}", 
                    command.commandId, process.ExitCode);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Console command {CommandId} was cancelled", command.commandId);
                result.success = false;
                result.result.errorMessage = "Command was cancelled";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when executing console command {CommandId}", command.commandId);
                result.success = false;
                result.result.errorMessage = $"Error: {ex.Message}";
                return result;
            }
        }
    }
}
