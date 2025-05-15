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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.IO;
using System.Text.Json;
using System.Net.Http;

namespace CMSAgent.Core
{
    /// <summary>
    /// Main service that coordinates agent operations, connects and manages all modules.
    /// </summary>
    public class AgentService : WorkerServiceBase
    {
        private readonly StateManager _stateManager;
        private readonly ConfigLoader _configLoader;
        private readonly IWebSocketConnector _webSocketConnector;
        private readonly SystemMonitor _systemMonitor;
        private readonly CommandExecutor _commandExecutor;
        private readonly UpdateHandler _updateHandler;
        private readonly SingletonMutex _singletonMutex;
        private readonly TokenProtector _tokenProtector;
        private readonly AgentSpecificSettingsOptions _agentSettings;
        private readonly IHostApplicationLifetime _applicationLifetime;
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

        // Add a static readonly field for JsonSerializerOptions
        private static readonly JsonSerializerOptions _errorPayloadJsonOptions = new() 
        { 
            PropertyNameCaseInsensitive = true 
        };

        /// <summary>
        /// Initializes a new instance of AgentService.
        /// </summary>
        /// <param name="logger">Logger for recording logs.</param>
        /// <param name="stateManager">Manages agent state.</param>
        /// <param name="configLoader">Loads and saves agent configuration.</param>
        /// <param name="webSocketConnector">WebSocket connection to server.</param>
        /// <param name="systemMonitor">Monitors system resources.</param>
        /// <param name="commandExecutor">Executes commands from server.</param>
        /// <param name="updateHandler">Handles agent updates.</param>
        /// <param name="singletonMutex">Ensures only one instance of agent runs.</param>
        /// <param name="tokenProtector">Protects agent token.</param>
        /// <param name="agentSettings">Specific configuration for agent.</param>
        /// <param name="applicationLifetime">Manages application lifecycle.</param>
        /// <param name="hardwareInfoCollector">Collects hardware information</param>
        /// <param name="httpClient">HTTP connection</param>
        public AgentService(
            ILogger<AgentService> logger,
            StateManager stateManager,
            ConfigLoader configLoader,
            IWebSocketConnector webSocketConnector,
            SystemMonitor systemMonitor,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler,
            SingletonMutex singletonMutex,
            TokenProtector tokenProtector,
            IOptions<AgentSpecificSettingsOptions> agentSettings,
            IHostApplicationLifetime applicationLifetime,
            HardwareInfoCollector hardwareInfoCollector,
            IHttpClientWrapper httpClient) 
            : base(logger)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _webSocketConnector = webSocketConnector ?? throw new ArgumentNullException(nameof(webSocketConnector));
            _systemMonitor = systemMonitor ?? throw new ArgumentNullException(nameof(systemMonitor));
            _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
            _updateHandler = updateHandler ?? throw new ArgumentNullException(nameof(updateHandler));
            _singletonMutex = singletonMutex ?? throw new ArgumentNullException(nameof(singletonMutex));
            _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));
            _agentSettings = agentSettings?.Value ?? throw new ArgumentNullException(nameof(agentSettings));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
            _hardwareInfoCollector = hardwareInfoCollector ?? throw new ArgumentNullException(nameof(hardwareInfoCollector));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Register event handlers
            _stateManager.StateChanged += OnStateChanged;
            _webSocketConnector.ConnectionClosed += OnConnectionClosed;
            _webSocketConnector.MessageReceived += OnMessageReceived;
        }

        /// <summary>
        /// Initialize service, set initial state and check if it can run.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the initialization process.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing AgentService...");

            // Check if this is the only instance
            if (!_singletonMutex.IsSingleInstance())
            {
                _logger.LogError("Detected another running instance of the agent. Exiting application...");
                _applicationLifetime.StopApplication();
                return;
            }

            // Initialize system monitor
            _logger.LogInformation("Initializing System Monitor...");
            _systemMonitor.Initialize();

            // Load runtime configuration
            var config = await _configLoader.LoadRuntimeConfigAsync();
            if (config == null || string.IsNullOrEmpty(_configLoader.GetAgentId()) || string.IsNullOrEmpty(config.AgentTokenEncrypted))
            {
                _logger.LogError("Valid configuration not found. Please run the configure command first.");
                _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                _applicationLifetime.StopApplication();
                return;
            }

            _initialized = true;
            _logger.LogInformation("AgentService initialization completed.");
        }

        /// <summary>
        /// Perform the main work of the service, connect to the server and set up periodic timers.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the work process.</returns>
        protected override async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            if (!_initialized)
            {
                _logger.LogWarning("AgentService not properly initialized, skipping main tasks.");
                await Task.Delay(5000, cancellationToken);
                return;
            }

            if (_stateManager.CurrentState == AgentState.CONFIGURATION_ERROR)
            {
                _logger.LogWarning("Agent is in configuration error state, skipping main tasks.");
                await Task.Delay(5000, cancellationToken);
                return;
            }

            // Create CancellationTokenSource combined with the token from the service stop operation
            _linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Connect to server
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
                // Start processing command queue
                _commandProcessingTask = _commandExecutor.StartProcessingAsync(_linkedTokenSource.Token);

                // Send initial hardware information
                await SendInitialHardwareInformationAsync();

                // Process and send saved offline error reports
                await ProcessOfflineErrorReportsAsync();

                // Set up timers
                SetupTimers();

                // Check for updates initially
                if (_agentSettings.EnableAutoUpdate)
                {
                    await _updateHandler.CheckForUpdateAsync();
                }

                // Maintain connection until stopped or disconnected
                while (!cancellationToken.IsCancellationRequested && _webSocketConnector.IsConnected)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            finally
            {
                // Stop and dispose timers if still active
                DisposeTimers();

                // Cancel running tasks
                if (_linkedTokenSource != null && !_linkedTokenSource.IsCancellationRequested)
                {
                    _linkedTokenSource.Cancel();
                    _linkedTokenSource.Dispose();
                    _linkedTokenSource = null;
                }

                // Disconnect WebSocket
                if (_webSocketConnector.IsConnected)
                {
                    await _webSocketConnector.DisconnectAsync();
                }

                // Set state
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

        /// <summary>
        /// Clean up resources when the service stops.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the cleanup process.</returns>
        protected override async Task CleanupAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cleaning up AgentService...");

            // Unregister event handlers
            _stateManager.StateChanged -= OnStateChanged;
            _webSocketConnector.ConnectionClosed -= OnConnectionClosed;
            _webSocketConnector.MessageReceived -= OnMessageReceived;

            // Ensure timers are disposed
            DisposeTimers();

            // Ensure token source is disposed
            if (_linkedTokenSource != null)
            {
                _linkedTokenSource.Dispose();
                _linkedTokenSource = null;
            }

            // Wait for running tasks to complete (maximum 5 seconds)
            if (_commandProcessingTask != null && !_commandProcessingTask.IsCompleted)
            {
                try
                {
                    _ = await Task.WhenAny(_commandProcessingTask, Task.Delay(5000, cancellationToken));
                }
                catch { }
            }

            _logger.LogInformation("AgentService cleanup completed.");
        }

        /// <summary>
        /// Connect to server via WebSocket.
        /// </summary>
        /// <returns>True if connection successful, False otherwise.</returns>
        private async Task<bool> ConnectToServerAsync()
        {
            if (_webSocketConnector.IsConnected)
            {
                return true;
            }

            // Check time between connection attempts
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

                // Load runtime configuration
                var config = await _configLoader.LoadRuntimeConfigAsync();
                if (config == null || string.IsNullOrEmpty(config.AgentTokenEncrypted))
                {
                    _logger.LogError("Agent token not found in runtime configuration.");
                    _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                    return false;
                }

                // Decrypt agent token
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

                // Connect and authenticate with server
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
                    
                    // If authentication error, try to refresh token
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

        /// <summary>
        /// Attempt to re-identify with server to refresh token.
        /// </summary>
        /// <param name="forceRenewToken">Force token renewal even if token is still valid.</param>
        /// <returns>True if successful, False if failed.</returns>
        private async Task<bool> AttemptReIdentifyAndConnectAsync(bool forceRenewToken = false)
        {
            try
            {
                _logger.LogInformation("Attempting to re-identify with server...");
                
                // Load runtime configuration
                var config = await _configLoader.LoadRuntimeConfigAsync();
                if (config == null || string.IsNullOrEmpty(_configLoader.GetAgentId()) || config.RoomConfig == null)
                {
                    _logger.LogError("Cannot re-identify: Missing runtime configuration information");
                    return false;
                }
                
                // Prepare payload for identify request
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
                
                // Send identify request
                var response = await _httpClient.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse>(
                    Common.Constants.ApiRoutes.Identify,
                    identifyPayload,
                    _configLoader.GetAgentId(),
                    null);
                
                if (response != null && response.status == "success" && !string.IsNullOrEmpty(response.agentToken))
                {
                    _logger.LogInformation("Received new token from server");
                    
                    // Encrypt and save new token
                    string encryptedToken = _tokenProtector.EncryptToken(response.agentToken);
                    config.AgentTokenEncrypted = encryptedToken;
                    await _configLoader.SaveRuntimeConfigAsync(config);
                    
                    // Try to reconnect with new token
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

        /// <summary>
        /// Send initial hardware information to server.
        /// </summary>
        /// <returns>Task representing the process of sending information.</returns>
        private async Task SendInitialHardwareInformationAsync()
        {
            try
            {
                _logger.LogInformation("Collecting hardware information to send to server...");
                
                // Collect hardware information
                var hardwareInfo = await _hardwareInfoCollector.CollectHardwareInfoAsync();
                
                // Load login information
                string agentId = _configLoader.GetAgentId();
                string encryptedToken = _configLoader.GetEncryptedAgentToken();
                
                if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogError("Cannot send hardware information: Missing agent authentication information");
                    return;
                }

                // Send information to server
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

        /// <summary>
        /// Set up periodic timers for status reporting, update checking and token refreshing.
        /// </summary>
        private void SetupTimers()
        {
            // Status report timer
            _statusReportTimer = new Timer(
                async (state) => await SendStatusReportAsync(),
                null,
                TimeSpan.FromSeconds(5), // Wait 5 seconds before sending first report
                TimeSpan.FromSeconds(_agentSettings.StatusReportIntervalSec));

            // Update check timer
            if (_agentSettings.EnableAutoUpdate)
            {
                _updateCheckTimer = new Timer(
                    async (state) => await _updateHandler.CheckForUpdateAsync(),
                    null,
                    TimeSpan.FromMinutes(10), // Wait 10 minutes before first update check
                    TimeSpan.FromSeconds(_agentSettings.AutoUpdateIntervalSec));
            }

            // Token refresh timer
            _tokenRefreshTimer = new Timer(
                async (state) => await RefreshTokenAsync(),
                null,
                TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec / 2), // Refresh token after half expiration time
                TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec));
        }

        /// <summary>
        /// Dispose of established timers.
        /// </summary>
        private void DisposeTimers()
        {
            _statusReportTimer?.Dispose();
            _statusReportTimer = null;

            _updateCheckTimer?.Dispose();
            _updateCheckTimer = null;

            _tokenRefreshTimer?.Dispose();
            _tokenRefreshTimer = null;
        }

        /// <summary>
        /// Send system resource status report to server.
        /// </summary>
        /// <returns>Task representing the process of sending the report.</returns>
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

        /// <summary>
        /// Refresh agent authentication token.
        /// </summary>
        /// <returns>Task representing the token refresh process.</returns>
        private async Task RefreshTokenAsync()
        {
            try
            {
                _logger.LogDebug("Refreshing agent token...");

                // Perform token refresh (change token and try to re-identify)
                _ = await AttemptReIdentifyAndConnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing agent token.");
            }
        }

        /// <summary>
        /// Handle agent state changes.
        /// </summary>
        /// <param name="oldState">Previous state.</param>
        /// <param name="newState">New state.</param>
        private void OnStateChanged(AgentState oldState, AgentState newState)
        {
            _logger.LogInformation("Agent state changed: {OldState} -> {NewState}", oldState, newState);
            
            // Handle when state is AUTHENTICATION_FAILED
            if (newState == AgentState.AUTHENTICATION_FAILED)
            {
                _logger.LogWarning("Detected AUTHENTICATION_FAILED state, will try to re-identify with server on next connection attempt");
            }
        }

        /// <summary>
        /// Handle event when WebSocket connection is closed.
        /// </summary>
        private void OnConnectionClosed(object? sender, EventArgs e)
        {
            _logger.LogInformation("Handling WebSocket disconnection event");
            _stateManager.SetState(AgentState.DISCONNECTED);
        }

        /// <summary>
        /// Handle event when a new message is received from WebSocket.
        /// </summary>
        private void OnMessageReceived(object? sender, string messageJson)
        {
            _logger.LogDebug("Received message from WebSocket: {Message}", messageJson);
        }

        /// <summary>
        /// Calculate wait time using exponential backoff algorithm.
        /// </summary>
        /// <returns>Wait time in seconds.</returns>
        private double GetExponentialBackoffDelay()
        {
            // Start with initial delay time from configuration
            double delay = _agentSettings.NetworkRetryInitialDelaySec;
            
            // Increase wait time exponentially, with maximum of 5 minutes
            if (_connectionRetryCount > 0)
            {
                delay = Math.Min(
                    _agentSettings.NetworkRetryInitialDelaySec * Math.Pow(2, _connectionRetryCount - 1),
                    300); // Maximum 5 minutes
            }
            
            return delay;
        }

        /// <summary>
        /// Get wait time before retrying when error occurs.
        /// </summary>
        /// <returns>Wait time.</returns>
        protected override TimeSpan GetRetryDelay()
        {
            return TimeSpan.FromSeconds(GetExponentialBackoffDelay());
        }

        /// <summary>
        /// Process and send stored offline error reports.
        /// </summary>
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
