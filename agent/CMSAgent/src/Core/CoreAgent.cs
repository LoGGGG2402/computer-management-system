using CMSAgent.Communication;
using CMSAgent.Configuration;
using CMSAgent.Models.Payloads;
using CMSAgent.Monitoring;
using Serilog;
using System.Diagnostics;

namespace CMSAgent.Core
{
    /// <summary>
    /// Core agent implementation
    /// </summary>
    public class CoreAgent : ICoreAgent, IDisposable
    {
        private readonly StaticConfigProvider _configProvider;
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly ISystemMonitor _systemMonitor;
        private readonly IServerConnector _serverConnector;
        private readonly CommandExecutor _commandExecutor;
        private readonly UpdateHandler _updateHandler;

        private Timer? _statusUpdateTimer;
        private Timer? _hardwareInfoTimer;
        private Timer? _updateCheckTimer;
        private Timer? _healthCheckTimer;

        private readonly Stopwatch _uptime = new Stopwatch();
        private readonly CancellationTokenSource _stopTokenSource = new CancellationTokenSource();
        private readonly TaskCompletionSource _stoppedTcs = new TaskCompletionSource();
        
        private AgentState _state = AgentState.NotStarted;
        private int _healthCheckFailures = 0;
        private DateTime _lastStatusUpdate = DateTime.MinValue;
        private bool _isDisposed = false;

        /// <summary>
        /// Gets the current state of the agent
        /// </summary>
        public AgentState State => _state;

        /// <summary>
        /// Gets whether the agent is connected to the server
        /// </summary>
        public bool IsConnected => _serverConnector.IsConnected;

        /// <summary>
        /// Creates a new instance of the CoreAgent class
        /// </summary>
        public CoreAgent(
            StaticConfigProvider configProvider,
            RuntimeStateManager runtimeStateManager,
            ISystemMonitor systemMonitor,
            IServerConnector serverConnector,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler)
        {
            _configProvider = configProvider;
            _runtimeStateManager = runtimeStateManager;
            _systemMonitor = systemMonitor;
            _serverConnector = serverConnector;
            _commandExecutor = commandExecutor;
            _updateHandler = updateHandler;

            // Subscribe to server events
            _serverConnector.OnCommandExecutionRequested += ServerConnector_OnCommandExecutionRequested;
            _serverConnector.OnUpdateAvailable += ServerConnector_OnUpdateAvailable;
        }

        /// <summary>
        /// Starts the agent
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                if (_state != AgentState.NotStarted && _state != AgentState.Stopped)
                {
                    Log.Warning("Cannot start agent: current state is {State}", _state);
                    return;
                }

                _state = AgentState.Starting;
                Log.Information("Starting CMS Agent...");

                // Initialize components
                await _configProvider.InitializeAsync();
                await _runtimeStateManager.InitializeAsync();
                await _serverConnector.InitializeAsync();
                await _commandExecutor.InitializeAsync();
                await _updateHandler.InitializeAsync();

                // Start the uptime stopwatch
                _uptime.Start();

                // Try to connect to the server
                bool connected = await _serverConnector.ConnectAsync();
                if (connected)
                {
                    Log.Information("Connected to server");
                    await _runtimeStateManager.UpdateServerConnectionTimestampAsync();
                }
                else
                {
                    Log.Warning("Failed to connect to server, will try again later");
                }

                // Send hardware information if it hasn't been sent for a day
                await SendHardwareInfoIfNeededAsync();

                // Set up timers for periodic tasks
                SetupTimers();

                // Initialize the command executor
                _commandExecutor.Start();

