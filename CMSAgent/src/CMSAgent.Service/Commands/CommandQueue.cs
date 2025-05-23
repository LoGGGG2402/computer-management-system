// CMSAgent.Service/Commands/CommandQueue.cs
using CMSAgent.Service.Commands.Factory;
using CMSAgent.Service.Commands.Models;
using CMSAgent.Service.Commands.Handlers;
using CMSAgent.Service.Communication.WebSocket; // For IAgentSocketClient (to send results)
using CMSAgent.Service.Configuration.Models; // For AppSettings
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using System.Text.Json; // For JsonSerializer

namespace CMSAgent.Service.Commands
{
    /// <summary>
    /// Manages queue and execution of commands received from Server.
    /// </summary>
    public class CommandQueue : IAsyncDisposable
    {
        private readonly ILogger<CommandQueue> _logger;
        private readonly ICommandHandlerFactory _handlerFactory;
        private readonly IAgentSocketClient _socketClient;
        private readonly AppSettings _appSettings;
        private readonly Channel<CommandRequest> _queue;
        private readonly List<Task> _workerTasks;
        private CancellationTokenSource? _cts; // Dùng để dừng các worker task
        private bool _isStopping;

        public CommandQueue(
            ILogger<CommandQueue> logger,
            ICommandHandlerFactory handlerFactory,
            IAgentSocketClient socketClient,
            IOptions<AppSettings> appSettingsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
            _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            // Create Channel with size limit from AppSettings
            var channelOptions = new BoundedChannelOptions(_appSettings.CommandExecution.MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait, // Wait if queue is full (or DropWrite)
                SingleReader = false, // Multiple workers can read
                SingleWriter = true   // Only one source can write (e.g., from WebSocket event)
            };
            _queue = Channel.CreateBounded<CommandRequest>(channelOptions);
            _workerTasks = new List<Task>();
            _isStopping = false;
        }

        /// <summary>
        /// Starts workers to process commands from the queue.
        /// </summary>
        /// <param name="cancellationToken">Token to stop workers.</param>
        public void StartProcessing(CancellationToken cancellationToken)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _logger.LogWarning("CommandQueue processing has already started.");
                return;
            }

            _isStopping = false;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int numberOfWorkers = _appSettings.CommandExecution.MaxParallelCommands;
            if (numberOfWorkers <= 0) numberOfWorkers = 1; // At least 1 worker

            _logger.LogInformation("Starting {NumberOfWorkers} worker(s) to process command queue.", numberOfWorkers);

            for (int i = 0; i < numberOfWorkers; i++)
            {
                var workerId = i + 1;
                _workerTasks.Add(Task.Run(async () => await ProcessQueueAsync(workerId, _cts.Token)));
            }
        }

        /// <summary>
        /// Stops queue processing and waits for workers to complete.
        /// </summary>
        public async Task StopProcessingAsync()
        {
            if (_isStopping)
            {
                _logger.LogInformation("Command queue is already stopping.");
                return;
            }

            _isStopping = true;
            _logger.LogInformation("Requesting to stop command queue processing...");
            
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // Send cancellation signal to workers
            }

            // Wait for all worker tasks to complete
            if (_workerTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(_workerTasks);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("One or more command workers were cancelled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while waiting for command workers to stop.");
                }
                _workerTasks.Clear();
            }

            // Only complete the channel if it's not already completed
            try
            {
                _queue.Writer.Complete();
            }
            catch (ChannelClosedException)
            {
                _logger.LogInformation("Channel was already completed.");
            }

            _logger.LogInformation("Command queue processing has stopped.");
        }

        /// <summary>
        /// Adds a command request to the queue.
        /// </summary>
        /// <param name="commandRequest">Command request.</param>
        /// <returns>True if added successfully, False if queue is closed or cancelled.</returns>
        public async Task<bool> EnqueueCommandAsync(CommandRequest commandRequest)
        {
            if (commandRequest == null)
            {
                _logger.LogWarning("Cannot add null command request to queue.");
                return false;
            }

            if (_isStopping)
            {
                _logger.LogWarning("Cannot add command to stopping queue. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }

            try
            {
                // Try to write to channel, may wait if queue is full (depending on BoundedChannelFullMode)
                await _queue.Writer.WriteAsync(commandRequest, _cts?.Token ?? CancellationToken.None);
                _logger.LogInformation("Added command ID: {CommandId}, Type: {CommandType} to queue.", commandRequest.CommandId, commandRequest.CommandType);
                return true;
            }
            catch (ChannelClosedException)
            {
                _logger.LogWarning("Cannot add command to closed queue. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }
            catch (OperationCanceledException)
            {
                 _logger.LogWarning("Command queue operation was cancelled. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding command to queue. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }
        }

        private async Task ProcessQueueAsync(int workerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Command worker {WorkerId} has started.", workerId);
            try
            {
                // Read from channel until it's closed and empty
                await foreach (var commandRequest in _queue.Reader.ReadAllAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    _logger.LogInformation("Worker {WorkerId} is processing command ID: {CommandId}, Type: {CommandType}",
                        workerId, commandRequest.CommandId, commandRequest.CommandType);

                    ICommandHandler? handler = _handlerFactory.CreateHandler(commandRequest.CommandType);
                    if (handler is null)
                    {
                        _logger.LogWarning("Worker {WorkerId}: No handler found for CommandType: {CommandType}, CommandId: {CommandId}. Skipping command.",
                            workerId, commandRequest.CommandType, commandRequest.CommandId);
                        // Send error result to server
                        var errorResult = new CommandResult
                        {
                            CommandId = commandRequest.CommandId,
                            CommandType = commandRequest.CommandType,
                            Success = false,
                            Result = CommandOutputResult.CreateError($"Unsupported command type: {commandRequest.CommandType}")
                        };
                        await SendResultToServerAsync(errorResult);
                        continue;
                    }

                    CommandResult result = await handler.ExecuteAsync(commandRequest, cancellationToken);
                    await SendResultToServerAsync(result);

                    _logger.LogInformation("Worker {WorkerId} has completed processing command ID: {CommandId}. Success: {Success}",
                        workerId, commandRequest.CommandId, result.Success);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Command worker {WorkerId} was cancelled.", workerId);
            }
            catch (ChannelClosedException) // Channel was closed while reading (ReadAllAsync)
            {
                _logger.LogInformation("Command worker {WorkerId}: Channel is closed, ending processing.", workerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Serious error in command worker {WorkerId}.", workerId);
                // Consider whether to stop the entire service if worker has unrecoverable error
            }
            finally
            {
                _logger.LogInformation("Command worker {WorkerId} has stopped.", workerId);
            }
        }

        private async Task SendResultToServerAsync(CommandResult result)
        {
            if (_socketClient.IsConnected)
            {
                try
                {
                    await _socketClient.SendCommandResultAsync(result);
                    _logger.LogInformation("Successfully sent command result ID: {CommandId} to server.", result.CommandId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending command result ID: {CommandId} to server.", result.CommandId);
                }
            }
            else
            {
                _logger.LogWarning("WebSocket not connected. Command result ID: {CommandId} could not be sent.", result.CommandId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing CommandQueue...");
            await StopProcessingAsync(); // Ensure workers have stopped
            _cts?.Dispose();
            _logger.LogInformation("CommandQueue disposed.");
            GC.SuppressFinalize(this);
        }
    }
}
