using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Configuration;
using CMSAgent.Security;
using CMSAgent.Core;
using CMSAgent.Update;
using CMSAgent.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketIOClient;

namespace CMSAgent.Communication
{
    /// <summary>
    /// Manages WebSocket (Socket.IO) connection with the server.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of WebSocketConnector.
    /// </remarks>
    /// <param name="logger">Logger for logging.</param>
    /// <param name="configLoader">ConfigLoader to load configuration.</param>
    /// <param name="tokenProtector">Protector to encrypt/decrypt token.</param>
    /// <param name="stateManager">StateManager to manage agent state.</param>
    /// <param name="options">WebSocket configuration.</param>
    /// <param name="commandExecutor">CommandExecutor to execute commands.</param>
    /// <param name="updateHandler">UpdateHandler to process updates.</param>
    /// <param name="httpClient">HttpClient to send HTTP requests.</param>
    public class WebSocketConnector(
        ILogger<WebSocketConnector> logger,
        ConfigLoader configLoader,
        TokenProtector tokenProtector,
        StateManager stateManager,
        IOptions<WebSocketSettingsOptions> options,
        CommandExecutor commandExecutor,
        UpdateHandler updateHandler) : IWebSocketConnector, IDisposable
    {
        private readonly ILogger<WebSocketConnector> _logger = logger;
        private readonly ConfigLoader _configLoader = configLoader;
        private readonly TokenProtector _tokenProtector = tokenProtector;
        private readonly StateManager _stateManager = stateManager;
        private readonly CommandExecutor _commandExecutor = commandExecutor;
        private readonly UpdateHandler _updateHandler = updateHandler;
        private readonly WebSocketSettingsOptions _settings = options.Value;
        
        private SocketIOClient.SocketIO? _socket;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private int _reconnectAttempts = 0;
        private Timer? _reconnectTimer;
        private bool _isReconnecting = false;
        private bool _disposed = false;

        /// <summary>
        /// Event when a message is received from WebSocket.
        /// </summary>
        public event EventHandler<string> MessageReceived = delegate { };

        /// <summary>
        /// Event when WebSocket connection is closed.
        /// </summary>
        public event EventHandler ConnectionClosed = delegate { };

        /// <summary>
        /// Checks if WebSocket is connected.
        /// </summary>
        public bool IsConnected => _socket?.Connected ?? false;

        /// <summary>
        /// Connects to the server via WebSocket and authenticates.
        /// </summary>
        /// <param name="agentToken">Agent authentication token.</param>
        /// <returns>True if connection and authentication are successful, False if failed.</returns>
        public async Task<bool> ConnectAsync(string agentToken)
        {
            try
            {
                await _connectionLock.WaitAsync();
                
                if (IsConnected)
                {
                    _logger.LogDebug("WebSocket is already connected, skipping new connection request");
                    return true;
                }

                string serverUrl = _configLoader.Settings.ServerUrl;
                string agentId = _configLoader.GetAgentId();
                
                if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(agentId))
                {
                    _logger.LogError("Cannot connect WebSocket: Missing serverUrl or agentId");
                    return false;
                }

                _logger.LogInformation("Connecting WebSocket to {ServerUrl}", serverUrl);
                
                _socket = new SocketIOClient.SocketIO(serverUrl, new SocketIOOptions
                {
                    Path = "/socket.io",
                    Reconnection = false, // We will manage reconnection ourselves
                    ExtraHeaders = new Dictionary<string, string>
                    {
                        { HttpHeaders.AgentIdHeader, agentId },
                        { HttpHeaders.AuthorizationHeader, $"{HttpHeaders.BearerPrefix}{agentToken}" },
                        { HttpHeaders.ClientTypeHeader, HttpHeaders.ClientTypeValue }
                    }
                });

                // Register events
                RegisterSocketEvents();
                
                // Connect
                await _socket.ConnectAsync();
                
                if (!(_socket?.Connected ?? false))
                {
                    _logger.LogError("Cannot connect WebSocket to {ServerUrl}", serverUrl);
                    return false;
                }

                _logger.LogInformation("WebSocket connected to {ServerUrl}, waiting for authentication", serverUrl);
                
                // Socket.IO will send auth success or failure event
                // We wait at least 5 seconds to know the authentication result
                bool authenticated = await Task.Run(async () =>
                {
                    int attempts = 0;
                    while (attempts < 10)
                    {
                        if (!_socket.Connected)
                        {
                            return false;
                        }

                        if (_stateManager.CurrentState == AgentState.CONNECTED)
                        {
                            return true;
                        }

                        await Task.Delay(500);
                        attempts++;
                    }

                    return false;
                });

                if (authenticated)
                {
                    _logger.LogInformation("WebSocket authenticated successfully");
                    // Reset reconnect attempts counter
                    _reconnectAttempts = 0;
                    return true;
                }
                else
                {
                    _logger.LogError("WebSocket authentication failed");
                    _stateManager.SetState(AgentState.AUTHENTICATION_FAILED);
                    await DisconnectAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting WebSocket");
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Closes the WebSocket connection.
        /// </summary>
        /// <returns>Task representing the connection closure.</returns>
        public async Task DisconnectAsync()
        {
            try
            {
                await _connectionLock.WaitAsync();
                
                if (_socket != null)
                {
                    if (_socket.Connected)
                    {
                        _logger.LogInformation("Closing WebSocket connection");
                        await _socket.DisconnectAsync();
                    }

                    _socket.Dispose();
                    _socket = null;
                }

                StopReconnectTimer();
                
                if (_stateManager.CurrentState == AgentState.CONNECTED)
                {
                    _stateManager.SetState(AgentState.DISCONNECTED);
                }
                
                _logger.LogInformation("WebSocket connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Sends resource status update to the server.
        /// </summary>
        /// <param name="payload">Resource status data.</param>
        /// <returns>Task representing the data sending process.</returns>
        public async Task SendStatusUpdateAsync(StatusUpdatePayload payload)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send status update: WebSocket is not connected");
                return;
            }

            if (_socket == null)
            {
                _logger.LogWarning("Cannot send status update: WebSocket is null");
                return;
            }

            try
            {
                await _socket.EmitAsync(WebSocketEvents.AgentStatusUpdate, payload);
                _logger.LogDebug("Sent status update: CPU {CpuUsage:F1}%, RAM {RamUsage:F1}%, Disk {DiskUsage:F1}%",
                    payload.cpuUsage, payload.ramUsage, payload.diskUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when sending status update");
            }
        }

        /// <summary>
        /// Sends command result to the server.
        /// </summary>
        /// <param name="payload">Payload containing command result.</param>
        /// <returns>Task with sending result: true if successful, false if failed.</returns>
        public async Task<bool> SendCommandResultAsync(CommandResultPayload payload)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send command result: WebSocket is not connected");
                return false;
            }

            if (_socket == null)
            {
                _logger.LogWarning("Cannot send command result: WebSocket is null");
                return false;
            }

            try
            {
                await _socket.EmitAsync(WebSocketEvents.AgentCommandResult, payload);
                _logger.LogDebug("Sent command result {CommandId}, status: {Success}",
                    payload.commandId, payload.success ? "Success" : "Failed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when sending command result {CommandId}", payload.commandId);
                return false;
            }
        }

        /// <summary>
        /// Registers WebSocket events.
        /// </summary>
        private void RegisterSocketEvents()
        {
            if (_socket == null)
                return;

            // Event when WebSocket connection is successful
            _socket.OnConnected += (sender, e) =>
            {
                _logger.LogInformation("WebSocket connected successfully, waiting for authentication");
            };

            // Event when WebSocket connection is disconnected
            _socket.OnDisconnected += (sender, e) =>
            {
                _logger.LogWarning("WebSocket disconnected: {Reason}", e);
                
                if (_stateManager.CurrentState == AgentState.CONNECTED)
                {
                    _stateManager.SetState(AgentState.DISCONNECTED);
                }
                
                ConnectionClosed?.Invoke(this, EventArgs.Empty);
                
                // Start reconnection
                StartReconnectTimer();
            };

            // Event when there is a WebSocket error
            _socket.OnError += (sender, e) =>
            {
                _logger.LogError("WebSocket error: {Error}", e);
            };

            // Event when WebSocket authentication is successful
            _socket.On(WebSocketEvents.AgentWsAuthSuccess, response =>
            {
                _logger.LogInformation("WebSocket authenticated successfully");
                _stateManager.SetState(AgentState.CONNECTED);
            });

            // Event when WebSocket authentication fails
            _socket.On(WebSocketEvents.AgentWsAuthFailed, response =>
            {
                string reason = "Unknown reason";
                try
                {
                    var responseValues = response.GetValue<object[]>();
                    if (responseValues != null && responseValues.Length > 0)
                    {
                        reason = response.GetValue<string>(0) ?? reason;
                    }
                }
                catch { }

                _logger.LogError("WebSocket authentication failed: {Reason}", reason);
                _stateManager.SetState(AgentState.AUTHENTICATION_FAILED);
                
                // Close connection
                _ = DisconnectAsync();
            });

            // Event when received a command to execute
            _socket.On(WebSocketEvents.CommandExecute, async response =>
            {
                try
                {
                    var command = response.GetValue<CommandPayload>();
                    if (command == null)
                    {
                        _logger.LogError("Received null command from server");
                        return;
                    }

                    _logger.LogInformation("Received {CommandType} command from server: {CommandId}", command.commandType, command.commandId);

                    MessageReceived?.Invoke(this, $"Received {command.commandType} command from server: {command.commandId}");

                    // Add command to queue
                    bool enqueued = _commandExecutor.TryEnqueueCommand(command);
                    if (!enqueued)
                    {
                        _logger.LogError("Cannot add command to queue: Queue is full");
                        
                        // Send error result back to server
                        var result = new CommandResultPayload
                        {
                            commandId = command.commandId,
                            success = false,
                            type = command.commandType,
                            result = new CommandResultData
                            {
                                stdout = string.Empty,
                                stderr = string.Empty,
                                errorMessage = "Cannot process command: Queue is full",
                                errorCode = string.Empty
                            }
                        };
                        
                        await SendCommandResultAsync(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when processing command from server");
                }
            });

            // Event when there is a new agent version
            _socket.On(WebSocketEvents.AgentNewVersionAvailable, async response =>
            {
                try
                {
                    var updateInfo = response.GetValue<UpdateCheckResponse>();
                    if (updateInfo == null)
                    {
                        _logger.LogError("Received null update information from server");
                        return;
                    }

                    // Kiểm tra xem thông tin update có đủ và hợp lệ không
                    if (string.IsNullOrEmpty(updateInfo.version))
                    {
                        _logger.LogError("Received update information without version");
                        return;
                    }

                    _logger.LogInformation("Detected new agent version: {NewVersion}", updateInfo.version);
                    
                    MessageReceived?.Invoke(this, $"Detected new agent version: {updateInfo.version}");
                    
                    // Validate response contains necessary fields for update
                    if (string.IsNullOrEmpty(updateInfo.download_url) || string.IsNullOrEmpty(updateInfo.checksum_sha256))
                    {
                        _logger.LogError("Received incomplete update information. Missing download URL or checksum");
                        return;
                    }

                    // Send to UpdateHandler for processing
                    await _updateHandler.ProcessUpdateAsync(updateInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when processing update information from server");
                }
            });
        }

        /// <summary>
        /// Starts timer for reconnection.
        /// </summary>
        private void StartReconnectTimer()
        {
            lock (this)
            {
                if (_isReconnecting || _disposed)
                    return;

                _isReconnecting = true;

                // Calculate reconnection wait time
                int delay = CalculateReconnectDelay();
                
                _logger.LogInformation("Will try reconnecting after {Delay} seconds (attempt {Attempt})",
                    delay, _reconnectAttempts + 1);

                StopReconnectTimer();
                
                _reconnectTimer = new Timer(ReconnectTimerCallback, null, TimeSpan.FromSeconds(delay), Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Stops reconnection timer.
        /// </summary>
        private void StopReconnectTimer()
        {
            lock (this)
            {
                _reconnectTimer?.Dispose();
                _reconnectTimer = null;
            }
        }

        /// <summary>
        /// Callback when reconnection timer activates.
        /// </summary>
        private async void ReconnectTimerCallback(object? state)
        {
            try
            {
                _reconnectAttempts++;
                
                _logger.LogInformation("Trying to reconnect attempt {Attempt}", _reconnectAttempts);
                
                // If the number of attempts exceeds the limit, stop
                if (_settings.ReconnectAttemptsMax.HasValue && _reconnectAttempts > _settings.ReconnectAttemptsMax.Value)
                {
                    _logger.LogError("Exceeded maximum reconnection attempts ({MaxAttempts})",
                        _settings.ReconnectAttemptsMax.Value);
                    
                    _isReconnecting = false;
                    return;
                }

                // Get token from configuration and decrypt
                string encryptedToken = _configLoader.GetEncryptedAgentToken();
                if (string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogError("Cannot reconnect: Missing authentication token");
                    StartReconnectTimer();
                    return;
                }

                string token;
                try 
                {
                    token = _tokenProtector.DecryptToken(encryptedToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot process offline queue: unable to decrypt token");
                    StartReconnectTimer();
                    return;
                }

                // Try to reconnect
                bool connected = await ConnectAsync(token);
                
                if (connected)
                {
                    _logger.LogInformation("WebSocket reconnection successful");
                    _isReconnecting = false;
                    _reconnectAttempts = 0;
                }
                else
                {
                    _logger.LogWarning("WebSocket reconnection failed, will try again later");
                    StartReconnectTimer();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when trying to reconnect WebSocket");
                StartReconnectTimer();
            }
        }

        /// <summary>
        /// Calculates reconnection delay time based on number of attempts.
        /// </summary>
        private int CalculateReconnectDelay()
        {
            // Increase wait time exponentially, but with a maximum limit
            int delay = (int)Math.Min(
                _settings.ReconnectDelayMaxSec,
                _settings.ReconnectDelayInitialSec * Math.Pow(1.5, _reconnectAttempts)
            );
            
            return delay;
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _ = DisconnectAsync().ConfigureAwait(false);
                    StopReconnectTimer();
                    _connectionLock?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
