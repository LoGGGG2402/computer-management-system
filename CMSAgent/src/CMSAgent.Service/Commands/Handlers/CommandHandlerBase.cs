using CMSAgent.Service.Commands.Models;
using CMSAgent.Shared.Constants;
using System.Text.Json;

namespace CMSAgent.Service.Commands.Handlers
{
    /// <summary>
    /// Abstract base class for command handlers, providing common functionality.
    /// </summary>
    public abstract class CommandHandlerBase : ICommandHandler
    {
        protected readonly ILogger Logger;

        protected CommandHandlerBase(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Abstract method that derived classes must implement to handle specific command logic.
        /// </summary>
        /// <param name="commandRequest">The command request to execute</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Command execution result</returns>
        protected abstract Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Executes the command and wraps the result.
        /// </summary>
        /// <param name="commandRequest">The command request to execute</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Command execution result</returns>
        public async Task<CommandResult> ExecuteAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Starting command execution ID: {CommandId}, Type: {CommandType}, Command: {CommandText}",
                commandRequest.CommandId, commandRequest.CommandType, commandRequest.Command);

            var commandResult = new CommandResult
            {
                CommandId = commandRequest.CommandId,
                CommandType = commandRequest.CommandType,
                Success = false
            };

            CancellationTokenSource? timeoutCts = null;
            int defaultTimeoutSeconds = 60;

            try
            {
                defaultTimeoutSeconds = GetDefaultCommandTimeoutSeconds(commandRequest);
                timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(defaultTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                commandResult.Result = await ExecuteInternalAsync(commandRequest, linkedCts.Token);
                commandResult.Success = string.IsNullOrEmpty(commandResult.Result.Stderr) && (commandResult.Result.ExitCode == 0 || IsExitCodeSuccessful(commandRequest, commandResult.Result.ExitCode));

                if (commandResult.Success)
                {
                    Logger.LogInformation("Command execution ID: {CommandId} successful. ExitCode: {ExitCode}",
                        commandRequest.CommandId, commandResult.Result.ExitCode);
                }
                else
                {
                    Logger.LogWarning("Command execution ID: {CommandId} failed. ExitCode: {ExitCode}, Error: {ErrorMessage}",
                        commandRequest.CommandId, commandResult.Result.ExitCode, commandResult.Result.Stderr);
                }
            }
            catch (OperationCanceledException ex) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex, "Command ID: {CommandId} timed out after {TimeoutSeconds} seconds.", commandRequest.CommandId, defaultTimeoutSeconds);
                commandResult.Result = CommandOutputResult.CreateError(
                    $"Command timed out after {defaultTimeoutSeconds} seconds.",
                    ex.ToString(),
                    AgentConstants.CommandExitCodes.Timeout
                );
                commandResult.Success = false;
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogWarning(ex, "Command ID: {CommandId} was cancelled.", commandRequest.CommandId);
                commandResult.Result = CommandOutputResult.CreateError(
                    "Command execution was canceled.",
                    ex.ToString(),
                    AgentConstants.CommandExitCodes.Cancelled
                );
                commandResult.Success = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during command execution ID: {CommandId}.", commandRequest.CommandId);
                commandResult.Result = CommandOutputResult.CreateError(
                    $"Unexpected error: {ex.Message}",
                    ex.ToString(),
                    AgentConstants.CommandExitCodes.GeneralError
                );
                commandResult.Success = false;
            }
            finally
            {
                timeoutCts?.Dispose();
            }

            return commandResult;
        }

        /// <summary>
        /// Gets the default timeout value for the command.
        /// Can be overridden by derived classes if more complex logic is needed.
        /// </summary>
        /// <param name="commandRequest">The command request</param>
        /// <returns>Timeout value in seconds</returns>
        protected virtual int GetDefaultCommandTimeoutSeconds(CommandRequest commandRequest)
        {
            if (commandRequest.Parameters != null &&
                commandRequest.Parameters.TryGetValue("timeout_sec", out var timeoutObj) &&
                timeoutObj is JsonElement timeoutJson &&
                timeoutJson.TryGetInt32(out int timeoutSec) &&
                timeoutSec > 0)
            {
                return timeoutSec;
            }
            return 60;
        }

        /// <summary>
        /// Checks if the exit code is considered successful based on 'expected_exit_codes' in parameters.
        /// </summary>
        /// <param name="commandRequest">The command request</param>
        /// <param name="exitCode">The exit code to check</param>
        /// <returns>True if the exit code is considered successful</returns>
        protected virtual bool IsExitCodeSuccessful(CommandRequest commandRequest, int exitCode)
        {
            if (commandRequest.Parameters != null &&
                commandRequest.Parameters.TryGetValue("expected_exit_codes", out var expectedCodesObj) &&
                expectedCodesObj is JsonElement expectedCodesJson &&
                expectedCodesJson.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    var expectedCodes = expectedCodesJson.Deserialize<List<int>>();
                    return expectedCodes?.Contains(exitCode) ?? (exitCode == 0);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, "Could not parse 'expected_exit_codes' for command ID: {CommandId}", commandRequest.CommandId);
                    return exitCode == 0;
                }
            }
            return exitCode == 0;
        }
    }
}
