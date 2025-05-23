using CMSAgent.Service.Commands.Models;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Models;
using CMSAgent.Shared.Constants;
using Microsoft.Extensions.Options;
using SocketIOClient; 
using SocketIOClient.Transport;
using System.Text.Json;

namespace CMSAgent.Service.Communication.WebSocket
{
    public class AgentSocketClient : IAgentSocketClient
    {
        private readonly ILogger<AgentSocketClient> _logger;
        private readonly AppSettings _appSettings;
        private SocketIOClient.SocketIO? _socket; // Nullable to allow reinitialization
        private string _serverUrl = string.Empty;
        private string _currentAgentId = string.Empty;
        private string _currentAgentToken = string.Empty;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _connectCts;
        private bool _isDisconnecting; // Added field to track active disconnection state

        public event Func<Task>? Connected;
        public event Func<Exception?, Task>? Disconnected;
        public event Func<string, Task>? AuthenticationFailed;
        public event Func<CommandRequest, Task>? CommandReceived;
        public event Func<UpdateNotification, Task>? NewVersionAvailableReceived;

        public bool IsConnected => _socket?.Connected ?? false;

        private readonly JsonSerializerOptions _jsonSerializerOptions;


        public AgentSocketClient(IOptions<AppSettings> appSettingsOptions, ILogger<AgentSocketClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            if (string.IsNullOrWhiteSpace(_appSettings.ServerUrl))
            {
                throw new InvalidOperationException("ServerUrl is not configured in appsettings for WebSocket.");
            }
            // URL for Socket.IO is typically the server's base URL.
            // SocketIOClient library will automatically add "/socket.io/" path.
            _serverUrl = _appSettings.ServerUrl;

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task ConnectAsync(string agentId, string agentToken, CancellationToken cancellationToken = default)
        {
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (IsConnected)
                {
                    _logger.LogInformation("WebSocket is already connected. Ignoring reconnection request.");
                    return;
                }

                _currentAgentId = agentId;
                _currentAgentToken = agentToken;
                _connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _logger.LogInformation("Initializing WebSocket connection to {ServerUrl} for AgentId: {AgentId}", _serverUrl, _currentAgentId);

                // Dispose old socket if exists to avoid resource leaks
                if (_socket != null)
                {
                    await DisposeSocketAsync(_socket);
                }

                _socket = new SocketIOClient.SocketIO(_serverUrl, new SocketIOOptions
                {
                    Transport = TransportProtocol.WebSocket, // Use WebSocket only
                    Query = new Dictionary<string, string>
                    {
                        // Some Socket.IO libraries may need query params instead of headers for initial handshake.
                        // However, according to API spec, we use headers.
                    },
                    ExtraHeaders = new Dictionary<string, string>
                    {
                        { "X-Client-Type", AgentConstants.HttpClientTypeAgent },
                        { "X-Agent-ID", _currentAgentId },
                        { "Authorization", $"Bearer {_currentAgentToken}" }
                    },
                    Reconnection = true, // Allow library to automatically reconnect
                    ReconnectionAttempts = _appSettings.WebSocketPolicy.MaxReconnectAttempts < 0 ? int.MaxValue : _appSettings.WebSocketPolicy.MaxReconnectAttempts,
                    ReconnectionDelay = _appSettings.WebSocketPolicy.ReconnectMinBackoffSeconds, // Use seconds directly
                    ReconnectionDelayMax = _appSettings.WebSocketPolicy.ReconnectMaxBackoffSeconds, // Use seconds directly
                    ConnectionTimeout = TimeSpan.FromSeconds(_appSettings.WebSocketPolicy.ConnectionTimeoutSeconds)
                });

                SetupEventHandlers();

                try
                {
                    _logger.LogInformation("Attempting WebSocket connection...");
                    await _socket.ConnectAsync().WaitAsync(_connectCts.Token); // Use WaitAsync with CancellationToken
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("WebSocket connection attempt was canceled.");
                    await HandleDisconnectionAsync(new Exception("Connection attempt canceled."));
                }
                catch (TimeoutException ex)
                {
                     _logger.LogError(ex, "Timeout while connecting to WebSocket.");
                     await HandleDisconnectionAsync(ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while connecting to WebSocket.");
                    await HandleDisconnectionAsync(ex); // Trigger Disconnected event
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void SetupEventHandlers()
        {
            if (_socket == null) return;

            _socket.OnConnected += (sender, e) =>
            {
                _logger.LogInformation("WebSocket connected successfully! Socket ID: {SocketId}. Endpoint: {Endpoint}", _socket.Id, _serverUrl);
                // API spec says server will send 'agent:ws_auth_success' or 'agent:ws_auth_failed'
                // Instead of relying on OnConnected directly, we should wait for these events.
                // However, OnConnected from the library usually means TCP/WS handshake is complete.
                // Application-level authentication (agentId, token) will be responded by server via separate event.
            };

            _socket.OnDisconnected += async (sender, reason) => // reason is string
            {
                _logger.LogWarning("WebSocket disconnected. Reason: {Reason}. IsConnected: {IsConnected}", reason, _socket?.Connected);
                // Library may automatically attempt to reconnect.
                // Only trigger our Disconnected event if not actively disconnected by us.
                await HandleDisconnectionAsync(new Exception($"Disconnected by server or network issue: {reason}"));
            };

            _socket.OnError += (sender, e) => // e is error string
            {
                _logger.LogError("WebSocket error: {ErrorMessage}", e);
                // Consider whether to trigger Disconnected here, depending on error type.
            };

            _socket.OnReconnectAttempt += (sender, attempt) =>
            {
                _logger.LogInformation("Attempting WebSocket reconnection... Attempt: {Attempt}", attempt);
            };

            _socket.OnReconnected += (sender, e) => // e is attempt count
            {
                _logger.LogInformation("WebSocket reconnected after {Attempts} attempts!", e);
                // Need to re-authenticate with server after reconnection.
                // Or server automatically authenticates based on saved headers/query.
                // If library doesn't resend headers on reconnect, we need to handle it.
                // Usually, Socket.IO client library will try to maintain session.
            };

            _socket.OnReconnectError += (sender, ex) =>
            {
                _logger.LogError(ex, "Error while attempting WebSocket reconnection.");
            };

            _socket.OnReconnectFailed += async (sender, e) =>
            {
                _logger.LogError("Failed to reconnect WebSocket after multiple attempts.");
                // Trigger Disconnected event and request reconfiguration
                await HandleDisconnectionAsync(new Exception("Failed to reconnect after multiple attempts"));
            };

            _socket.OnPing += (sender, e) =>
            {
                _logger.LogTrace("WebSocket Ping received.");
            };
            _socket.OnPong += (sender, e) => // e is TimeSpan (latency)
            {
                _logger.LogTrace("WebSocket Pong received. Latency: {Latency}ms", e.TotalMilliseconds);
            };


            // Listen for Server events according to API spec
            _socket.On("agent:ws_auth_success", async response =>
            {
                _logger.LogInformation("WebSocket authentication successful from Server.");
                if (Connected != null) await Connected.Invoke();
            });

            _socket.On("agent:ws_auth_failed", async response =>
            {
                string errorMessage = "WebSocket authentication failed.";
                try { errorMessage = response.GetValue<string>() ?? errorMessage; } catch { /* ignore */ }
                _logger.LogError("WebSocket authentication failed from Server: {ErrorMessage}", errorMessage);
                if (AuthenticationFailed != null) await AuthenticationFailed.Invoke(errorMessage);
                // Disconnect and request reconfiguration
                await DisconnectAsync();
            });
            
            // Old API spec had connect_error, but SocketIOClient uses OnError or ReconnectError/Failed events
            // Instead, we rely on agent:ws_auth_failed

            _socket.On("command:execute", async response =>
            {
                try
                {
                    _logger.LogDebug("Received 'command:execute' event: {ResponseText}", response.ToString());
                    var commandRequest = response.GetValue<CommandRequest>();
                    if (commandRequest != null && !string.IsNullOrEmpty(commandRequest.CommandId))
                    {
                        _logger.LogInformation("Received command: Type='{CommandType}', ID='{CommandId}'", commandRequest.CommandType, commandRequest.CommandId);
                        if (CommandReceived != null) await CommandReceived.Invoke(commandRequest);
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse CommandRequest from 'command:execute' event.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing 'command:execute' event.");
                }
            });

            _socket.On("agent:new_version_available", async response =>
            {
                try
                {
                    _logger.LogDebug("Received 'agent:new_version_available' event: {ResponseText}", response.ToString());
                    var updateNotification = response.GetValue<UpdateNotification>();
                    if (updateNotification != null && !string.IsNullOrEmpty(updateNotification.Version))
                    {
                        _logger.LogInformation("Received new version notification: {Version}", updateNotification.Version);
                        if (NewVersionAvailableReceived != null) await NewVersionAvailableReceived.Invoke(updateNotification);
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse UpdateNotification from 'agent:new_version_available' event.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing 'agent:new_version_available' event.");
                }
            });
        }

        private async Task HandleDisconnectionAsync(Exception? ex)
        {
            if (_isDisconnecting) return; // Skip if we're actively disconnecting

            _logger.LogWarning(ex, "Handling WebSocket disconnection.");
            if (Disconnected != null)
            {
                try
                {
                    await Disconnected.Invoke(ex);
                }
                catch (Exception eventEx)
                {
                    _logger.LogError(eventEx, "Error in Disconnected event handler.");
                }
            }
        }

        public async Task DisconnectAsync()
        {
            _isDisconnecting = true;
            try
            {
                if (_socket != null)
                {
                    _logger.LogInformation("Disconnecting WebSocket...");
                    await _socket.DisconnectAsync();
                    await DisposeSocketAsync(_socket);
                    _socket = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disconnecting WebSocket.");
            }
            finally
            {
                _isDisconnecting = false;
            }
        }

        public async Task SendStatusUpdateAsync(double cpuUsage, double ramUsage, double diskUsage)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send status update: WebSocket is not connected.");
                return;
            }

            var statusPayload = new { cpu_usage = cpuUsage, ram_usage = ramUsage, disk_usage = diskUsage };
            _logger.LogDebug("Sending status update: CPU={CpuUsage}%, RAM={RamUsage}%, Disk={DiskUsage}%", cpuUsage, ramUsage, diskUsage);
            await _socket!.EmitAsync("agent:status_update", statusPayload);
        }

        public async Task SendCommandResultAsync(CommandResult commandResult)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send command result: WebSocket is not connected.");
                return;
            }

            _logger.LogDebug("Sending command result for CommandId: {CommandId}", commandResult.CommandId);
            await _socket!.EmitAsync("agent:command_result", commandResult);
        }

        public async Task SendUpdateStatusAsync(object statusPayload)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send update status: WebSocket is not connected.");
                return;
            }

            _logger.LogDebug("Sending update status: {StatusPayload}", statusPayload);
            await _socket!.EmitAsync("agent:update_status", statusPayload);
        }

        private async Task DisposeSocketAsync(SocketIOClient.SocketIO? socketToDispose)
        {
            if (socketToDispose == null) return;

            try
            {
                // Remove all event handlers to prevent memory leaks
                socketToDispose.OnConnected -= null;
                socketToDispose.OnDisconnected -= null;
                socketToDispose.OnError -= null;
                socketToDispose.OnReconnectAttempt -= null;
                socketToDispose.OnReconnected -= null;
                socketToDispose.OnReconnectError -= null;
                socketToDispose.OnReconnectFailed -= null;
                socketToDispose.OnPing -= null;
                socketToDispose.OnPong -= null;

                // Remove all event listeners
                socketToDispose.Off("agent:ws_auth_success");
                socketToDispose.Off("agent:ws_auth_failed");
                socketToDispose.Off("command:execute");
                socketToDispose.Off("agent:new_version_available");

                if (socketToDispose.Connected)
                {
                    await socketToDispose.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disposing WebSocket.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            _connectCts?.Cancel();
            _connectCts?.Dispose();
            await DisconnectAsync();
            _connectionLock.Dispose();
        }
    }
}
