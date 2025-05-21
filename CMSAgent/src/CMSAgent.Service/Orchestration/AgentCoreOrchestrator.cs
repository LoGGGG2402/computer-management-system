 // CMSAgent.Service/Orchestration/AgentCoreOrchestrator.cs
using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Communication.WebSocket;
using CMSAgent.Service.Configuration.Manager;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Monitoring;
using CMSAgent.Service.Security;
using CMSAgent.Service.Update;
using CMSAgent.Service.Commands;
using CMSAgent.Service.Commands.Models; // For CommandRequest
using CMSAgent.Service.Models; // For UpdateNotification
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Enums;
using CMSAgent.Shared.Models; // For AgentErrorReport
using CMSAgent.Shared.Utils; // For ErrorReportingUtils
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; // For LINQ operations if any

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
        private string? _agentToken; // Token đã giải mã
        private Timer? _periodicUpdateCheckTimer;
        private Timer? _periodicTokenRefreshTimer;
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
            // Có thể gửi trạng thái này lên server nếu cần
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentCoreOrchestrator đang bắt đầu...");
            SetStatus(AgentStatus.Initializing);
            _mainLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // 1. Tải cấu hình runtime và xác thực Agent
                if (!await LoadConfigAndAuthenticateAsync(_mainLoopCts.Token))
                {
                    _logger.LogCritical("Không thể tải cấu hình hoặc xác thực Agent. Orchestrator dừng lại.");
                    SetStatus(AgentStatus.Error, "Initialization failed: Config/Auth error.");
                    _hostApplicationLifetime.StopApplication(); // Dừng toàn bộ service
                    return;
                }

                // 2. Thiết lập các kết nối và sự kiện WebSocket
                SetupWebSocketEventHandlers();
                await ConnectWebSocketAsync(_mainLoopCts.Token); // Cố gắng kết nối lần đầu

                // 3. Bắt đầu các tác vụ nền
                _commandQueue.StartProcessing(_mainLoopCts.Token); // Bắt đầu xử lý hàng đợi lệnh
                
                // Resource Monitor sẽ được bắt đầu sau khi WebSocket kết nối thành công (trong OnSocketConnected)
                // để có thể gửi status update.

                // Bắt đầu các timer định kỳ
                StartPeriodicTasks();


                // Vòng lặp chính của Orchestrator (nếu cần) hoặc chỉ chờ cancellationToken
                _logger.LogInformation("AgentCoreOrchestrator đã khởi động thành công và đang chạy.");
                SetStatus(AgentStatus.Connected, "Orchestrator initialized and connected."); // Giả định kết nối WS thành công ban đầu

                // Giữ cho StartAsync chạy cho đến khi bị hủy
                await Task.Delay(Timeout.Infinite, _mainLoopCts.Token);

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("AgentCoreOrchestrator.StartAsync bị hủy bỏ.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Lỗi không mong muốn trong AgentCoreOrchestrator.StartAsync.");
                SetStatus(AgentStatus.Error, $"Critical error: {ex.Message}");
                _hostApplicationLifetime.StopApplication(); // Dừng service nếu có lỗi nghiêm trọng
            }
            finally
            {
                _logger.LogInformation("AgentCoreOrchestrator.StartAsync kết thúc.");
                await PerformShutdownTasksAsync(CancellationToken.None); // Dọn dẹp khi StartAsync thoát
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentCoreOrchestrator đang dừng...");
            SetStatus(AgentStatus.Stopping);

            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
            {
                _mainLoopCts.Cancel(); // Gửi tín hiệu dừng cho StartAsync và các tác vụ con
            }
            
            await PerformShutdownTasksAsync(cancellationToken);

            _logger.LogInformation("AgentCoreOrchestrator đã dừng.");
            SetStatus(AgentStatus.Stopped);
        }
        
        private async Task PerformShutdownTasksAsync(CancellationToken cancellationToken)
        {
            // Dừng các timer
            _periodicUpdateCheckTimer?.Change(Timeout.Infinite, 0);
            _periodicUpdateCheckTimer?.Dispose();
            _periodicTokenRefreshTimer?.Change(Timeout.Infinite, 0);
            _periodicTokenRefreshTimer?.Dispose();
            _logger.LogDebug("Các timer định kỳ đã được dừng.");

            // Dừng Resource Monitor
            await _resourceMonitor.StopMonitoringAsync();
            _logger.LogDebug("Resource Monitor đã được dừng.");

            // Dừng Command Queue
            await _commandQueue.StopProcessingAsync(); // Đợi các lệnh đang xử lý hoàn thành (nếu có thể)
            _logger.LogDebug("Command Queue đã được dừng.");

            // Ngắt kết nối WebSocket
            if (_socketClient.IsConnected)
            {
                await _socketClient.DisconnectAsync();
            }
            _logger.LogDebug("WebSocket client đã được yêu cầu ngắt kết nối.");
        }


        private async Task<bool> LoadConfigAndAuthenticateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Đang tải cấu hình runtime...");
            var runtimeConfig = await _runtimeConfigManager.LoadConfigAsync();
            if (string.IsNullOrWhiteSpace(runtimeConfig.AgentId) ||
                string.IsNullOrWhiteSpace(runtimeConfig.AgentTokenEncrypted) ||
                runtimeConfig.RoomConfig == null)
            {
                _logger.LogError("Cấu hình AgentId, AgentTokenEncrypted, hoặc RoomConfig không đầy đủ. " +
                               "Vui lòng chạy Agent với tham số 'configure' để thiết lập.");
                return false;
            }

            _agentId = runtimeConfig.AgentId;
            _agentToken = _dpapiProtector.Unprotect(runtimeConfig.AgentTokenEncrypted);

            if (string.IsNullOrWhiteSpace(_agentToken))
            {
                _logger.LogError("Không thể giải mã AgentToken. Token có thể bị hỏng hoặc DPAPI lỗi.");
                return false;
            }

            _logger.LogInformation("Cấu hình runtime đã được tải và token đã được giải mã cho AgentId: {AgentId}", _agentId);
            _apiClient.SetAuthenticationCredentials(_agentId, _agentToken); // Cập nhật creds cho HTTP client
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
                _logger.LogError("Không thể kết nối WebSocket: AgentId hoặc AgentToken chưa được thiết lập.");
                return;
            }

            if (_socketClient.IsConnected)
            {
                _logger.LogInformation("WebSocket đã kết nối. Bỏ qua.");
                return;
            }

            SetStatus(AgentStatus.Connecting, "Attempting WebSocket connection.");
            try
            {
                _logger.LogInformation("Đang kết nối WebSocket...");
                await _socketClient.ConnectAsync(_agentId, _agentToken, cancellationToken);
                // Sự kiện Connected sẽ được kích hoạt nếu thành công
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Nỗ lực kết nối WebSocket bị hủy.");
                SetStatus(AgentStatus.Disconnected, "WebSocket connection canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cố gắng kết nối WebSocket ban đầu.");
                SetStatus(AgentStatus.Disconnected, $"WebSocket connection error: {ex.Message}");
                // Logic retry sẽ được xử lý bởi SocketIOClient library hoặc có thể thêm ở đây nếu cần
            }
        }

        private Task OnSocketConnected()
        {
            _logger.LogInformation("WebSocket đã kết nối và xác thực thành công!");
            SetStatus(AgentStatus.Connected);

            // Thực hiện các tác vụ sau khi kết nối thành công
            _ = Task.Run(async () =>
            {
                try
                {
                    // 1. Gửi thông tin phần cứng (nếu chưa gửi hoặc cần gửi lại)
                    // Cần logic để quyết định khi nào gửi lại hardware info. Ví dụ: chỉ gửi 1 lần sau khi agent khởi động và kết nối.
                    _logger.LogInformation("Thu thập và gửi thông tin phần cứng...");
                    var hardwareInfo = await _hardwareCollector.CollectHardwareInfoAsync();
                    if (hardwareInfo != null)
                    {
                        await _apiClient.ReportHardwareInfoAsync(hardwareInfo);
                    }

                    // 2. Bắt đầu Resource Monitor để gửi status update
                    if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                    {
                         _logger.LogInformation("Bắt đầu Resource Monitor...");
                        await _resourceMonitor.StartMonitoringAsync(
                            _appSettings.StatusReportIntervalSec,
                            async (cpu, ram, disk) => await _socketClient.SendStatusUpdateAsync(cpu, ram, disk),
                            _mainLoopCts.Token
                        );
                    }

                    // 3. Kiểm tra cập nhật ban đầu
                    if (_appSettings.EnableAutoUpdate && _mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                    {
                        _logger.LogInformation("Kiểm tra cập nhật ban đầu...");
                        await _updateManager.UpdateAndInitiateAsync(_appSettings.Version, _mainLoopCts.Token);
                    }
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Lỗi trong tác vụ OnSocketConnected.");
                }
            });
            return Task.CompletedTask;
        }

        private Task OnSocketDisconnected(Exception? ex)
        {
            _logger.LogWarning(ex, "WebSocket bị ngắt kết nối. Lý do: {ExceptionMessage}", ex?.Message ?? "N/A");
            SetStatus(AgentStatus.Disconnected, $"WebSocket disconnected: {ex?.Message ?? "N/A"}");
            // Dừng Resource Monitor nếu đang chạy
            _ = _resourceMonitor.StopMonitoringAsync();

            // Thư viện SocketIOClient có cơ chế tự động kết nối lại.
            // Nếu muốn kiểm soát việc kết nối lại hoàn toàn, cần tắt Reconnection trong SocketIOOptions
            // và triển khai logic retry ở đây.
            // Hiện tại, ta dựa vào thư viện.
            return Task.CompletedTask;
        }

        private async Task OnSocketAuthFailed(string errorMessage)
        {
            _logger.LogError("Xác thực WebSocket thất bại: {ErrorMessage}. Cần làm mới token hoặc cấu hình lại.", errorMessage);
            SetStatus(AgentStatus.Error, $"WebSocket Auth Failed: {errorMessage}");
            // Cố gắng làm mới token
            await RefreshAgentTokenAsync();
            // Sau khi làm mới token, thử kết nối lại WebSocket
            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested && !string.IsNullOrWhiteSpace(_agentToken))
            {
                await ConnectWebSocketAsync(_mainLoopCts.Token);
            }
            else
            {
                _logger.LogError("Không thể kết nối lại WebSocket sau lỗi xác thực do token không hợp lệ hoặc quá trình đã bị hủy.");
            }
        }

        private Task OnCommandReceived(CommandRequest commandRequest)
        {
            _logger.LogInformation("Orchestrator nhận được lệnh: ID={CommandId}, Type={CommandType}", commandRequest.CommandId, commandRequest.CommandType);
            // Đưa lệnh vào hàng đợi để xử lý
            _ = _commandQueue.EnqueueCommandAsync(commandRequest); // Không chờ, để không block luồng của WebSocket
            return Task.CompletedTask;
        }

        private Task OnNewVersionAvailable(UpdateNotification updateNotification)
        {
            _logger.LogInformation("Orchestrator nhận được thông báo phiên bản mới: {Version}", updateNotification.Version);
            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
            {
                 _ = _updateManager.ProcessUpdateNotificationAsync(updateNotification, _mainLoopCts.Token);
            }
            return Task.CompletedTask;
        }


        private void StartPeriodicTasks()
        {
            // 1. Timer kiểm tra cập nhật định kỳ
            if (_appSettings.EnableAutoUpdate && _appSettings.AutoUpdateIntervalSec > 0)
            {
                _logger.LogInformation("Thiết lập timer kiểm tra cập nhật định kỳ mỗi {Interval} giây.", _appSettings.AutoUpdateIntervalSec);
                _periodicUpdateCheckTimer = new Timer(
                    async _ =>
                    {
                        if (_socketClient.IsConnected && !_updateManager.IsUpdateInProgress && _mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                        {
                            _logger.LogInformation("[Timer] Kiểm tra cập nhật...");
                            await _updateManager.UpdateAndInitiateAsync(_appSettings.Version, _mainLoopCts.Token);
                        }
                    },
                    null,
                    TimeSpan.FromSeconds(_appSettings.AutoUpdateIntervalSec), // Delay ban đầu
                    TimeSpan.FromSeconds(_appSettings.AutoUpdateIntervalSec)  // Khoảng thời gian lặp lại
                );
            }

            // 2. Timer làm mới token định kỳ
            if (_appSettings.TokenRefreshIntervalSec > 0)
            {
                 _logger.LogInformation("Thiết lập timer làm mới token định kỳ mỗi {Interval} giây.", _appSettings.TokenRefreshIntervalSec);
                _periodicTokenRefreshTimer = new Timer(
                    async _ => await RefreshAgentTokenAsync(),
                    null,
                    TimeSpan.FromSeconds(_appSettings.TokenRefreshIntervalSec),
                    TimeSpan.FromSeconds(_appSettings.TokenRefreshIntervalSec)
                );
            }
        }

        private async Task RefreshAgentTokenAsync()
        {
            _logger.LogInformation("Đang làm mới Agent Token...");
            if (string.IsNullOrWhiteSpace(_agentId) || _mainLoopCts == null || _mainLoopCts.IsCancellationRequested)
            {
                _logger.LogWarning("Không thể làm mới token: AgentId rỗng hoặc quá trình đã bị hủy.");
                return;
            }

            var currentPosition = await _runtimeConfigManager.GetPositionInfoAsync();
            if (currentPosition == null)
            {
                _logger.LogError("Không thể làm mới token: Không tìm thấy thông tin vị trí hiện tại.");
                return;
            }

            try
            {
                var (status, newToken, errorMessage) = await _apiClient.IdentifyAgentAsync(_agentId, currentPosition, forceRenewToken: true, _mainLoopCts.Token);

                if (status == "success" && !string.IsNullOrWhiteSpace(newToken))
                {
                    _logger.LogInformation("Làm mới Agent Token thành công.");
                    _agentToken = newToken;
                    string encryptedToken = _dpapiProtector.Protect(newToken);
                    if (!string.IsNullOrWhiteSpace(encryptedToken))
                    {
                        await _runtimeConfigManager.UpdateEncryptedAgentTokenAsync(encryptedToken);
                        _apiClient.SetAuthenticationCredentials(_agentId, _agentToken); // Cập nhật cho HTTP client

                        // Nếu WebSocket đang kết nối, ngắt và kết nối lại với token mới
                        if (_socketClient.IsConnected)
                        {
                            _logger.LogInformation("Đang kết nối lại WebSocket với token mới...");
                            await _socketClient.DisconnectAsync(); // Ngắt kết nối hiện tại
                            // ConnectAsync sẽ được gọi lại bởi logic retry hoặc khi Disconnected event được xử lý
                            // Hoặc có thể gọi ConnectAsync trực tiếp ở đây nếu cần
                            if (_mainLoopCts != null && !_mainLoopCts.IsCancellationRequested)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(2), _mainLoopCts.Token); // Đợi chút trước khi kết nối lại
                                await ConnectWebSocketAsync(_mainLoopCts.Token);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogError("Không thể mã hóa token mới.");
                    }
                }
                else if (status == "mfa_required")
                {
                    _logger.LogWarning("Làm mới token yêu cầu MFA. Agent không thể tự động xử lý MFA trong quá trình làm mới token tự động. Cần can thiệp thủ công.");
                    // Gửi báo cáo lỗi về server
                    await _apiClient.ReportErrorAsync(ErrorReportingUtils.CreateErrorReport("TokenRefreshMfaRequired", "Token refresh requires MFA, manual intervention needed.", customDetails: new { AgentId = _agentId }));
                }
                else
                {
                    _logger.LogError("Làm mới Agent Token thất bại. Status: {Status}, Error: {ErrorMessage}", status, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình làm mới Agent Token.");
            }
        }


        public async Task<bool> RunInitialConfigurationAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu quy trình cấu hình ban đầu của Agent...");
            SetStatus(AgentStatus.Configuring);

            // 1. Lấy hoặc tạo AgentId
            string? agentId = await _runtimeConfigManager.GetAgentIdAsync();
            if (string.IsNullOrWhiteSpace(agentId))
            {
                agentId = Guid.NewGuid().ToString();
                await _runtimeConfigManager.UpdateAgentIdAsync(agentId);
                _logger.LogInformation("AgentId mới đã được tạo: {AgentId}", agentId);
            }
            else
            {
                _logger.LogInformation("Sử dụng AgentId hiện có: {AgentId}", agentId);
            }
            _agentId = agentId; // Lưu lại để dùng

            // 2. Yêu cầu người dùng nhập thông tin vị trí
            Console.WriteLine($"--- CMS Agent Configuration ---");
            Console.WriteLine($"Agent ID: {agentId}");
            Console.Write("Nhập tên phòng (Room Name): ");
            string? roomName = Console.ReadLine()?.Trim();
            Console.Write("Nhập tọa độ X (PosX - số nguyên): ");
            string? posXStr = Console.ReadLine()?.Trim();
            Console.Write("Nhập tọa độ Y (PosY - số nguyên): ");
            string? posYStr = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(roomName) || !int.TryParse(posXStr, out int posX) || !int.TryParse(posYStr, out int posY) || posX < 0 || posY < 0)
            {
                _logger.LogError("Thông tin vị trí không hợp lệ.");
                Console.WriteLine("Lỗi: Thông tin vị trí không hợp lệ. Vui lòng nhập đúng định dạng.");
                SetStatus(AgentStatus.Error, "Configuration failed: Invalid position info.");
                return false;
            }
            var positionInfo = new PositionInfo { RoomName = roomName, PosX = posX, PosY = posY };

            // 3. Xác thực với Server (Identify Flow)
            _logger.LogInformation("Đang gửi yêu cầu Identify đến server...");
            var (status, receivedToken, errorMessage) = await _apiClient.IdentifyAgentAsync(agentId, positionInfo, cancellationToken: cancellationToken);

            if (status == "mfa_required")
            {
                _logger.LogInformation("Server yêu cầu MFA.");
                Console.Write("Nhập mã MFA (OTP): ");
                string? mfaCode = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(mfaCode))
                {
                    _logger.LogError("Mã MFA không được nhập.");
                    Console.WriteLine("Lỗi: Mã MFA không được để trống.");
                    SetStatus(AgentStatus.Error, "Configuration failed: MFA code not entered.");
                    return false;
                }

                _logger.LogInformation("Đang gửi yêu cầu VerifyMfa...");
                (status, receivedToken, errorMessage) = await _apiClient.VerifyMfaAsync(agentId, mfaCode, cancellationToken);
            }

            if (status == "success" && !string.IsNullOrWhiteSpace(receivedToken))
            {
                _logger.LogInformation("Xác thực thành công, nhận được AgentToken.");
                string? encryptedToken = _dpapiProtector.Protect(receivedToken);
                if (string.IsNullOrWhiteSpace(encryptedToken))
                {
                    _logger.LogError("Không thể mã hóa AgentToken nhận được.");
                    Console.WriteLine("Lỗi: Không thể bảo vệ token. Vui lòng thử lại.");
                     SetStatus(AgentStatus.Error, "Configuration failed: Token encryption error.");
                    return false;
                }

                await _runtimeConfigManager.UpdateEncryptedAgentTokenAsync(encryptedToken);
                await _runtimeConfigManager.UpdatePositionInfoAsync(positionInfo);
                _logger.LogInformation("Cấu hình Agent thành công. Token và vị trí đã được lưu.");
                Console.WriteLine("Cấu hình Agent thành công!");
                SetStatus(AgentStatus.Stopped, "Configuration successful."); // Hoặc một trạng thái "Configured"
                return true;
            }
            else
            {
                _logger.LogError("Xác thực với server thất bại. Status: {Status}, Error: {ErrorMessage}", status, errorMessage);
                Console.WriteLine($"Lỗi cấu hình: {errorMessage ?? "Không thể xác thực với server."} (Status: {status})");
                SetStatus(AgentStatus.Error, $"Configuration failed: Server auth error - {errorMessage}");
                return false;
            }
        }
    }
}
