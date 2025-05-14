using System;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Configuration;
using CMSAgent.Communication;
using CMSAgent.Monitoring;
using CMSAgent.Commands;
using CMSAgent.Update;
using CMSAgent.Persistence;
using CMSAgent.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Core
{
    /// <summary>
    /// Dịch vụ chính điều phối hoạt động của agent, kết nối và quản lý tất cả các module.
    /// </summary>
    public class AgentService : WorkerServiceBase
    {
        private readonly StateManager _stateManager;
        private readonly ConfigLoader _configLoader;
        private readonly WebSocketConnector _webSocketConnector;
        private readonly SystemMonitor _systemMonitor;
        private readonly CommandExecutor _commandExecutor;
        private readonly UpdateHandler _updateHandler;
        private readonly SingletonMutex _singletonMutex;
        private readonly OfflineQueueManager _offlineQueueManager;
        private readonly TokenProtector _tokenProtector;
        private readonly AgentSpecificSettingsOptions _agentSettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _applicationLifetime;

        private Timer? _statusReportTimer;
        private Timer? _updateCheckTimer;
        private Timer? _tokenRefreshTimer;
        private DateTime _lastConnectionAttempt;
        private int _connectionRetryCount = 0;
        private CancellationTokenSource? _linkedTokenSource;
        private Task? _commandProcessingTask;
        private Task? _offlineQueueProcessingTask;
        private bool _initialized = false;

        /// <summary>
        /// Khởi tạo một instance mới của AgentService.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="stateManager">Quản lý trạng thái của agent.</param>
        /// <param name="configLoader">Tải và lưu cấu hình agent.</param>
        /// <param name="webSocketConnector">Kết nối WebSocket với server.</param>
        /// <param name="systemMonitor">Giám sát tài nguyên hệ thống.</param>
        /// <param name="commandExecutor">Thực thi các lệnh từ server.</param>
        /// <param name="updateHandler">Xử lý cập nhật agent.</param>
        /// <param name="singletonMutex">Đảm bảo chỉ một instance của agent chạy.</param>
        /// <param name="offlineQueueManager">Quản lý hàng đợi offline.</param>
        /// <param name="tokenProtector">Bảo vệ token agent.</param>
        /// <param name="agentSettings">Cấu hình đặc thù cho agent.</param>
        /// <param name="serviceProvider">Service provider để resolve các dependency.</param>
        /// <param name="applicationLifetime">Quản lý vòng đời của ứng dụng.</param>
        public AgentService(
            ILogger<AgentService> logger,
            StateManager stateManager,
            ConfigLoader configLoader,
            WebSocketConnector webSocketConnector,
            SystemMonitor systemMonitor,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler,
            SingletonMutex singletonMutex,
            OfflineQueueManager offlineQueueManager,
            TokenProtector tokenProtector,
            IOptions<AgentSpecificSettingsOptions> agentSettings,
            IServiceProvider serviceProvider,
            IHostApplicationLifetime applicationLifetime) 
            : base(logger)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _webSocketConnector = webSocketConnector ?? throw new ArgumentNullException(nameof(webSocketConnector));
            _systemMonitor = systemMonitor ?? throw new ArgumentNullException(nameof(systemMonitor));
            _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
            _updateHandler = updateHandler ?? throw new ArgumentNullException(nameof(updateHandler));
            _singletonMutex = singletonMutex ?? throw new ArgumentNullException(nameof(singletonMutex));
            _offlineQueueManager = offlineQueueManager ?? throw new ArgumentNullException(nameof(offlineQueueManager));
            _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));
            _agentSettings = agentSettings?.Value ?? throw new ArgumentNullException(nameof(agentSettings));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));

            // Đăng ký các event handler
            _stateManager.StateChanged += OnStateChanged;
            _webSocketConnector.ConnectionClosed += OnConnectionClosed;
            _webSocketConnector.MessageReceived += OnMessageReceived;
        }

        /// <summary>
        /// Khởi tạo dịch vụ, thiết lập trạng thái ban đầu và kiểm tra xem có thể chạy không.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình khởi tạo.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Đang khởi tạo AgentService...");

            // Kiểm tra xem đây có phải là instance duy nhất không
            if (!_singletonMutex.IsSingleInstance())
            {
                _logger.LogError("Phát hiện một phiên bản khác của agent đang chạy. Thoát ứng dụng...");
                _applicationLifetime.StopApplication();
                return;
            }

            // Khởi tạo bộ giám sát hệ thống
            _logger.LogInformation("Khởi tạo System Monitor...");
            _systemMonitor.Initialize();

            // Tải cấu hình runtime
            var config = await _configLoader.LoadRuntimeConfigAsync();
            if (config == null || string.IsNullOrEmpty(config.AgentId) || string.IsNullOrEmpty(config.AgentTokenEncrypted))
            {
                _logger.LogError("Không tìm thấy cấu hình hợp lệ. Vui lòng chạy lệnh configure trước.");
                _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                _applicationLifetime.StopApplication();
                return;
            }

            _initialized = true;
            _logger.LogInformation("Khởi tạo AgentService hoàn tất.");
        }

        /// <summary>
        /// Thực hiện công việc chính của dịch vụ, kết nối với server và thiết lập các timer định kỳ.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình thực hiện công việc.</returns>
        protected override async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            if (!_initialized)
            {
                _logger.LogWarning("AgentService chưa được khởi tạo đúng cách, bỏ qua tác vụ chính.");
                await Task.Delay(5000, cancellationToken);
                return;
            }

            if (_stateManager.CurrentState == AgentState.CONFIGURATION_ERROR)
            {
                _logger.LogWarning("Agent đang ở trạng thái lỗi cấu hình, bỏ qua tác vụ chính.");
                await Task.Delay(5000, cancellationToken);
                return;
            }

            // Tạo CancellationTokenSource kết hợp với token từ thao tác dừng dịch vụ
            _linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Kết nối tới server
            if (!await ConnectToServerAsync(cancellationToken))
            {
                _logger.LogWarning("Không thể kết nối tới server, sẽ thử lại sau.");
                await Task.Delay(GetRetryDelay(), cancellationToken);
                return;
            }

            _connectionRetryCount = 0;
            _stateManager.SetState(AgentState.CONNECTED);

            try
            {
                // Bắt đầu xử lý hàng đợi lệnh
                _commandProcessingTask = _commandExecutor.StartProcessingAsync(_linkedTokenSource.Token);

                // Bắt đầu xử lý hàng đợi offline
                _offlineQueueProcessingTask = _offlineQueueManager.ProcessQueuesAsync(_linkedTokenSource.Token);

                // Thiết lập các timer
                SetupTimers();

                // Kiểm tra cập nhật lần đầu
                if (_agentSettings.EnableAutoUpdate)
                {
                    await _updateHandler.CheckForUpdateAsync();
                }

                // Duy trì kết nối cho đến khi bị dừng hoặc ngắt kết nối
                while (!cancellationToken.IsCancellationRequested && _webSocketConnector.IsConnected)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            finally
            {
                // Dừng và hủy các timer nếu còn hoạt động
                DisposeTimers();

                // Hủy các task đang chạy
                if (_linkedTokenSource != null && !_linkedTokenSource.IsCancellationRequested)
                {
                    _linkedTokenSource.Cancel();
                    _linkedTokenSource.Dispose();
                    _linkedTokenSource = null;
                }

                // Ngắt kết nối WebSocket
                if (_webSocketConnector.IsConnected)
                {
                    await _webSocketConnector.DisconnectAsync();
                }

                // Thiết lập trạng thái
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
        /// Dọn dẹp tài nguyên khi dịch vụ dừng lại.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình dọn dẹp.</returns>
        protected override async Task CleanupAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Dọn dẹp AgentService...");

            // Hủy đăng ký các event handler
            _stateManager.StateChanged -= OnStateChanged;
            _webSocketConnector.ConnectionClosed -= OnConnectionClosed;
            _webSocketConnector.MessageReceived -= OnMessageReceived;

            // Đảm bảo các timer đã được giải phóng
            DisposeTimers();

            // Đảm bảo token source đã được giải phóng
            if (_linkedTokenSource != null)
            {
                _linkedTokenSource.Dispose();
                _linkedTokenSource = null;
            }

            // Đợi các task đang chạy hoàn thành (tối đa 5 giây)
            if (_commandProcessingTask != null && !_commandProcessingTask.IsCompleted)
            {
                try
                {
                    await Task.WhenAny(_commandProcessingTask, Task.Delay(5000, cancellationToken));
                }
                catch { }
            }

            if (_offlineQueueProcessingTask != null && !_offlineQueueProcessingTask.IsCompleted)
            {
                try
                {
                    await Task.WhenAny(_offlineQueueProcessingTask, Task.Delay(5000, cancellationToken));
                }
                catch { }
            }

            _logger.LogInformation("Dọn dẹp AgentService hoàn tất.");
        }

        /// <summary>
        /// Kết nối tới server thông qua WebSocket.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>True nếu kết nối thành công, ngược lại là False.</returns>
        private async Task<bool> ConnectToServerAsync(CancellationToken cancellationToken)
        {
            if (_webSocketConnector.IsConnected)
            {
                return true;
            }

            // Kiểm tra thời gian giữa các lần thử kết nối
            var now = DateTime.UtcNow;
            if (_lastConnectionAttempt != default && (now - _lastConnectionAttempt).TotalSeconds < GetExponentialBackoffDelay())
            {
                return false;
            }

            _lastConnectionAttempt = now;
            _stateManager.SetState(AgentState.CONNECTING);

            try
            {
                _logger.LogInformation("Đang kết nối tới server... (Lần thử: {Attempt})", _connectionRetryCount + 1);

                // Tải cấu hình runtime
                var config = await _configLoader.LoadRuntimeConfigAsync();
                if (config == null || string.IsNullOrEmpty(config.AgentTokenEncrypted))
                {
                    _logger.LogError("Không tìm thấy token agent trong cấu hình runtime.");
                    _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                    return false;
                }

                // Giải mã token agent
                string agentToken;
                try
                {
                    agentToken = _tokenProtector.DecryptToken(config.AgentTokenEncrypted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể giải mã token agent.");
                    _stateManager.SetState(AgentState.CONFIGURATION_ERROR);
                    return false;
                }

                // Kết nối và xác thực với server
                bool connected = await _webSocketConnector.ConnectAsync(agentToken);
                
                if (connected)
                {
                    _logger.LogInformation("Đã kết nối thành công tới server.");
                    _connectionRetryCount = 0;
                    return true;
                }
                else
                {
                    _logger.LogWarning("Không thể kết nối tới server.");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kết nối tới server.");
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
        /// Thiết lập các timer định kỳ cho việc báo cáo trạng thái, kiểm tra cập nhật và làm mới token.
        /// </summary>
        private void SetupTimers()
        {
            // Timer báo cáo trạng thái
            _statusReportTimer = new Timer(
                async (state) => await SendStatusReportAsync(),
                null,
                TimeSpan.FromSeconds(5), // Chờ 5 giây trước khi gửi báo cáo đầu tiên
                TimeSpan.FromSeconds(_agentSettings.StatusReportIntervalSec));

            // Timer kiểm tra cập nhật
            if (_agentSettings.EnableAutoUpdate)
            {
                _updateCheckTimer = new Timer(
                    async (state) => await _updateHandler.CheckForUpdateAsync(),
                    null,
                    TimeSpan.FromMinutes(10), // Chờ 10 phút trước khi kiểm tra cập nhật lần đầu
                    TimeSpan.FromSeconds(_agentSettings.AutoUpdateIntervalSec));
            }

            // Timer làm mới token
            _tokenRefreshTimer = new Timer(
                async (state) => await RefreshTokenAsync(),
                null,
                TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec / 2), // Làm mới token khi đã qua nửa thời gian hết hạn
                TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec));
        }

        /// <summary>
        /// Giải phóng các timer đã thiết lập.
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
        /// Gửi báo cáo trạng thái tài nguyên hệ thống lên server.
        /// </summary>
        /// <returns>Task đại diện cho tiến trình gửi báo cáo.</returns>
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
                    _logger.LogTrace("Đã gửi báo cáo trạng thái tài nguyên lên server.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi báo cáo trạng thái tài nguyên.");
            }
        }

        /// <summary>
        /// Làm mới token xác thực của agent.
        /// </summary>
        /// <returns>Task đại diện cho tiến trình làm mới token.</returns>
        private async Task RefreshTokenAsync()
        {
            try
            {
                if (!_webSocketConnector.IsConnected)
                {
                    return;
                }

                _logger.LogInformation("Đang làm mới token agent...");
                
                // TODO: Triển khai logic làm mới token khi API được cung cấp
                await Task.Delay(1);
                
                _logger.LogInformation("Đã làm mới token agent thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi làm mới token agent.");
            }
        }

        /// <summary>
        /// Xử lý khi trạng thái agent thay đổi.
        /// </summary>
        /// <param name="oldState">Trạng thái cũ.</param>
        /// <param name="newState">Trạng thái mới.</param>
        private void OnStateChanged(AgentState oldState, AgentState newState)
        {
            // Không cần xử lý gì thêm, các thành phần khác có thể đăng ký sự kiện này để phản ứng
        }

        /// <summary>
        /// Xử lý sự kiện khi kết nối WebSocket bị ngắt.
        /// </summary>
        private void OnConnectionClosed(object? sender, EventArgs e)
        {
            _logger.LogInformation("Xử lý sự kiện ngắt kết nối WebSocket");
            _stateManager.SetState(AgentState.DISCONNECTED);
        }

        /// <summary>
        /// Xử lý sự kiện khi nhận được thông điệp mới từ WebSocket.
        /// </summary>
        private void OnMessageReceived(object? sender, string messageJson)
        {
            _logger.LogDebug("Nhận được thông điệp từ WebSocket: {Message}", messageJson);
        }

        /// <summary>
        /// Tính khoảng thời gian chờ theo thuật toán exponential backoff.
        /// </summary>
        /// <returns>Khoảng thời gian chờ tính bằng giây.</returns>
        private double GetExponentialBackoffDelay()
        {
            // Bắt đầu với thời gian chờ ban đầu được cấu hình
            double delay = _agentSettings.NetworkRetryInitialDelaySec;
            
            // Tăng thời gian chờ theo cấp số nhân, với mức tối đa là 5 phút
            if (_connectionRetryCount > 0)
            {
                delay = Math.Min(
                    _agentSettings.NetworkRetryInitialDelaySec * Math.Pow(2, _connectionRetryCount - 1),
                    300); // Tối đa 5 phút
            }
            
            return delay;
        }

        /// <summary>
        /// Lấy thời gian chờ trước khi thử lại khi có lỗi.
        /// </summary>
        /// <returns>Thời gian chờ.</returns>
        protected override TimeSpan GetRetryDelay()
        {
            return TimeSpan.FromSeconds(GetExponentialBackoffDelay());
        }
    }
}
