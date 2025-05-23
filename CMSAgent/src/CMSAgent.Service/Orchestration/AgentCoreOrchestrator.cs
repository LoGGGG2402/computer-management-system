using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Communication.WebSocket;
using CMSAgent.Service.Configuration.Manager;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Monitoring;
using CMSAgent.Service.Security;
using CMSAgent.Service.Update;
using CMSAgent.Service.Commands;
using CMSAgent.Service.Commands.Models; // For CommandRequest
using CMSAgent.Service.Models; 
using CMSAgent.Shared.Enums;
using Microsoft.Extensions.Options;
namespace CMSAgent.Service.Orchestration
{
    public class AgentCoreOrchestrator : IAgentCoreOrchestrator
    {
        private readonly ILogger<AgentCoreOrchestrator> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IRuntimeConfigManager _runtimeConfigManager;
        private readonly IDpapiProtector _dpapiProtector;
        private readonly IAgentApiClient _apiClient;
        private readonly IAgentSocketClient _socketClient;
        private readonly IHardwareCollector _hardwareCollector;
        private readonly IResourceMonitor _resourceMonitor;
        private readonly CommandQueue _commandQueue;
        private readonly IAgentUpdateManager _updateManager;
        private readonly AppSettings _appSettings;

        private AgentStatus _currentStatus = AgentStatus.Initializing;
        private string? _agentId;
        private string? _agentToken; // Decrypted token
        private Timer? _periodicUpdateCheckTimer;
        private CancellationTokenSource? _mainLoopCts;

