using CMSAgent.Configuration;
using CMSAgent.Models.Payloads;
using Serilog;
using System.Diagnostics;
using System.Text; // Required for StringBuilder

namespace CMSAgent.CommandHandlers
{
    /// <summary>
    /// Handles console commands as per Standard.md
    /// </summary>
    public class ConsoleCommandHandler : IConsoleCommandHandler
    {
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly StaticConfigProvider _staticConfigProvider;

        /// <summary>
        /// Creates a new instance of the ConsoleCommandHandler class
        /// </summary>
        public ConsoleCommandHandler(RuntimeStateManager runtimeStateManager, StaticConfigProvider staticConfigProvider)
        {
            _runtimeStateManager = runtimeStateManager;
            _staticConfigProvider = staticConfigProvider;
        }

        /// <summary>
        /// Initializes the console command handler
        /// </summary>
        public Task InitializeAsync()
        {
            Log.Information("Console command handler initialized");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a console command by executing it in the shell.
        /// </summary>
        public async Task<CommandResult> HandleCommandAsync(CommandRequest commandRequest)
        {
            if (commandRequest.commandType.ToLowerInvariant() != "console")
            {
                Log.Warning("ConsoleCommandHandler received command with incorrect type: {CommandType}", commandRequest.commandType);
                return CommandResult.CreateSystemFailureResult(
                    _runtimeStateManager.DeviceId ?? "unknown_agent", 
                    commandRequest.commandId, 
                    commandRequest.commandType, 
                    $"Invalid command type '{commandRequest.commandType}' for ConsoleCommandHandler.");
            }

            Log.Information("Handling console command ID: {CommandId}, Command: {CommandText}", commandRequest.commandId, commandRequest.command);

            var agentId = _runtimeStateManager.DeviceId ?? "unknown_agent";
            var commandToExecute = commandRequest.command;

            if (string.IsNullOrWhiteSpace(commandToExecute))
            {
                Log.Warning("Received empty console command for ID: {CommandId}", commandRequest.commandId);
                return CommandResult.CreateFailureResult( 
                    agentId,
                    commandRequest.commandId,
                    commandRequest.commandType,
                    "Command text cannot be empty.",
                    -1 
                );
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe", 
                Arguments = $"/c \"{commandToExecute.Replace("\"", "\\\"")}\"", // Basic escaping for cmd.exe
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var outputStringBuilder = new StringBuilder();
            var errorStringBuilder = new StringBuilder();
            int exitCode = -1;

            try
            {
                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputStringBuilder.AppendLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorStringBuilder.AppendLine(e.Data); };

                    Log.Debug("Starting process for command ID: {CommandId}. Executing: {FileName} {Arguments}",
                        commandRequest.commandId, processStartInfo.FileName, processStartInfo.Arguments);

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var commandTimeoutMs = (_staticConfigProvider.Config?.console_command_timeout_seconds ?? 60) * 1000;
            
                    if (await Task.Run(() => process.WaitForExit(commandTimeoutMs)))
                    {
                        await Task.Delay(200); // Small delay to catch trailing output, adjust as needed.
                        exitCode = process.ExitCode;
                        Log.Information("Console command ID: {CommandId} finished with exit code: {ExitCode}", commandRequest.commandId, exitCode);
                        
                        string stdOut = outputStringBuilder.ToString().TrimEnd();
                        string stdErr = errorStringBuilder.ToString().TrimEnd();

                        if (exitCode == 0)
                        {
                            return CommandResult.CreateSuccessResult(
                                agentId, commandRequest.commandId, commandRequest.commandType,
                                stdOut, exitCode
                            );
                        }
                        else
                        {
                            string combinedOutput = string.IsNullOrWhiteSpace(stdErr) ? stdOut : $"{stdOut}\n---STDERR---\n{stdErr}";
                            return CommandResult.CreateFailureResult(
                                agentId, commandRequest.commandId, commandRequest.commandType,
                                combinedOutput, exitCode
                            );
                        }
                    }
                    else
                    {
                        Log.Warning("Console command ID: {CommandId} timed out after {TimeoutMs}ms.", commandRequest.commandId, commandTimeoutMs);
                        try
                        {
                            if (!process.HasExited) process.Kill(true); 
                        }
                        catch (Exception killEx)
                        {
                            Log.Error(killEx, "Exception while trying to kill timed-out process for command ID: {CommandId}", commandRequest.commandId);
                        }
                        return CommandResult.CreateTimeoutResult(
                            agentId, commandRequest.commandId, commandRequest.commandType,
                            "Command execution timed out.", 
                            outputStringBuilder.ToString().TrimEnd(), 
                            errorStringBuilder.ToString().TrimEnd()
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception executing console command ID: {CommandId}. Command: {CommandText}", commandRequest.commandId, commandToExecute);
                return CommandResult.CreateSystemFailureResult(
                    agentId, commandRequest.commandId, commandRequest.commandType,
                    $"Failed to execute command: {ex.Message}. Stderr: {errorStringBuilder.ToString().TrimEnd()}"
                );
            }
        }
    }
}