using Microsoft.Extensions.Logging;
using SocketIOClient; // Updated import
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Communication
{
    public class SocketIOClientWrapper : IAsyncDisposable
    {
        private readonly ILogger<SocketIOClientWrapper> _logger;
        private SocketIOClient.SocketIO? _socket; // Fully qualified class name
        private readonly string _serverUrl;
        private readonly Dictionary<string, List<Func<JsonElement, Task>>> _eventHandlers = new();
        private readonly Dictionary<string, List<Func<JsonElement, Task>>> _eventHandlersWithAck = new();
        private CancellationTokenSource? _connectionCts;

        public event Func<Task>? Connected;
        public event Func<Task>? Authenticated;
        public event Func<string?, Task>? AuthenticationFailed;
        public event Func<Task>? Disconnected;
        public event Func<Exception?, Task>? ConnectError;
        public event Func<string, Task>? Reconnecting;
        public event Func<Task>? Reconnected;
        public event Func<Task>? PingReceived;
        public event Func<TimeSpan, Task>? PongReceived;

        public bool IsTransportConnected => _socket?.Connected ?? false;
        public bool IsAuthenticated { get; private set; }

        public SocketIOClientWrapper(ILogger<SocketIOClientWrapper> logger, string serverUrl)
        {
            _logger = logger;
            // The serverUrl from config is expected to be the base URL (e.g., http://your-server.com:3000)
            // Socket.IO will connect to its path (usually /socket.io/) relative to this.
            _serverUrl = serverUrl.TrimEnd('/');
        }

        public async Task ConnectAsync(string deviceId, string agentToken, CancellationToken cancellationToken = default)
        {
            if (IsTransportConnected)
            {
                _logger.LogInformation("Socket.IO client transport is already connected.");
                if (IsAuthenticated) _logger.LogInformation("Socket.IO client is already authenticated.");
                return;
            }

            _logger.LogInformation("Attempting to connect to Socket.IO server at {ServerUrl} for DeviceId: {DeviceId}", _serverUrl, deviceId);
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsAuthenticated = false;

            var options = new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
                // Auth object for Socket.IO v3+ connection (matches agent_standard.md)
                Auth = new { agent_id = deviceId, agent_token = agentToken },
                // ExtraHeaders are not typically needed if Auth is used correctly,
                // but agent_standard.md mentions them for Socket.IO client connection.
                // The standard is a bit ambiguous here, Python client uses header-like auth in connect().
                // For SocketIOClient library, 'Auth' is the standard way.
                // We'll keep ExtraHeaders for now if the server specifically expects them despite 'Auth'.
                ExtraHeaders = new Dictionary<string, string>
                {
                    // These might be redundant if server correctly uses 'Auth' object
                    // { "X-Agent-Id", deviceId }, 
                    // { "Authorization", $"Bearer {agentToken}" } 
                },
                Query = new Dictionary<string, string>
                {
                    // Query parameters can also be used for auth if server expects it,
                    // but 'Auth' object is preferred for Socket.IO v3+
                    // { "agent_id", deviceId },
                    // { "token", agentToken }
                }
            };

            _logger.LogInformation("Using Auth object for Socket.IO connection: agent_id={AgentId}, agent_token present: {TokenPresent}", deviceId, !string.IsNullOrEmpty(agentToken));

            _socket = new SocketIOClient.SocketIO(_serverUrl, options); // serverUrl should be base URL

            _socket.OnConnected += async (sender, e) =>
            {
                _logger.LogInformation("Successfully connected Socket.IO transport. Waiting for server authentication.");
                IsAuthenticated = false;
                if (Connected != null) await Connected.Invoke();
            };

            _socket.OnDisconnected += async (sender, reason) =>
            {
                bool wasAuthenticated = IsAuthenticated;
                IsAuthenticated = false;
                _logger.LogWarning("Disconnected from Socket.IO server. Reason: {Reason}. WasAuthenticated: {WasAuthenticated}", reason, wasAuthenticated);
                if (Disconnected != null) await Disconnected.Invoke();
            };

            _socket.OnError += async (sender, error) =>
            {
                _logger.LogError("Socket.IO connection error: {Error}", error);
                IsAuthenticated = false;
                if (ConnectError != null) await ConnectError.Invoke(new Exception(error));
            };

            _socket.OnReconnectAttempt += async (sender, attempt) =>
            {
                _logger.LogInformation("Attempting to reconnect to Socket.IO server... Attempt #{Attempt}", attempt);
                IsAuthenticated = false;
                if (Reconnecting != null) await Reconnecting.Invoke(attempt.ToString());
            };

            _socket.OnReconnected += async (sender, attempts) =>
            {
                _logger.LogInformation("Successfully reconnected Socket.IO transport after {Attempts} attempts. Waiting for server authentication.", attempts);
                IsAuthenticated = false;
                if (Reconnected != null) await Reconnected.Invoke();
            };

            _socket.OnPing += async (sender, e) =>
            {
                _logger.LogTrace("Socket.IO Ping received.");
                if (PingReceived != null) await PingReceived.Invoke();
            };

            _socket.OnPong += async (sender, e) =>
            {
                _logger.LogTrace("Socket.IO Pong received in {Duration}", e);
                if (PongReceived != null) await PongReceived.Invoke(e);
            };

            _socket.On("agent:ws_auth_success", HandleAuthSuccessResponse);
            _socket.On("agent:ws_auth_failed", HandleAuthFailedResponse);

            foreach (var kvp in _eventHandlers)
            {
                RegisterInternalHandler(kvp.Key, false);
            }
            foreach (var kvp in _eventHandlersWithAck)
            {
                RegisterInternalHandler(kvp.Key, true);
            }

            try
            {
                await _socket.ConnectAsync().WaitAsync(_connectionCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Socket.IO connection attempt was canceled.");
                IsAuthenticated = false;
                if (ConnectError != null) await ConnectError.Invoke(new OperationCanceledException("Connection cancelled by token."));
                await CleanupSocketAsync(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Socket.IO server at {ServerUrl}", _serverUrl);
                IsAuthenticated = false;
                if (ConnectError != null) await ConnectError.Invoke(ex);
                await CleanupSocketAsync(false);
                throw;
            }
        }

        private async Task HandleAuthSuccessResponse(SocketIOResponse response)
        {
            // Standard: "Server Gửi Cho Agent: agent:ws_auth_success" - no specific payload defined, implies success by event name
            _logger.LogInformation("Received 'agent:ws_auth_success' from server. Agent WebSocket is authenticated.");
            IsAuthenticated = true;
            if (Authenticated != null) await Authenticated.Invoke();
        }

        private async Task HandleAuthFailedResponse(SocketIOResponse response)
        {
            string reason = "Authentication failed"; // Default reason
            try
            {
                // Python client expects a dict with 'message'. Let's try to parse that.
                var json = response.GetValue<JsonElement>();
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("message", out var messageElement))
                {
                    reason = messageElement.GetString() ?? reason;
                }
                else
                {
                    reason = response.ToString(); // Fallback to raw response
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse reason from agent:ws_auth_failed response: {Data}", response.ToString());
            }
            _logger.LogError("Received 'agent:ws_auth_failed' from server. Authentication failed. Reason: {Reason}", reason);
            IsAuthenticated = false;
            if (AuthenticationFailed != null) await AuthenticationFailed.Invoke(reason);
        }

        public void On(string eventName, Func<JsonElement, Task> callback)
        {
            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName] = new List<Func<JsonElement, Task>>();
            }
            _eventHandlers[eventName].Add(callback);

            if (_socket != null && _socket.Connected)
            {
                RegisterInternalHandler(eventName, false);
            }
        }

        public void OnAck(string eventName, Func<JsonElement, Task> callbackWithAckData)
        {
            if (!_eventHandlersWithAck.ContainsKey(eventName))
            {
                _eventHandlersWithAck[eventName] = new List<Func<JsonElement, Task>>();
            }
            _eventHandlersWithAck[eventName].Add(callbackWithAckData);

            if (_socket != null && _socket.Connected)
            {
                RegisterInternalHandler(eventName, true);
            }
        }

        private void RegisterInternalHandler(string eventName, bool isForAck)
        {
            _socket?.On(eventName, async response =>
            {
                var handlerList = isForAck ? _eventHandlersWithAck : _eventHandlers;
                _logger.LogTrace("Received Socket.IO event '{EventName}'. Data: {Data}", eventName, response.ToString());

                if (handlerList.TryGetValue(eventName, out var callbacks))
                {
                    JsonElement data = response.GetValue<JsonElement>();
                    foreach (var cb in callbacks.ToList())
                    {
                        try
                        {
                            await cb(data);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing callback for event '{EventName}'.", eventName);
                        }
                    }
                }
            });
        }

        public async Task EmitAsync(string eventName, params object[] data)
        {
            if (!IsAuthenticated) // Check IsAuthenticated, not just IsTransportConnected
            {
                _logger.LogWarning("Socket.IO client is not authenticated. Cannot emit event '{EventName}'.", eventName);
                return;
            }
            // IsTransportConnected check is implicitly handled by IsAuthenticated usually, but good for robustness
            if (!IsTransportConnected)
            {
                _logger.LogWarning("Socket.IO client transport is not connected. Cannot emit event '{EventName}'.", eventName);
                IsAuthenticated = false; // If transport died, auth is lost
                return;
            }
            try
            {
                _logger.LogTrace("Emitting Socket.IO event '{EventName}' with data: {Data}", eventName, data.Length > 0 ? JsonSerializer.Serialize(data) : "[No data]");
                await _socket!.EmitAsync(eventName, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emitting Socket.IO event '{EventName}'.", eventName);
            }
        }

        public async Task EmitWithAckAsync<TResponse>(string eventName, Func<TResponse, Task> ackCallback, TimeSpan? timeout = null, params object[] data) where TResponse : class
        {
            if (!IsAuthenticated)
            {
                _logger.LogWarning("Socket.IO client is not authenticated. Cannot emit event '{EventName}' with ack.", eventName);
                throw new InvalidOperationException("Socket is not authenticated for emitting with acknowledgement.");
            }
            if (!IsTransportConnected)
            {
                _logger.LogWarning("Socket.IO client transport is not connected. Cannot emit event '{EventName}' with ack.", eventName);
                IsAuthenticated = false; // If transport died, auth is lost
                throw new InvalidOperationException("Socket transport is not connected for emitting with acknowledgement.");
            }

            _logger.LogTrace("Emitting Socket.IO event '{EventName}' with ack, data: {Data}", eventName, data.Length > 0 ? JsonSerializer.Serialize(data) : "[No data]");

            var tcs = new TaskCompletionSource<bool>();

            CancellationTokenSource timeoutCts = timeout.HasValue ?
                new CancellationTokenSource(timeout.Value) :
                new CancellationTokenSource(TimeSpan.FromSeconds(30));

            timeoutCts.Token.Register(() => tcs.TrySetCanceled(new CancellationToken(true)));

            try
            {
                await _socket!.EmitAsync(eventName,
                    async serverAckResponse =>
                    {
                        if (timeoutCts.IsCancellationRequested) return;
                        try
                        {
                            _logger.LogTrace("Received ack for event '{EventName}'. Ack data: {AckData}", eventName, serverAckResponse.ToString());
                            var ackData = serverAckResponse.GetValue<TResponse>();
                            if (ackData != null)
                            {
                                await ackCallback(ackData);
                                tcs.TrySetResult(true);
                            }
                            else
                            {
                                _logger.LogWarning("Ack data for event '{EventName}' was null or could not be deserialized to {Type}.", eventName, typeof(TResponse).Name);
                                tcs.TrySetException(new InvalidOperationException("Ack data was null or invalid."));
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger.LogError(jsonEx, "JSON deserialization error processing acknowledgement for event '{EventName}'. Ack data: {AckData}", eventName, serverAckResponse.ToString());
                            tcs.TrySetException(jsonEx);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing acknowledgement callback for event '{EventName}'.", eventName);
                            tcs.TrySetException(ex);
                        }
                    },
                    data);

                await tcs.Task;
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == timeoutCts.Token)
            {
                _logger.LogWarning("Acknowledgement for event '{EventName}' timed out.", eventName);
                throw new TimeoutException($"Acknowledgement for event '{eventName}' timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during EmitWithAckAsync for event '{EventName}'.", eventName);
                throw;
            }
            finally
            {
                timeoutCts.Dispose();
            }
        }

        public async Task DisconnectAsync()
        {
            _logger.LogInformation("DisconnectAsync called.");
            _connectionCts?.Cancel();
            await CleanupSocketAsync(true);
        }

        private async Task CleanupSocketAsync(bool attemptDisconnect)
        {
            if (_socket != null)
            {
                _logger.LogInformation("Cleaning up Socket.IO instance. Attempting disconnect: {AttemptDisconnect}", attemptDisconnect);
                IsAuthenticated = false;

                _socket.Off("agent:ws_auth_success");
                _socket.Off("agent:ws_auth_failed");

                if (attemptDisconnect && _socket.Connected)
                {
                    try
                    {
                        _logger.LogInformation("Attempting to gracefully disconnect socket");
                        await _socket.DisconnectAsync();
                        _logger.LogInformation("Socket disconnected gracefully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Exception during explicit socket disconnect.");
                    }
                }

                // Manually dispose resources instead of using DisposeAsync which might not be available
                _socket = null;
                _logger.LogInformation("Socket.IO instance disposed.");
            }
            else
            {
                _logger.LogInformation("Socket.IO instance was null during cleanup.");
            }

            _connectionCts?.Dispose();
            _connectionCts = null;
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing SocketIOClientWrapper...");
            await DisconnectAsync();

            _eventHandlers.Clear();
            _eventHandlersWithAck.Clear();

            _logger.LogInformation("SocketIOClientWrapper disposed.");
            GC.SuppressFinalize(this);
        }
    }
}
