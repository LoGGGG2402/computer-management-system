using System;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using CMSAgent.Common.DTOs;
using CMSAgent.Configuration;
using CMSAgent.Common.Interfaces;
using CMSAgent.Monitoring;
using CMSAgent.Commands;
using CMSAgent.Update;
using CMSAgent.Security;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Net.Http;

namespace CMSAgent.Core
{
    /// <summary>
    /// Chứa toàn bộ logic hoạt động của agent
    /// </summary>
    public class AgentOperations
    {
        private readonly ILogger<AgentService> _logger;
        private readonly StateManager _stateManager;
        private readonly IConfigLoader _configLoader;
        private readonly IWebSocketConnector _webSocketConnector;
        private readonly SystemMonitor _systemMonitor;
        private readonly CommandExecutor _commandExecutor;
        private readonly UpdateHandler _updateHandler;
        private readonly SingletonMutex _singletonMutex;
        private readonly TokenProtector _tokenProtector;
        private readonly AgentSpecificSettingsOptions _agentSettings;
        private readonly HardwareInfoCollector _hardwareInfoCollector;
        private readonly IHttpClientWrapper _httpClient;

        private Timer? _statusReportTimer;
        private Timer? _updateCheckTimer;
        private Timer? _tokenRefreshTimer;
        private DateTime _lastConnectionAttempt;
        private int _connectionRetryCount = 0;
        private CancellationTokenSource? _linkedTokenSource;
        private Task? _commandProcessingTask;
        private bool _initialized = false;

        private static readonly JsonSerializerOptions _errorPayloadJsonOptions = new() 
        { 
            PropertyNameCaseInsensitive = true 
        };

        public AgentOperations(
            ILogger<AgentService> logger,
            StateManager stateManager,
            IConfigLoader configLoader,
            IWebSocketConnector webSocketConnector,
            SystemMonitor systemMonitor,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler,
            SingletonMutex singletonMutex,
            TokenProtector tokenProtector,
            AgentSpecificSettingsOptions agentSettings,
            HardwareInfoCollector hardwareInfoCollector,
            IHttpClientWrapper httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _webSocketConnector = webSocketConnector ?? throw new ArgumentNullException(nameof(webSocketConnector));
            _systemMonitor = systemMonitor ?? throw new ArgumentNullException(nameof(systemMonitor));
            _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
            _updateHandler = updateHandler ?? throw new ArgumentNullException(nameof(updateHandler));
            _singletonMutex = singletonMutex ?? throw new ArgumentNullException(nameof(singletonMutex));
            _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));
            _agentSettings = agentSettings ?? throw new ArgumentNullException(nameof(agentSettings));
            _hardwareInfoCollector = hardwareInfoCollector ?? throw new ArgumentNullException(nameof(hardwareInfoCollector));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Register event handlers
            _stateManager.StateChanged += OnStateChanged;
            _webSocketConnector.ConnectionClosed += OnConnectionClosed;
            _webSocketConnector.MessageReceived += OnMessageReceived;
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing AgentOperations...");

            if (!_singletonMutex.IsSingleInstance())
            {
                _logger.LogError("Detected another running instance of the agent. Exiting application...");
                return;
            }

            _logger.LogInformation("Initializing System Monitor...");
            _systemMonitor.Initialize();

            var config = await _configLoader.LoadRuntimeConfigAsync();
            if (config == null || string.IsNullOrEmpty(_configLoader.GetAgentId()) || string.IsNullOrEmpty(config.AgentTokenEncrypted))
            {
                _logger.LogError("Valid configuration not found. Please run the configure command first.");
                _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                return;
            }

