using System;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using CMSAgent.Common.DTOs;
using CMSAgent.Configuration;
using CMSAgent.Communication;
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
        private readonly TokenProtector _tokenProtector;
        private readonly AgentSpecificSettingsOptions _agentSettings;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly HardwareInfoCollector _hardwareInfoCollector;
        private readonly HttpClientWrapper _httpClient;

        private Timer? _statusReportTimer;
        private Timer? _updateCheckTimer;
        private Timer? _tokenRefreshTimer;
        private DateTime _lastConnectionAttempt;
        private int _connectionRetryCount = 0;
        private CancellationTokenSource? _linkedTokenSource;
        private Task? _commandProcessingTask;
        private bool _initialized = false;

        // Thêm một trường static readonly cho JsonSerializerOptions
        private static readonly JsonSerializerOptions _errorPayloadJsonOptions = new() 
        { 
            PropertyNameCaseInsensitive = true 
        };

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
        /// <param name="tokenProtector">Bảo vệ token agent.</param>
        /// <param name="agentSettings">Cấu hình đặc thù cho agent.</param>
        /// <param name="applicationLifetime">Quản lý vòng đời của ứng dụng.</param>
        /// <param name="hardwareInfoCollector">Thu thập thông tin phần cứng</param>
        /// <param name="httpClient">Kết nối HTTP</param>
        public AgentService(
            ILogger<AgentService> logger,
            StateManager stateManager,
            ConfigLoader configLoader,
            WebSocketConnector webSocketConnector,
            SystemMonitor systemMonitor,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler,
            SingletonMutex singletonMutex,
            TokenProtector tokenProtector,
            IOptions<AgentSpecificSettingsOptions> agentSettings,
            IHostApplicationLifetime applicationLifetime,
            HardwareInfoCollector hardwareInfoCollector,
            HttpClientWrapper httpClient) 
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
            if (!await ConnectToServerAsync())
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

                // Gửi thông tin phần cứng ban đầu
                await SendInitialHardwareInformationAsync();

                // Xử lý và gửi các báo cáo lỗi offline đã lưu
                await ProcessOfflineErrorReportsAsync();

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

            _logger.LogInformation("Dọn dẹp AgentService hoàn tất.");
        }

        /// <summary>
        /// Kết nối tới server thông qua WebSocket.
        /// </summary>
        /// <returns>True nếu kết nối thành công, ngược lại là False.</returns>
        private async Task<bool> ConnectToServerAsync()
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
                    
                    // Nếu là lỗi xác thực, thử làm mới token
                    if (_stateManager.CurrentState == AgentState.AUTHENTICATION_FAILED)
                    {
                        _logger.LogInformation("Xác thực thất bại. Đang thử làm mới token...");
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
        /// Thử định danh lại với server để làm mới token.
        /// </summary>
        /// <param name="forceRenewToken">Buộc làm mới token ngay cả khi token còn hợp lệ.</param>
        /// <returns>True nếu thành công, False nếu thất bại.</returns>
        private async Task<bool> AttemptReIdentifyAndConnectAsync(bool forceRenewToken = false)
        {
            try
            {
                _logger.LogInformation("Đang thử định danh lại với server...");
                
                // Tải cấu hình runtime
                var config = await _configLoader.LoadRuntimeConfigAsync();
                if (config == null || string.IsNullOrEmpty(config.AgentId) || config.RoomConfig == null)
                {
                    _logger.LogError("Không thể định danh lại: Thiếu thông tin cấu hình runtime");
                    return false;
                }
                
                // Chuẩn bị payload cho yêu cầu identify
                var identifyPayload = new AgentIdentifyRequest
                {
                    agentId = config.AgentId,
                    positionInfo = new PositionInfo
                    {
                        roomName = config.RoomConfig.RoomName,
                        posX = config.RoomConfig.PosX,
                        posY = config.RoomConfig.PosY
                    },
                    forceRenewToken = forceRenewToken
                };
                
                // Gửi yêu cầu identify
                var response = await _httpClient.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse>(
                    Common.Constants.ApiRoutes.Identify,
                    identifyPayload,
                    config.AgentId,
                    null);
                
                if (response != null && response.status == "success" && !string.IsNullOrEmpty(response.agentToken))
                {
                    _logger.LogInformation("Đã nhận được token mới từ server");
                    
                    // Mã hóa và lưu token mới
                    string encryptedToken = _tokenProtector.EncryptToken(response.agentToken);
                    config.AgentTokenEncrypted = encryptedToken;
                    await _configLoader.SaveRuntimeConfigAsync(config);
                    
                    // Thử kết nối lại với token mới
                    bool connected = await _webSocketConnector.ConnectAsync(response.agentToken);
                    
                    if (connected)
                    {
                        _logger.LogInformation("Đã kết nối lại thành công với token mới");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Không thể kết nối lại với token mới");
                        return false;
                    }
                }
                else if (response != null)
                {
                    if (response.status == "auth_failure")
                    {
                        _logger.LogWarning("Server không chấp nhận yêu cầu định danh: token không hợp lệ hoặc bị thu hồi");
                        return false;
                    }
                    else if (response.status == "mfa_required")
                    {
                        _logger.LogWarning("Server yêu cầu MFA. Không thể xử lý tự động trong ngữ cảnh service.");
                        return false;
                    }
                    else if (response.status == "position_error")
                    {
                        _logger.LogWarning("Lỗi vị trí: {Message}", response.message);
                        return false;
                    }
                }
                
                _logger.LogWarning("Không thể định danh lại với server: Phản hồi không hợp lệ");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thử định danh lại với server");
                return false;
            }
        }

        /// <summary>
        /// Gửi thông tin phần cứng ban đầu lên server.
        /// </summary>
        /// <returns>Task đại diện cho tiến trình gửi thông tin.</returns>
        private async Task SendInitialHardwareInformationAsync()
        {
            try
            {
                _logger.LogInformation("Đang thu thập thông tin phần cứng để gửi lên server...");
                
                // Thu thập thông tin phần cứng
                var hardwareInfo = await _hardwareInfoCollector.CollectHardwareInfoAsync();
                
                // Tải thông tin đăng nhập
                string agentId = _configLoader.GetAgentId();
                string encryptedToken = _configLoader.GetEncryptedAgentToken();
                
                if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogError("Không thể gửi thông tin phần cứng: Thiếu thông tin xác thực agent");
                    return;
                }
                
                // Gửi thông tin lên server
                await _httpClient.PostAsync<CMSAgent.Common.DTOs.HardwareInfoPayload, object>(
                    Common.Constants.ApiRoutes.HardwareInfo,
                    hardwareInfo,
                    agentId,
                    encryptedToken);
                
                _logger.LogInformation("Đã gửi thông tin phần cứng lên server thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi thông tin phần cứng ban đầu");
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
                _logger.LogDebug("Đang làm mới token agent...");
                
                // Thực hiện làm mới token (đổi token rồi thử định danh lại)
                await AttemptReIdentifyAndConnectAsync(true);
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
            _logger.LogInformation("Trạng thái agent thay đổi: {OldState} -> {NewState}", oldState, newState);
            
            // Xử lý khi trạng thái là AUTHENTICATION_FAILED
            if (newState == AgentState.AUTHENTICATION_FAILED)
            {
                _logger.LogWarning("Phát hiện trạng thái AUTHENTICATION_FAILED, sẽ thử định danh lại với server trong lần kết nối tiếp theo");
            }
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

        /// <summary>
        /// Xử lý và gửi các báo cáo lỗi đã được lưu trữ offline.
        /// </summary>
        private async Task ProcessOfflineErrorReportsAsync()
        {
            _logger.LogInformation("Bắt đầu xử lý các báo cáo lỗi offline...");
            string errorLogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "CMSAgent",
                "error_reports");

            if (!Directory.Exists(errorLogDirectory))
            {
                _logger.LogDebug("Thư mục báo cáo lỗi offline không tồn tại: {ErrorLogDirectory}", errorLogDirectory);
                return;
            }

            var errorFiles = Directory.GetFiles(errorLogDirectory, "*.json");
            if (errorFiles.Length == 0)
            {
                _logger.LogInformation("Không có báo cáo lỗi offline nào cần xử lý.");
                return;
            }

            _logger.LogInformation("Tìm thấy {Count} báo cáo lỗi offline.", errorFiles.Length);

            var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
            if (runtimeConfig == null || string.IsNullOrEmpty(runtimeConfig.AgentId) || string.IsNullOrEmpty(runtimeConfig.AgentTokenEncrypted))
            {
                _logger.LogError("Không thể tải cấu hình runtime hoặc token để gửi báo cáo lỗi offline. Các báo cáo lỗi sẽ được giữ lại.");
                return;
            }

            string? agentToken;
            try
            {
                agentToken = _tokenProtector.DecryptToken(runtimeConfig.AgentTokenEncrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Giải mã agent token thất bại khi xử lý lỗi offline. Các báo cáo lỗi sẽ được giữ lại.");
                return;
            }

            if (string.IsNullOrEmpty(agentToken))
            {
                 _logger.LogError("Agent token không hợp lệ sau khi giải mã khi xử lý lỗi offline. Các báo cáo lỗi sẽ được giữ lại.");
                 return;
            }

            // Sửa cách lấy ServerUrl
            string serverUrl = _configLoader.Settings?.ServerUrl ?? string.Empty;
            if (string.IsNullOrEmpty(serverUrl))
            {
                _logger.LogError("ServerUrl không được cấu hình trong appsettings.json. Không thể gửi báo cáo lỗi offline.");
                return;
            }
            // Sử dụng hằng số từ ApiRoutes
            string reportErrorEndpoint = $"{serverUrl.TrimEnd('/')}{Common.Constants.ApiRoutes.ReportError}";


            foreach (var errorFile in errorFiles)
            {
                try
                {
                    string jsonContent = await File.ReadAllTextAsync(errorFile);
                    var errorPayload = JsonSerializer.Deserialize<ErrorReportPayload>(jsonContent, _errorPayloadJsonOptions);

                    if (errorPayload != null)
                    {
                        _logger.LogDebug("Đang gửi báo cáo lỗi từ file: {ErrorFile} đến endpoint {ReportErrorEndpoint}", errorFile, reportErrorEndpoint);
                        
                        await _httpClient.PostAsync(reportErrorEndpoint, errorPayload, runtimeConfig.AgentId, agentToken);
                        
                        _logger.LogInformation("Đã gửi thành công báo cáo lỗi từ file: {ErrorFile}. Đang xóa file...", errorFile);
                        File.Delete(errorFile);
                    }
                    else
                    {
                        _logger.LogWarning("Không thể deserialize báo cáo lỗi từ file: {ErrorFile}. Có thể file bị hỏng hoặc nội dung không hợp lệ.", errorFile);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Lỗi deserialize JSON từ file báo cáo lỗi offline: {ErrorFile}. File có thể bị hỏng.", errorFile);
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogError(httpEx, "Lỗi HTTP khi gửi báo cáo lỗi từ file {ErrorFile}. Mã trạng thái (nếu có từ inner exception): {StatusCode}. Báo cáo lỗi sẽ được giữ lại.", 
                        errorFile, httpEx.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Đã xảy ra lỗi không mong muốn khi xử lý file báo cáo lỗi offline: {ErrorFile}. Báo cáo lỗi sẽ được giữ lại.", errorFile);
                }
            }
            _logger.LogInformation("Hoàn tất xử lý các báo cáo lỗi offline.");
        }
    }
}
