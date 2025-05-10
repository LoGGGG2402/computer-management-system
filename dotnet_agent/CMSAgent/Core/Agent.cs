using CMSAgent.CommandHandlers;
using CMSAgent.Communication;
using CMSAgent.Configuration;
using CMSAgent.Models;
using CMSAgent.Monitoring;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Core
{
    public class Agent
    {
        private readonly ILogger<Agent> _logger;
        private readonly ConfigManager _configManager;
        private readonly StateManager _stateManager;
        private readonly ServerConnector _serverConnector;
        private readonly CommandExecutor _commandExecutor;
        private readonly SystemMonitor _systemMonitor;
        private readonly UpdateHandler _updateHandler;

        private Timer? _statusReportTimer;
        private Timer? _updateCheckTimer;
        private AgentState _currentState = AgentState.Starting;
        private readonly object _stateLock = new object();
        private CancellationTokenSource _agentCts = new CancellationTokenSource();
        private string _agentVersion = "1.0.0";

        public Agent(
            ILogger<Agent> logger,
            ConfigManager configManager,
            StateManager stateManager,
            ServerConnector serverConnector,
            CommandExecutor commandExecutor,
            SystemMonitor systemMonitor,
            UpdateHandler updateHandler)
        {
            _logger = logger;
            _configManager = configManager;
            _stateManager = stateManager;
            _serverConnector = serverConnector;
            _commandExecutor = commandExecutor;
            _systemMonitor = systemMonitor;
            _updateHandler = updateHandler;

            _commandExecutor.CommandProcessedAsync += HandleCommandProcessedAsync;
            _serverConnector.OnCommandExecuteReceived += HandleServerCommandExecuteAsync;
            _serverConnector.OnNewVersionAvailableReceived += HandleServerNewVersionAvailableAsync;

            try
            {
                _agentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine assembly version. Using default {DefaultVersion}.", _agentVersion);
            }
            _configManager.SetAgentVersion(_agentVersion);
        }

        private AgentState GetState()
        {
            lock (_stateLock) { return _currentState; }
        }

        private void SetState(AgentState newState)
        {
            lock (_stateLock)
            {
                if (_currentState != newState)
                {
                    _logger.LogInformation("Agent state changing from {OldState} to {NewState}", _currentState, newState);
                    _currentState = newState;
                }
            }
        }

        public async Task StartAsync(CancellationToken serviceCancellationToken)
        {
            _logger.LogInformation("CMS Agent version {AgentVersion} starting...", _agentVersion);
            SetState(AgentState.Starting);
            _agentCts = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken);

            try
            {
                _serverConnector.OnSocketTransportConnected += OnSocketTransportConnectedAsync;
                _serverConnector.OnSocketAuthenticated += OnSocketAuthenticatedAsync;
                _serverConnector.OnSocketAuthenticationFailed += OnSocketAuthenticationFailedAsync;
                _serverConnector.OnSocketDisconnected += OnSocketDisconnectedAsync;
                _serverConnector.OnSocketConnectError += OnSocketConnectErrorAsync;

                _commandExecutor.StartProcessing(_agentCts.Token);

                await _stateManager.EnsureDeviceIdAsync();
                RoomPosition? roomConfig = _stateManager.GetRoomConfig();

                if (roomConfig == null || string.IsNullOrEmpty(roomConfig.RoomName))
                {
                    _logger.LogWarning("Room configuration not found or incomplete. Agent requires initial setup via --configure or manual config.");
                    await _serverConnector.ReportErrorToBackendAsync(
                        "ConfigurationError",
                        "Agent not configured with room position.",
                        new Dictionary<string, object?> { { "details", "RoomConfig is null or RoomName is empty in agent_state.json" } });
                    SetState(AgentState.Error);
                    return;
                }

                bool initialized = await _serverConnector.InitializeAsync(roomConfig, null, false);

                if (!initialized)
                {
                    _logger.LogError("Agent initialization and connection to server failed. Check logs for details. Agent will not be operational.");
                    SetState(AgentState.Error);
                    return;
                }

                _logger.LogInformation("Agent initialization process completed. Waiting for WebSocket events or shutdown.");
                await Task.Delay(Timeout.Infinite, _agentCts.Token);
            }
            catch (MfaRequiredException mfaEx)
            {
                _logger.LogWarning(mfaEx, "MFA is required. Agent cannot complete startup in service/console mode without prior MFA completion or interactive configuration. Please run with --configure if MFA is needed and not yet supplied.");
                SetState(AgentState.Error);
                await _serverConnector.ReportErrorToBackendAsync("AuthenticationError", "MFA required for agent operation.", new Dictionary<string, object?> { { "details", mfaEx.Message } });
            }
            catch (OperationCanceledException) when (_agentCts.IsCancellationRequested)
            {
                _logger.LogInformation("Agent stopping due to cancellation request.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled exception during agent startup or main loop.");
                SetState(AgentState.Error);
                await _serverConnector.ReportErrorToBackendAsync("AgentCrash", "Agent encountered a critical unhandled exception during startup/main loop.", new Dictionary<string, object?> { { "exception", ex.ToString() } });
            }
            finally
            {
                await GracefulShutdownAsync();
            }
        }

        private Task OnSocketTransportConnectedAsync()
        {
            _logger.LogInformation("WebSocket transport connected. Waiting for authentication with server.");
            return Task.CompletedTask;
        }

        private async Task OnSocketAuthenticatedAsync()
        {
            _logger.LogInformation("WebSocket authenticated successfully. Agent is now fully operational with the server.");
            SetState(AgentState.Idle);
            await _serverConnector.SendInitialHardwareInfoAsync();
            SetupTimers();
        }

        private Task OnSocketAuthenticationFailedAsync(string? reason)
        {
            _logger.LogError("WebSocket authentication failed. Reason: {Reason}. Agent will not be operational until re-authenticated. Check token or server status.", reason ?? "Unknown");
            SetState(AgentState.Error);
            StopTimers();
            return Task.CompletedTask;
        }

        private Task OnSocketDisconnectedAsync()
        {
            _logger.LogWarning("Disconnected from WebSocket. Agent state set to Error. ServerConnector will attempt to reconnect.");
            SetState(AgentState.Error);
            StopTimers();
            return Task.CompletedTask;
        }

        private Task OnSocketConnectErrorAsync(Exception? ex)
        {
            _logger.LogError(ex, "WebSocket connection error. ServerConnector will attempt to reconnect.");
            SetState(AgentState.Error);
            StopTimers();
            return Task.CompletedTask;
        }

        private void StopTimers()
        {
            _statusReportTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _updateCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.LogInformation("Status reporting and update check timers stopped.");
        }

        private void SetupTimers()
        {
            if (!_serverConnector.IsWebSocketAuthenticated)
            {
                _logger.LogWarning("Skipping timer setup as WebSocket is not authenticated.");
                return;
            }

            var agentSettings = _configManager.AgentConfig.Agent;

            var statusInterval = TimeSpan.FromSeconds(agentSettings.StatusReportIntervalSec);
            _statusReportTimer?.Dispose();
            _statusReportTimer = new Timer(async _ => await ReportStatusAsync(), null, statusInterval, statusInterval);
            _logger.LogInformation("Status reporting timer started. Interval: {StatusIntervalSeconds} seconds.", statusInterval.TotalSeconds);

            if (agentSettings.EnableAutoUpdate)
            {
                var updateInterval = TimeSpan.FromSeconds(agentSettings.AutoUpdateIntervalSec);
                _updateCheckTimer?.Dispose();
                _updateCheckTimer = new Timer(async _ => await CheckForUpdatesProactivelyAsync(), null, updateInterval, updateInterval);
                _logger.LogInformation("Proactive update check timer started. Interval: {UpdateIntervalSeconds} seconds.", updateInterval.TotalSeconds);
            }
            else
            {
                _logger.LogInformation("Proactive update checks are disabled by configuration.");
                _updateCheckTimer?.Dispose();
                _updateCheckTimer = null;
            }
        }

        private async Task ReportStatusAsync()
        {
            if (GetState() == AgentState.Updating || !_serverConnector.IsWebSocketAuthenticated)
            {
                _logger.LogDebug("Skipping status report. State: {CurrentState}, Authenticated: {IsAuthenticated}", GetState(), _serverConnector.IsWebSocketAuthenticated);
                return;
            }

            try
            {
                _logger.LogDebug("Gathering system usage stats for status report...");
                var usageStats = await _systemMonitor.GetUsageStatsAsync();
                await _serverConnector.SendStatusUpdateAsync(usageStats);
                _logger.LogDebug("Status report sent successfully via WebSocket.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting agent status.");
            }
        }

        private async Task CheckForUpdatesProactivelyAsync()
        {
            var agentSettings = _configManager.AgentConfig.Agent;
            if (GetState() == AgentState.Updating || !agentSettings.EnableAutoUpdate || !_serverConnector.IsWebSocketAuthenticated)
            {
                _logger.LogDebug("Skipping proactive update check. State: {CurrentState}, AutoUpdateEnabled: {EnableAutoUpdate}, Authenticated: {IsAuthenticated}",
                    GetState(), agentSettings.EnableAutoUpdate, _serverConnector.IsWebSocketAuthenticated);
                return;
            }

            _logger.LogInformation("Proactively checking for agent updates via HTTP GET /check-update...");
            try
            {
                var updateInfo = await _serverConnector.CheckForUpdateAsync(_agentVersion);

                if (updateInfo != null &&
                    !string.IsNullOrEmpty(updateInfo.Version) &&
                    !string.IsNullOrEmpty(updateInfo.DownloadUrl) &&
                    !string.IsNullOrEmpty(updateInfo.ChecksumSha256))
                {
                    _logger.LogInformation("New agent version {NewVersion} available (current: {CurrentVersion}). Download URL: {DownloadUrl}",
                        updateInfo.Version, _agentVersion, updateInfo.DownloadUrl);
                    ProcessUpdateNotification(updateInfo.Version, updateInfo.DownloadUrl, updateInfo.ChecksumSha256, null);
                }
                else
                {
                    _logger.LogInformation("Agent is up to date (version {AgentVersion}) or no update information received from proactive check.", _agentVersion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during proactive check for agent updates.");
            }
        }

        private async Task HandleServerCommandExecuteAsync(ExecuteCommandEventPayload serverCmdPayload)
        {
            if (serverCmdPayload == null)
            {
                _logger.LogWarning("Received null ExecuteCommandEventPayload from server. Ignoring.");
                return;
            }

            _logger.LogInformation("Received command from server: ID={CommandId}, Type={CommandType}, Command='{CommandText}'",
                serverCmdPayload.CommandId, serverCmdPayload.CommandType, serverCmdPayload.Command);

            SetState(AgentState.Busy);

            try
            {
                if (string.IsNullOrEmpty(serverCmdPayload.CommandId) || string.IsNullOrEmpty(serverCmdPayload.Command))
                {
                    _logger.LogError("Invalid command structure from server. ID: {CommandId}, Command is null/empty: {IsCommandNullOrEmpty}",
                        serverCmdPayload.CommandId, string.IsNullOrEmpty(serverCmdPayload.Command));

                    await _serverConnector.ReportErrorToBackendAsync("CommandError", "Invalid command structure from server (missing ID or command text).",
                        new Dictionary<string, object?> {
                            { "receivedCommandId", serverCmdPayload.CommandId ?? "N/A" },
                            { "receivedCommandType", serverCmdPayload.CommandType },
                            { "receivedCommand", serverCmdPayload.Command }
                        });
                    await _serverConnector.ReportCommandResultAsync(serverCmdPayload.CommandId, serverCmdPayload.CommandType, false, string.Empty, "Invalid command structure from server", -1);
                    return;
                }

                var internalCmdToQueue = new CommandPayload
                {
                    CommandId = serverCmdPayload.CommandId,
                    CommandType = serverCmdPayload.CommandType,
                    Command = serverCmdPayload.Command,
                    TimeoutSec = _configManager.AgentConfig.CommandExecutor.DefaultTimeoutSec
                };

                _logger.LogInformation("Queueing command for execution. ID: {CommandId}, Type: {CommandType}", internalCmdToQueue.CommandId, internalCmdToQueue.CommandType);
                await _commandExecutor.QueueCommandAsync(internalCmdToQueue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling/queueing command ID {CommandId} from server.", serverCmdPayload.CommandId);
                await _serverConnector.ReportErrorToBackendAsync("CommandError", $"Generic error handling/queueing server command ID {serverCmdPayload.CommandId}.",
                    new Dictionary<string, object?> { { "error", ex.Message }, { "commandId", serverCmdPayload.CommandId } });
                await _serverConnector.ReportCommandResultAsync(serverCmdPayload.CommandId, serverCmdPayload.CommandType, false, string.Empty, ex.Message, -1);

                if (_commandExecutor.GetQueueCount() == 0)
                {
                    SetState(AgentState.Idle);
                }
            }
        }

        private async Task HandleCommandProcessedAsync(CommandResult commandResult)
        {
            if (commandResult == null)
            {
                _logger.LogWarning("HandleCommandProcessedAsync received a null CommandResult.");
                return;
            }

            _logger.LogInformation("Command ID {CommandId} (Type: {CommandType}) processed by executor. Success: {Success}. Sending result to server.",
                commandResult.CommandId, commandResult.CommandType, commandResult.Success);
            try
            {
                await _serverConnector.ReportCommandResultAsync(
                    commandResult.CommandId,
                    commandResult.CommandType,
                    commandResult.Success,
                    commandResult.Output ?? string.Empty,
                    commandResult.Error ?? string.Empty,
                    commandResult.ExitCode
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command processing result for ID {CommandId} to server.", commandResult.CommandId);
            }
            finally
            {
                if (_commandExecutor.GetQueueCount() == 0)
                {
                    SetState(AgentState.Idle);
                }
                else
                {
                    _logger.LogInformation("{QueueCount} commands still in queue. Agent remains Busy.", _commandExecutor.GetQueueCount());
                }
            }
        }

        private async Task HandleServerNewVersionAvailableAsync(NewVersionAvailablePayload versionPayload)
        {
            if (versionPayload == null)
            {
                _logger.LogWarning("Received null NewVersionAvailablePayload from server event. Ignoring.");
                return;
            }

            _logger.LogInformation("Received 'agent:new_version_available' event from server: Version={Version}, URL={DownloadUrl}, Checksum={Checksum}",
                versionPayload.Version, versionPayload.DownloadUrl, versionPayload.ChecksumSha256);

            bool enableAutoUpdate = _configManager.AgentConfig.Agent.EnableAutoUpdate;
            if (!enableAutoUpdate)
            {
                _logger.LogInformation("Auto-update is disabled by configuration. Ignoring new version notification for {Version}.", versionPayload.Version);
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(versionPayload.Version) ||
                    string.IsNullOrEmpty(versionPayload.DownloadUrl) ||
                    string.IsNullOrEmpty(versionPayload.ChecksumSha256))
                {
                    _logger.LogError("Invalid update notification payload from server event. Version: {Version}, URL: {Url}, Checksum: {Checksum}.",
                        versionPayload.Version, versionPayload.DownloadUrl, versionPayload.ChecksumSha256);
                    await _serverConnector.ReportErrorToBackendAsync("UpdateNotificationError", "Invalid update notification payload from server (via WebSocket event).",
                        new Dictionary<string, object?> {
                            {"version", versionPayload.Version},
                            {"url", versionPayload.DownloadUrl},
                            {"checksum", versionPayload.ChecksumSha256}
                        });
                    return;
                }

                ProcessUpdateNotification(versionPayload.Version, versionPayload.DownloadUrl, versionPayload.ChecksumSha256, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing update notification for version {Version} (from WebSocket event).", versionPayload.Version);
                await _serverConnector.ReportErrorToBackendAsync("UpdateNotificationError", $"Generic error processing update notification for {versionPayload.Version} (from WebSocket event).",
                    new Dictionary<string, object?> { { "error", ex.Message }, { "version", versionPayload.Version } });
            }
        }

        private void ProcessUpdateNotification(string newVersion, string downloadUrl, string checksum, string? releaseNotes = null)
        {
            if (GetState() == AgentState.Updating)
            {
                _logger.LogInformation("Update already in progress. Ignoring new notification for version {NewVersion}.", newVersion);
                return;
            }

            Version currentVer;
            Version newVer;
            try
            {
                currentVer = new Version(_agentVersion);
                newVer = new Version(newVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing version strings for comparison. Current: '{CurrentVersion}', New: '{NewVersion}'. Proceeding with update attempt.", _agentVersion, newVersion);
                currentVer = new Version(0, 0, 0);
                newVer = new Version(0, 0, 1);
            }

            if (newVer <= currentVer)
            {
                _logger.LogInformation("Received update notification for version {NewVersion}, but current version {CurrentVersion} is the same or newer. Ignoring.", newVersion, _agentVersion);
                return;
            }

            _logger.LogInformation("Agent update to version {NewVersion} triggered. Current: {CurrentVersion}. Download: {DownloadUrl}", newVersion, _agentVersion, downloadUrl);
            SetState(AgentState.Updating);
            StopTimers();

            _ = Task.Run(async () =>
            {
                bool success = await _updateHandler.PerformUpdateAsync(downloadUrl, checksum, releaseNotes, _agentCts.Token);
                if (success)
                {
                    _logger.LogInformation("Update to version {NewVersion} successfully initiated by updater. Agent will attempt to restart via updater. Shutting down current instance.", newVersion);
                    if (!_agentCts.IsCancellationRequested) _agentCts.Cancel();
                }
                else
                {
                    _logger.LogError("Update to version {NewVersion} failed. Agent will continue with current version {CurrentVersion}.", newVersion, _agentVersion);
                    await _serverConnector.ReportErrorToBackendAsync("UpdateFailed", $"Agent failed to update to version {newVersion}.",
                        new Dictionary<string, object?> { { "target_version", newVersion }, { "current_version", _agentVersion } });

                    if (!_agentCts.IsCancellationRequested)
                    {
                        SetState(AgentState.Idle);
                        SetupTimers();
                    }
                }
            });
        }

        public void SetAgentVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                _logger.LogWarning("Attempted to set empty agent version. Using current version {CurrentVersion} instead.", _agentVersion);
                return;
            }

            if (version != _agentVersion)
            {
                _logger.LogInformation("Agent version externally set from {OldVersion} to {NewVersion}", _agentVersion, version);
                _agentVersion = version;
                _configManager.SetAgentVersion(version);
            }
        }

        public async Task GracefulShutdownAsync()
        {
            if (GetState() == AgentState.ShuttingDown || GetState() == AgentState.Stopped)
            {
                _logger.LogInformation("Agent is already shutting down or stopped.");
                return;
            }
            _logger.LogInformation("Graceful shutdown initiated for agent version {AgentVersion}.", _agentVersion);
            SetState(AgentState.ShuttingDown);

            if (!_agentCts.IsCancellationRequested)
            {
                _agentCts.Cancel();
            }

            StopTimers();

            if (_commandExecutor != null)
            {
                _logger.LogInformation("Stopping command executor...");
                await _commandExecutor.StopProcessingAsync();
                _logger.LogInformation("Command executor stopped.");
            }

            if (_serverConnector != null)
            {
                _logger.LogInformation("Disconnecting from server...");
                await _serverConnector.DisposeAsync();
                _logger.LogInformation("Disconnected from server.");
            }

            if (_commandExecutor != null) _commandExecutor.CommandProcessedAsync -= HandleCommandProcessedAsync;
            if (_serverConnector != null)
            {
                _serverConnector.OnCommandExecuteReceived -= HandleServerCommandExecuteAsync;
                _serverConnector.OnNewVersionAvailableReceived -= HandleServerNewVersionAvailableAsync;
                _serverConnector.OnSocketTransportConnected -= OnSocketTransportConnectedAsync;
                _serverConnector.OnSocketAuthenticated -= OnSocketAuthenticatedAsync;
                _serverConnector.OnSocketAuthenticationFailed -= OnSocketAuthenticationFailedAsync;
                _serverConnector.OnSocketDisconnected -= OnSocketDisconnectedAsync;
                _serverConnector.OnSocketConnectError -= OnSocketConnectErrorAsync;
            }

            _logger.LogInformation("Agent version {AgentVersion} has shut down.", _agentVersion);
            SetState(AgentState.Stopped);
            _agentCts.Dispose();
        }
    }
}
