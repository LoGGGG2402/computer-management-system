```
agent/
├── .gitignore                # Cấu hình bỏ qua các file/thư mục không cần thiết cho Git
├── README.md                 # Hướng dẫn tổng quan, build, setup môi trường phát triển
├── src/                      # Thư mục chứa mã nguồn chính
│   ├── CMSAgent/             # Dự án chính của Agent (Windows Service & CLI)
│   │   ├── CMSAgent.csproj   # File dự án C# cho CMSAgent
│   │   ├── Program.cs
│   │   │   └── public class Program
│   │   │       // Fields: (Không có field trực tiếp, sử dụng biến cục bộ trong `Main`)
│   │   │       // Methods:
│   │   │       //  - public static async Task<int> Main(string[] args)
│   │   │       //    - Tạo HostBuilder: Host.CreateDefaultBuilder(args)
│   │   │       //    - ConfigureAppConfiguration: Tải appsettings.json, appsettings.{Environment}.json.
│   │   │       //    - ConfigureLogging: Cấu hình Serilog (UseSerilog()), đọc cấu hình từ appsettings.json.
│   │   │       //    - ConfigureServices(IServiceCollection services, IConfiguration configuration):
│   │   │       //      - Đăng ký Options (đọc từ `configuration`):
│   │   │       //        - services.Configure<CmsAgentSettingsOptions>(configuration.GetSection(ConfigurationKeys.CmsAgentSettingsSection));
│   │   │       //        - services.Configure<AgentSpecificSettingsOptions>(configuration.GetSection(ConfigurationKeys.AgentSettingsSection));
│   │   │       //        - services.Configure<HttpClientSettingsOptions>(configuration.GetSection($"{ConfigurationKeys.CmsAgentSettingsSection}:HttpClientSettings"));
│   │   │       //        - services.Configure<WebSocketSettingsOptions>(configuration.GetSection($"{ConfigurationKeys.CmsAgentSettingsSection}:WebSocketSettings"));
│   │   │       //        - services.Configure<CommandExecutorSettingsOptions>(configuration.GetSection($"{ConfigurationKeys.CmsAgentSettingsSection}:CommandExecutorSettings"));
│   │   │       //        - services.Configure<ResourceLimitsOptions>(configuration.GetSection($"{ConfigurationKeys.CmsAgentSettingsSection}:ResourceLimits"));
│   │   │       //        - services.Configure<OfflineQueueSettingsOptions>(configuration.GetSection($"{ConfigurationKeys.AgentSettingsSection}:OfflineQueue"));
│   │   │       //      - Đăng ký các services:
│   │   │       //        - Singleton: IStateManager, ITokenProtector, IConfigLoader, IWebSocketConnector, ISystemMonitor,
│   │   │       //                     IHardwareInfoCollector, IUpdateHandler, ICommandHandlerFactory, ICommandExecutor,
│   │   │       //                     IOfflineQueueManager, IDateTimeProvider.
│   │   │       //        - SingletonMutex: services.AddSingleton<SingletonMutex>(sp => new SingletonMutex(MutexNames.AgentSingleton, sp.GetRequiredService<ILogger<SingletonMutex>>()));
│   │   │       //        - HttpClient: services.AddHttpClient<IHttpClientWrapper, HttpClientWrapper>()
│   │   │       //                        .SetHandlerLifetime(TimeSpan.FromMinutes(5)) // Thời gian sống của HttpMessageHandler
│   │   │       //                        .AddPolicyHandler(GetRetryPolicy()) // Áp dụng Polly retry policy
│   │   │       //                        .AddPolicyHandler(GetCircuitBreakerPolicy()); // Áp dụng Polly circuit breaker
│   │   │       //        - ICommandHandler (Đăng ký cụ thể từng handler):
│   │   │       //          - services.AddTransient<ConsoleCommandHandler>();
│   │   │       //          - services.AddTransient<SystemActionCommandHandler>();
│   │   │       //          - services.AddTransient<GetLogsCommandHandler>();
│   │   │       //        - CLI command classes (Đăng ký là Transient):
│   │   │       //          - services.AddTransient<ConfigureCommand>();
│   │   │       //          - services.AddTransient<StartCommand>();
│   │   │       //          - services.AddTransient<StopCommand>();
│   │   │       //          - services.AddTransient<UninstallCommand>();
│   │   │       //          - services.AddTransient<DebugCommand>();
│   │   │       //        - ServiceUtils: services.AddSingleton<ServiceUtils>(); // Service tiện ích cho SCM
│   │   │       //        - CliHandler: services.AddSingleton<CliHandler>();
│   │   │       //      - Đăng ký AgentService: services.AddHostedService<AgentService>();
│   │   │       //    - Cấu hình Windows Service: .UseWindowsService(options => { options.ServiceName = "CMSAgentService"; }) // Tên service được định nghĩa rõ ràng.
│   │   │       //    - Build Host: var host = builder.Build();
│   │   │       //    - Xử lý lỗi DI nghiêm trọng: Khi `host.Services.GetRequiredService<T>()` thất bại (ví dụ: service core không được đăng ký),
│   │   │       //      InvalidOperationException sẽ được throw, làm ứng dụng dừng lại. Log lỗi này ở mức Critical/Fatal.
│   │   │       //    - Xử lý lệnh CLI:
│   │   │       //      - var cliHandler = host.Services.GetRequiredService<CliHandler>();
│   │   │       //      - int cliExitCode = await cliHandler.HandleAsync(args); // Truyền args, serviceProvider được inject vào CliHandler constructor
│   │   │       //      - if (cliHandler.IsCliCommandExecuted && (args.Length == 0 || (args.Length > 0 && args[0].ToLower() != "debug"))) return cliExitCode; // Nếu lệnh CLI (không phải debug và không phải chạy không tham số) đã thực thi, thoát.
│   │   │       //    - Chạy Host (nếu không có lệnh CLI nào được thực thi, hoặc là lệnh `debug`, hoặc chạy không tham số): await host.RunAsync(); return CliExitCodes.Success;
│   │   │       //  - static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() // Chính sách Retry của Polly cho HttpClient
│   │   │       //    - return HttpPolicyExtensions.HandleTransientHttpError() // Xử lý lỗi HTTP 5xx, 408 (Request Timeout)
│   │   │       //                        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // Xử lý HTTP 429
│   │   │       //                        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Thử lại 3 lần với exponential backoff
│   │   │       //                                           onRetry: (outcome, timespan, retryAttempt, context) => {
│   │   │       //                                               var logger = context.GetLogger(); // Lấy logger từ context
│   │   │       //                                               logger.LogWarning("Delaying for {Delay}ms, then making retry {RetryAttempt} for {RequestUri}", timespan.TotalMilliseconds, retryAttempt, context.RequestMessage.RequestUri);
│   │   │       //                                           });
│   │   │       //  - static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() // Chính sách Circuit Breaker của Polly
│   │   │       //    - return HttpPolicyExtensions.HandleTransientHttpError()
│   │   │       //                        .CircuitBreakerAsync(
│   │   │       //                            handledEventsAllowedBeforeBreaking: 5, // Số lỗi cho phép trước khi mở circuit
│   │   │       //                            durationOfBreak: TimeSpan.FromSeconds(30), // Thời gian mở circuit
│   │   │       //                            onBreak: (result, timespan, context) => { var logger = context.GetLogger(); logger.LogWarning("Circuit breaker opened for {RequestUri}. Breaking for {BreakTime}ms due to {StatusCode}.", context.RequestMessage.RequestUri, timespan.TotalMilliseconds, result.Result?.StatusCode); },
│   │   │       //                            onReset: (context) => { var logger = context.GetLogger(); logger.LogInformation("Circuit breaker reset for {RequestUri}.", context.RequestMessage.RequestUri); },
│   │   │       //                            onHalfOpen: () => { var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole()); var logger = loggerFactory.CreateLogger("Polly"); logger.LogInformation("Circuit breaker half-opened. Next call is a trial."); }
│   │   │       //                        );
│   │   │
│   │   ├── appsettings.json        # Cấu hình mặc định
│   │   ├── appsettings.Development.json # Cấu hình cho môi trường Development
│   │   ├── appsettings.Production.json  # Cấu hình cho môi trường Production
│   │   │
│   │   ├── Core/
│   │   │   ├── AgentService.cs
│   │   │   │   └── public class AgentService : BackgroundService
│   │   │   │       // Fields (private readonly):
│   │   │   │       //  - ILogger<AgentService> _logger;
│   │   │   │       //  - IStateManager _stateManager;
│   │   │   │       //  - IConfigLoader _configLoader;
│   │   │   │       //  - IWebSocketConnector _webSocketConnector;
│   │   │   │       //  - ISystemMonitor _systemMonitor;
│   │   │   │       //  - IHardwareInfoCollector _hardwareInfoCollector;
│   │   │   │       //  - IUpdateHandler _updateHandler;
│   │   │   │       //  - ICommandExecutor _commandExecutor;
│   │   │   │       //  - SingletonMutex _singletonMutex;
│   │   │   │       //  - IOfflineQueueManager _offlineQueueManager;
│   │   │   │       //  - IHttpClientWrapper _httpClientWrapper;
│   │   │   │       //  - ITokenProtector _tokenProtector;
│   │   │   │       //  - AgentSpecificSettingsOptions _agentSettings; // Lấy từ IOptions<AgentSpecificSettingsOptions>
│   │   │   │       //  - Timer _statusReportTimer;
│   │   │   │       //  - Timer _updateCheckTimer;
│   │   │   │       //  - Timer _tokenRefreshTimer;
│   │   │   │       //  - Timer _offlineQueueProcessTimer;
│   │   │   │       //  - string _decryptedAgentToken; // Lưu token đã giải mã
│   │   │   │       //  - IServiceProvider _serviceProvider; // Để WebSocketConnector.TryRefreshTokenAsync có thể resolve AgentService và gọi UpdateDecryptedToken
│   │   │   │       // Methods:
│   │   │   │       //  - public AgentService(ILogger<AgentService> logger, IStateManager stateManager, IConfigLoader configLoader, IWebSocketConnector webSocketConnector, ISystemMonitor systemMonitor, IHardwareInfoCollector hardwareInfoCollector, IUpdateHandler updateHandler, ICommandExecutor commandExecutor, SingletonMutex singletonMutex, IOfflineQueueManager offlineQueueManager, IHttpClientWrapper httpClientWrapper, ITokenProtector tokenProtector, IOptions<AgentSpecificSettingsOptions> agentSettingsOptions, IServiceProvider serviceProvider) // Constructor DI
│   │   │   │       //    - // Gán tất cả các dependency vào các field tương ứng.
│   │   │   │       //    - _serviceProvider = serviceProvider;
│   │   │   │       //    - _agentSettings = agentSettingsOptions.Value;
│   │   │   │       //  - protected override async Task ExecuteAsync(CancellationToken stoppingToken)
│   │   │   │       //    - _logger.LogInformation("CMSAgent Service starting...");
│   │   │   │       //    - try
│   │   │   │       //    - {
│   │   │   │       //    -   _stateManager.SetState(AgentState.INITIALIZING);
│   │   │   │       //    -   if (!_singletonMutex.TryAcquire()) { _logger.LogCritical("Another instance of CMSAgent is already running. Exiting."); return; }
│   │   │   │       //    -   EnsureDataDirectories();
│   │   │   │       //    -   bool configLoaded = await LoadConfigurationAndTokenAsync(stoppingToken);
│   │   │   │       //    -   if (!configLoaded) { _stateManager.SetState(AgentState.ERROR); _logger.LogCritical("Failed to load critical configuration or token. Agent cannot start."); return; }
│   │   │   │       //    -   await InitializeModulesAsync(stoppingToken);
│   │   │   │       //    -   await StartMainLoopAsync(stoppingToken);
│   │   │   │       //    - }
│   │   │   │       //    - catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { _logger.LogInformation("AgentService execution cancelled by request."); }
│   │   │   │       //    - catch (Exception ex) { _logger.LogCritical(ex, "Unhandled critical exception in AgentService ExecuteAsync. Agent will stop."); _stateManager.SetState(AgentState.ERROR); await _offlineQueueManager.EnqueueErrorReportAsync(new ErrorReportPayload { error_type = ErrorType.UNHANDLED_EXCEPTION, error_message = "Critical failure in AgentService.", error_details = ex.ToString(), timestamp = DateTime.UtcNow }); }
│   │   │   │       //    - finally { StopTimers(); _singletonMutex?.Dispose(); _logger.LogInformation("CMSAgent Service stopped."); }
│   │   │   │       //  - private async Task<bool> LoadConfigurationAndTokenAsync(CancellationToken stoppingToken)
│   │   │   │       //    - try {
│   │   │   │       //        var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
│   │   │   │       //        if (runtimeConfig == null || string.IsNullOrEmpty(runtimeConfig.agent_token_encrypted)) { _logger.LogError("Runtime configuration or encrypted token is missing."); return false; }
│   │   │   │       //        _decryptedAgentToken = _tokenProtector.DecryptToken(runtimeConfig.agent_token_encrypted);
│   │   │   │       //        if (string.IsNullOrEmpty(_decryptedAgentToken)) { _logger.LogError("Failed to decrypt agent token."); return false; }
│   │   │   │       //        return true;
│   │   │   │       //      } catch (Exception ex) { _logger.LogError(ex, "Error loading configuration or decrypting token."); return false; }
│   │   │   │       //  - private Task InitializeModulesAsync(CancellationToken stoppingToken) { _systemMonitor.Initialize(); return Task.CompletedTask; }
│   │   │   │       //  - private async Task StartMainLoopAsync(CancellationToken stoppingToken)
│   │   │   │       //    - while (!stoppingToken.IsCancellationRequested)
│   │   │   │       //    - {
│   │   │   │       //    -   var currentState = _stateManager.CurrentState;
│   │   │   │       //    -   if (currentState == AgentState.DISCONNECTED || currentState == AgentState.INITIALIZING || currentState == AgentState.AUTHENTICATING)
│   │   │   │       //    -   {
│   │   │   │       //    -     await EstablishConnectionAsync(stoppingToken);
│   │   │   │       //    -     if (!_webSocketConnector.IsConnected && !stoppingToken.IsCancellationRequested) { await Task.Delay(TimeSpan.FromSeconds(_agentSettings.NetworkRetryInitialDelaySec), stoppingToken); }
│   │   │   │       //    -   }
│   │   │   │       //    -   else if (currentState == AgentState.CONNECTED)
│   │   │   │       //    -   {
│   │   │   │       //    -     StartTimersIfNeeded(stoppingToken);
│   │   │   │       //    -     await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
│   │   │   │       //    -   }
│   │   │   │       //    -   else if (currentState == AgentState.ERROR || currentState == AgentState.STOPPING) { _logger.LogInformation("Agent in {State} state. Main loop will exit.", currentState); break; }
│   │   │   │       //    -   else { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); } // Trạng thái UPDATING
│   │   │   │       //    - }
│   │   │   │       //  - private async Task EstablishConnectionAsync(CancellationToken stoppingToken)
│   │   │   │       //    - try {
│   │   │   │       //        _stateManager.SetState(AgentState.AUTHENTICATING);
│   │   │   │       //        bool connected = await _webSocketConnector.ConnectAsync(_decryptedAgentToken);
│   │   │   │       //        if (connected) {
│   │   │   │       //            _stateManager.SetState(AgentState.CONNECTED);
│   │   │   │       //            await SendInitialHardwareInfoAsync(stoppingToken);
│   │   │   │       //            await _commandExecutor.StartProcessingAsync(stoppingToken);
│   │   │   │       //        } else if (!stoppingToken.IsCancellationRequested) { _stateManager.SetState(AgentState.DISCONNECTED); }
│   │   │   │       //      } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { _logger.LogInformation("Connection establishment cancelled by agent stopping."); }
│   │   │   │       //      catch (Exception ex) { _logger.LogError(ex, "Error establishing WebSocket connection."); _stateManager.SetState(AgentState.DISCONNECTED); }
│   │   │   │       //  - private async Task SendInitialHardwareInfoAsync(CancellationToken stoppingToken)
│   │   │   │       //    - try {
│   │   │   │       //        var hwInfo = await _hardwareInfoCollector.CollectHardwareInfoAsync();
│   │   │   │       //        var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
│   │   │   │       //        if (runtimeConfig == null) { _logger.LogError("Cannot send hardware info: runtime config is missing."); return; }
│   │   │   │       //        await _httpClientWrapper.PostAsync(ApiRoutes.HardwareInfo, hwInfo, runtimeConfig.agentId, _decryptedAgentToken);
│   │   │   │       //      } catch (Exception ex) { _logger.LogError(ex, "Failed to send initial hardware info."); await _offlineQueueManager.EnqueueErrorReportAsync(new ErrorReportPayload { error_type = ErrorType.HARDWARE_INFO_COLLECTION_FAILED, error_message = ex.Message, error_details = ex.ToString(), timestamp = DateTime.UtcNow }); }
│   │   │   │       //  - public void UpdateDecryptedToken(string newDecryptedToken) { _decryptedAgentToken = newDecryptedToken; _logger.LogInformation("Internal decrypted token updated."); }
│   │   │   │       //  - private void StartTimersIfNeeded(CancellationToken stoppingToken)
│   │   │   │       //    - if (_statusReportTimer == null) { _statusReportTimer = new Timer(StatusReportTimerCallback, stoppingToken, TimeSpan.Zero, TimeSpan.FromSeconds(_agentSettings.StatusReportIntervalSec)); }
│   │   │   │       //    - if (_updateCheckTimer == null && _agentSettings.EnableAutoUpdate) { _updateCheckTimer = new Timer(UpdateCheckTimerCallback, stoppingToken, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(_agentSettings.AutoUpdateIntervalSec)); } // Chờ 1 phút sau khi kết nối rồi mới check update
│   │   │   │       //    - if (_tokenRefreshTimer == null) { _tokenRefreshTimer = new Timer(TokenRefreshTimerCallback, stoppingToken, TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec), TimeSpan.FromSeconds(_agentSettings.TokenRefreshIntervalSec)); }
│   │   │   │       //    - if (_offlineQueueProcessTimer == null) { _offlineQueueProcessTimer = new Timer(OfflineQueueProcessTimerCallback, stoppingToken, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)); } // Xử lý queue sau 1 phút, rồi mỗi 5 phút
│   │   │   │       //  - private async void StatusReportTimerCallback(object tokenState)
│   │   │   │       //    - var stoppingToken = (CancellationToken)tokenState;
│   │   │   │       //    - if (stoppingToken.IsCancellationRequested) return;
│   │   │   │       //    - try {
│   │   │   │       //        var status = await _systemMonitor.GetCurrentStatusAsync();
│   │   │   │       //        if (_webSocketConnector.IsConnected) { await _webSocketConnector.SendStatusUpdateAsync(status); }
│   │   │   │       //        else { await _offlineQueueManager.EnqueueStatusReportAsync(status); }
│   │   │   │       //      } catch (Exception ex) { _logger.LogError(ex, "Error in StatusReportTimerCallback."); }
│   │   │   │       //  - private async void UpdateCheckTimerCallback(object tokenState)
│   │   │   │       //    - var stoppingToken = (CancellationToken)tokenState;
│   │   │   │       //    - if (stoppingToken.IsCancellationRequested || _stateManager.CurrentState != AgentState.CONNECTED) return;
│   │   │   │       //    - try { await _updateHandler.CheckForUpdateAsync(); } catch (Exception ex) { _logger.LogError(ex, "Error in UpdateCheckTimerCallback."); }
│   │   │   │       //  - private async void TokenRefreshTimerCallback(object tokenState)
│   │   │   │       //    - var stoppingToken = (CancellationToken)tokenState;
│   │   │   │       //    - if (stoppingToken.IsCancellationRequested || _stateManager.CurrentState != AgentState.CONNECTED) return;
│   │   │   │       //    - try {
│   │   │   │       //        var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync(true);
│   │   │   │       //        if (runtimeConfig == null) { _logger.LogWarning("Cannot refresh token via timer: runtime config is null."); return; }
│   │   │   │       //        var request = new AgentIdentifyRequest { agentId = runtimeConfig.agentId, positionInfo = new PositionInfo { roomName = runtimeConfig.room_config.roomName, posX = runtimeConfig.room_config.posX, posY = runtimeConfig.room_config.posY }, forceRenewToken = true };
│   │   │   │       //        var identifyResponse = await _httpClientWrapper.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse>(ApiRoutes.Identify, request, runtimeConfig.agentId, null);
│   │   │   │       //        if (identifyResponse?.status == "success" && !string.IsNullOrEmpty(identifyResponse.agentToken)) {
│   │   │   │       //            runtimeConfig.agent_token_encrypted = _tokenProtector.EncryptToken(identifyResponse.agentToken);
│   │   │   │       //            await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
│   │   │   │       //            UpdateDecryptedToken(identifyResponse.agentToken); // Cập nhật token nội bộ
│   │   │   │       //            _logger.LogInformation("Agent token refreshed successfully via timer.");
│   │   │   │       //        } else { _logger.LogWarning("Failed to refresh token via timer. Status: {Status}", identifyResponse?.status); }
│   │   │   │       //      } catch (Exception ex) { _logger.LogError(ex, "Error in TokenRefreshTimerCallback."); }
│   │   │   │       //  - private async void OfflineQueueProcessTimerCallback(object tokenState)
│   │   │   │       //    - var stoppingToken = (CancellationToken)tokenState;
│   │   │   │       //    - if (stoppingToken.IsCancellationRequested || _stateManager.CurrentState != AgentState.CONNECTED) return;
│   │   │   │       //    - try { await _offlineQueueManager.ProcessQueuesAsync(stoppingToken); } catch (Exception ex) { _logger.LogError(ex, "Error in OfflineQueueProcessTimerCallback."); }
│   │   │   │       //  - private void StopTimers() { _statusReportTimer?.Dispose(); _updateCheckTimer?.Dispose(); _tokenRefreshTimer?.Dispose(); _offlineQueueProcessTimer?.Dispose(); _statusReportTimer = null; _updateCheckTimer = null; _tokenRefreshTimer = null; _offlineQueueProcessTimer = null; }
│   │   │   │       //  - public override async Task StopAsync(CancellationToken cancellationToken) { _logger.LogInformation("AgentService StopAsync called."); StopTimers(); await _webSocketConnector.DisconnectAsync(); await base.StopAsync(cancellationToken); }
│   │   │   │       //  - private void EnsureDataDirectories() { try { Directory.CreateDirectory(_configLoader.GetDataPath()); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "logs")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "runtime_config")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "updates", "download")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "updates", "extracted")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "updates", "backup")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "error_reports")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "offline_queue", "status_reports")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "offline_queue", "command_results")); Directory.CreateDirectory(Path.Combine(_configLoader.GetDataPath(), "offline_queue", "error_reports")); } catch (Exception ex) { _logger.LogError(ex, "Failed to ensure data directories."); } }
│   │   │
│   │   │   ├── StateManager.cs
│   │   │   │   └── public class StateManager : IStateManager
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly object _lock = new object();
│   │   │   │       //  - private AgentState _currentState = AgentState.INITIALIZING;
│   │   │   │       //  - private readonly ILogger<StateManager> _logger;
│   │   │   │       // Events:
│   │   │   │       //  - public event Action<AgentState, AgentState> StateChanged;
│   │   │   │       // Properties:
│   │   │   │       //  - public AgentState CurrentState { get { lock (_lock) return _currentState; } }
│   │   │   │       // Methods:
│   │   │   │       //  - public StateManager(ILogger<StateManager> logger) { _logger = logger; }
│   │   │   │       //  - public void SetState(AgentState newState)
│   │   │   │       //    - AgentState oldState;
│   │   │   │       //    - lock (_lock) { if (_currentState == newState) return; oldState = _currentState; _currentState = newState; }
│   │   │   │       //    - _logger.LogInformation("Agent state changed from {OldState} to {NewState}", oldState, newState);
│   │   │   │       //    - StateChanged?.Invoke(oldState, newState);
│   │   │
│   │   │   ├── SingletonMutex.cs
│   │   │   │   └── public class SingletonMutex : IDisposable
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly ILogger<SingletonMutex> _logger;
│   │   │   │       //  - private readonly string _mutexName;
│   │   │   │       //  - private Mutex _mutex;
│   │   │   │       //  - private bool _hasHandle = false;
│   │   │   │       // Methods:
│   │   │   │       //  - public SingletonMutex(string mutexName, ILogger<SingletonMutex> logger) { _mutexName = mutexName; _logger = logger; }
│   │   │   │       //  - public bool TryAcquire()
│   │   │   │       //    - _mutex = new Mutex(true, _mutexName, out bool createdNew);
│   │   │   │       //    - _hasHandle = createdNew;
│   │   │   │       //    - if (!_hasHandle) { _logger.LogError("Another instance with Mutex '{MutexName}' is already running.", _mutexName); _mutex.Dispose(); _mutex = null; }
│   │   │   │       //    - else { _logger.LogInformation("Successfully acquired Mutex '{MutexName}'.", _mutexName); }
│   │   │   │       //    - return _hasHandle;
│   │   │   │       //  - public void Dispose() { if (_hasHandle && _mutex != null) { _mutex.ReleaseMutex(); _logger.LogInformation("Mutex '{MutexName}' released.", _mutexName); } _mutex?.Dispose(); }
│   │   │
│   │   ├── Communication/
│   │   │   ├── HttpClientWrapper.cs
│   │   │   │   └── public class HttpClientWrapper : IHttpClientWrapper
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly HttpClient _httpClient;
│   │   │   │       //  - private readonly ILogger<HttpClientWrapper> _logger;
│   │   │   │       // Methods:
│   │   │   │       //  - public HttpClientWrapper(HttpClient httpClient, ILogger<HttpClientWrapper> logger) { _httpClient = httpClient; _logger = logger; }
│   │   │   │       //  - public async Task<TResponse> GetAsync<TResponse>(string endpoint, string agentId, string token, Dictionary<string, string> queryParams = null)
│   │   │   │       //    - var request = CreateRequestMessage(HttpMethod.Get, endpoint + BuildQueryString(queryParams), agentId, token);
│   │   │   │       //    - var response = await SendWithRetryAsync(async () => request, CancellationToken.None);
│   │   │   │       //    - return await response.Content.ReadFromJsonAsync<TResponse>();
│   │   │   │       //  - public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, string agentId, string token)
│   │   │   │       //    - var request = CreateRequestMessage(HttpMethod.Post, endpoint, agentId, token, JsonContent.Create(payload));
│   │   │   │       //    - var response = await SendWithRetryAsync(async () => request, CancellationToken.None);
│   │   │   │       //    - return await response.Content.ReadFromJsonAsync<TResponse>();
│   │   │   │       //  - public async Task PostAsync<TRequest>(string endpoint, TRequest payload, string agentId, string token)
│   │   │   │       //    - var request = CreateRequestMessage(HttpMethod.Post, endpoint, agentId, token, JsonContent.Create(payload));
│   │   │   │       //    - await SendWithRetryAsync(async () => request, CancellationToken.None);
│   │   │   │       //  - public async Task<Stream> DownloadFileAsync(string endpoint, string agentId, string token)
│   │   │   │       //    - var request = CreateRequestMessage(HttpMethod.Get, endpoint, agentId, token);
│   │   │   │       //    - var response = await SendWithRetryAsync(async () => request, CancellationToken.None);
│   │   │   │       //    - return await response.Content.ReadAsStreamAsync();
│   │   │   │       //  - private HttpRequestMessage CreateRequestMessage(HttpMethod method, string relativeUrl, string agentId, string token, HttpContent content = null)
│   │   │   │       //    - var request = new HttpRequestMessage(method, relativeUrl);
│   │   │   │       //    - if (!string.IsNullOrEmpty(agentId)) request.Headers.Add(HttpHeaders.AgentIdHeader, agentId);
│   │   │   │       //    - if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(HttpHeaders.BearerPrefix.Trim(), token);
│   │   │   │       //    - request.Headers.Add(HttpHeaders.ClientTypeHeader, HttpHeaders.ClientTypeValue);
│   │   │   │       //    - if (content != null) request.Content = content;
│   │   │   │       //    - return request;
│   │   │   │       //  - private string BuildQueryString(Dictionary<string, string> queryParams)
│   │   │   │       //    - if (queryParams == null || !queryParams.Any()) return string.Empty;
│   │   │   │       //    - var builder = new System.Text.StringBuilder("?");
│   │   │   │       //    - foreach (var pair in queryParams) { builder.Append($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}&"); }
│   │   │   │       //    - return builder.ToString().TrimEnd('&');
│   │   │   │       //  - private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpRequestMessage>> requestFactoryAsync, CancellationToken cancellationToken)
│   │   │   │       //    - var request = await requestFactoryAsync();
│   │   │   │       //    - _logger.LogDebug("Sending HTTP {Method} request to {Uri}", request.Method, request.RequestUri);
│   │   │   │       //    - var response = await _httpClient.SendAsync(request, cancellationToken);
│   │   │   │       //    - if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) { _logger.LogWarning("HTTP request to {Uri} failed with 401 Unauthorized.", request.RequestUri); throw new UnauthorizedAccessException($"Token is invalid or expired for {request.RequestUri}. Server responded with 401."); }
│   │   │   │       //    - if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) { _logger.LogWarning("HTTP request to {Uri} failed with 403 Forbidden.", request.RequestUri); throw new HttpRequestException($"Forbidden for {request.RequestUri}. Server responded with 403.", null, System.Net.HttpStatusCode.Forbidden); }
│   │   │   │       //    - response.EnsureSuccessStatusCode(); // Polly handles retries for transient errors. This throws for non-transient errors after retries.
│   │   │   │       //    - _logger.LogDebug("HTTP {Method} request to {Uri} completed with status {StatusCode}", request.Method, request.RequestUri, response.StatusCode);
│   │   │   │       //    - return response;
│   │   │
│   │   │   ├── WebSocketConnector.cs
│   │   │   │   └── public class WebSocketConnector : IWebSocketConnector, IDisposable
│   │   │   │       // Fields:
│   │   │   │       //  - private SocketIOClient.SocketIO _socket;
│   │   │   │       //  - private readonly ILogger<WebSocketConnector> _logger;
│   │   │   │       //  - private readonly IConfigLoader _configLoader;
│   │   │   │       //  - private readonly ITokenProtector _tokenProtector;
│   │   │   │       //  - private readonly IStateManager _stateManager;
│   │   │   │       //  - private readonly ICommandExecutor _commandExecutor;
│   │   │   │       //  - private readonly IUpdateHandler _updateHandler;
│   │   │   │       //  - private readonly WebSocketSettingsOptions _wsSettings;
│   │   │   │       //  - private readonly IHttpClientWrapper _httpClientWrapper;
│   │   │   │       //  - private readonly IServiceProvider _serviceProvider;
│   │   │   │       //  - private bool _isDisposed = false;
│   │   │   │       //  - private TaskCompletionSource<bool> _connectionTcs;
│   │   │   │       // Properties:
│   │   │   │       //  - public bool IsConnected => _socket?.Connected ?? false;
│   │   │   │       // Methods:
│   │   │   │       //  - public WebSocketConnector(ILogger<WebSocketConnector> logger, IStateManager stateManager, ICommandExecutor commandExecutor, IUpdateHandler updateHandler, IOptions<WebSocketSettingsOptions> wsOptions, IHttpClientWrapper httpClientWrapper, IConfigLoader configLoader, ITokenProtector tokenProtector, IServiceProvider serviceProvider)
│   │   │   │       //    - // Gán dependencies
│   │   │   │       //    - _wsSettings = wsOptions.Value;
│   │   │   │       //  - public async Task<bool> ConnectAsync(string agentToken)
│   │   │   │       //    - if (IsConnected || _isDisposed) { _logger.LogDebug("ConnectAsync called but already connected or disposed."); return IsConnected; }
│   │   │   │       //    - _connectionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
│   │   │   │       //    - var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
│   │   │   │       //    - if (runtimeConfig == null) { _logger.LogError("Cannot connect WebSocket: runtime config is missing."); _connectionTcs.TrySetResult(false); return false; }
│   │   │   │       //    - var agentId = runtimeConfig.agentId;
│   │   │   │       //    - var serverUrl = _configLoader.Settings.ServerUrl;
│   │   │   │       //    - var options = new SocketIOClient.SocketIOOptions { /* ... cấu hình headers, reconnection ... */ };
│   │   │   │       //    - _socket = new SocketIOClient.SocketIO(serverUrl, options);
│   │   │   │       //    - RegisterSocketEventHandlers();
│   │   │   │       //    - try { await _socket.ConnectAsync(); } catch (Exception ex) { _logger.LogError(ex, "Exception during WebSocket ConnectAsync call."); _connectionTcs.TrySetResult(false); return false; }
│   │   │   │       //    - return await _connectionTcs.Task;
│   │   │   │       //  - public async Task DisconnectAsync() { if (IsConnected && !_isDisposed) { await _socket.DisconnectAsync(); } }
│   │   │   │       //  - public async Task SendStatusUpdateAsync(StatusUpdatePayload payload) => await EmitAsync(WebSocketEvents.AgentStatusUpdate, payload);
│   │   │   │       //  - public async Task SendCommandResultAsync(CommandResultPayload payload) => await EmitAsync(WebSocketEvents.AgentCommandResult, payload);
│   │   │   │       //  - public async Task SendUpdateStatusAsync(UpdateStatusPayload payload) => await EmitAsync(WebSocketEvents.AgentUpdateStatus, payload);
│   │   │   │       //  - private async Task EmitAsync(string eventName, params object[] data) { if (IsConnected) { try { await _socket.EmitAsync(eventName, data); } catch (Exception ex) { _logger.LogError(ex, "Error emitting WebSocket event {EventName}", eventName); } } else { _logger.LogWarning("Cannot emit WebSocket event {EventName}, not connected.", eventName); } }
│   │   │   │       //  - private void RegisterSocketEventHandlers() { /* ... Đăng ký OnConnected, OnDisconnected, OnError, OnReconnectAttempt, On(AgentWsAuthSuccess), On(AgentWsAuthFailed), On(CommandExecute), On(AgentNewVersionAvailable) ... */ }
│   │   │   │       //  - private void HandleOnConnected(object sender, EventArgs e) { _logger.LogInformation("WebSocket connected. Awaiting authentication..."); /* Không set _connectionTcs ở đây */ }
│   │   │   │       //  - private void HandleOnDisconnected(object sender, string reason) { _logger.LogWarning("WebSocket disconnected: {Reason}", reason); _stateManager.SetState(AgentState.DISCONNECTED); _connectionTcs?.TrySetResult(false); }
│   │   │   │       //  - private void HandleOnError(object sender, string error) { _logger.LogError("WebSocket error: {Error}", error); _connectionTcs?.TrySetResult(false); } // Thường lỗi này sẽ dẫn đến Disconnected
│   │   │   │       //  - private void HandleOnReconnectAttempt(object sender, int attempt) { _logger.LogInformation("WebSocket reconnect attempt {Attempt}", attempt); _stateManager.SetState(AgentState.AUTHENTICATING); }
│   │   │   │       //  - private void HandleWsAuthSuccess(SocketIOResponse response) { _logger.LogInformation("WebSocket authenticated successfully."); _connectionTcs?.TrySetResult(true); }
│   │   │   │       //  - private async void HandleWsAuthFailed(SocketIOResponse response) { /* ... như đã mô tả trước ... */ }
│   │   │   │       //  - private void HandleCommandExecute(SocketIOResponse response) { var command = response.GetValue<CommandPayload>(); _commandExecutor.TryEnqueueCommand(command); }
│   │   │   │       //  - private void HandleNewVersionAvailable(SocketIOResponse response) { var updateInfo = response.GetValue<UpdateCheckResponse>(); _updateHandler.ProcessUpdateAsync(updateInfo); }
│   │   │   │       //  - private async Task<bool> TryRefreshTokenAsync() { /* ... như đã mô tả trước ... */ return false;}
│   │   │   │       //  - public void Dispose() { if (!_isDisposed) { _socket?.Dispose(); _isDisposed = true; } }
│   │   │
│   │   ├── Configuration/
│   │   │   ├── ConfigLoader.cs
│   │   │   │   └── public class ConfigLoader : IConfigLoader
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly ILogger<ConfigLoader> _logger;
│   │   │   │       //  - private readonly IOptionsMonitor<CmsAgentSettingsOptions> _settingsMonitor;
│   │   │   │       //  - private RuntimeConfig _runtimeConfigCache;
│   │   │   │       //  - private readonly string _runtimeConfigPath;
│   │   │   │       //  - private readonly string _installPath;
│   │   │   │       //  - private readonly string _dataPath;
│   │   │   │       //  - private readonly string _runtimeConfigFileName = "runtime_config.json";
│   │   │   │       // Properties:
│   │   │   │       //  - public CmsAgentSettingsOptions Settings => _settingsMonitor.CurrentValue;
│   │   │   │       //  - public AgentSpecificSettingsOptions AgentSettings => _settingsMonitor.CurrentValue.AgentSettings; // Truy cập trực tiếp từ CmsAgentSettingsOptions
│   │   │   │       // Methods:
│   │   │   │       //  - public ConfigLoader(ILogger<ConfigLoader> logger, IOptionsMonitor<CmsAgentSettingsOptions> settingsMonitor)
│   │   │   │       //    - _logger = logger; _settingsMonitor = settingsMonitor;
│   │   │   │       //    - _installPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
│   │   │   │       //    - _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Settings.AppName ?? "CMSAgent");
│   │   │   │       //    - _runtimeConfigPath = Path.Combine(_dataPath, "runtime_config", _runtimeConfigFileName);
│   │   │   │       //  - public async Task<RuntimeConfig> LoadRuntimeConfigAsync(bool forceReload = false)
│   │   │   │       //    - if (_runtimeConfigCache != null && !forceReload) return _runtimeConfigCache;
│   │   │   │       //    - try { if (!File.Exists(_runtimeConfigPath)) { _logger.LogWarning("Runtime config file not found at {Path}", _runtimeConfigPath); return null; } var json = await File.ReadAllTextAsync(_runtimeConfigPath); _runtimeConfigCache = JsonSerializer.Deserialize<RuntimeConfig>(json); return _runtimeConfigCache; } catch (Exception ex) { _logger.LogError(ex, "Failed to load runtime config from {Path}", _runtimeConfigPath); return null; }
│   │   │   │       //  - public async Task SaveRuntimeConfigAsync(RuntimeConfig config)
│   │   │   │       //    - try { Directory.CreateDirectory(Path.GetDirectoryName(_runtimeConfigPath)); var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }); await File.WriteAllTextAsync(_runtimeConfigPath, json); _runtimeConfigCache = config; _logger.LogInformation("Runtime config saved to {Path}", _runtimeConfigPath); } catch (Exception ex) { _logger.LogError(ex, "Failed to save runtime config to {Path}", _runtimeConfigPath); }
│   │   │   │       //  - public string GetAgentId() => _runtimeConfigCache?.agentId;
│   │   │   │       //  - public string GetEncryptedAgentToken() => _runtimeConfigCache?.agent_token_encrypted;
│   │   │   │       //  - public string GetInstallPath() => _installPath;
│   │   │   │       //  - public string GetDataPath() => _dataPath;
│   │   │
│   │   │   └── Models/
│   │   │       ├── CmsAgentSettingsOptions.cs
│   │   │       │   └── public class CmsAgentSettingsOptions { [Required] public string AppName { get; set; } = "CMSAgent"; [Required, Url] public string ServerUrl { get; set; } [Required] public string Version { get; set; } public AgentSpecificSettingsOptions AgentSettings { get; set; } = new(); public HttpClientSettingsOptions HttpClientSettings { get; set; } = new(); public WebSocketSettingsOptions WebSocketSettings { get; set; } = new(); public CommandExecutorSettingsOptions CommandExecutorSettings { get; set; } = new(); public ResourceLimitsOptions ResourceLimits { get; set; } = new(); }
│   │   │       ├── AgentSpecificSettingsOptions.cs
│   │   │       │   └── public class AgentSpecificSettingsOptions { [Range(1, 3600)] public int StatusReportIntervalSec { get; set; } = 30; public bool EnableAutoUpdate { get; set; } = true; [Range(60, 86400 * 7)] public int AutoUpdateIntervalSec { get; set; } = 86400; [Range(1, 10)] public int NetworkRetryMaxAttempts { get; set; } = 5; [Range(1, 60)] public int NetworkRetryInitialDelaySec { get; set; } = 5; [Range(3600, 86400 * 7)] public int TokenRefreshIntervalSec { get; set; } = 86400; public OfflineQueueSettingsOptions OfflineQueue { get; set; } = new(); }
│   │   │       ├── HttpClientSettingsOptions.cs
│   │   │       │   └── public class HttpClientSettingsOptions { [Range(5, 120)] public int RequestTimeoutSec { get; set; } = 15; }
│   │   │       ├── WebSocketSettingsOptions.cs
│   │   │       │   └── public class WebSocketSettingsOptions { [Range(1, 60)] public int ReconnectDelayInitialSec { get; set; } = 5; [Range(5, 300)] public int ReconnectDelayMaxSec { get; set; } = 60; public int? ReconnectAttemptsMax { get; set; } }
│   │   │       ├── CommandExecutorSettingsOptions.cs
│   │   │       │   └── public class CommandExecutorSettingsOptions { [Range(30, 3600)] public int DefaultTimeoutSec { get; set; } = 300; [Range(1, 10)] public int MaxParallelCommands { get; set; } = 2; [Range(10, 1000)] public int MaxQueueSize { get; set; } = 100; [Required] public string ConsoleEncoding { get; set; } = "utf-8"; }
│   │   │       ├── ResourceLimitsOptions.cs
│   │   │       │   └── public class ResourceLimitsOptions { [Range(10, 100)] public int MaxCpuPercentage { get; set; } = 75; [Range(64, 2048)] public int MaxRamMegabytes { get; set; } = 512; }
│   │   │       ├── OfflineQueueSettingsOptions.cs
│   │   │       │   └── public class OfflineQueueSettingsOptions { [Range(1, 1024)] public int MaxSizeMb { get; set; } = 100; [Range(1, 24 * 30)] public int MaxAgeHours { get; set; } = 24; [Range(10, 10000)] public int StatusReportsMaxCount { get; set; } = 1000; [Range(10, 1000)] public int CommandResultsMaxCount { get; set; } = 500; [Range(10, 1000)] public int ErrorReportsMaxCount { get; set; } = 200; public string BasePath { get; set; } }
│   │   │       └── RuntimeConfig.cs
│   │   │           └── public class RuntimeConfig { public string agentId { get; set; } public RoomConfig room_config { get; set; } public string agent_token_encrypted { get; set; } }
│   │   │           └── public class RoomConfig { public string roomName { get; set; } public int posX { get; set; } public int posY { get; set; } }
│   │   │
│   │   ├── Commands/
│   │   │   ├── CommandExecutor.cs
│   │   │   │   └── public class CommandExecutor : ICommandExecutor
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly ILogger<CommandExecutor> _logger;
│   │   │   │       //  - private readonly ICommandHandlerFactory _handlerFactory;
│   │   │   │       //  - private readonly IWebSocketConnector _webSocketConnector;
│   │   │   │       //  - private readonly CommandExecutorSettingsOptions _settings;
│   │   │   │       //  - private readonly Channel<CommandPayload> _commandQueue;
│   │   │   │       //  - private readonly SemaphoreSlim _parallelismLimiter;
│   │   │   │       //  - private readonly IOfflineQueueManager _offlineQueueManager;
│   │   │   │       // Methods:
│   │   │   │       //  - public CommandExecutor(ILogger<CommandExecutor> logger, ICommandHandlerFactory handlerFactory, IWebSocketConnector webSocketConnector, IOptions<CommandExecutorSettingsOptions> settingsOptions, IOfflineQueueManager offlineQueueManager)
│   │   │   │       //    - _settings = settingsOptions.Value;
│   │   │   │       //    - _commandQueue = Channel.CreateBounded<CommandPayload>(new BoundedChannelOptions(_settings.MaxQueueSize) { FullMode = BoundedChannelFullMode.DropOldest });
│   │   │   │       //    - _parallelismLimiter = new SemaphoreSlim(_settings.MaxParallelCommands);
│   │   │   │       //  - public bool TryEnqueueCommand(CommandPayload command)
│   │   │   │       //    - if (_commandQueue.Writer.TryWrite(command)) { _logger.LogInformation("Command {CommandId} enqueued.", command.commandId); return true; }
│   │   │   │       //    - else { _logger.LogWarning("Command queue is full. Command {CommandId} dropped.", command.commandId); /* Gửi lỗi COMMAND_QUEUE_FULL */ return false; }
│   │   │   │       //  - public Task StartProcessingAsync(CancellationToken cancellationToken)
│   │   │   │       //    - var tasks = new List<Task>();
│   │   │   │       //    - for (int i = 0; i < _settings.MaxParallelCommands; i++) { tasks.Add(ProcessQueueAsync(cancellationToken)); }
│   │   │   │       //    - return Task.WhenAll(tasks);
│   │   │   │       //  - private async Task ProcessQueueAsync(CancellationToken cancellationToken)
│   │   │   │       //    - await foreach (var command in _commandQueue.Reader.ReadAllAsync(cancellationToken))
│   │   │   │       //    - {
│   │   │   │       //    -   await _parallelismLimiter.WaitAsync(cancellationToken);
│   │   │   │       //    -   try { await ProcessCommandInternalAsync(command, cancellationToken); }
│   │   │   │       //    -   finally { _parallelismLimiter.Release(); }
│   │   │   │       //    - }
│   │   │   │       //  - private async Task ProcessCommandInternalAsync(CommandPayload command, CancellationToken cancellationToken)
│   │   │   │       //    - CommandResultPayload result;
│   │   │   │       //    - try {
│   │   │   │       //        _logger.LogInformation("Processing command {CommandId} of type {CommandType}: {CommandText}", command.commandId, command.commandType, command.command);
│   │   │   │       //        var handler = _handlerFactory.GetHandler(command.commandType);
│   │   │   │       //        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
│   │   │   │       //        cts.CancelAfter(TimeSpan.FromSeconds(_settings.DefaultTimeoutSec));
│   │   │   │       //        result = await handler.ExecuteAsync(command, cts.Token);
│   │   │   │       //      }
│   │   │   │       //    - catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { result = new CommandResultPayload { commandId = command.commandId, success = false, type = command.commandType, result = new CommandResultData { errorMessage = "Command cancelled due to agent stopping.", errorCode = "AGENT_STOPPING" } }; }
│   │   │   │       //    - catch (OperationCanceledException) { result = new CommandResultPayload { commandId = command.commandId, success = false, type = command.commandType, result = new CommandResultData { errorMessage = $"Command timed out after {_settings.DefaultTimeoutSec} seconds.", errorCode = "TIMEOUT" } }; }
│   │   │   │       //    - catch (Exception ex) { _logger.LogError(ex, "Error executing command {CommandId}.", command.commandId); result = new CommandResultPayload { commandId = command.commandId, success = false, type = command.commandType, result = new CommandResultData { errorMessage = ex.Message, errorCode = "EXECUTION_ERROR" } }; }
│   │   │   │       //    - if (_webSocketConnector.IsConnected) { await _webSocketConnector.SendCommandResultAsync(result); } else { await _offlineQueueManager.EnqueueCommandResultAsync(result); }
│   │   │
│   │   │   ├── CommandHandlerFactory.cs
│   │   │   │   └── public class CommandHandlerFactory : ICommandHandlerFactory
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly IServiceProvider _serviceProvider;
│   │   │   │       // Methods:
│   │   │   │       //  - public CommandHandlerFactory(IServiceProvider serviceProvider) { _serviceProvider = serviceProvider; }
│   │   │   │       //  - public ICommandHandler GetHandler(CommandType commandType)
│   │   │   │       //    - return commandType switch {
│   │   │   │       //        CommandType.CONSOLE => _serviceProvider.GetRequiredService<ConsoleCommandHandler>(),
│   │   │   │       //        CommandType.SYSTEM_ACTION => _serviceProvider.GetRequiredService<SystemActionCommandHandler>(),
│   │   │   │       //        CommandType.GET_LOGS => _serviceProvider.GetRequiredService<GetLogsCommandHandler>(),
│   │   │   │       //        _ => throw new ArgumentOutOfRangeException(nameof(commandType), $"Unsupported command type: {commandType}")
│   │   │   │       //    };
│   │   │
│   │   │   ├── ICommandHandler.cs
│   │   │   │   └── public interface ICommandHandler { Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken); }
│   │   │
│   │   │   ├── Handlers/
│   │   │   │   ├── ConsoleCommandHandler.cs
│   │   │   │   │   └── public class ConsoleCommandHandler : ICommandHandler
│   │   │   │   │       // Fields: private readonly ILogger<ConsoleCommandHandler> _logger; private readonly CommandExecutorSettingsOptions _settings;
│   │   │   │   │       // Methods: public ConsoleCommandHandler(ILogger<ConsoleCommandHandler> logger, IOptions<CommandExecutorSettingsOptions> settingsOptions) { _logger = logger; _settings = settingsOptions.Value; }
│   │   │   │   │       //          public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken) { /* ... Logic thực thi lệnh console, thu thập stdout/stderr, exit code ... */ return new CommandResultPayload(); }
│   │   │   │   │
│   │   │   │   ├── SystemActionCommandHandler.cs
│   │   │   │   │   └── public class SystemActionCommandHandler : ICommandHandler
│   │   │   │   │       // Fields: private readonly ILogger<SystemActionCommandHandler> _logger;
│   │   │   │   │       // Methods: public SystemActionCommandHandler(ILogger<SystemActionCommandHandler> logger) { _logger = logger; }
│   │   │   │   │       //          public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken) { /* ... Logic thực thi hành động hệ thống (reboot, shutdown) ... */ return new CommandResultPayload(); }
│   │   │   │   │
│   │   │   │   ├── GetLogsCommandHandler.cs
│   │   │   │   │   └── public class GetLogsCommandHandler : ICommandHandler
│   │   │   │   │       // Fields: private readonly ILogger<GetLogsCommandHandler> _logger; private readonly IConfigLoader _configLoader; private readonly IHttpClientWrapper _httpClient; private readonly IDateTimeProvider _dateTimeProvider;
│   │   │   │   │       // Methods: public GetLogsCommandHandler(ILogger<GetLogsCommandHandler> logger, IConfigLoader configLoader, IHttpClientWrapper httpClient, IDateTimeProvider dateTimeProvider) { /* ... */ }
│   │   │   │   │       //          public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken) { /* ... Logic thu thập, nén log, gửi qua API /report-error ... */ return new CommandResultPayload(); }
│   │   │
│   │   ├── Monitoring/
│   │   │   ├── SystemMonitor.cs
│   │   │   │   └── public class SystemMonitor : ISystemMonitor, IDisposable
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly ILogger<SystemMonitor> _logger;
│   │   │   │       //  - private PerformanceCounter _cpuCounter;
│   │   │   │       //  - private PerformanceCounter _ramCounter;
│   │   │   │       //  - private DriveInfo _diskDriveInfo;
│   │   │   │       // Methods:
│   │   │   │       //  - public SystemMonitor(ILogger<SystemMonitor> logger) { _logger = logger; }
│   │   │   │       //  - public void Initialize() { try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true); _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", null, true); _diskDriveInfo = new DriveInfo("C"); _cpuCounter.NextValue(); _ramCounter.NextValue(); } catch (Exception ex) { _logger.LogError(ex, "Failed to initialize performance counters."); /* Agent vẫn có thể chạy nhưng không có thông tin giám sát */ } }
│   │   │   │       //  - public Task<StatusUpdatePayload> GetCurrentStatusAsync() { double cpu = _cpuCounter?.NextValue() ?? -1; double ram = _ramCounter?.NextValue() ?? -1; double disk = -1; if (_diskDriveInfo != null && _diskDriveInfo.IsReady) { disk = 100.0 * (1.0 - (double)_diskDriveInfo.AvailableFreeSpace / _diskDriveInfo.TotalSize); } return Task.FromResult(new StatusUpdatePayload { cpuUsage = cpu, ramUsage = ram, diskUsage = disk }); }
│   │   │   │       //  - public void Dispose() { _cpuCounter?.Dispose(); _ramCounter?.Dispose(); }
│   │   │
│   │   │   ├── HardwareInfoCollector.cs
│   │   │   │   └── public class HardwareInfoCollector : IHardwareInfoCollector
│   │   │   │       // Fields: private readonly ILogger<HardwareInfoCollector> _logger;
│   │   │   │       // Methods: public HardwareInfoCollector(ILogger<HardwareInfoCollector> logger) { _logger = logger; }
│   │   │   │       //          public Task<HardwareInfoPayload> CollectHardwareInfoAsync() { /* ... Logic thu thập thông tin phần cứng sử dụng WMI hoặc System.Management ... */ return Task.FromResult(new HardwareInfoPayload()); }
│   │   │
│   │   ├── Update/
│   │   │   ├── UpdateHandler.cs
│   │   │   │   └── public class UpdateHandler : IUpdateHandler
│   │   │   │       // Fields:
│   │   │   │       //  - private readonly ILogger<UpdateHandler> _logger;
│   │   │   │       //  - private readonly IHttpClientWrapper _httpClient;
│   │   │   │       //  - private readonly IConfigLoader _configLoader;
│   │   │   │       //  - private readonly IWebSocketConnector _webSocketConnector;
│   │   │   │       //  - private readonly AgentSpecificSettingsOptions _agentSettings;
│   │   │   │       //  - private readonly IHostApplicationLifetime _appLifetime;
│   │   │   │       //  - private readonly IStateManager _stateManager;
│   │   │   │       //  - private readonly IOfflineQueueManager _offlineQueueManager;
│   │   │   │       //  - private readonly IDateTimeProvider _dateTimeProvider;
│   │   │   │       //  - private static bool _isUpdateInProgress = false;
│   │   │   │       //  - private static readonly object _updateLock = new object();
│   │   │   │       // Methods:
│   │   │   │       //  - public UpdateHandler(ILogger<UpdateHandler> logger, IHttpClientWrapper httpClient, IConfigLoader configLoader, IWebSocketConnector webSocketConnector, IOptions<AgentSpecificSettingsOptions> agentSettingsOptions, IHostApplicationLifetime appLifetime, IStateManager stateManager, IOfflineQueueManager offlineQueueManager, IDateTimeProvider dateTimeProvider) { /* ... gán dependencies ... */ }
│   │   │   │       //  - public async Task CheckForUpdateAsync(bool manualCheck = false) { /* ... Gọi API /check-update ... */ }
│   │   │   │       //  - public async Task ProcessUpdateAsync(UpdateCheckResponse updateInfo) { /* ... Logic tải, xác minh, giải nén, khởi chạy Updater, báo cáo trạng thái ... */ }
│   │   │   │       //  - private async Task DownloadUpdatePackageAsync(string downloadUrl, string destinationPath, string agentId, string token) { using var stream = await _httpClient.DownloadFileAsync(_configLoader.Settings.ServerUrl + downloadUrl, agentId, token); using var fileStream = new FileStream(destinationPath, FileMode.Create); await stream.CopyToAsync(fileStream); }
│   │   │   │       //  - private bool VerifyChecksum(string filePath, string expectedChecksum) { /* ... Tính SHA256, so sánh ... */ return true; }
│   │   │   │       //  - private void ExtractPackage(string zipPath, string extractPath) { if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true); ZipFile.ExtractToDirectory(zipPath, extractPath); }
│   │   │   │       //  - private void LaunchUpdater(string extractedAgentPath, string newVersion) { /* ... Logic khởi chạy CMSUpdater.exe ... */ }
│   │   │   │       //  - private async Task SendUpdateStatusAsync(UpdateStatus status, string version, string reason = null) { await _webSocketConnector.SendUpdateStatusAsync(new UpdateStatusPayload { status = status, new_version = version, reason = reason }); }
│   │   │
│   │   ├── Security/
│   │   │   ├── TokenProtector.cs
│   │   │   │   └── public class TokenProtector : ITokenProtector
│   │   │   │       // Fields: private readonly ILogger<TokenProtector> _logger;
│   │   │   │       // Methods:
│   │   │   │       //  - public TokenProtector(ILogger<TokenProtector> logger) { _logger = logger; }
│   │   │   │       //  - public string EncryptToken(string plainToken) { try { byte[] plainBytes = Encoding.UTF8.GetBytes(plainToken); byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine); return Convert.ToBase64String(encryptedBytes); } catch (CryptographicException ex) { _logger.LogError(ex, "Error encrypting token."); throw; } }
│   │   │   │       //  - public string DecryptToken(string encryptedTokenBase64) { try { byte[] encryptedBytes = Convert.FromBase64String(encryptedTokenBase64); byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine); return Encoding.UTF8.GetString(plainBytes); } catch (CryptographicException ex) { _logger.LogError(ex, "Error decrypting token. Token might be corrupted or from a different machine/user context."); throw; } }
│   │   │
│   │   ├── Cli/
│   │   │   ├── CliHandler.cs
│   │   │   │   └── public class CliHandler
│   │   │   │       // Fields:
│   │   │   │       //  - public bool IsCliCommandExecuted { get; private set; } = false;
│   │   │   │       //  - private readonly RootCommand _rootCommand;
│   │   │   │       //  - private readonly IServiceProvider _serviceProvider;
│   │   │   │       // Methods:
│   │   │   │       //  - public CliHandler(IServiceProvider serviceProvider)
│   │   │   │       //    - _serviceProvider = serviceProvider;
│   │   │   │       //    - _rootCommand = new RootCommand("CMSAgent Management CLI");
│   │   │   │       //    - var configureCmd = new Command("configure", "Configure agent initial settings (interactive).");
│   │   │   │       //    - configureCmd.SetHandler(async (InvocationContext context) => { IsCliCommandExecuted = true; var cmd = _serviceProvider.GetRequiredService<ConfigureCommand>(); context.ExitCode = await cmd.ExecuteAsync(context.Console, context.GetCancellationToken()); });
│   │   │   │       //    - _rootCommand.AddCommand(configureCmd);
│   │   │   │       //    - var startCmd = new Command("start", "Start the CMSAgent Windows service.");
│   │   │   │       //    - startCmd.SetHandler(async (InvocationContext context) => { IsCliCommandExecuted = true; var cmd = _serviceProvider.GetRequiredService<StartCommand>(); context.ExitCode = await cmd.ExecuteAsync(context.Console); });
│   │   │   │       //    - _rootCommand.AddCommand(startCmd);
│   │   │   │       //    - var stopCmd = new Command("stop", "Stop the CMSAgent Windows service.");
│   │   │   │       //    - stopCmd.SetHandler(async (InvocationContext context) => { IsCliCommandExecuted = true; var cmd = _serviceProvider.GetRequiredService<StopCommand>(); context.ExitCode = await cmd.ExecuteAsync(context.Console); });
│   │   │   │       //    - _rootCommand.AddCommand(stopCmd);
│   │   │   │       //    - var uninstallCmd = new Command("uninstall", "Uninstall the CMSAgent Windows service.");
│   │   │   │       //    - var removeDataOption = new Option<bool>(new[] { "--remove-data", "-r" }, "Remove agent data directories from ProgramData."); uninstallCmd.AddOption(removeDataOption);
│   │   │   │       //    - uninstallCmd.SetHandler(async (InvocationContext context) => { IsCliCommandExecuted = true; bool removeDataValue = context.ParseResult.GetValueForOption(removeDataOption); var cmd = _serviceProvider.GetRequiredService<UninstallCommand>(); context.ExitCode = await cmd.ExecuteAsync(context.Console, removeDataValue); }, removeDataOption);
│   │   │   │       //    - _rootCommand.AddCommand(uninstallCmd);
│   │   │   │       //    - var debugCmd = new Command("debug", "Run the agent in debug mode as a console application.");
│   │   │   │       //    - debugCmd.SetHandler((InvocationContext context) => { /* IsCliCommandExecuted KHÔNG set true */ var cmd = _serviceProvider.GetRequiredService<DebugCommand>(); context.ExitCode = cmd.Execute(context.Console); });
│   │   │   │       //    - _rootCommand.AddCommand(debugCmd);
│   │   │   │       //  - public async Task<int> HandleAsync(string[] args) { return await _rootCommand.InvokeAsync(args); }
│   │   │
│   │   │   └── Commands/
│   │   │       ├── ConfigureCommand.cs
│   │   │       │   └── public class ConfigureCommand
│   │   │       │       // Fields: private readonly ILogger<ConfigureCommand> _logger; private readonly IConfigLoader _configLoader; private readonly IHttpClientWrapper _httpClient; private readonly ITokenProtector _tokenProtector; private readonly IDateTimeProvider _dateTimeProvider;
│   │   │       │       // Methods: public ConfigureCommand(ILogger<ConfigureCommand> logger, IConfigLoader configLoader, IHttpClientWrapper httpClient, ITokenProtector tokenProtector, IDateTimeProvider dateTimeProvider) { /* ... */ }
│   │   │       │       //          public async Task<int> ExecuteAsync(IConsole console, CancellationToken cancellationToken) { /* ... Logic cấu hình tương tác, gọi API /identify, /verify-mfa, lưu runtime_config ... */ return CliExitCodes.Success; }
│   │   │       │       //          private string PromptForInput(IConsole console, string promptMessage, string defaultValue = null) { /* ... */ return ""; }
│   │   │       │       //          private bool TryParseInt(IConsole console, string promptMessage, out int value, string defaultValue = null) { /* ... */ value = 0; return false;}
│   │   │
│   │   │       ├── StartCommand.cs
│   │   │       │   └── public class StartCommand
│   │   │       │       // Fields: private readonly ILogger<StartCommand> _logger; private readonly ServiceUtils _serviceUtils; private readonly string _serviceName;
│   │   │       │       // Methods: public StartCommand(ILogger<StartCommand> logger, ServiceUtils serviceUtils, IOptions<CmsAgentSettingsOptions> settings) { _logger = logger; _serviceUtils = serviceUtils; _serviceName = settings.Value.AppName + "Service"; /* Hoặc một hằng số */ }
│   │   │       │       //          public async Task<int> ExecuteAsync(IConsole console) { /* ... Logic gọi _serviceUtils.StartService ... */ return CliExitCodes.Success; }
│   │   │
│   │   │       ├── StopCommand.cs
│   │   │       │   └── public class StopCommand
│   │   │       │       // Fields: private readonly ILogger<StopCommand> _logger; private readonly ServiceUtils _serviceUtils; private readonly string _serviceName;
│   │   │       │       // Methods: public StopCommand(ILogger<StopCommand> logger, ServiceUtils serviceUtils, IOptions<CmsAgentSettingsOptions> settings) { /* ... */ }
│   │   │       │       //          public async Task<int> ExecuteAsync(IConsole console) { /* ... Logic gọi _serviceUtils.StopService ... */ return CliExitCodes.Success; }
│   │   │
│   │   │       ├── UninstallCommand.cs
│   │   │       │   └── public class UninstallCommand
│   │   │       │       // Fields: private readonly ILogger<UninstallCommand> _logger; private readonly ServiceUtils _serviceUtils; private readonly IConfigLoader _configLoader; private readonly string _serviceName;
│   │   │       │       // Methods: public UninstallCommand(ILogger<UninstallCommand> logger, ServiceUtils serviceUtils, IConfigLoader configLoader, IOptions<CmsAgentSettingsOptions> settings) { /* ... */ }
│   │   │       │       //          public async Task<int> ExecuteAsync(IConsole console, bool removeData) { /* ... Logic dừng, gỡ service, xóa thư mục cài đặt/dữ liệu ... */ return CliExitCodes.Success; }
│   │   │
│   │   │       ├── DebugCommand.cs
│   │   │       │   └── public class DebugCommand
│   │   │       │       // Fields: private readonly ILogger<DebugCommand> _logger;
│   │   │       │       // Methods: public DebugCommand(ILogger<DebugCommand> logger) { _logger = logger; }
│   │   │       │       //          public int Execute(IConsole console) { console.WriteLine("CMSAgent is configured to run in debug mode (as a console application)."); _logger.LogInformation("CMSAgent debug mode initiated via CLI. Service host will now run."); return CliExitCodes.Success; }
│   │   │
│   │   │       └── ServiceUtils.cs
│   │   │           └── public class ServiceUtils
│   │   │               // Fields: private readonly ILogger<ServiceUtils> _logger; private const string ScExe = "sc.exe";
│   │   │               // Methods: public ServiceUtils(ILogger<ServiceUtils> logger) { _logger = logger; }
│   │   │               //          public bool IsServiceInstalled(string serviceName) { try { return ServiceController.GetServices().Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)); } catch (Exception ex) { _logger.LogWarning(ex, "Error checking if service {ServiceName} is installed.", serviceName); return false; } }
│   │   │               //          public void InstallService(string serviceName, string displayName, string description, string exePath) { /* ... Logic gọi sc.exe create, description, config, failure ... */ }
│   │   │               //          public void UninstallService(string serviceName) { /* ... Logic gọi sc.exe delete ... */ }
│   │   │               //          public void StartService(string serviceName) { /* ... Logic dùng ServiceController.Start() và WaitForStatus ... */ }
│   │   │               //          public void StopService(string serviceName) { /* ... Logic dùng ServiceController.Stop() và WaitForStatus ... */ }
│   │   │               //          public ServiceControllerStatus GetServiceStatus(string serviceName) { try { return new ServiceController(serviceName).Status; } catch { return ServiceControllerStatus.Stopped; /* Hoặc throw */ } }
│   │   │               //          private void ExecuteScCommand(string arguments, string errorMessagePrefix) { /* ... Logic chạy sc.exe, kiểm tra ExitCode, đọc StdErr/StdOut ... */ }
│   │   │               //          private bool IsAdministrator() { using var identity = WindowsIdentity.GetCurrent(); var principal = new WindowsPrincipal(identity); return principal.IsInRole(WindowsBuiltInRole.Administrator); }
│   │   │
│   │   └── Persistence/
│   │       ├── OfflineQueueManager.cs
│   │       │   └── public class OfflineQueueManager : IOfflineQueueManager
│   │       │       // Fields:
│   │       │       //  - private readonly ILogger<OfflineQueueManager> _logger;
│   │       │       //  - private readonly IConfigLoader _configLoader;
│   │       │       //  - private readonly IWebSocketConnector _wsConnector;
│   │       │       //  - private readonly IHttpClientWrapper _httpClient;
│   │       │       //  - private readonly ITokenProtector _tokenProtector;
│   │       │       //  - private readonly IDateTimeProvider _dateTimeProvider;
│   │       │       //  - private readonly FileQueue<StatusUpdatePayload> _statusQueue;
│   │       │       //  - private readonly FileQueue<CommandResultPayload> _commandResultQueue;
│   │       │       //  - private readonly FileQueue<ErrorReportPayload> _errorReportQueue;
│   │       │       //  - private readonly string _basePath;
│   │       │       // Methods:
│   │       │       //  - public OfflineQueueManager(ILogger<OfflineQueueManager> logger, IConfigLoader configLoader, IOptions<OfflineQueueSettingsOptions> queueSettings, IDateTimeProvider dateTimeProvider, IWebSocketConnector wsConnector, IHttpClientWrapper httpClient, ITokenProtector tokenProtector)
│   │       │       //    - // Gán dependencies
│   │       │       //    - _basePath = queueSettings.Value.BasePath ?? Path.Combine(configLoader.GetDataPath(), "offline_queue");
│   │       │       //    - _statusQueue = new FileQueue<StatusUpdatePayload>(Path.Combine(_basePath, "status_reports"), logger, _dateTimeProvider, queueSettings.Value.StatusReportsMaxCount, queueSettings.Value.MaxSizeMb, queueSettings.Value.MaxAgeHours);
│   │       │       //    - _commandResultQueue = new FileQueue<CommandResultPayload>(Path.Combine(_basePath, "command_results"), logger, _dateTimeProvider, queueSettings.Value.CommandResultsMaxCount, queueSettings.Value.MaxSizeMb, queueSettings.Value.MaxAgeHours);
│   │       │       //    - _errorReportQueue = new FileQueue<ErrorReportPayload>(Path.Combine(_basePath, "error_reports"), logger, _dateTimeProvider, queueSettings.Value.ErrorReportsMaxCount, queueSettings.Value.MaxSizeMb, queueSettings.Value.MaxAgeHours);
│   │       │       //  - public async Task EnqueueStatusReportAsync(StatusUpdatePayload payload) => await _statusQueue.EnqueueAsync(payload);
│   │       │       //  - public async Task EnqueueCommandResultAsync(CommandResultPayload payload) => await _commandResultQueue.EnqueueAsync(payload);
│   │       │       //  - public async Task EnqueueErrorReportAsync(ErrorReportPayload payload) => await _errorReportQueue.EnqueueAsync(payload);
│   │       │       //  - public async Task ProcessQueuesAsync(CancellationToken cancellationToken)
│   │       │       //    - var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
│   │       │       //    - if (runtimeConfig == null || string.IsNullOrEmpty(runtimeConfig.agent_token_encrypted)) { _logger.LogWarning("Cannot process offline queues: runtime config or token is missing."); return; }
│   │       │       //    - string decryptedToken; try { decryptedToken = _tokenProtector.DecryptToken(runtimeConfig.agent_token_encrypted); } catch (Exception ex) { _logger.LogError(ex, "Cannot process offline queues: failed to decrypt token."); return; }
│   │       │       //    - var agentId = runtimeConfig.agentId;
│   │       │       //    - await ProcessSpecificQueueAsync(_statusQueue, async (item) => await _wsConnector.SendStatusUpdateAsync(item.Data), cancellationToken);
│   │       │       //    - await ProcessSpecificQueueAsync(_commandResultQueue, async (item) => await _wsConnector.SendCommandResultAsync(item.Data), cancellationToken);
│   │       │       //    - await ProcessSpecificQueueAsync(_errorReportQueue, async (item) => await TrySendErrorReportViaHttpAsync(item.Data, agentId, decryptedToken), cancellationToken);
│   │       │       //  - private async Task ProcessSpecificQueueAsync<T>(FileQueue<T> queue, Func<QueuedItem<T>, Task<bool>> sendAction, CancellationToken cancellationToken) where T : class
│   │       │       //    - QueuedItem<T> queuedItem;
│   │       │       //    - while ((queuedItem = await queue.TryDequeueAsync()) != null && !cancellationToken.IsCancellationRequested)
│   │       │       //    - { bool success = false; try { success = await sendAction(queuedItem); } catch (Exception ex) { _logger.LogError(ex, "Error sending queued item {ItemId} of type {Type}", queuedItem.ItemId, typeof(T).Name); }
│   │       │       //    -   if (!success) { await queue.RequeueAsync(queuedItem); _logger.LogWarning("Failed to send queued item {ItemId}, re-queued.", queuedItem.ItemId); break; }
│   │       │       //    -   else { _logger.LogInformation("Successfully sent queued item {ItemId} of type {Type}", queuedItem.ItemId, typeof(T).Name); }
│   │       │       //    - }
│   │       │       //  - private async Task<bool> TrySendErrorReportViaHttpAsync(ErrorReportPayload payload, string agentId, string token)
│   │       │       //    - try { await _httpClient.PostAsync(ApiRoutes.ReportError, payload, agentId, token); return true; }
│   │       │       //    - catch (Exception ex) { _logger.LogError(ex, "Failed to send offline error report via HTTP."); return false; }
│   │       │
│   │       ├── IOfflineQueueManager.cs
│   │       │   └── public interface IOfflineQueueManager { Task EnqueueStatusReportAsync(StatusUpdatePayload payload); Task EnqueueCommandResultAsync(CommandResultPayload payload); Task EnqueueErrorReportAsync(ErrorReportPayload payload); Task ProcessQueuesAsync(CancellationToken cancellationToken); }
│   │       │
│   │       ├── FileQueue.cs
│   │       │   └── public class FileQueue<T> where T : class
│   │       │       // Fields:
│   │       │       //  - private readonly string _queueDirectory;
│   │       │       //  - private readonly ILogger _logger; // Sử dụng ILogger thay vì ILogger<FileQueue<T>> để dễ inject hơn
│   │       │       //  - private readonly IDateTimeProvider _dateTimeProvider;
│   │       │       //  - private readonly int _maxCount;
│   │       │       //  - private readonly long _maxSizeBytes;
│   │       │       //  - private readonly int _maxAgeHours;
│   │       │       //  - private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNameCaseInsensitive = true };
│   │       │       // Methods:
│   │       │       //  - public FileQueue(string queueDirectory, ILogger logger, IDateTimeProvider dtp, int maxCount, int maxSizeMb, int maxAgeHours) { _queueDirectory = queueDirectory; _logger = logger; _dateTimeProvider = dtp; _maxCount = maxCount; _maxSizeBytes = (long)maxSizeMb * 1024 * 1024; _maxAgeHours = maxAgeHours; Directory.CreateDirectory(_queueDirectory); }
│   │       │       //  - public async Task EnqueueAsync(T item) { await CleanUpOldAndExceedingItemsAsync(); var queuedItem = new QueuedItem<T> { ItemId = Guid.NewGuid().ToString(), Data = item, EnqueuedTimestampUtc = _dateTimeProvider.UtcNow }; var filePath = Path.Combine(_queueDirectory, $"{queuedItem.ItemId}.json"); try { await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(queuedItem, _jsonOptions)); _logger.LogDebug("Enqueued item {ItemId} to {QueueDirectory}", queuedItem.ItemId, _queueDirectory); } catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue item to {QueueDirectory}", _queueDirectory); } }
│   │       │       //  - public async Task<QueuedItem<T>> TryDequeueAsync() { var oldestFile = GetQueueFiles().OrderBy(f => f.CreationTimeUtc).FirstOrDefault(); if (oldestFile == null) return null; try { var jsonContent = await File.ReadAllTextAsync(oldestFile.FullName); var queuedItem = JsonSerializer.Deserialize<QueuedItem<T>>(jsonContent, _jsonOptions); File.Delete(oldestFile.FullName); _logger.LogDebug("Dequeued item {ItemId} from {QueueDirectory}", queuedItem?.ItemId, _queueDirectory); return queuedItem; } catch (Exception ex) { _logger.LogError(ex, "Failed to dequeue item from {FilePath}. File might be corrupted or inaccessible. Deleting problematic file.", oldestFile.FullName); try { File.Delete(oldestFile.FullName); } catch (Exception deleteEx) { _logger.LogError(deleteEx, "Failed to delete problematic queue file {FilePath}", oldestFile.FullName); } return null; } }
│   │       │       //  - public async Task RequeueAsync(QueuedItem<T> queuedItem) { queuedItem.RetryAttempts++; queuedItem.EnqueuedTimestampUtc = _dateTimeProvider.UtcNow; var filePath = Path.Combine(_queueDirectory, $"{queuedItem.ItemId}.json"); try { await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(queuedItem, _jsonOptions)); _logger.LogDebug("Re-queued item {ItemId} in {QueueDirectory}, attempt {RetryAttempts}", queuedItem.ItemId, _queueDirectory, queuedItem.RetryAttempts); } catch (Exception ex) { _logger.LogError(ex, "Failed to re-queue item {ItemId} in {QueueDirectory}", queuedItem.ItemId, _queueDirectory); } }
│   │       │       //  - private IEnumerable<FileInfo> GetQueueFiles() { try { return new DirectoryInfo(_queueDirectory).GetFiles("*.json"); } catch (Exception ex) { _logger.LogError(ex, "Failed to get queue files from {QueueDirectory}", _queueDirectory); return Enumerable.Empty<FileInfo>(); } }
│   │       │       //  - private async Task CleanUpOldAndExceedingItemsAsync() { /* ... Logic dọn dẹp file cũ theo tuổi, số lượng, kích thước ... */ }
│   │       │
│   │       └── Models/
│   │           └── QueuedItem.cs
│   │               └── public class QueuedItem<T> { public string ItemId { get; set; } public T Data { get; set; } public DateTime EnqueuedTimestampUtc { get; set; } public int RetryAttempts { get; set; } = 0; }
│   │
│   ├── CMSUpdater/
│   │   ├── CMSUpdater.csproj
│   │   ├── Program.cs
│   │   │   └── public class Program
│   │   │       // Fields: private static ILogger _logger; // Logger tĩnh cho Updater
│   │   │       // Methods:
│   │   │       //  - public static async Task<int> Main(string[] args)
│   │   │       //    - // Parse args: pid, newAgentPath, currentInstallDir, updaterLogDir, currentAgentVersion
│   │   │       //    - // Ví dụ manual parsing hoặc dùng System.CommandLine
│   │   │       //    - var updateParams = ParseArguments(args);
│   │   │       //    - if (updateParams == null) { Console.Error.WriteLine("Invalid arguments for CMSUpdater."); return UpdaterExitCodes.InvalidArguments; }
│   │   │       //    - _logger = LoggingSetup.CreateUpdaterLogger(updateParams.UpdaterLogDir, updateParams.CurrentAgentVersion);
│   │   │       //    - _logger.LogInformation("CMSUpdater started with PID: {PID}, NewPath: {NewPath}, CurrentDir: {CurrentDir}, LogDir: {LogDir}, CurrentVersion: {Version}", updateParams.AgentProcessIdToWait, updateParams.NewAgentPath, updateParams.CurrentAgentInstallDir, updateParams.UpdaterLogDir, updateParams.CurrentAgentVersion);
│   │   │       //    - var serviceHelper = new ServiceHelper(_logger);
│   │   │       //    - var rollbackManager = new RollbackManager(_logger, updateParams, serviceHelper);
│   │   │       //    - var updaterLogic = new UpdaterLogic(_logger, rollbackManager, serviceHelper, updateParams);
│   │   │       //    - return await updaterLogic.ExecuteUpdateAsync();
│   │   │       //  - private static UpdateParameters ParseArguments(string[] args) { /* ... Logic parse ... */ return new UpdateParameters(); }
│   │   │
│   │   ├── UpdaterLogic.cs
│   │   │   └── public class UpdaterLogic
│   │   │       // Fields: private readonly ILogger<UpdaterLogic> _logger; private readonly RollbackManager _rollbackManager; private readonly ServiceHelper _serviceHelper; private readonly UpdateParameters _params; private readonly string _agentServiceName = "CMSAgentService"; // Lấy từ config hoặc hằng số
│   │   │       // Methods: public UpdaterLogic(ILogger<UpdaterLogic> logger, RollbackManager rm, ServiceHelper sh, UpdateParameters p) { /* ... */ }
│   │   │       //          public async Task<int> ExecuteUpdateAsync() { /* ... Luồng cập nhật: Chờ agent cũ dừng, Sao lưu, Triển khai mới, Khởi động mới, Watchdog, Dọn dẹp ... */ return UpdaterExitCodes.Success; }
│   │   │       //          private async Task<bool> WaitForProcessExitAsync(int pid, TimeSpan timeout) { /* ... */ return true; }
│   │   │       //          private async Task BackupAgentAsync() { /* ... Đổi tên hoặc copy thư mục cài đặt cũ ... */ }
│   │   │       //          private async Task DeployNewAgentAsync() { /* ... Xóa nội dung thư mục cài đặt (trừ backup), copy file mới từ _params.NewAgentPath ... */ }
│   │   │       //          private async Task<bool> WatchdogServiceAsync(TimeSpan watchTime, TimeSpan checkInterval) { /* ... Theo dõi service mới, kiểm tra trạng thái định kỳ ... */ return true; }
│   │   │       //          private async Task CleanUpAsync() { /* ... Xóa thư mục backup, file download/extracted của agent cũ ... */ }
│   │   │
│   │   ├── RollbackManager.cs
│   │   │   └── public class RollbackManager
│   │   │       // Fields: private readonly ILogger<RollbackManager> _logger; private readonly UpdateParameters _params; private readonly ServiceHelper _serviceHelper; private readonly string _agentServiceName = "CMSAgentService";
│   │   │       // Methods: public RollbackManager(ILogger<RollbackManager> logger, UpdateParameters p, ServiceHelper sh) { /* ... */ }
│   │   │       //          public async Task RollbackAsync(RollbackReason reason) { /* ... Dừng service mới, khôi phục backup, khởi động lại service cũ ... */ }
│   │   │
│   │   ├── ServiceHelper.cs // Lớp tiện ích tương tác SCM cho Updater
│   │   │   └── public class ServiceHelper
│   │   │       // Fields: private readonly ILogger _logger; // ILogger thay vì ILogger<ServiceHelper> vì có thể dùng chung
│   │   │       // Methods: public ServiceHelper(ILogger logger) { _logger = logger; }
│   │   │       //          public void StartAgentService(string serviceName) { /* ... */ }
│   │   │       //          public void StopAgentService(string serviceName) { /* ... */ }
│   │   │       //          public bool IsAgentServiceRunning(string serviceName) { /* ... */ return false; }
│   │   │
│   │   └── LoggingSetup.cs
│   │       └── public static class LoggingSetup
│   │           // Methods: public static ILogger CreateUpdaterLogger(string logDirectory, string currentAgentVersion) { /* ... Cấu hình Serilog ghi ra file updater_YYYYMMDD_HHMMSS_version.log ... */ return new LoggerConfiguration().CreateLogger(); }
│   │   └── UpdateParameters.cs // Class chứa các tham số dòng lệnh cho Updater
│   │       └── public class UpdateParameters { public int AgentProcessIdToWait { get; set; } public string NewAgentPath { get; set; } public string CurrentAgentInstallDir { get; set; } public string UpdaterLogDir { get; set; } public string CurrentAgentVersion { get; set; } }
│   │
│   ├── CMSAgent.Common/
│   │   ├── CMSAgent.Common.csproj
│   │   ├── DTOs/
│   │   │   ├── AgentIdentifyRequest.cs { public string agentId { get; set; } public PositionInfo positionInfo { get; set; } public bool forceRenewToken { get; set; } }
│   │   │   ├── AgentIdentifyResponse.cs { public string status { get; set; } public string agentId { get; set; } public string agentToken { get; set; } public string message { get; set; } }
│   │   │   ├── VerifyMfaRequest.cs { public string agentId { get; set; } public string mfaCode { get; set; } }
│   │   │   ├── VerifyMfaResponse.cs { public string status { get; set; } public string agentId { get; set; } public string agentToken { get; set; } public string message { get; set; } }
│   │   │   ├── HardwareInfoPayload.cs { public string os_info { get; set; } public string cpu_info { get; set; } public string gpu_info { get; set; } public long total_ram { get; set; } public long total_disk_space { get; set; } }
│   │   │   ├── UpdateCheckResponse.cs { public string status { get; set; } public bool update_available { get; set; } public string version { get; set; } public string download_url { get; set; } public string checksum_sha256 { get; set; } public string notes { get; set; } }
│   │   │   ├── ErrorReportPayload.cs { public ErrorType error_type { get; set; } public string error_message { get; set; } public object error_details { get; set; } public DateTime timestamp { get; set; } }
│   │   │   ├── LogUploadPayload.cs { public string log_filename { get; set; } public string log_content_base64 { get; set; } }
│   │   │   ├── CommandPayload.cs { public string commandId { get; set; } public string command { get; set; } public CommandType commandType { get; set; } public Dictionary<string, object> parameters { get; set; } }
│   │   │   ├── CommandResultPayload.cs { public string commandId { get; set; } public bool success { get; set; } public CommandType type { get; set; } public CommandResultData result { get; set; } }
│   │   │   └── CommandResultData.cs { public string stdout { get; set; } public string stderr { get; set; } public int? exitCode { get; set; } public string errorMessage { get; set; } public string errorCode { get; set; } }
│   │   │   ├── StatusUpdatePayload.cs { public double cpuUsage { get; set; } public double ramUsage { get; set; } public double diskUsage { get; set; } }
│   │   │   ├── UpdateStatusPayload.cs { public UpdateStatus status { get; set; } public string reason { get; set; } public string new_version { get; set; } }
│   │   │   └── PositionInfo.cs { public string roomName { get; set; } public int posX { get; set; } public int posY { get; set; } }
│   │   │
│   │   ├── Enums/
│   │   │   ├── AgentState.cs { INITIALIZING, AUTHENTICATING, CONNECTED, DISCONNECTED, UPDATING, ERROR, STOPPING }
│   │   │   ├── CommandType.cs { CONSOLE, SYSTEM_ACTION, GET_LOGS }
│   │   │   ├── ErrorType.cs { WEBSOCKET_CONNECTION_FAILED, WEBSOCKET_AUTH_FAILED, HTTP_REQUEST_FAILED, CONFIG_LOAD_FAILED, CONFIG_VALIDATION_FAILED, TOKEN_DECRYPTION_FAILED, HARDWARE_INFO_COLLECTION_FAILED, STATUS_REPORTING_FAILED, COMMAND_EXECUTION_FAILED, COMMAND_QUEUE_FULL, UPDATE_DOWNLOAD_FAILED, UPDATE_CHECKSUM_MISMATCH, UPDATE_EXTRACTION_FAILED, UPDATE_ROLLBACK_FAILED, UPDATE_SERVICE_START_FAILED, LOGGING_FAILED, RESOURCE_LIMIT_EXCEEDED, UNHANDLED_EXCEPTION, OFFLINE_QUEUE_ERROR, LOG_UPLOAD_REQUESTED }
│   │   │   ├── UpdateStatus.cs { UPDATE_STARTED, UPDATE_DOWNLOADED, UPDATE_EXTRACTED, UPDATER_LAUNCHED, UPDATE_SUCCESS, UPDATE_FAILED }
│   │   │   ├── CliExitCodes.cs { Success = 0, GeneralError = 1, MissingPermissions = 2, UserCancelled = 3, ServerConnectionFailed = 4, ConfigSaveFailed = 5, ServiceOperationFailed = 6, ServiceNotInstalled = 7, InvalidArguments = 8, UpdaterErrorBase = 10 /* Các mã lỗi của Updater sẽ cộng thêm vào đây */ }
│   │   │   └── RollbackReason.cs { UpdateDeploymentFailed, NewServiceStartFailed, NewServiceUnstable }
│   │   │   └── UpdaterExitCodes.cs // Mã lỗi riêng cho CMSUpdater.exe
│   │   │       └── public enum UpdaterExitCodes { Success = 0, InvalidArguments = 15, AgentStopTimeout = 16, BackupFailed = 11, DeployFailed = 12, NewServiceStartFailed = 13, RollbackFailed = 14, WatchdogTriggeredRollback = 17, GeneralError = 99 }
│   │   │
│   │   ├── Constants/
│   │   │   ├── ApiRoutes.cs { public const string Identify = "/identify"; public const string VerifyMfa = "/verify-mfa"; public const string HardwareInfo = "/hardware-info"; public const string CheckUpdate = "/check-update"; public const string ReportError = "/report-error"; public const string DownloadPackageBase = "/download/agent-packages/"; }
│   │   │   ├── WebSocketEvents.cs { public const string AgentWsAuthSuccess = "agent:ws_auth_success"; public const string AgentWsAuthFailed = "agent:ws_auth_failed"; public const string CommandExecute = "command:execute"; public const string AgentNewVersionAvailable = "agent:new_version_available"; public const string AgentAuthenticate = "agent:authenticate"; public const string AgentStatusUpdate = "agent:status_update"; public const string AgentCommandResult = "agent:command_result"; public const string AgentUpdateStatus = "agent:update_status"; }
│   │   │   ├── MutexNames.cs { public const string AgentSingleton = "Global\\CMSAgentSingletonMutex_E17A2F8D-9B74-4A6A-8E0A-3F9F7B1B3C5D"; /* GUID đã được sinh */ }
│   │   │   ├── HttpHeaders.cs { public const string AgentIdHeader = "X-Agent-Id"; public const string ClientTypeHeader = "X-Client-Type"; public const string ClientTypeValue = "agent"; public const string AuthorizationHeader = "Authorization"; public const string BearerPrefix = "Bearer "; }
│   │   │   └── ConfigurationKeys.cs { public const string CmsAgentSettingsSection = "CMSAgentSettings"; public const string ServerUrlKey = $"{CmsAgentSettingsSection}:ServerUrl"; public const string AgentSettingsSection = $"{CmsAgentSettingsSection}:AgentSettings"; public const string StatusReportIntervalKey = $"{AgentSettingsSection}:StatusReportIntervalSec"; }
│   │   │
│   │   └── Interfaces/
│   │       └── IDateTimeProvider.cs
│   │           └── public interface IDateTimeProvider { DateTime UtcNow { get; } DateTime Now { get; } }
│   │           └── public class DateTimeProvider : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; public DateTime Now => DateTime.Now; }
│   │
│   └── Setup/
│       └── SetupScript.iss   # File script Inno Setup
│
├── tests/                    # Thư mục chứa các dự án test
│   ├── CMSAgent.UnitTests/   # Unit test cho các module của CMSAgent
│   │   └── CMSAgent.UnitTests.csproj
│   │   └── Core/
│   │       └── AgentServiceTests.cs
│   │       └── StateManagerTests.cs
│   │   └── Communication/
│   │       └── HttpClientWrapperTests.cs
│   │       └── WebSocketConnectorTests.cs
│   │   └── Commands/
│   │       └── CommandExecutorTests.cs
│   │       └── Handlers/
│   │           └── ConsoleCommandHandlerTests.cs
│   │   └── Configuration/
│   │       └── ConfigLoaderTests.cs
│   │   └── Persistence/
│   │       └── OfflineQueueManagerTests.cs
│   │       └── FileQueueTests.cs
│   │
│   ├── CMSUpdater.UnitTests/ # Unit test cho CMSUpdater
│   │   └── CMSUpdater.UnitTests.csproj
│   │   └── UpdaterLogicTests.cs
│   │   └── RollbackManagerTests.cs
│   │
│   ├── CMSAgent.Common.UnitTests/ # Unit test cho thư viện Common (nếu có logic phức tạp)
│   │   └── CMSAgent.Common.UnitTests.csproj
│   │
│   └── CMSAgent.IntegrationTests/ # Integration test
│       └── CMSAgent.IntegrationTests.csproj
│       └── AgentServerCommunicationTests.cs // Test với mock HTTP server, mock WebSocket server
│       └── FullUpdateFlowTests.cs
│
├── docs/                     # Thư mục chứa tài liệu dự án
│   ├── CMSAgent_Comprehensive_Doc_v7.4.md
│   ├── Architecture.md
│   ├── Flow.md
│   └── Flowcharts/           # Chứa các file hình ảnh sơ đồ luồng đã render từ Mermaid hoặc các file .mermaid
│       ├── install_flow.mermaid
│       └── update_flow.mermaid
│   └── API_Specification.md  # Tài liệu đặc tả API chi tiết (Swagger/OpenAPI)
│
└── scripts/                  # Các script hỗ trợ (build, deploy,...)
    ├── build.ps1             # Script build bằng PowerShell (dotnet build, dotnet publish)
    ├── set_permissions.ps1   # Script PowerShell để thiết lập quyền cho thư mục ProgramData (sử dụng icacls)
    ├── package.ps1           # Script để chạy Inno Setup tạo bộ cài đặt
    └── run_tests.ps1         # Script để chạy tất cả các unit/integration tests
```