using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.CommandHandlers;
using CMSAgent.Models;
using Microsoft.Extensions.DependencyInjection;
using CMSAgent.Configuration;

namespace CMSAgent.Core
{
    public class CommandExecutor
    {
        private readonly ILogger<CommandExecutor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly BlockingCollection<CommandPayload> _commandQueue;
        private readonly List<Task> _workerTasks;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly int _maxParallelCommands;
        private readonly int _defaultCommandTimeoutSec;

        public event Func<CommandResult, Task>? CommandProcessedAsync;

        public CommandExecutor(ILogger<CommandExecutor> logger, IServiceProvider serviceProvider, ConfigManager configManager)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            var cmdExecutorSettings = configManager.AgentConfig.CommandExecutor;
            _commandQueue = new BlockingCollection<CommandPayload>(cmdExecutorSettings.MaxQueueSize);
            _workerTasks = new List<Task>();
            _maxParallelCommands = cmdExecutorSettings.MaxParallelCommands;
            _defaultCommandTimeoutSec = cmdExecutorSettings.DefaultTimeoutSec;
        }

        public Task QueueCommandAsync(CommandPayload commandPayload)
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogWarning("CommandExecutor is not running. Cannot queue command {CommandId}", commandPayload.CommandId);
                if (CommandProcessedAsync != null)
                {
                    var internalResult = new CommandResult(commandPayload.CommandId, commandPayload.CommandType);
                    internalResult.MarkCompleted(false, null, "CommandExecutor is not running.", -1, CommandStatus.Failed);
                    _ = CommandProcessedAsync.Invoke(internalResult);
                }
                return Task.CompletedTask;
            }

            if (_commandQueue.TryAdd(commandPayload))
            {
                _logger.LogInformation("Command {CommandId} of type {CommandType} queued.", commandPayload.CommandId, commandPayload.CommandType);
            }
            else
            {
                _logger.LogWarning("Command queue is full. Command {CommandId} of type {CommandType} was not added.", commandPayload.CommandId, commandPayload.CommandType);
                if (CommandProcessedAsync != null)
                {
                    var internalResult = new CommandResult(commandPayload.CommandId, commandPayload.CommandType);
                    internalResult.MarkCompleted(false, null, "Command queue is full.", -1, CommandStatus.Failed);
                    _ = CommandProcessedAsync.Invoke(internalResult);
                }
            }
            return Task.CompletedTask;
        }

        public void StartProcessing(CancellationToken serviceCancellationToken)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogWarning("CommandExecutor is already running.");
                return;
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken);
            _logger.LogInformation("Starting command processing with {MaxParallelCommands} worker(s)...", _maxParallelCommands);

            for (int i = 0; i < _maxParallelCommands; i++)
            {
                var workerTask = Task.Run(async () => await ProcessQueueAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                _workerTasks.Add(workerTask);
            }
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Command processing worker started.");
            try
            {
                foreach (var commandPayload in _commandQueue.GetConsumingEnumerable(cancellationToken))
                {
                    _logger.LogInformation("Processing command {CommandId} of type {CommandType}...", commandPayload.CommandId, commandPayload.CommandType);
                    CommandResult internalResult = await ProcessCommandInternalAsync(commandPayload, cancellationToken);

                    if (CommandProcessedAsync != null)
                    {
                        try
                        {
                            await CommandProcessedAsync(internalResult);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in CommandProcessedAsync handler for command {CommandId}", internalResult.CommandId);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Command processing worker was canceled (queue consumption).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in command processing worker (queue consumption).");
            }
            finally
            {
                _logger.LogInformation("Command processing worker stopped.");
            }
        }

        public int GetQueueCount() => _commandQueue.Count;

        public async Task StopProcessingAsync()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("CommandExecutor is not running or already stopping.");
                return;
            }

            _logger.LogInformation("Stopping command processing...");
            _commandQueue.CompleteAdding();
            _cancellationTokenSource.Cancel();

            var allWorkersStopped = Task.WhenAll(_workerTasks);
            try
            {
                await Task.WhenAny(allWorkersStopped, Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token));
                if (!allWorkersStopped.IsCompleted)
                {
                    _logger.LogWarning("Not all command processing workers stopped gracefully within the timeout.");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stopping command processing was canceled during wait for workers.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while waiting for command workers to stop.");
            }

            _workerTasks.Clear();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _logger.LogInformation("Command processing stopped.");
        }

        private async Task<CommandResult> ProcessCommandInternalAsync(CommandPayload commandPayload, CancellationToken workerCancellationToken)
        {
            var internalCmdResult = new CommandResult(commandPayload.CommandId, commandPayload.CommandType);
            CancellationTokenSource? commandTimeoutCts = null;
            CancellationToken effectiveToken = workerCancellationToken;

            try
            {
                int timeoutSec = commandPayload.TimeoutSec ?? _defaultCommandTimeoutSec;
                if (timeoutSec > 0)
                {
                    commandTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(workerCancellationToken);
                    commandTimeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
                    effectiveToken = commandTimeoutCts.Token;
                    _logger.LogDebug("Command {CommandId} has a timeout of {TimeoutSeconds} seconds.", commandPayload.CommandId, timeoutSec);
                }

                effectiveToken.ThrowIfCancellationRequested();

                ICommandHandler? handler = commandPayload.CommandType.ToLowerInvariant() switch
                {
                    "console" => _serviceProvider.GetService<ConsoleCommandHandler>(),
                    "system" => _serviceProvider.GetService<SystemCommandHandler>(),
                    _ => null
                };

                if (handler != null)
                {
                    _logger.LogDebug("Executing command {CommandId} with handler {HandlerType}", commandPayload.CommandId, handler.GetType().Name);
                    var tempResult = await handler.ExecuteCommandAsync(commandPayload.Command, effectiveToken);
                    internalCmdResult.MarkCompleted(tempResult.Success, tempResult.Output, tempResult.Error, tempResult.ExitCode, tempResult.Status);

                    _logger.LogInformation("Command {CommandId} executed by {HandlerType}. Status: {Status}", internalCmdResult.CommandId, handler.GetType().Name, internalCmdResult.Status);
                }
                else
                {
                    _logger.LogWarning("No handler found for command type: {CommandType}, CommandId: {CommandId}", commandPayload.CommandType, commandPayload.CommandId);
                    internalCmdResult.MarkCompleted(false, null, $"No handler found for command type: {commandPayload.CommandType}", -1, CommandStatus.UnhandledType);
                }
            }
            catch (OperationCanceledException)
            {
                if (commandTimeoutCts != null && commandTimeoutCts.IsCancellationRequested && !workerCancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Execution of command {CommandId} timed out after {TimeoutSeconds} seconds.", commandPayload.CommandId, commandPayload.TimeoutSec ?? _defaultCommandTimeoutSec);
                    internalCmdResult.MarkCompleted(false, null, $"Command execution timed out after {commandPayload.TimeoutSec ?? _defaultCommandTimeoutSec} seconds.", -1, CommandStatus.Timeout);
                }
                else
                {
                    _logger.LogWarning("Execution of command {CommandId} was canceled.", commandPayload.CommandId);
                    internalCmdResult.MarkCompleted(false, null, "Command execution was canceled.", -1, CommandStatus.Canceled);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command {CommandId} of type {CommandType}.", commandPayload.CommandId, commandPayload.CommandType);
                internalCmdResult.MarkCompleted(false, ex.ToString(), ex.Message, -1, CommandStatus.Failed);
            }
            finally
            {
                commandTimeoutCts?.Dispose();
            }
            return internalCmdResult;
        }
    }
}
