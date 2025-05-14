using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
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
    /// Quản lý kết nối WebSocket (Socket.IO) với server.
    /// </summary>
    public class WebSocketConnector : IWebSocketConnector, IDisposable
    {
        private readonly ILogger<WebSocketConnector> _logger;
        private readonly ConfigLoader _configLoader;
        private readonly TokenProtector _tokenProtector;
        private readonly StateManager _stateManager;
        private readonly CommandExecutor _commandExecutor;
        private readonly UpdateHandler _updateHandler;
        private readonly HttpClientWrapper _httpClient;
        private readonly WebSocketSettingsOptions _settings;
        
        private SocketIOClient.SocketIO? _socket;
        private SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private int _reconnectAttempts = 0;
        private Timer? _reconnectTimer;
        private bool _isReconnecting = false;
        private bool _disposed = false;

        /// <summary>
        /// Sự kiện khi nhận được thông điệp từ WebSocket.
        /// </summary>
        public event EventHandler<string> MessageReceived = delegate { };

        /// <summary>
        /// Sự kiện khi kết nối WebSocket bị ngắt.
        /// </summary>
        public event EventHandler ConnectionClosed = delegate { };

        /// <summary>
        /// Kiểm tra xem WebSocket có đang kết nối không.
        /// </summary>
        public bool IsConnected => _socket?.Connected ?? false;

        /// <summary>
        /// Khởi tạo một instance mới của WebSocketConnector.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="configLoader">ConfigLoader để tải cấu hình.</param>
        /// <param name="tokenProtector">Protector để mã hóa/giải mã token.</param>
        /// <param name="stateManager">StateManager để quản lý trạng thái agent.</param>
        /// <param name="options">Cấu hình WebSocket.</param>
        /// <param name="commandExecutor">CommandExecutor để thực thi lệnh.</param>
        /// <param name="updateHandler">UpdateHandler để xử lý cập nhật.</param>
        /// <param name="httpClient">HttpClient để gửi request HTTP.</param>
        public WebSocketConnector(
            ILogger<WebSocketConnector> logger,
            ConfigLoader configLoader,
            TokenProtector tokenProtector,
            StateManager stateManager,
            IOptions<WebSocketSettingsOptions> options,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler,
            HttpClientWrapper httpClient)
        {
            _logger = logger;
            _configLoader = configLoader;
            _tokenProtector = tokenProtector;
            _stateManager = stateManager;
            _settings = options.Value;
            _commandExecutor = commandExecutor;
            _updateHandler = updateHandler;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Kết nối đến server qua WebSocket và xác thực.
        /// </summary>
        /// <param name="agentToken">Token xác thực của agent.</param>
        /// <returns>True nếu kết nối và xác thực thành công, False nếu thất bại.</returns>
        public async Task<bool> ConnectAsync(string agentToken)
        {
            try
            {
                await _connectionLock.WaitAsync();
                
                if (IsConnected)
                {
                    _logger.LogDebug("WebSocket đã được kết nối, bỏ qua yêu cầu kết nối mới");
                    return true;
                }

                string serverUrl = _configLoader.Settings.ServerUrl;
                string agentId = _configLoader.GetAgentId();
                
                if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(agentId))
                {
                    _logger.LogError("Không thể kết nối WebSocket: Thiếu serverUrl hoặc agentId");
                    return false;
                }

                _logger.LogInformation("Đang kết nối WebSocket đến {ServerUrl}", serverUrl);
                
                _socket = new SocketIOClient.SocketIO(serverUrl, new SocketIOOptions
                {
                    Path = "/socket.io",
                    Reconnection = false, // Chúng ta sẽ tự quản lý việc kết nối lại
                    ExtraHeaders = new Dictionary<string, string>
                    {
                        { HttpHeaders.AgentIdHeader, agentId },
                        { HttpHeaders.AuthorizationHeader, $"{HttpHeaders.BearerPrefix}{agentToken}" },
                        { HttpHeaders.ClientTypeHeader, HttpHeaders.ClientTypeValue }
                    }
                });

                // Đăng ký các sự kiện
                RegisterSocketEvents();
                
                // Kết nối
                await _socket.ConnectAsync();
                
                if (!(_socket?.Connected ?? false))
                {
                    _logger.LogError("Không thể kết nối WebSocket tới {ServerUrl}", serverUrl);
                    return false;
                }

                _logger.LogInformation("Đã kết nối WebSocket tới {ServerUrl}, đang chờ xác thực", serverUrl);
                
                // Socket.IO sẽ gửi sự kiện auth success hoặc failure
                // Chúng ta đợi ít nhất 5 giây để biết kết quả xác thực
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
                    _logger.LogInformation("WebSocket đã được xác thực thành công");
                    // Reset đếm số lần kết nối lại
                    _reconnectAttempts = 0;
                    return true;
                }
                else
                {
                    _logger.LogError("Xác thực WebSocket thất bại");
                    _stateManager.SetState(AgentState.AUTHENTICATION_FAILED);
                    await DisconnectAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kết nối WebSocket");
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Đóng kết nối WebSocket.
        /// </summary>
        /// <returns>Task đại diện cho việc đóng kết nối.</returns>
        public async Task DisconnectAsync()
        {
            try
            {
                await _connectionLock.WaitAsync();
                
                if (_socket != null)
                {
                    if (_socket.Connected)
                    {
                        _logger.LogInformation("Đang đóng kết nối WebSocket");
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
                
                _logger.LogInformation("Đã đóng kết nối WebSocket");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đóng kết nối WebSocket");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Gửi cập nhật trạng thái tài nguyên lên server.
        /// </summary>
        /// <param name="payload">Dữ liệu trạng thái tài nguyên.</param>
        /// <returns>Task đại diện cho việc gửi dữ liệu.</returns>
        public async Task SendStatusUpdateAsync(StatusUpdatePayload payload)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Không thể gửi cập nhật trạng thái: WebSocket không được kết nối");
                return;
            }

            try
            {
                await _socket.EmitAsync(WebSocketEvents.AgentStatusUpdate, payload);
                _logger.LogDebug("Đã gửi cập nhật trạng thái: CPU {CpuUsage:F1}%, RAM {RamUsage:F1}%, Disk {DiskUsage:F1}%",
                    payload.cpuUsage, payload.ramUsage, payload.diskUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi cập nhật trạng thái");
            }
        }

        /// <summary>
        /// Gửi kết quả lệnh đến server.
        /// </summary>
        /// <param name="payload">Payload chứa kết quả lệnh.</param>
        /// <returns>Task với kết quả gửi: true nếu thành công, false nếu thất bại.</returns>
        public async Task<bool> SendCommandResultAsync(CommandResultPayload payload)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Không thể gửi kết quả lệnh: WebSocket không được kết nối");
                return false;
            }

            try
            {
                await _socket.EmitAsync(WebSocketEvents.AgentCommandResult, payload);
                _logger.LogDebug("Đã gửi kết quả lệnh {CommandId}, trạng thái: {Success}",
                    payload.commandId, payload.success ? "Thành công" : "Thất bại");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi kết quả lệnh {CommandId}", payload.commandId);
                return false;
            }
        }

        /// <summary>
        /// Đăng ký các sự kiện WebSocket.
        /// </summary>
        private void RegisterSocketEvents()
        {
            if (_socket == null)
                return;

            // Sự kiện khi kết nối WebSocket thành công
            _socket.OnConnected += (sender, e) =>
            {
                _logger.LogInformation("WebSocket kết nối thành công, đang chờ xác thực");
            };

            // Sự kiện khi kết nối WebSocket bị ngắt
            _socket.OnDisconnected += (sender, e) =>
            {
                _logger.LogWarning("WebSocket bị ngắt kết nối: {Reason}", e);
                
                if (_stateManager.CurrentState == AgentState.CONNECTED)
                {
                    _stateManager.SetState(AgentState.DISCONNECTED);
                }
                
                ConnectionClosed?.Invoke(this, EventArgs.Empty);
                
                // Bắt đầu kết nối lại
                StartReconnectTimer();
            };

            // Sự kiện khi có lỗi WebSocket
            _socket.OnError += (sender, e) =>
            {
                _logger.LogError("Lỗi WebSocket: {Error}", e);
            };

            // Sự kiện khi xác thực WebSocket thành công
            _socket.On(WebSocketEvents.AgentWsAuthSuccess, response =>
            {
                _logger.LogInformation("WebSocket đã được xác thực thành công");
                _stateManager.SetState(AgentState.CONNECTED);
            });

            // Sự kiện khi xác thực WebSocket thất bại
            _socket.On(WebSocketEvents.AgentWsAuthFailed, response =>
            {
                string reason = "Không rõ nguyên nhân";
                try
                {
                    var responseValues = response.GetValue<object[]>();
                    if (responseValues != null && responseValues.Length > 0)
                    {
                        reason = response.GetValue<string>(0) ?? reason;
                    }
                }
                catch { }

                _logger.LogError("Xác thực WebSocket thất bại: {Reason}", reason);
                _stateManager.SetState(AgentState.AUTHENTICATION_FAILED);
                
                // Đóng kết nối
                _ = DisconnectAsync();
            });

            // Sự kiện khi nhận được lệnh cần thực thi
            _socket.On(WebSocketEvents.CommandExecute, async response =>
            {
                try
                {
                    var command = response.GetValue<CommandPayload>();
                    if (command == null)
                    {
                        _logger.LogError("Nhận được lệnh null từ server");
                        return;
                    }

                    _logger.LogInformation("Nhận được lệnh {CommandType} từ server: {CommandId}", command.commandType, command.commandId);

                    MessageReceived?.Invoke(this, $"Nhận được lệnh {command.commandType} từ server: {command.commandId}");

                    // Thêm lệnh vào hàng đợi
                    bool enqueued = _commandExecutor.TryEnqueueCommand(command);
                    if (!enqueued)
                    {
                        _logger.LogError("Không thể thêm lệnh vào hàng đợi: Hàng đợi đã đầy");
                        
                        // Gửi kết quả lỗi về server
                        var result = new CommandResultPayload
                        {
                            commandId = command.commandId,
                            success = false,
                            type = command.commandType,
                            result = new CommandResultData
                            {
                                stdout = string.Empty,
                                stderr = string.Empty,
                                errorMessage = "Không thể xử lý lệnh: Hàng đợi đã đầy",
                                errorCode = string.Empty
                            }
                        };
                        
                        await SendCommandResultAsync(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý lệnh từ server");
                }
            });

            // Sự kiện khi có phiên bản agent mới
            _socket.On(WebSocketEvents.AgentNewVersionAvailable, async response =>
            {
                try
                {
                    var updateInfo = response.GetValue<UpdateCheckResponse>();
                    _logger.LogInformation("Phát hiện phiên bản agent mới: {NewVersion}", updateInfo.version);
                    
                    MessageReceived?.Invoke(this, $"Phát hiện phiên bản agent mới: {updateInfo.version}");
                    
                    // Gửi đến UpdateHandler để xử lý
                    await _updateHandler.ProcessUpdateAsync(updateInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý thông tin cập nhật từ server");
                }
            });
        }

        /// <summary>
        /// Bắt đầu timer để kết nối lại.
        /// </summary>
        private void StartReconnectTimer()
        {
            lock (this)
            {
                if (_isReconnecting || _disposed)
                    return;

                _isReconnecting = true;

                // Tính toán thời gian chờ kết nối lại
                int delay = CalculateReconnectDelay();
                
                _logger.LogInformation("Sẽ thử kết nối lại sau {Delay} giây (lần thử {Attempt})",
                    delay, _reconnectAttempts + 1);

                StopReconnectTimer();
                
                _reconnectTimer = new Timer(ReconnectTimerCallback, null, TimeSpan.FromSeconds(delay), Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Dừng timer kết nối lại.
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
        /// Callback khi timer kết nối lại kích hoạt.
        /// </summary>
        private async void ReconnectTimerCallback(object? state)
        {
            try
            {
                _reconnectAttempts++;
                
                _logger.LogInformation("Đang thử kết nối lại lần thứ {Attempt}", _reconnectAttempts);
                
                // Nếu số lần thử vượt quá giới hạn thì dừng
                if (_settings.ReconnectAttemptsMax.HasValue && _reconnectAttempts > _settings.ReconnectAttemptsMax.Value)
                {
                    _logger.LogError("Đã vượt quá số lần thử kết nối lại tối đa ({MaxAttempts})",
                        _settings.ReconnectAttemptsMax.Value);
                    
                    _isReconnecting = false;
                    return;
                }

                // Lấy token từ cấu hình và giải mã
                string encryptedToken = _configLoader.GetEncryptedAgentToken();
                if (string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogError("Không thể kết nối lại: Thiếu token xác thực");
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
                    _logger.LogError(ex, "Không thể xử lý hàng đợi offline: không thể giải mã token");
                    StartReconnectTimer();
                    return;
                }

                // Thử kết nối lại
                bool connected = await ConnectAsync(token);
                
                if (connected)
                {
                    _logger.LogInformation("Kết nối lại WebSocket thành công");
                    _isReconnecting = false;
                    _reconnectAttempts = 0;
                }
                else
                {
                    _logger.LogWarning("Kết nối lại WebSocket thất bại, sẽ thử lại sau");
                    StartReconnectTimer();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thử kết nối lại WebSocket");
                StartReconnectTimer();
            }
        }

        /// <summary>
        /// Tính toán thời gian chờ kết nối lại dựa trên số lần thử.
        /// </summary>
        private int CalculateReconnectDelay()
        {
            // Tăng thời gian chờ theo cấp số nhân, nhưng có giới hạn tối đa
            int delay = (int)Math.Min(
                _settings.ReconnectDelayMaxSec,
                _settings.ReconnectDelayInitialSec * Math.Pow(1.5, _reconnectAttempts)
            );
            
            return delay;
        }

        /// <summary>
        /// Giải phóng tài nguyên.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Giải phóng tài nguyên.
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
