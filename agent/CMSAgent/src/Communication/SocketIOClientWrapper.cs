using CMSAgent.Configuration;
using Serilog;
using SocketIOClient;

namespace CMSAgent.Communication
{
    public class SocketIOClientWrapper : ISocketIOClientWrapper
    {
        private readonly SocketIO _socketClient;
        private readonly WebsocketSettings _settings;
        private bool _isDisposed = false;

        public bool IsConnected => _socketClient.Connected;

        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<int>? OnReconnecting;
        public event Action? OnReconnectFailed;
        public event Action<string, string>? OnMessage;

        public SocketIOClientWrapper(string serverUrl, WebsocketSettings settings)
        {
            _settings = settings;

            var uri = new Uri($"{serverUrl.TrimEnd('/')}");
            _socketClient = new SocketIO(uri);

            // Configure socket options
            _socketClient.Options.ReconnectionAttempts = _settings.reconnect_attempts_max ?? int.MaxValue;
            _socketClient.Options.ReconnectionDelay = TimeSpan.FromSeconds(_settings.reconnect_delay_initial_sec);
            _socketClient.Options.ReconnectionDelayMax = TimeSpan.FromSeconds(_settings.reconnect_delay_max_sec);

            // Set up socket events
            _socketClient.OnConnected += (sender, e) =>
            {
                OnConnected?.Invoke();
            };

            _socketClient.OnDisconnected += (sender, e) =>
            {
                OnDisconnected?.Invoke(e);
            };

            _socketClient.OnReconnectAttempt += (sender, attempt) =>
            {
                OnReconnecting?.Invoke(attempt);
            };

            _socketClient.OnReconnectFailed += (sender, e) =>
            {
                OnReconnectFailed?.Invoke();
            };

            // Set up event handlers for specific server events
            // Authentication events
            _socketClient.On("agent:ws_auth_success", response =>
            {
                string jsonData = response.GetValue().ToString() ?? "{}";
                OnMessage?.Invoke("agent:ws_auth_success", jsonData);
            });

            _socketClient.On("agent:ws_auth_failed", response =>
            {
                string jsonData = response.GetValue().ToString() ?? "{}";
                OnMessage?.Invoke("agent:ws_auth_failed", jsonData);
            });

            // Command execution event
            _socketClient.On("command:execute", response =>
            {
                string jsonData = response.GetValue().ToString() ?? "{}";
                OnMessage?.Invoke("command:execute", jsonData);
            });

            // Update notification event
            _socketClient.On("agent:new_version_available", response =>
            {
                string jsonData = response.GetValue().ToString() ?? "{}";
                OnMessage?.Invoke("agent:new_version_available", jsonData);
            });
        }

        public async Task<bool> ConnectAsync(Dictionary<string, string> authData)
        {
            try
            {
                if (_isDisposed)
                {
                    Log.Error("Cannot connect: SocketIOClientWrapper is disposed");
                    return false;
                }

                // Add authentication data
                _socketClient.Options.Auth = authData;

                // Connect to the server
                await _socketClient.ConnectAsync();

                // Give it a moment to check connection status
                await Task.Delay(1000);
                
                return _socketClient.Connected;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error connecting to Socket.IO server: {Message}", ex.Message);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                await _socketClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting from Socket.IO server: {Message}", ex.Message);
            }
        }

        public async Task EmitAsync(string eventName, object data)
        {
            try
            {
                if (_isDisposed || !_socketClient.Connected)
                {
                    Log.Warning("Cannot emit event: socket is disposed or not connected");
                    return;
                }

                await _socketClient.EmitAsync(eventName, data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error emitting Socket.IO event: {Message}", ex.Message);
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _socketClient.Dispose();
                _isDisposed = true;
            }
        }
    }
}