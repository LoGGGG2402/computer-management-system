 // CMSAgent.Service/Communication/WebSocket/AgentSocketClient.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Models;
using CMSAgent.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketIOClient; // Giả sử sử dụng thư viện SocketIOClient của Engyte
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Communication.WebSocket
{
    public class AgentSocketClient : IAgentSocketClient
    {
        private readonly ILogger<AgentSocketClient> _logger;
        private readonly AppSettings _appSettings;
        private SocketIO? _socket; // Nullable để có thể khởi tạo lại
        private string _serverUrl = string.Empty;
        private string _currentAgentId = string.Empty;
        private string _currentAgentToken = string.Empty;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _connectCts;

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
                throw new InvalidOperationException("ServerUrl không được cấu hình trong appsettings cho WebSocket.");
            }
            // URL cho Socket.IO thường là base URL của server.
            // Thư viện SocketIOClient sẽ tự thêm "/socket.io/" path.
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
                    _logger.LogInformation("WebSocket đã kết nối. Bỏ qua yêu cầu kết nối lại.");
                    return;
                }

                _currentAgentId = agentId;
                _currentAgentToken = agentToken;
                _connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _logger.LogInformation("Đang khởi tạo kết nối WebSocket đến {ServerUrl} cho AgentId: {AgentId}", _serverUrl, _currentAgentId);

                // Hủy socket cũ nếu có để tránh rò rỉ tài nguyên
                if (_socket != null)
                {
                    await DisposeSocketAsync(_socket);
                }

                _socket = new SocketIO(_serverUrl, new SocketIOOptions
                {
                    Transport = TransportProtocol.WebSocket, // Chỉ sử dụng WebSocket
                    Query = new Dictionary<string, string>
                    {
                        // Một số thư viện Socket.IO có thể cần query params thay vì headers cho handshake ban đầu.
                        // Tuy nhiên, theo API spec, ta dùng headers.
                    },
                    ExtraHeaders = new Dictionary<string, string>
                    {
                        { "X-Client-Type", AgentConstants.HttpClientTypeAgent },
                        { "X-Agent-ID", _currentAgentId },
                        { "Authorization", $"Bearer {_currentAgentToken}" }
                    },
                    Reconnection = true, // Cho phép thư viện tự động kết nối lại
                    ReconnectionAttempts = _appSettings.WebSocketPolicy.MaxReconnectAttempts < 0 ? int.MaxValue : _appSettings.WebSocketPolicy.MaxReconnectAttempts,
                    ReconnectionDelay = TimeSpan.FromSeconds(_appSettings.WebSocketPolicy.ReconnectMinBackoffSeconds),
                    ReconnectionDelayMax = TimeSpan.FromSeconds(_appSettings.WebSocketPolicy.ReconnectMaxBackoffSeconds),
                    ConnectionTimeout = TimeSpan.FromSeconds(_appSettings.WebSocketPolicy.ConnectionTimeoutSeconds)
                });

                SetupEventHandlers();

                try
                {
                    _logger.LogInformation("Đang thực hiện kết nối WebSocket...");
                    await _socket.ConnectAsync().WaitAsync(_connectCts.Token); // Sử dụng WaitAsync với CancellationToken
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Nỗ lực kết nối WebSocket bị hủy.");
                    await HandleDisconnectionAsync(new Exception("Connection attempt canceled."));
                }
                catch (TimeoutException ex)
                {
                     _logger.LogError(ex, "Timeout khi kết nối WebSocket.");
                     await HandleDisconnectionAsync(ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi kết nối WebSocket.");
                    await HandleDisconnectionAsync(ex); // Kích hoạt sự kiện Disconnected
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
                _logger.LogInformation("WebSocket đã kết nối thành công! Socket ID: {SocketId}. Endpoint: {Endpoint}", _socket.Id, _socket.ServerUri);
                // API spec nói server sẽ gửi 'agent:ws_auth_success' hoặc 'agent:ws_auth_failed'
                // Thay vì dựa vào OnConnected trực tiếp, ta nên chờ các sự kiện này.
                // Tuy nhiên, OnConnected của thư viện thường có nghĩa là handshake TCP/WS đã xong.
                // Việc xác thực logic của ứng dụng (agentId, token) sẽ được server phản hồi qua event riêng.
            };

            _socket.OnDisconnected += async (sender, reason) => // reason là chuỗi
            {
                _logger.LogWarning("WebSocket bị ngắt kết nối. Lý do: {Reason}. IsConnected: {IsConnected}", reason, _socket?.Connected);
                // Thư viện có thể tự động thử kết nối lại.
                // Chỉ kích hoạt Disconnected event của chúng ta nếu không phải do ta chủ động ngắt.
                await HandleDisconnectionAsync(new Exception($"Disconnected by server or network issue: {reason}"));
            };

            _socket.OnError += (sender, e) => // e là chuỗi lỗi
            {
                _logger.LogError("Lỗi WebSocket: {ErrorMessage}", e);
                // Cân nhắc có nên kích hoạt Disconnected ở đây không, tùy thuộc vào loại lỗi.
            };

            _socket.OnReconnectAttempt += (sender, attempt) =>
            {
                _logger.LogInformation("Đang thử kết nối lại WebSocket... Lần thử: {Attempt}", attempt);
            };

            _socket.OnReconnected += (sender, e) => // e là số lần thử
            {
                _logger.LogInformation("Đã kết nối lại WebSocket sau {Attempts} lần thử!", e);
                // Cần xác thực lại với server sau khi kết nối lại.
                // Hoặc server tự động xác thực dựa trên headers/query đã lưu.
                // Nếu thư viện không gửi lại headers khi reconnect, ta cần xử lý.
                // Thông thường, thư viện Socket.IO client sẽ cố gắng duy trì session.
            };

            _socket.OnReconnectError += (sender, ex) =>
            {
                _logger.LogError(ex, "Lỗi khi thử kết nối lại WebSocket.");
            };

            _socket.OnReconnectFailed += (sender, e) =>
            {
                _logger.LogError("Thất bại khi kết nối lại WebSocket sau nhiều lần thử.");
                // Lúc này, Disconnected event nên được kích hoạt.
            };

            _socket.OnPing += (sender, e) =>
            {
                _logger.LogTrace("WebSocket Ping nhận được.");
            };
            _socket.OnPong += (sender, e) => // e là TimeSpan (latency)
            {
                _logger.LogTrace("WebSocket Pong nhận được. Latency: {Latency}ms", e.TotalMilliseconds);
            };


            // Lắng nghe các sự kiện từ Server theo API spec
            _socket.On("agent:ws_auth_success", async response =>
            {
                _logger.LogInformation("Xác thực WebSocket thành công từ Server.");
                if (Connected != null) await Connected.Invoke();
            });

            _socket.On("agent:ws_auth_failed", async response =>
            {
                string errorMessage = "Xác thực WebSocket thất bại.";
                try { errorMessage = response.GetValue<string>() ?? errorMessage; } catch { /* ignore */ }
                _logger.LogError("Xác thực WebSocket thất bại từ Server: {ErrorMessage}", errorMessage);
                if (AuthenticationFailed != null) await AuthenticationFailed.Invoke(errorMessage);
                // Cân nhắc ngắt kết nối nếu xác thực thất bại
                await DisconnectAsync();
            });
            
            // API spec cũ có connect_error, nhưng SocketIOClient dùng OnError hoặc các sự kiện ReconnectError/Failed
            // Thay vào đó, ta dựa vào agent:ws_auth_failed

            _socket.On("command:execute", async response =>
            {
                try
                {
                    _logger.LogDebug("Nhận được sự kiện 'command:execute': {ResponseText}", response.ToString());
                    var commandRequest = response.GetValue<CommandRequest>(_jsonSerializerOptions); // Thư viện sẽ deserialize
                    if (commandRequest != null && !string.IsNullOrEmpty(commandRequest.CommandId))
                    {
                        _logger.LogInformation("Nhận được lệnh: Type='{CommandType}', ID='{CommandId}'", commandRequest.CommandType, commandRequest.CommandId);
                        if (CommandReceived != null) await CommandReceived.Invoke(commandRequest);
                    }
                    else
                    {
                        _logger.LogWarning("Không thể parse CommandRequest từ sự kiện 'command:execute'.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý sự kiện 'command:execute'.");
                }
            });

            _socket.On("agent:new_version_available", async response =>
            {
                try
                {
                    _logger.LogDebug("Nhận được sự kiện 'agent:new_version_available': {ResponseText}", response.ToString());
                    var updateNotification = response.GetValue<UpdateNotification>(_jsonSerializerOptions);
                    if (updateNotification != null && !string.IsNullOrEmpty(updateNotification.Version))
                    {
                        _logger.LogInformation("Nhận được thông báo phiên bản mới: {Version}", updateNotification.Version);
                        if (NewVersionAvailableReceived != null) await NewVersionAvailableReceived.Invoke(updateNotification);
                    }
                    else
                    {
                         _logger.LogWarning("Không thể parse UpdateNotification từ sự kiện 'agent:new_version_available'.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý sự kiện 'agent:new_version_available'.");
                }
            });
        }

        private async Task HandleDisconnectionAsync(Exception? ex)
        {
            if (_connectCts != null && !_connectCts.IsCancellationRequested)
            {
                 // Nếu đang trong quá trình ConnectAsync và bị hủy/lỗi, không gọi Disconnected event
                 // vì nó sẽ được xử lý bởi try-catch trong ConnectAsync.
                 // Tuy nhiên, nếu thư viện tự động ngắt kết nối (không phải do ConnectAsync bị hủy),
                 // thì ta cần kích hoạt Disconnected.
            }

            // Chỉ kích hoạt Disconnected nếu không phải là do DisconnectAsync chủ động gọi.
            // Cần một flag để kiểm tra việc này, hoặc dựa vào trạng thái của _socket.
            // Hiện tại, cứ kích hoạt nếu Disconnected event handler được gán.
            if (Disconnected != null)
            {
                await Disconnected.Invoke(ex);
            }
        }


        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync(); // Chờ nếu đang có thao tác kết nối
            try
            {
                if (_socket == null || !IsConnected)
                {
                    _logger.LogInformation("WebSocket chưa kết nối hoặc đã ngắt. Bỏ qua yêu cầu ngắt kết nối.");
                    return;
                }

                _logger.LogInformation("Đang thực hiện ngắt kết nối WebSocket...");
                if (_connectCts != null && !_connectCts.IsCancellationRequested)
                {
                    _connectCts.Cancel(); // Hủy các nỗ lực kết nối đang diễn ra
                }
                await _socket.DisconnectAsync();
                _logger.LogInformation("Đã gửi yêu cầu ngắt kết nối WebSocket.");
                // Sự kiện OnDisconnected của thư viện sẽ được kích hoạt.
                // Ta không gọi HandleDisconnectionAsync ở đây để tránh gọi 2 lần.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện ngắt kết nối WebSocket.");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task SendStatusUpdateAsync(double cpuUsage, double ramUsage, double diskUsage)
        {
            if (!IsConnected || _socket == null)
            {
                _logger.LogWarning("Không thể gửi status update: WebSocket chưa kết nối.");
                return;
            }

            var payload = new { cpuUsage, ramUsage, diskUsage };
            try
            {
                _logger.LogTrace("Đang gửi agent:status_update: CPU={Cpu}%, RAM={Ram}%, Disk={Disk}%", cpuUsage, ramUsage, diskUsage);
                await _socket.EmitAsync("agent:status_update", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi agent:status_update.");
            }
        }

        public async Task SendCommandResultAsync(CommandResult commandResult)
        {
            if (!IsConnected || _socket == null)
            {
                _logger.LogWarning("Không thể gửi command result: WebSocket chưa kết nối. CommandId: {CommandId}", commandResult.CommandId);
                return;
            }

            try
            {
                _logger.LogInformation("Đang gửi agent:command_result cho CommandId: {CommandId}, Success: {Success}", commandResult.CommandId, commandResult.Success);
                await _socket.EmitAsync("agent:command_result", commandResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi agent:command_result cho CommandId: {CommandId}", commandResult.CommandId);
            }
        }

        public async Task SendUpdateStatusAsync(object statusPayload)
        {
            if (!IsConnected || _socket == null)
            {
                _logger.LogWarning("Không thể gửi update status: WebSocket chưa kết nối.");
                return;
            }
            try
            {
                _logger.LogInformation("Đang gửi agent:update_status: {StatusPayload}", JsonSerializer.Serialize(statusPayload, _jsonSerializerOptions));
                await _socket.EmitAsync("agent:update_status", statusPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi agent:update_status.");
            }
        }

        private async Task DisposeSocketAsync(SocketIO? socketToDispose)
        {
            if (socketToDispose != null)
            {
                // Hủy đăng ký tất cả các event handlers để tránh memory leak
                socketToDispose. 각종_Event_Handlers_Off(); // Phương thức giả định, thư viện có thể có cách khác
                
                if (socketToDispose.Connected)
                {
                    try { await socketToDispose.DisconnectAsync(); } catch { /* ignore */ }
                }
                try { socketToDispose.Dispose(); } catch { /* ignore */ }
                _logger.LogDebug("SocketIO instance đã được dispose.");
            }
        }


        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Đang dispose AgentSocketClient...");
            await _connectionLock.WaitAsync();
            try
            {
                 if (_connectCts != null && !_connectCts.IsCancellationRequested)
                {
                    _connectCts.Cancel();
                    _connectCts.Dispose();
                    _connectCts = null;
                }
                await DisposeSocketAsync(_socket);
                _socket = null;
            }
            finally
            {
                _connectionLock.Release();
                _connectionLock.Dispose();
            }
            _logger.LogInformation("AgentSocketClient đã được dispose.");
            GC.SuppressFinalize(this);
        }
    }
}
