using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Models;
using CMSAgent.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands
{
    /// <summary>
    /// Manages command queue and executes commands.
    /// </summary>
    public class CommandExecutor
    {
        private readonly ILogger<CommandExecutor> _logger;
        private readonly CommandHandlerFactory _commandHandlerFactory;
        private readonly IWebSocketConnector _webSocketConnector;
        private readonly CommandExecutorSettingsOptions _settings;
        
        private readonly ConcurrentQueue<CommandPayload> _commandQueue = new();
        private readonly SemaphoreSlim _executionSemaphore;
        private bool _isProcessing = false;

        /// <summary>
        /// Initialize a new instance of CommandExecutor.
        /// </summary>
        /// <param name="logger">Logger to log events.</param>
        /// <param name="commandHandlerFactory">Factory to create command execution handlers.</param>
        /// <param name="webSocketConnector">WebSocket connector to send command results.</param>
        /// <param name="settingsOptions">Command execution configuration.</param>
        public CommandExecutor(
            ILogger<CommandExecutor> logger,
            CommandHandlerFactory commandHandlerFactory,
            IWebSocketConnector webSocketConnector,
            IOptions<CommandExecutorSettingsOptions> settingsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _commandHandlerFactory = commandHandlerFactory ?? throw new ArgumentNullException(nameof(commandHandlerFactory));
            _webSocketConnector = webSocketConnector ?? throw new ArgumentNullException(nameof(webSocketConnector));
            _settings = settingsOptions?.Value ?? throw new ArgumentNullException(nameof(settingsOptions));
            
            // Initialize semaphore with the number of commands that can be executed simultaneously
            _executionSemaphore = new SemaphoreSlim(_settings.MaxParallelCommands, _settings.MaxParallelCommands);
        }

        /// <summary>
        /// Try to add a command to the queue.
        /// </summary>
        /// <param name="command">Command to add to the queue.</param>
        /// <returns>True if added successfully, False if the queue is full.</returns>
        public bool TryEnqueueCommand(CommandPayload command)
        {
            ArgumentNullException.ThrowIfNull(command);

            // Check if the queue is already full
            if (_commandQueue.Count >= _settings.MaxQueueSize)
            {
                _logger.LogWarning("Cannot add command to queue: Queue is full ({QueueSize}/{MaxSize})",
                    _commandQueue.Count, _settings.MaxQueueSize);
                return false;
            }

            _commandQueue.Enqueue(command);
            _logger.LogInformation("Added {CommandType} command to queue: {CommandId} (queue size: {QueueSize})",
                command.commandType, command.commandId, _commandQueue.Count);
            
            return true;
        }

        /// <summary>
        /// Start processing commands in the queue.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the command processing.</returns>
        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("A command processing task is already running");
                return;
            }

            _isProcessing = true;
            _logger.LogInformation("Starting command queue processing");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // No commands in the queue, wait a bit and check again
                    if (_commandQueue.IsEmpty)
                    {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    // Get command from queue
                    if (!_commandQueue.TryDequeue(out var command))
                    {
                        continue;
                    }

                    // Wait for semaphore to ensure we don't exceed maximum number of concurrent commands
                    await _executionSemaphore.WaitAsync(cancellationToken);

                    // Execute command in a separate task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteCommandAsync(command, cancellationToken);
                        }
                        finally
                        {
                            // Release semaphore when done
                            _executionSemaphore.Release();
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Command queue processing was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when processing command queue");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Execute a specific command.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing command execution.</returns>
        private async Task ExecuteCommandAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting execution of {CommandType} command: {CommandId}", 
                command.commandType, command.commandId);

            CommandResultPayload result;

            try
            {
                // Get appropriate handler from factory
                var handler = _commandHandlerFactory.GetHandler(command.commandType);
                
                // Execute command
                result = await handler.ExecuteAsync(command, cancellationToken);
                
                _logger.LogInformation("Executed command {CommandId} with result: {Success}", 
                    command.commandId, result.success ? "Success" : "Failure");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when executing command {CommandId}", command.commandId);
                
                // Create error result
                result = new CommandResultPayload
                {
                    commandId = command.commandId,
                    type = command.commandType,
                    success = false,
                    result = new CommandResultData
                    {
                        stdout = string.Empty,
                        stderr = string.Empty,
                        errorMessage = $"Unhandled error: {ex.Message}",
                        errorCode = string.Empty
                    }
                };
            }
            
            // Send result to server
            await SendCommandResultAsync(result);
        }

        /// <summary>
        /// Send command result to server.
        /// </summary>
        /// <param name="result">Command result to send.</param>
        /// <returns>Task representing the sending operation.</returns>
        private async Task SendCommandResultAsync(CommandResultPayload result)
        {
            try
            {
                // Try to send result via WebSocket first
                if (await _webSocketConnector.SendCommandResultAsync(result))
                {
                    _logger.LogInformation("Successfully sent command result {CommandId} via WebSocket", 
                        result.commandId);
                    return;
                }
                
                _logger.LogWarning("Unable to send command result {CommandId} via WebSocket (server may be unavailable)", 
                    result.commandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when sending command result {CommandId}", result.commandId);
            }
        }
    }
}