            _initialized = true;
            _logger.LogInformation("AgentOperations initialization completed.");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_initialized)
            {
                _logger.LogWarning("AgentOperations not properly initialized, skipping main tasks.");
                await Task.Delay(5000, cancellationToken);
                return;
            }

            if (_stateManager.CurrentState == AgentState.CONFIGURATION_ERROR)
            {
                _logger.LogWarning("Agent is in configuration error state, skipping main tasks.");
                await Task.Delay(5000, cancellationToken);
                return;
            }

            _linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (!await ConnectToServerAsync())
            {
                _logger.LogWarning("Unable to connect to server, will try again later.");
                await Task.Delay(GetRetryDelay(), cancellationToken);
                return;
            }

            _connectionRetryCount = 0;
            _stateManager.SetState(AgentState.CONNECTED);

            try
            {
                _commandProcessingTask = _commandExecutor.StartProcessingAsync(_linkedTokenSource.Token);
                await SendInitialHardwareInformationAsync();
                await ProcessOfflineErrorReportsAsync();
                SetupTimers();

                if (_agentSettings.EnableAutoUpdate)
                {
                    await _updateHandler.CheckForUpdateAsync();
                }

                while (!cancellationToken.IsCancellationRequested && _webSocketConnector.IsConnected)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            finally
            {
                DisposeTimers();

                if (_linkedTokenSource != null && !_linkedTokenSource.IsCancellationRequested)
                {
                    _linkedTokenSource.Cancel();
                    _linkedTokenSource.Dispose();
                    _linkedTokenSource = null;
                }

                if (_webSocketConnector.IsConnected)
                {
                    await _webSocketConnector.DisconnectAsync();
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    _stateManager.SetState(AgentState.RECONNECTING);
                }
                else
                {
                    _stateManager.SetState(AgentState.SHUTTING_DOWN);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping AgentOperations...");

            _stateManager.StateChanged -= OnStateChanged;
            _webSocketConnector.ConnectionClosed -= OnConnectionClosed;
            _webSocketConnector.MessageReceived -= OnMessageReceived;

            DisposeTimers();

            if (_linkedTokenSource != null)
            {
                _linkedTokenSource.Dispose();
                _linkedTokenSource = null;
            }

            if (_commandProcessingTask != null && !_commandProcessingTask.IsCompleted)
            {
                try
                {
                    _ = await Task.WhenAny(_commandProcessingTask, Task.Delay(5000, cancellationToken));
                }
                catch { }
            }

            _logger.LogInformation("AgentOperations stopped.");
        }

        private async Task<bool> ConnectToServerAsync()
        {
            if (_webSocketConnector.IsConnected)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            if (_lastConnectionAttempt != default && (now - _lastConnectionAttempt).TotalSeconds < GetExponentialBackoffDelay())
            {
                return false;
            }

            _lastConnectionAttempt = now;
            _stateManager.SetState(AgentState.CONNECTING);

            try
            {
                _logger.LogInformation("Connecting to server... (Attempt: {Attempt})", _connectionRetryCount + 1);

                var config = await _configLoader.LoadRuntimeConfigAsync();
                if (config == null || string.IsNullOrEmpty(config.AgentTokenEncrypted))
                {
                    _logger.LogError("Agent token not found in runtime configuration.");
                    _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                    return false;
                }

                string agentToken;
                try
                {
                    agentToken = _tokenProtector.DecryptToken(config.AgentTokenEncrypted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to decrypt agent token.");
                    _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                    return false;
                }

                bool connected = await _webSocketConnector.ConnectAsync(agentToken);
                
                if (connected)
                {
                    _logger.LogInformation("Successfully connected to server.");
                    _connectionRetryCount = 0;
                    return true;
                }
                else
                {
                    _logger.LogWarning("Unable to connect to server.");
                    _connectionRetryCount++;
                    
                    if (_stateManager.CurrentState == AgentState.AUTHENTICATION_FAILED)
                    {
                        _logger.LogInformation("Authentication failed. Trying to refresh token...");
                        bool tokenRenewed = await AttemptReIdentifyAndConnectAsync(false);
                        if (tokenRenewed)
                        {
                            return true;
                        }
                    }
                    
                    if (_connectionRetryCount > _agentSettings.NetworkRetryMaxAttempts)
                    {
                        _stateManager.SetState(AgentState.OFFLINE);
                    }
                    else
                    {
                        _stateManager.SetState(AgentState.RECONNECTING);
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to server.");
                _connectionRetryCount++;
                
                if (_connectionRetryCount > _agentSettings.NetworkRetryMaxAttempts)
                {
                    _stateManager.SetState(AgentState.OFFLINE);
                }
                else
                {
                    _stateManager.SetState(AgentState.RECONNECTING);
                }
                
                return false;
            }
        }

        private async Task<bool> AttemptReIdentifyAndConnectAsync(bool forceRenewToken = false)
        {
            try
            {
                _logger.LogInformation("Attempting to re-identify with server...");
                
                var config = await _configLoader.LoadRuntimeConfigAsync();
                if (config == null || string.IsNullOrEmpty(_configLoader.GetAgentId()) || config.RoomConfig == null)
                {
                    _logger.LogError("Cannot re-identify: Missing runtime configuration information");
                    return false;
                }
                
                var identifyPayload = new AgentIdentifyRequest
                {
                    agentId = _configLoader.GetAgentId(),
                    positionInfo = new PositionInfo
                    {
                        roomName = config.RoomConfig.RoomName,
                        posX = config.RoomConfig.PosX,
                        posY = config.RoomConfig.PosY
                    },
                    forceRenewToken = forceRenewToken
                };
                
                var response = await _httpClient.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse>(
                    Common.Constants.ApiRoutes.Identify,
                    identifyPayload,
                    _configLoader.GetAgentId(),
                    null);
                
                if (response != null && response.status == "success" && !string.IsNullOrEmpty(response.agentToken))
                {
                    _logger.LogInformation("Received new token from server");
                    
                    string encryptedToken = _tokenProtector.EncryptToken(response.agentToken);
                    config.AgentTokenEncrypted = encryptedToken;
                    await _configLoader.SaveRuntimeConfigAsync(config);
                    
                    bool connected = await _webSocketConnector.ConnectAsync(response.agentToken);
                    
                    if (connected)
                    {
                        _logger.LogInformation("Successfully reconnected with new token");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Unable to reconnect with new token");
                        return false;
                    }
                }
                else if (response != null)
                {
                    if (response.status == "auth_failure")
                    {
                        _logger.LogWarning("Server rejected identify request: token invalid or revoked");
                        return false;
                    }
                    else if (response.status == "mfa_required")
                    {
                        _logger.LogWarning("Server requires MFA. Cannot process automatically in service context.");
                        return false;
                    }
                    else if (response.status == "position_error")
                    {
                        _logger.LogWarning("Position error: {Message}", response.message);
                        return false;
                    }
                }
                
                _logger.LogWarning("Could not re-identify with server: Invalid response");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error trying to re-identify with server");
                return false;
            }
        }

        private async Task SendInitialHardwareInformationAsync()
        {
            try
            {
                _logger.LogInformation("Collecting hardware information to send to server...");
                
                var hardwareInfo = await _hardwareInfoCollector.CollectHardwareInfoAsync();
                
                string agentId = _configLoader.GetAgentId();
                string encryptedToken = _configLoader.GetEncryptedAgentToken();
                
                if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogError("Cannot send hardware information: Missing agent authentication information");
                    return;
                }

                _ = await _httpClient.PostAsync<HardwareInfoPayload, object>(
                    Common.Constants.ApiRoutes.HardwareInfo,
                    hardwareInfo,
                    agentId,
                    encryptedToken);
                
                _logger.LogInformation("Successfully sent hardware information to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending initial hardware information");
            }
        }

        private void SetupTimers()
        {
            _statusReportTimer = new Timer(
                async (state) => await SendStatusReportAsync(),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(_agentSettings.StatusReportIntervalSec));

            if (_agentSettings.EnableAutoUpdate)
            {
                _updateCheckTimer = new Timer(
                    async (state) => await _updateHandler.CheckForUpdateAsync(),
                    null,
                    TimeSpan.FromMinutes(10),
                    TimeSpan.FromSeconds(_agentSettings.AutoUpdateIntervalSec));
            }

            _tokenRefreshTimer = new Timer(
                async (state) => await RefreshTokenAsync(),
                null,
                TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec / 2),
                TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec));
        }

        private void DisposeTimers()
        {
            _statusReportTimer?.Dispose();
            _statusReportTimer = null;

            _updateCheckTimer?.Dispose();
            _updateCheckTimer = null;

            _tokenRefreshTimer?.Dispose();
            _tokenRefreshTimer = null;
        }

        private async Task SendStatusReportAsync()
        {
            try
            {
                if (!_webSocketConnector.IsConnected)
                {
                    return;
                }

                var statusPayload = await _systemMonitor.GetCurrentStatusAsync();
                
                if (_webSocketConnector.IsConnected)
                {
                    await _webSocketConnector.SendStatusUpdateAsync(statusPayload);
                    _logger.LogTrace("Sent resource status report to server.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending resource status report.");
            }
        }

        private async Task RefreshTokenAsync()
        {
            try
            {
                _logger.LogDebug("Refreshing agent token...");
                _ = await AttemptReIdentifyAndConnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing agent token.");
            }
        }

        private void OnStateChanged(AgentState oldState, AgentState newState)
        {
            _logger.LogInformation("Agent state changed: {OldState} -> {NewState}", oldState, newState);
            
            if (newState == AgentState.AUTHENTICATION_FAILED)
            {
                _logger.LogWarning("Detected AUTHENTICATION_FAILED state, will try to re-identify with server on next connection attempt");
            }
        }

        private void OnConnectionClosed(object? sender, EventArgs e)
        {
            _logger.LogInformation("Handling WebSocket disconnection event");
            _stateManager.SetState(AgentState.DISCONNECTED);
        }

        private void OnMessageReceived(object? sender, string messageJson)
        {
            _logger.LogDebug("Received message from WebSocket: {Message}", messageJson);
        }

        private double GetExponentialBackoffDelay()
        {
            double delay = _agentSettings.NetworkRetryInitialDelaySec;
            
            if (_connectionRetryCount > 0)
            {
                delay = Math.Min(
                    _agentSettings.NetworkRetryInitialDelaySec * Math.Pow(2, _connectionRetryCount - 1),
                    300);
            }
            
            return delay;
        }

        private TimeSpan GetRetryDelay()
        {
            return TimeSpan.FromSeconds(GetExponentialBackoffDelay());
        }

        private async Task ProcessOfflineErrorReportsAsync()
        {
            _logger.LogInformation("Starting to process offline error reports...");
            string errorLogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "CMSAgent",
                "error_reports");

            if (!Directory.Exists(errorLogDirectory))
            {
                _logger.LogDebug("Offline error report directory does not exist: {ErrorLogDirectory}", errorLogDirectory);
                return;
            }

            var errorFiles = Directory.GetFiles(errorLogDirectory, "*.json");
            if (errorFiles.Length == 0)
            {
                _logger.LogInformation("No offline error reports to process.");
                return;
            }

            _logger.LogInformation("Found {Count} offline error reports.", errorFiles.Length);

            var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
            if (runtimeConfig == null || string.IsNullOrEmpty(_configLoader.GetAgentId()) || string.IsNullOrEmpty(runtimeConfig.AgentTokenEncrypted))
            {
                _logger.LogError("Cannot load runtime configuration or token to send offline error reports. Error reports will be retained.");
                return;
            }

            string? agentToken;
            try
            {
                agentToken = _tokenProtector.DecryptToken(runtimeConfig.AgentTokenEncrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent token decryption failed when processing offline errors. Error reports will be retained.");
                return;
            }

            if (string.IsNullOrEmpty(agentToken))
            {
                 _logger.LogError("Agent token invalid after decryption when processing offline errors. Error reports will be retained.");
                 return;
            }

            foreach (var errorFile in errorFiles)
            {
                try
                {
                    string jsonContent = await File.ReadAllTextAsync(errorFile);
                    var errorPayload = JsonSerializer.Deserialize<ErrorReportPayload>(jsonContent, _errorPayloadJsonOptions);

                    if (errorPayload != null)
                    {
                        _logger.LogDebug("Sending error report from file: {ErrorFile}", errorFile);
                        
                        await _httpClient.PostAsync<ErrorReportPayload, object>(Common.Constants.ApiRoutes.ReportError, errorPayload, _configLoader.GetAgentId(), agentToken);
                        
                        _logger.LogInformation("Successfully sent error report from file: {ErrorFile}. Deleting file...", errorFile);
                        File.Delete(errorFile);
                    }
                    else
                    {
                        _logger.LogWarning("Could not deserialize error report from file: {ErrorFile}. File may be corrupted or content invalid.", errorFile);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization error from offline error report file: {ErrorFile}. File may be corrupted.", errorFile);
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogError(httpEx, "HTTP error when sending error report from file {ErrorFile}. Status code (if available from inner exception): {StatusCode}. Error report will be retained.", 
                        errorFile, httpEx.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error occurred when processing offline error report file: {ErrorFile}. Error report will be retained.", errorFile);
                }
            }
            _logger.LogInformation("Finished processing offline error reports.");
        }
    }
} 