                // Set state to running
                _state = AgentState.Running;
                Log.Information("CMS Agent started successfully");
            }
            catch (Exception ex)
            {
                _state = AgentState.Error;
                Log.Error(ex, "Error starting agent: {Message}", ex.Message);
                
                // Report error to server
                await ReportErrorAsync("agent_start_error", ex.Message, ex.StackTrace ?? "");
                
                // Re-throw the exception
                throw;
            }
        }

        /// <summary>
        /// Stops the agent
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_state == AgentState.Stopped)
                {
                    return;
                }

                Log.Information("Stopping CMS Agent...");
                _state = AgentState.Stopping;

                // Signal cancellation
                _stopTokenSource.Cancel();

                // Dispose timers
                DisposeTimers();

                // Stop the command executor
                _commandExecutor.Stop();

                // Disconnect from the server
                await _serverConnector.DisconnectAsync();

                // Stop the uptime stopwatch
                _uptime.Stop();

                // Set state to stopped
                _state = AgentState.Stopped;
                Log.Information("CMS Agent stopped");

                // Signal completion
                _stoppedTcs.TrySetResult();
            }
            catch (Exception ex)
            {
                _state = AgentState.Error;
                Log.Error(ex, "Error stopping agent: {Message}", ex.Message);
                
                // Set completion anyway
                _stoppedTcs.TrySetResult();
                
                // Re-throw the exception
                throw;
            }
        }

        /// <summary>
        /// Returns a task that completes when the agent has stopped
        /// </summary>
        public Task WaitForCompletionAsync()
        {
            return _stoppedTcs.Task;
        }

        /// <summary>
        /// Sets up the timers for periodic tasks
        /// </summary>
        private void SetupTimers()
        {
            // Status update timer
            int statusIntervalMs = _configProvider.Config.monitoring_settings.collection_interval_sec * 1000;
            _statusUpdateTimer = new Timer(async _ => await SendStatusUpdateAsync(), null, statusIntervalMs, statusIntervalMs);

            // Hardware info timer (daily)
            TimeSpan hardwareInterval = TimeSpan.FromHours(24);
            _hardwareInfoTimer = new Timer(async _ => await SendHardwareInfoAsync(), null, hardwareInterval, hardwareInterval);

            // Update check timer
            TimeSpan updateCheckInterval = TimeSpan.FromHours(_configProvider.Config.update_settings.check_interval_hours);
            _updateCheckTimer = new Timer(async _ => await CheckForUpdatesAsync(), null, updateCheckInterval, updateCheckInterval);

            // Health check timer
            int healthCheckIntervalMs = _configProvider.Config.health_check_settings.check_interval_sec * 1000;
            _healthCheckTimer = new Timer(async _ => await PerformHealthCheckAsync(), null, healthCheckIntervalMs, healthCheckIntervalMs);

            Log.Debug("Agent timers set up");
        }

        /// <summary>
        /// Disposes the timers
        /// </summary>
        private void DisposeTimers()
        {
            _statusUpdateTimer?.Dispose();
            _statusUpdateTimer = null;

            _hardwareInfoTimer?.Dispose();
            _hardwareInfoTimer = null;

            _updateCheckTimer?.Dispose();
            _updateCheckTimer = null;
            
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;

            Log.Debug("Agent timers disposed");
        }

        /// <summary>
        /// Sends a status update to the server
        /// </summary>
        private async Task SendStatusUpdateAsync()
        {
            try
            {
                if (_state != AgentState.Running || !_serverConnector.IsConnected)
                {
                    return;
                }

                // Don't send updates too frequently
                if ((DateTime.UtcNow - _lastStatusUpdate).TotalSeconds < 15)
                {
                    return;
                }

                // Get system metrics
                var metrics = await _systemMonitor.GetSystemMetricsAsync();

                // Create status payload
                var statusPayload = new AgentStatusPayload
                {
                    cpu_usage = metrics.CpuUsage,
                    ram_usage = metrics.RamUsage,
                    disk_usage = metrics.DiskUsage,
                    uptime_seconds = (long)_uptime.Elapsed.TotalSeconds,
                    agent_version = _runtimeStateManager.GetCurrentVersion(),
                    status = _state.ToString().ToLowerInvariant(),
                    timestamp = DateTime.UtcNow
                };

                // Send status update
                await _serverConnector.SendStatusUpdateAsync(statusPayload);
                _lastStatusUpdate = DateTime.UtcNow;

                Log.Debug("Status update sent: CPU {CpuUsage}%, RAM {RamUsage}%, Disk {DiskUsage}%",
                    metrics.CpuUsage, metrics.RamUsage, metrics.DiskUsage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending status update: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Sends hardware information to the server
        /// </summary>
        private async Task SendHardwareInfoAsync()
        {
            try
            {
                if (_state != AgentState.Running || !_serverConnector.IsConnected)
                {
                    return;
                }

                Log.Information("Sending hardware information to server...");

                // Get hardware information
                var hardwareInfo = await _systemMonitor.GetHardwareInfoAsync();

                // Send to server
                await _serverConnector.SendHardwareInfoAsync(hardwareInfo);

                // Update last sent timestamp
                await _runtimeStateManager.UpdateHardwareInfoSentTimestampAsync();

                Log.Information("Hardware information sent to server");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending hardware information: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Sends hardware information if it hasn't been sent for a day
        /// </summary>
        private async Task SendHardwareInfoIfNeededAsync()
        {
            try
            {
                var runtimeConfig = await _runtimeStateManager.GetRuntimeConfigAsync();
                
                if (runtimeConfig.last_hardware_info_sent == null || 
                    (DateTime.UtcNow - runtimeConfig.last_hardware_info_sent.Value).TotalHours >= 24)
                {
                    await SendHardwareInfoAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking hardware info send status: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Checks for updates
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                if (_state != AgentState.Running || !_serverConnector.IsConnected)
                {
                    return;
                }

                // Get current version
                string currentVersion = _runtimeStateManager.GetCurrentVersion();

                // Check for updates
                Log.Information("Checking for updates...");
                var updateResponse = await _serverConnector.CheckForUpdateAsync(currentVersion);

                // Update last check timestamp
                await _runtimeStateManager.UpdateUpdateCheckTimestampAsync();

                // If an update is available
                if (updateResponse != null && updateResponse.update_available)
                {
                    Log.Information("Update available: {Version}", updateResponse.version);
                    
                    // Save update info
                    await _runtimeStateManager.UpdateUpdateInfoAsync(
                        updateResponse.version,
                        updateResponse.download_url);

                    // Process the update
                    await _updateHandler.ProcessUpdateAsync(updateResponse);
                }
                else
                {
                    Log.Information("No updates available");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Performs a health check
        /// </summary>
        private async Task PerformHealthCheckAsync()
        {
            try
            {
                if (_state != AgentState.Running)
                {
                    return;
                }

                // Check server connection
                if (!_serverConnector.IsConnected)
                {
                    _healthCheckFailures++;
                    Log.Warning("Health check failed: not connected to server (failure {Count}/{Threshold})",
                        _healthCheckFailures, _configProvider.Config.health_check_settings.failure_threshold);

                    // If too many failures, try to reconnect
                    if (_healthCheckFailures >= _configProvider.Config.health_check_settings.failure_threshold)
                    {
                        Log.Warning("Health check threshold reached, attempting reconnection");
                        
                        // Try to reconnect
                        bool reconnected = await _serverConnector.ConnectAsync();
                        if (reconnected)
                        {
                            Log.Information("Successfully reconnected to server");
                            _healthCheckFailures = 0;
                            await _runtimeStateManager.UpdateServerConnectionTimestampAsync();
                        }
                    }
                }
                else
                {
                    // Reset failure count if connected
                    _healthCheckFailures = 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during health check: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Reports an error to the server
        /// </summary>
        private async Task ReportErrorAsync(string errorType, string errorMessage, string errorDetails)
        {
            try
            {
                if (!_serverConnector.IsConnected)
                {
                    Log.Warning("Cannot report error: not connected to server");
                    return;
                }

                var errorReport = new ErrorReport
                {
                    error_type = errorType,
                    error_message = errorMessage,
                    error_details = errorDetails,
                    timestamp = DateTime.UtcNow,
                    context = new Dictionary<string, string>
                    {
                        { "agent_state", _state.ToString() },
                        { "agent_version", _runtimeStateManager.GetCurrentVersion() },
                        { "uptime_seconds", _uptime.Elapsed.TotalSeconds.ToString("F0") }
                    }
                };

                await _serverConnector.ReportErrorAsync(errorReport);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reporting error to server: {Message}", ex.Message);
            }
        }

        #region Event Handlers

        /// <summary>
        /// Handles command execution requests from the server
        /// </summary>
        private void ServerConnector_OnCommandExecutionRequested(CommandRequest commandRequest)
        {
            try
            {
                Log.Information("Received command execution request: {CommandId}, Type: {CommandType}",
                    commandRequest.commandId, commandRequest.commandType);

                // Queue the command
                _commandExecutor.QueueCommand(commandRequest);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling command execution request: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Handles update available notifications from the server
        /// </summary>
        private async void ServerConnector_OnUpdateAvailable(UpdateCheckResponse updateInfo)
        {
            try
            {
                Log.Information("Received update notification: {Version}", updateInfo.version);

                // Save update info
                await _runtimeStateManager.UpdateUpdateInfoAsync(
                    updateInfo.version,
                    updateInfo.download_url);

                // Process the update
                await _updateHandler.ProcessUpdateAsync(updateInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling update notification: {Message}", ex.Message);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the agent
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the agent
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose timers
                    DisposeTimers();

                    // Dispose cancellation token source
                    _stopTokenSource.Dispose();

                    // Dispose command executor
                    (_commandExecutor as IDisposable)?.Dispose();

                    // Dispose server connector
                    (_serverConnector as IDisposable)?.Dispose();
                }

                _isDisposed = true;
            }
        }

        #endregion
    }
}