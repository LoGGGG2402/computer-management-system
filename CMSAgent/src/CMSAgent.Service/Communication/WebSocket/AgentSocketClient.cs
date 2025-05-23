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
        private SocketIOClient.SocketIO? _socket;
        private string _serverUrl = string.Empty;
        private string _currentAgentId = string.Empty;
        private string _currentAgentToken = string.Empty;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _connectCts;
        private bool _isDisconnecting;

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
            _serverUrl = _appSettings.ServerUrl;

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
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

                if (_socket != null)
                {
                    await DisposeSocketAsync(_socket);
                }

                _socket = new SocketIOClient.SocketIO(_serverUrl, new SocketIOOptions
                {
                    Transport = TransportProtocol.WebSocket,
                    ExtraHeaders = new Dictionary<string, string>
                    {
                        { "X-Client-Type", "agent" },
                        { "X-Agent-ID", _currentAgentId },
                        { "Authorization", $"Bearer {_currentAgentToken}" }
                    },
                    Reconnection = true,
                    ReconnectionAttempts = _appSettings.WebSocketPolicy.MaxReconnectAttempts < 0 ? int.MaxValue : _appSettings.WebSocketPolicy.MaxReconnectAttempts,
                    ReconnectionDelay = _appSettings.WebSocketPolicy.ReconnectMinBackoffSeconds,
                    ReconnectionDelayMax = _appSettings.WebSocketPolicy.ReconnectMaxBackoffSeconds,
                    ConnectionTimeout = TimeSpan.FromSeconds(_appSettings.WebSocketPolicy.ConnectionTimeoutSeconds)
                });

                // Configure Socket.IO client to use our custom JSON serializer options
                _socket.Serializer = new SocketIO.Serializer.SystemTextJson.SystemTextJsonSerializer(_jsonSerializerOptions);

                SetupEventHandlers();

                try
                {
                    _logger.LogInformation("Attempting WebSocket connection...");
                    await _socket.ConnectAsync().WaitAsync(_connectCts.Token);
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
                    await HandleDisconnectionAsync(ex);
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

            _socket.OnConnected += async (sender, e) =>
            {
                _logger.LogInformation("WebSocket connected successfully! Socket ID: {SocketId}. Endpoint: {Endpoint}", _socket.Id, _serverUrl);
                if (Connected != null) await Connected.Invoke();
            };

            _socket.OnDisconnected += async (sender, reason) =>
            {
                _logger.LogWarning("WebSocket disconnected. Reason: {Reason}. IsConnected: {IsConnected}", reason, _socket?.Connected);
                await HandleDisconnectionAsync(new Exception($"Disconnected by server or network issue: {reason}"));
            };

            _socket.OnError += async (sender, e) =>
            {
                _logger.LogError("WebSocket error: {ErrorMessage}", e);
                if (e.Contains("auth_error", StringComparison.OrdinalIgnoreCase))
                {
                    string errorMessage = "WebSocket authentication failed.";
                    try { errorMessage = e; } catch { /* ignore */ }
                    _logger.LogError("WebSocket authentication failed from Server: {ErrorMessage}", errorMessage);
                    if (AuthenticationFailed != null) await AuthenticationFailed.Invoke(errorMessage);
                    await DisconnectAsync();
                }
            };

            _socket.OnReconnectAttempt += (sender, attempt) =>
            {
                _logger.LogInformation("Attempting WebSocket reconnection... Attempt: {Attempt}", attempt);
            };

            _socket.OnReconnected += (sender, e) =>
            {
                _logger.LogInformation("WebSocket reconnected after {Attempts} attempts!", e);
            };

            _socket.OnReconnectError += (sender, ex) =>
            {
                _logger.LogError(ex, "Error while attempting WebSocket reconnection.");
            };

            _socket.OnReconnectFailed += async (sender, e) =>
            {
                _logger.LogError("Failed to reconnect WebSocket after multiple attempts.");
                await HandleDisconnectionAsync(new Exception("Failed to reconnect after multiple attempts"));
            };

            _socket.OnPing += (sender, e) =>
            {
                _logger.LogTrace("WebSocket Ping received.");
            };

            _socket.OnPong += (sender, e) =>
            {
                _logger.LogTrace("WebSocket Pong received. Latency: {Latency}ms", e.TotalMilliseconds);
            };

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
            if (_isDisconnecting) return;

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

            var statusPayload = new
            {
                cpuUsage,
                ramUsage,
                diskUsage
            };            
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

        private async Task DisposeSocketAsync(SocketIOClient.SocketIO? socketToDispose)
        {
            if (socketToDispose == null) return;

            try
            {
                socketToDispose.OnConnected -= null;
                socketToDispose.OnDisconnected -= null;
                socketToDispose.OnError -= null;
                socketToDispose.OnReconnectAttempt -= null;
                socketToDispose.OnReconnected -= null;
                socketToDispose.OnReconnectError -= null;
                socketToDispose.OnReconnectFailed -= null;
                socketToDispose.OnPing -= null;
                socketToDispose.OnPong -= null;

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
