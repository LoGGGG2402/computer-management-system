using System;
using System.Diagnostics;
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
    /// Handler to execute system commands (restart, shutdown, etc).
    /// </summary>
    public class SystemActionCommandHandler(ILogger<SystemActionCommandHandler> logger) : ICommandHandler
    {
        private readonly ILogger<SystemActionCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Supported system actions
        private enum SystemAction
        {
            Restart,
            Shutdown,
            LogOff,
            Sleep,
            Hibernate
        }

        /// <summary>
        /// Execute a system command.
        /// </summary>
        /// <param name="command">Command information to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Command execution result.</returns>
        public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            _logger.LogInformation("Starting system command execution: {CommandId}", command.commandId);

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
                // Determine the system action to perform
                if (string.IsNullOrEmpty(command.command))
                {
                    result.result.errorMessage = "No system action specified";
                    return result;
                }

                bool forceAction = false;
                int delaySeconds = 0;

                // Get parameters from command
                if (command.parameters != null)
                {
                    if (command.parameters.TryGetValue("force", out var forceParam) && forceParam is bool forceValue)
                    {
                        forceAction = forceValue;
                    }
                    
                    if (command.parameters.TryGetValue("delay_sec", out var delayParam) && delayParam is int delayValue)
                    {
                        delaySeconds = Math.Max(0, Math.Min(delayValue, 3600)); // limit from 0s to 1h
                    }
                }

                // Parse the action to perform
                if (!Enum.TryParse<SystemAction>(command.command, true, out var action))
                {
                    result.result.errorMessage = $"Invalid system action: {command.command}";
                    return result;
                }

                // Is there a delay?
                if (delaySeconds > 0)
                {
                    _logger.LogInformation("Will perform action {Action} after {Delay} seconds", action, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }

                // Execute the system action
                _logger.LogWarning("Executing system action: {Action}, Force: {Force}", action, forceAction);
                
                bool actionResult = await ExecuteSystemActionAsync(action, forceAction);
                
                result.success = actionResult;
                if (!actionResult)
                {
                    result.result.errorMessage = $"Could not perform action {action}";
                }
                else
                {
                    result.result.stdout = $"Successfully performed action {action}";
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("System command {CommandId} was cancelled", command.commandId);
                result.result.errorMessage = "Command was cancelled";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when executing system command {CommandId}", command.commandId);
                result.result.errorMessage = $"Error: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Execute a specific system action.
        /// </summary>
        /// <param name="action">Action to perform.</param>
        /// <param name="force">Force execution without confirmation.</param>
        /// <returns>True if successful, False if failed.</returns>
        private async Task<bool> ExecuteSystemActionAsync(SystemAction action, bool force)
        {
            try
            {
                string arguments = string.Empty;
                
                switch (action)
                {
                    case SystemAction.Restart:
                        arguments = force ? "/r /f /t 0" : "/r /t 60";
                        break;
                    case SystemAction.Shutdown:
                        arguments = force ? "/s /f /t 0" : "/s /t 60";
                        break;
                    case SystemAction.LogOff:
                        arguments = force ? "/l /f" : "/l";
                        break;
                    case SystemAction.Sleep:
                        // Use PowerShell to sleep
                        return await RunPowerShellCommandAsync("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Application]::SetSuspendState('Suspend', $false, $false)");
                    case SystemAction.Hibernate:
                        // Use PowerShell to hibernate
                        return await RunPowerShellCommandAsync("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Application]::SetSuspendState('Hibernate', $false, $false)");
                    default:
                        _logger.LogError("Unsupported system action: {Action}", action);
                        return false;
                }

                if (action != SystemAction.Sleep && action != SystemAction.Hibernate)
                {
                    using var process = new Process();
                    process.StartInfo.FileName = "shutdown.exe";
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    
                    _logger.LogDebug("Executing command: shutdown.exe {Arguments}", arguments);
                    process.Start();
                    await process.WaitForExitAsync();
                    
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when performing system action: {Action}", action);
                return false;
            }
        }

        /// <summary>
        /// Run a PowerShell command.
        /// </summary>
        /// <param name="command">PowerShell command to run.</param>
        /// <returns>True if successful, False if failed.</returns>
        private async Task<bool> RunPowerShellCommandAsync(string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                
                _logger.LogDebug("Executing PowerShell command: {Command}", command);
                process.Start();
                await process.WaitForExitAsync();
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when executing PowerShell command: {Command}", command);
                return false;
            }
        }
    }
}
