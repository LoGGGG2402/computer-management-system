using CMSAgent.CommandHandlers;
using CMSAgent.Communication;
using CMSAgent.Configuration;
using CMSAgent.Models.Payloads;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CMSAgent.Core
{
    /// <summary>
    /// Executes commands received from the server
    /// </summary>
    public class CommandExecutor : IDisposable
    {
        private readonly IServerConnector _serverConnector;
        private readonly StaticConfigProvider _configProvider;
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly ISystemCommandHandler _systemCommandHandler;
        private readonly IConsoleCommandHandler _consoleCommandHandler;
        
        private readonly ConcurrentQueue<CommandRequest> _commandQueue = new ConcurrentQueue<CommandRequest>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _queueSignal = new SemaphoreSlim(0);
        private readonly Dictionary<string, Task> _runningCommands = new Dictionary<string, Task>();
        private readonly object _runningCommandsLock = new object();
        
        private Task? _processingTask;
        private bool _isDisposed = false;
        private bool _isRunning = false;

        /// <summary>
        /// Creates a new instance of the CommandExecutor class
        /// </summary>
        public CommandExecutor(
            IServerConnector serverConnector,
            StaticConfigProvider configProvider,
            RuntimeStateManager runtimeStateManager,
            ISystemCommandHandler systemCommandHandler,
            IConsoleCommandHandler consoleCommandHandler)
        {
            _serverConnector = serverConnector;
            _configProvider = configProvider;
            _runtimeStateManager = runtimeStateManager;
            _systemCommandHandler = systemCommandHandler;
            _consoleCommandHandler = consoleCommandHandler;
        }

        /// <summary>
        /// Initializes the command executor
        /// </summary>
        public async Task InitializeAsync()
        {
            Log.Information("Initializing command executor...");
            await _systemCommandHandler.InitializeAsync();
            await _consoleCommandHandler.InitializeAsync();
            Log.Information("Command executor initialized");
        }

        /// <summary>
        /// Starts processing commands
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            Log.Information("Starting command executor...");
            _isRunning = true;
            _processingTask = Task.Run(ProcessCommandQueueAsync);
            Log.Information("Command executor started");
        }

        /// <summary>
        /// Stops processing commands
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            Log.Information("Stopping command executor...");
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            
            // Signal the processing task to wake up and see cancellation
            _queueSignal.Release();
            
            // Wait for processing task to complete
            _processingTask?.Wait(TimeSpan.FromSeconds(5));
            
            Log.Information("Command executor stopped");
        }

        /// <summary>
        /// Queues a command for execution
        /// </summary>
        public void QueueCommand(CommandRequest command)
        {
            if (!_isRunning)
            {
                Log.Warning("Cannot queue command: command executor is not running");
                return;
            }

            Log.Information("Queueing command {CommandId} of type {CommandType}", command.commandId, command.commandType);
            _commandQueue.Enqueue(command);
            _queueSignal.Release();
        }

        /// <summary>
        /// Processes commands from the queue
        /// </summary>
        private async Task ProcessCommandQueueAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for a command to be queued
                    await _queueSignal.WaitAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error waiting for queued command: {Message}", ex.Message);
                    continue;
                }

                // Try to dequeue a command
                if (!_commandQueue.TryDequeue(out CommandRequest? command))
                {
                    continue;
                }

                // Execute command
                Task commandTask = ExecuteCommandAsync(command);
                
                // Add to running commands
                lock (_runningCommandsLock)
                {
                    _runningCommands[command.commandId] = commandTask;
                }
                
                // Start task to remove from running commands when done
                _ = CleanupCommandWhenDoneAsync(command.commandId, commandTask);
            }
        }

        /// <summary>
        /// Executes a command
        /// </summary>
        private async Task ExecuteCommandAsync(CommandRequest command)
        {
            Log.Information("Executing command {CommandId} of type {CommandType}", command.commandId, command.commandType);
            
            Stopwatch stopwatch = Stopwatch.StartNew();
            CommandResult result;

            try
            {
                // Determine command type and execute
                result = command.commandType.ToLowerInvariant() switch
                {
                    "system" => await ExecuteSystemCommandAsync(command),
                    "console" => await ExecuteConsoleCommandAsync(command),
                    _ => CommandResult.Failure(command.commandId, $"Unknown command type: {command.commandType}")
                };
            }
            catch (OperationCanceledException)
            {
                result = CommandResult.Failure(command.commandId, "Command execution was cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing command {CommandId}: {Message}", command.commandId, ex.Message);
                result = CommandResult.Failure(command.commandId, $"Error: {ex.Message}");
            }

            stopwatch.Stop();
            result.executionTimeMs = stopwatch.ElapsedMilliseconds;

            // Send result to server
            try
            {
                await _serverConnector.SendCommandResultAsync(result);
                Log.Information("Command {CommandId} completed with status: {Status}", command.commandId, result.status);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending command result: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Executes a system command
        /// </summary>
        private async Task<CommandResult> ExecuteSystemCommandAsync(CommandRequest command)
        {
            return await _systemCommandHandler.HandleCommandAsync(command);
        }

        /// <summary>
        /// Executes a console command
        /// </summary>
        private async Task<CommandResult> ExecuteConsoleCommandAsync(CommandRequest command)
        {
            return await _consoleCommandHandler.HandleCommandAsync(command);
        }

        /// <summary>
        /// Removes a command from the running commands list when it's done
        /// </summary>
        private async Task CleanupCommandWhenDoneAsync(string commandId, Task commandTask)
        {
            try
            {
                await commandTask;
            }
            catch
            {
                // Ignore exceptions, they're handled in ExecuteCommandAsync
            }
            finally
            {
                lock (_runningCommandsLock)
                {
                    _runningCommands.Remove(commandId);
                }
            }
        }

        /// <summary>
        /// Disposes the command executor
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the command executor
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                    _cancellationTokenSource.Dispose();
                    _queueSignal.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}