        public AgentCoreOrchestrator(
            ILogger<AgentCoreOrchestrator> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IRuntimeConfigManager runtimeConfigManager,
            IDpapiProtector dpapiProtector,
            IAgentApiClient apiClient,
            IAgentSocketClient socketClient,
            IHardwareCollector hardwareCollector,
            IResourceMonitor resourceMonitor,
            CommandQueue commandQueue,
            IAgentUpdateManager updateManager,
            IOptions<AppSettings> appSettingsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hostApplicationLifetime = hostApplicationLifetime ?? throw new ArgumentNullException(nameof(hostApplicationLifetime));
            _runtimeConfigManager = runtimeConfigManager ?? throw new ArgumentNullException(nameof(runtimeConfigManager));
            _dpapiProtector = dpapiProtector ?? throw new ArgumentNullException(nameof(dpapiProtector));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
            _hardwareCollector = hardwareCollector ?? throw new ArgumentNullException(nameof(hardwareCollector));
            _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
            _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            _updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));
        }

        private void SetStatus(AgentStatus newStatus, string? message = null)
        {
            if (_currentStatus == newStatus) return;
            _logger.LogInformation("Agent status changed from {OldStatus} to {NewStatus}. {Message}", _currentStatus, newStatus, message ?? string.Empty);
            _currentStatus = newStatus;
            // Can send this status to server if needed
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentCoreOrchestrator is starting...");
            SetStatus(AgentStatus.Initializing);
            _mainLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // 1. Load runtime configuration and authenticate Agent
                if (!await LoadConfigAndAuthenticateAsync(_mainLoopCts.Token))
                {
                    _logger.LogCritical("Cannot load configuration or authenticate Agent. Orchestrator stopping.");
                    SetStatus(AgentStatus.Error, "Initialization failed: Config/Auth error.");
                    _hostApplicationLifetime.StopApplication(); // Stop entire service
                    return;
                }

                // 2. Setup WebSocket connections and events
                SetupWebSocketEventHandlers();
                await ConnectWebSocketAsync(_mainLoopCts.Token); // Try initial connection

                // 3. Start background tasks
                _commandQueue.StartProcessing(_mainLoopCts.Token); // Start command queue processing
                
                // Resource Monitor will be started after WebSocket connection is successful (in OnSocketConnected)
                // to be able to send status updates.

                // Start periodic timers
                StartPeriodicTasks();

                // Main Orchestrator loop (if needed) or just wait for cancellationToken
                _logger.LogInformation("AgentCoreOrchestrator has started successfully and is running.");
                SetStatus(AgentStatus.Connected, "Orchestrator initialized and connected."); // Assume initial WS connection success

                // Keep StartAsync running until cancelled
                await Task.Delay(Timeout.Infinite, _mainLoopCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("AgentCoreOrchestrator.StartAsync was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unexpected error in AgentCoreOrchestrator.StartAsync.");
                SetStatus(AgentStatus.Error, $"Critical error: {ex.Message}");
                _hostApplicationLifetime.StopApplication(); // Stop service if critical error
            }
            finally
            {
                _logger.LogInformation("AgentCoreOrchestrator.StartAsync ending.");
                await PerformShutdownTasksAsync(CancellationToken.None); // Cleanup when StartAsync exits
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentCoreOrchestrator is stopping...");
            SetStatus(AgentStatus.Stopping);

            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
            {
                _mainLoopCts.Cancel(); // Send stop signal to StartAsync and child tasks
            }
            
            await PerformShutdownTasksAsync(cancellationToken);

            _logger.LogInformation("AgentCoreOrchestrator has stopped.");
            SetStatus(AgentStatus.Stopped);
        }
        
        private async Task PerformShutdownTasksAsync(CancellationToken cancellationToken)
        {
            // Stop timers
            _periodicUpdateCheckTimer?.Change(Timeout.Infinite, 0);
            _periodicUpdateCheckTimer?.Dispose();
            _logger.LogDebug("Periodic timers have been stopped.");

            // Stop Resource Monitor
            await _resourceMonitor.StopMonitoringAsync();
            _logger.LogDebug("Resource Monitor has been stopped.");

            // Stop Command Queue
            await _commandQueue.StopProcessingAsync(); // Wait for commands being processed to complete (if possible)
            _logger.LogDebug("Command Queue has been stopped.");

            // Disconnect WebSocket
            if (_socketClient.IsConnected)
            {
                await _socketClient.DisconnectAsync();
            }
            _logger.LogDebug("WebSocket client has been requested to disconnect.");
        }

        private async Task<bool> LoadConfigAndAuthenticateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Loading runtime configuration...");
            var runtimeConfig = await _runtimeConfigManager.LoadConfigAsync();
            if (string.IsNullOrWhiteSpace(runtimeConfig.AgentId) ||
                string.IsNullOrWhiteSpace(runtimeConfig.AgentTokenEncrypted) ||
                runtimeConfig.RoomConfig == null)
            {
                _logger.LogError("AgentId, AgentTokenEncrypted, or RoomConfig configuration is incomplete. " +
                               "Please run Agent with 'configure' parameter to set up.");
                return false;
            }

            _agentId = runtimeConfig.AgentId;
            _agentToken = _dpapiProtector.Unprotect(runtimeConfig.AgentTokenEncrypted);

            if (string.IsNullOrWhiteSpace(_agentToken))
            {
                _logger.LogError("Cannot decrypt AgentToken. Token may be corrupted or DPAPI error.");
                return false;
            }

            _logger.LogInformation("Runtime configuration has been loaded and token has been decrypted for AgentId: {AgentId}", _agentId);
            _apiClient.SetAuthenticationCredentials(_agentId, _agentToken); // Update creds for HTTP client
            return true;
        }

        private void SetupWebSocketEventHandlers()
        {
            _socketClient.Connected += OnSocketConnected;
            _socketClient.Disconnected += OnSocketDisconnected;
            _socketClient.AuthenticationFailed += OnSocketAuthFailed;
            _socketClient.CommandReceived += OnCommandReceived;
            _socketClient.NewVersionAvailableReceived += OnNewVersionAvailable;
        }

        private async Task ConnectWebSocketAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_agentId) || string.IsNullOrWhiteSpace(_agentToken))
            {
                _logger.LogError("Cannot connect WebSocket: AgentId or AgentToken not set.");
                return;
            }

            if (_socketClient.IsConnected)
            {
                _logger.LogInformation("WebSocket is already connected. Ignoring.");
                return;
            }

            SetStatus(AgentStatus.Connecting, "Attempting WebSocket connection.");
            try
            {
                _logger.LogInformation("Connecting WebSocket...");
                await _socketClient.ConnectAsync(_agentId, _agentToken, cancellationToken);
                // Connected event will be triggered if successful
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("WebSocket connection attempt was cancelled.");
                SetStatus(AgentStatus.Disconnected, "WebSocket connection canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error trying to connect WebSocket initially.");
                SetStatus(AgentStatus.Disconnected, $"WebSocket connection error: {ex.Message}");
                // Retry logic will be handled by SocketIOClient library or can be added here if needed
            }
        }

        private Task OnSocketConnected()
        {
            _logger.LogInformation("WebSocket has connected and authenticated successfully!");
            SetStatus(AgentStatus.Connected);

            // Perform tasks after successful connection
            _ = Task.Run(async () =>
            {
                try
                {
                    // 1. Send hardware information (if not sent or needs to be resent)
                    // Need logic to decide when to resend hardware info. Example: only send once after agent starts and connects.
                    _logger.LogInformation("Collecting and sending hardware information...");
                    var hardwareInfo = await _hardwareCollector.CollectHardwareInfoAsync();
                    if (hardwareInfo != null)
                    {
                        await _apiClient.ReportHardwareInfoAsync(hardwareInfo);
                    }

                    // 2. Start Resource Monitor to send status updates
                    if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                    {
                         _logger.LogInformation("Starting Resource Monitor...");
                        await _resourceMonitor.StartMonitoringAsync(
                            _appSettings.StatusReportIntervalSec,
                            async (cpu, ram, disk) => await _socketClient.SendStatusUpdateAsync(cpu, ram, disk),
                            _mainLoopCts.Token
                        );
                    }

                    // 3. Initial update check
                    if (_appSettings.EnableAutoUpdate && _mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                    {
                        _logger.LogInformation("Performing initial update check...");
                        await _updateManager.UpdateAndInitiateAsync(_appSettings.Version, _mainLoopCts.Token);
                    }
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Error in OnSocketConnected task.");
                }
            });
            return Task.CompletedTask;
        }

        private Task OnSocketDisconnected(Exception? ex)
        {
            _logger.LogWarning(ex, "WebSocket disconnected. Reason: {ExceptionMessage}", ex?.Message ?? "N/A");
            SetStatus(AgentStatus.Disconnected, $"WebSocket disconnected: {ex?.Message ?? "N/A"}");
            // Stop Resource Monitor if running
            _ = _resourceMonitor.StopMonitoringAsync();

            // SocketIOClient library has automatic reconnection mechanism.
            // If want to fully control reconnection, need to disable Reconnection in SocketIOOptions
            // and implement retry logic here.
            // Currently, we rely on the library.
            return Task.CompletedTask;
        }

        private async Task OnSocketAuthFailed(string errorMessage)
        {
            _logger.LogError("WebSocket authentication failed: {ErrorMessage}. Need to refresh token or reconfigure.", errorMessage);
            SetStatus(AgentStatus.Error, $"WebSocket Auth Failed: {errorMessage}");
            // Try to refresh token
            // After refreshing token, try to reconnect WebSocket
            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested && !string.IsNullOrWhiteSpace(_agentToken))
            {
                await ConnectWebSocketAsync(_mainLoopCts.Token);
            }
            else
            {
                _logger.LogError("Cannot reconnect WebSocket after auth error due to invalid token or process cancelled.");
            }
        }

        private Task OnCommandReceived(CommandRequest commandRequest)
        {
            _logger.LogInformation("Orchestrator received command: ID={CommandId}, Type={CommandType}", commandRequest.CommandId, commandRequest.CommandType);
            // Queue command for processing
            _ = _commandQueue.EnqueueCommandAsync(commandRequest); // Don't wait, to not block WebSocket thread
            return Task.CompletedTask;
        }

        private Task OnNewVersionAvailable(UpdateNotification updateNotification)
        {
            _logger.LogInformation("Orchestrator received new version notification: {Version}", updateNotification.Version);
            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
            {
                 _ = _updateManager.ProcessUpdateNotificationAsync(updateNotification, _mainLoopCts.Token);
            }
            return Task.CompletedTask;
        }

        private void StartPeriodicTasks()
        {
            // 1. Periodic update check timer
            if (_appSettings.EnableAutoUpdate && _appSettings.AutoUpdateIntervalSec > 0)
            {
                _logger.LogInformation("Setting up periodic update check timer every {Interval} seconds.", _appSettings.AutoUpdateIntervalSec);
                _periodicUpdateCheckTimer = new Timer(
                    async _ =>
                    {
                        if (_socketClient.IsConnected && !_updateManager.IsUpdateInProgress && _mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                        {
                            _logger.LogInformation("[Timer] Checking for updates...");
                            await _updateManager.UpdateAndInitiateAsync(_appSettings.Version, _mainLoopCts.Token);
                        }
                    },
                    null,
                    TimeSpan.FromSeconds(_appSettings.AutoUpdateIntervalSec), // Initial delay
                    TimeSpan.FromSeconds(_appSettings.AutoUpdateIntervalSec)  // Repeat interval
                );
            }
        }
        public async Task<bool> RunInitialConfigurationAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Agent's initial configuration process...");
            SetStatus(AgentStatus.Configuring);

            // 1. Get or create AgentId
            string? agentId = await _runtimeConfigManager.GetAgentIdAsync();
            if (string.IsNullOrWhiteSpace(agentId))
            {
                agentId = Guid.NewGuid().ToString();
                await _runtimeConfigManager.UpdateAgentIdAsync(agentId);
                _logger.LogInformation("New AgentId has been created: {AgentId}", agentId);
            }
            else
            {
                _logger.LogInformation("Using existing AgentId: {AgentId}", agentId);
            }
            _agentId = agentId; // Save for use

            // 2. Request user to input position information
            Console.WriteLine($"--- CMS Agent Configuration ---");
            Console.WriteLine($"Agent ID: {agentId}");
            Console.Write("Enter room name (Room Name): ");
            string? roomName = Console.ReadLine()?.Trim();
            Console.Write("Enter X coordinate (PosX - integer): ");
            string? posXStr = Console.ReadLine()?.Trim();
            Console.Write("Enter Y coordinate (PosY - integer): ");
            string? posYStr = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(roomName) || !int.TryParse(posXStr, out int posX) || !int.TryParse(posYStr, out int posY) || posX < 0 || posY < 0)
            {
                _logger.LogError("Invalid position information.");
                Console.WriteLine("Error: Invalid position information. Please enter correct format.");
                SetStatus(AgentStatus.Error, "Configuration failed: Invalid position info.");
                return false;
            }
            var positionInfo = new PositionInfo { RoomName = roomName, PosX = posX, PosY = posY };

            // 3. Authenticate with Server (Identify Flow)
            _logger.LogInformation("Sending Identify request to server...");
            var (status, receivedToken, errorMessage) = await _apiClient.IdentifyAgentAsync(agentId, positionInfo, cancellationToken: cancellationToken);

            if (status == "mfa_required")
            {
                _logger.LogInformation("Server requires MFA.");
                Console.Write("Enter MFA code (OTP): ");
                string? mfaCode = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(mfaCode))
                {
                    _logger.LogError("MFA code not entered.");
                    Console.WriteLine("Error: MFA code cannot be empty.");
                    SetStatus(AgentStatus.Error, "Configuration failed: MFA required but not provided.");
                    return false;
                }

                // Retry Identify with MFA code
                (status, receivedToken, errorMessage) = await _apiClient.VerifyMfaAsync(agentId, mfaCode, cancellationToken);
            }

            if (status != "success" || string.IsNullOrWhiteSpace(receivedToken))
            {
                _logger.LogError("Agent identification failed. Status: {Status}, Error: {ErrorMessage}", status, errorMessage);
                Console.WriteLine($"Error: Agent identification failed. {errorMessage}");
                SetStatus(AgentStatus.Error, $"Configuration failed: {errorMessage}");
                return false;
            }

            // 4. Save configuration
            try
            {
                // Save position info
                await _runtimeConfigManager.UpdatePositionInfoAsync(positionInfo);

                // Encrypt and save token
                string? encryptedToken = _dpapiProtector.Protect(receivedToken);
                if (string.IsNullOrWhiteSpace(encryptedToken))
                {
                    _logger.LogError("Failed to encrypt token.");
                    Console.WriteLine("Error: Failed to encrypt token.");
                    SetStatus(AgentStatus.Error, "Configuration failed: Token encryption error.");
                    return false;
                }
                await _runtimeConfigManager.UpdateEncryptedAgentTokenAsync(encryptedToken);

                _logger.LogInformation("Initial configuration completed successfully.");
                Console.WriteLine("Configuration completed successfully!");
                SetStatus(AgentStatus.Connected);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration.");
                Console.WriteLine($"Error: Failed to save configuration. {ex.Message}");
                SetStatus(AgentStatus.Error, $"Configuration failed: {ex.Message}");
                return false;
            }
        }
    }
}
