 // CMSAgent.Service/Program.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.CommandLine; // For System.CommandLine
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CMSAgent.Service.Workers;
using CMSAgent.Service.Orchestration;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Configuration.Manager;
using CMSAgent.Shared.Logging;
using CMSAgent.Shared.Constants;
using CMSAgent.Shared; // For IVersionIgnoreManager, VersionIgnoreManager
using CMSAgent.Service.Security;
using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Communication.WebSocket;
using CMSAgent.Service.Monitoring;
using CMSAgent.Service.Commands;
using CMSAgent.Service.Commands.Factory;
using CMSAgent.Service.Commands.Handlers;
using CMSAgent.Service.Update;
using Polly; // For Polly retry policies

namespace CMSAgent.Service
{
    public class Program
    {
        private static MutexManager? _mutexManager; // To release mutex on exit

        public static async Task<int> Main(string[] args)
        {
            // --- Cấu hình ban đầu cho Serilog (ghi ra Console để debug sớm) ---
            // Cấu hình đầy đủ sẽ được thực hiện sau khi IConfiguration được load.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger(); // Logger tạm thời

            Log.Information("CMSAgent.Service đang khởi động...");
            Log.Information("Tham số dòng lệnh: {Args}", string.Join(" ", args));

            // --- Cấu hình System.CommandLine ---
            var configureCommand = new Command("configure", "Chạy quy trình cấu hình ban đầu cho Agent.");
            var debugCommand = new Command("debug", "Chạy Agent ở chế độ debug (console) thay vì Windows Service.");

            var rootCommand = new RootCommand("CMS Agent Service")
            {
                configureCommand,
                debugCommand
            };

            rootCommand.SetHandler(async (InvocationContext context) =>
            {
                // Mặc định chạy như service nếu không có lệnh con nào được gọi
                await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: false, isConfigureModeFromArg: false);
            });

            configureCommand.SetHandler(async () =>
            {
                await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: false, isConfigureModeFromArg: true);
            });

            debugCommand.SetHandler(async () =>
            {
                await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: true, isConfigureModeFromArg: false);
            });

            // Nếu không có lệnh con nào được truyền vào (ví dụ: chỉ chạy CMSAgent.Service.exe)
            // thì System.CommandLine sẽ không gọi SetHandler của rootCommand.
            // Do đó, ta cần kiểm tra args.
            if (args.Length == 0 || (!args.Contains("configure") && !args.Contains("debug")))
            {
                 Log.Information("Không có lệnh 'configure' hoặc 'debug'. Chạy ở chế độ Windows Service mặc định.");
                return await RunAsServiceOrDebugAsync(args, isDebugModeFromArg: false, isConfigureModeFromArg: false);
            }

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task<int> RunAsServiceOrDebugAsync(string[] args, bool isDebugModeFromArg, bool isConfigureModeFromArg)
        {
            IHost? host = null;
            try
            {
                var hostBuilder = CreateHostBuilder(args, isDebugModeFromArg, isConfigureModeFromArg);
                host = hostBuilder.Build();

                // --- Kiểm tra Mutex sau khi ILogger và AppSettings đã được DI ---
                // MutexManager cần AppSettings để lấy AgentInstanceGuid
                _mutexManager = host.Services.GetRequiredService<MutexManager>();
                if (!_mutexManager.RequestOwnership())
                {
                    Log.Fatal("Một instance khác của CMSAgent.Service đã chạy. Thoát ứng dụng.");
                    // Không cần gọi ReleaseOwnership vì chưa giành được
                    return 1; // Exit code cho lỗi
                }

                // --- Nếu là chế độ configure ---
                if (isConfigureModeFromArg)
                {
                    Log.Information("Chạy ở chế độ cấu hình (configure)...");
                    var orchestrator = host.Services.GetRequiredService<IAgentCoreOrchestrator>();
                    bool configSuccess = await orchestrator.RunInitialConfigurationAsync();
                    if (configSuccess)
                    {
                        Log.Information("Cấu hình hoàn tất thành công. Agent sẽ cần được khởi động (như một service) để hoạt động.");
                        // Cân nhắc: có nên tự động start service sau khi configure thành công? (Installer thường làm việc này)
                    }
                    else
                    {
                        Log.Error("Quá trình cấu hình thất bại.");
                    }
                    // Dù thành công hay thất bại, chế độ configure chỉ chạy một lần rồi thoát.
                    return configSuccess ? 0 : 1;
                }

                // --- Chạy Host (Service hoặc Debug Console) ---
                Log.Information("Bắt đầu chạy Host...");
                await host.RunAsync();
                Log.Information("Host đã dừng.");
                return 0; // Thành công
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Hoạt động của Host bị hủy bỏ.");
                return 0; // Không phải lỗi nghiêm trọng
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Lỗi nghiêm trọng xảy ra trong quá trình Host chạy hoặc khởi tạo.");
                return 1; // Exit code cho lỗi
            }
            finally
            {
                _mutexManager?.ReleaseOwnership(); // Đảm bảo giải phóng Mutex
                _mutexManager?.Dispose();

                if (host is IAsyncDisposable asyncDisposableHost)
                {
                    await asyncDisposableHost.DisposeAsync();
                }
                else
                {
                    host?.Dispose();
                }
                SerilogConfigurator.CloseAndFlush(); // Đảm bảo tất cả log được ghi trước khi thoát
            }
        }


        public static IHostBuilder CreateHostBuilder(string[] args, bool isDebugMode, bool isConfigureMode) =>
            Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory) // Đảm bảo đường dẫn gốc đúng khi chạy như service
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;
                    config.SetBasePath(AppContext.BaseDirectory); // Quan trọng cho việc tìm appsettings.json
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                    Log.Information("Đã load cấu hình từ appsettings.json. Environment: {Environment}", env.EnvironmentName);
                })
                .ConfigureLogging((hostingContext, loggingBuilder) =>
                {
                    // Xóa các provider logging mặc định nếu muốn chỉ dùng Serilog
                    loggingBuilder.ClearProviders();
                    // Serilog sẽ được cấu hình trong UseSerilog
                })
                .UseSerilog((hostingContext, services, loggerConfiguration) =>
                {
                    // Lấy IRuntimeConfigManager để có đường dẫn ProgramData
                    // Cần đăng ký IRuntimeConfigManager trước khi UseSerilog được gọi nếu nó là dependency
                    // Hoặc, tạo instance tạm thời ở đây (không lý tưởng)
                    // Cách tốt hơn là SerilogConfigurator không phụ thuộc vào IRuntimeConfigManager trực tiếp
                    // mà nhận đường dẫn ProgramData như một tham số.

                    // Tạm thời, ta sẽ tự xác định ProgramData path ở đây cho Serilog
                    string agentProgramDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        AgentConstants.AgentProgramDataFolderName);
                    Directory.CreateDirectory(Path.Combine(agentProgramDataPath, AgentConstants.LogsSubFolderName)); // Đảm bảo thư mục log tồn tại

                    SerilogConfigurator.Configure(
                        hostingContext.Configuration,
                        agentProgramDataPath,
                        AgentConstants.AgentLogFilePrefix, // Tiền tố cho log của Service
                        isDebugMode || isConfigureMode // Chạy ở console nếu debug hoặc configure
                    );
                    Log.Information("Serilog đã được cấu hình đầy đủ.");
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // --- Đăng ký Cấu hình ---
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));
                    // Đảm bảo AppSettings được load và có AgentInstanceGuid trước khi MutexManager được tạo
                    // Validate AppSettings, đặc biệt là AgentInstanceGuid
                    var appSettings = hostContext.Configuration.GetSection("AppSettings").Get<AppSettings>();
                    if (appSettings == null || string.IsNullOrWhiteSpace(appSettings.AgentInstanceGuid))
                    {
                        // Ghi log bằng logger tạm thời nếu ILogger chưa sẵn sàng
                        Log.Warning("AgentInstanceGuid không được tìm thấy hoặc rỗng trong appsettings.json. " +
                                   "Mutex sẽ sử dụng một GUID mặc định (ít an toàn hơn) hoặc ứng dụng có thể không khởi động đúng cách.");
                        // Cân nhắc việc throw exception ở đây nếu AgentInstanceGuid là bắt buộc.
                        // Nếu không throw, MutexManager sẽ throw khi không có GUID.
                        // Để đơn giản, ta sẽ để MutexManager xử lý.
                    }


                    services.AddSingleton<IRuntimeConfigManager, RuntimeConfigManager>();

                    // --- Đăng ký Shared Services ---
                    services.AddSingleton<IVersionIgnoreManager>(provider =>
                        new VersionIgnoreManager(provider.GetRequiredService<IRuntimeConfigManager>().GetAgentProgramDataPath())
                    );

                    // --- Đăng ký Security ---
                    services.AddSingleton<IDpapiProtector, DpapiProtector>();
                    services.AddSingleton<MutexManager>(); // Singleton vì nó quản lý global resource

                    // --- Đăng ký Communication ---
                    services.AddHttpClient<IAgentApiClient, AgentApiClient>()
                        .AddPolicyHandler((serviceProvider, request) => // Cấu hình Polly retry
                        {
                            var settings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value.HttpRetryPolicy;
                            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PollyHttpRetry");
                            return HttpPolicyExtensions
                                .HandleTransientHttpError()
                                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound && msg.RequestMessage?.Method == HttpMethod.Get)
                                .WaitAndRetryAsync(
                                    retryCount: settings.MaxRetries,
                                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(settings.InitialDelaySeconds, retryAttempt)),
                                    onRetry: (outcome, timespan, retryAttempt, context) =>
                                    {
                                        logger.LogWarning("Thử lại yêu cầu HTTP lần {RetryAttempt}/{MaxRetries} đến {Uri} sau {Timespan}s. Lý do: {StatusCodeOrException}",
                                            retryAttempt, settings.MaxRetries, request.RequestUri, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                                    });
                        });

                    services.AddSingleton<IAgentSocketClient, AgentSocketClient>();

                    // --- Đăng ký Monitoring ---
                    services.AddTransient<IHardwareCollector, HardwareCollector>(); // Transient vì thường chỉ dùng 1 lần khi cần
                    services.AddSingleton<IResourceMonitor, ResourceMonitor>(); // Singleton vì chạy nền liên tục

                    // --- Đăng ký Commands ---
                    services.AddSingleton<ICommandHandlerFactory, CommandHandlerFactory>();
                    services.AddTransient<ConsoleCommandHandler>();
                    services.AddTransient<SystemActionCommandHandler>();
                    services.AddTransient<SoftwareInstallCommandHandler>();
                    services.AddTransient<SoftwareUninstallCommandHandler>();
                    services.AddTransient<GetLogsCommandHandler>();
                    // Đăng ký các ICommandHandler khác ở đây

                    services.AddSingleton<CommandQueue>();

                    // --- Đăng ký Update ---
                    // Func<Task> requestServiceShutdown sẽ được tạo và truyền vào từ AgentCoreOrchestrator hoặc AgentWorker
                    // Hiện tại, ta sẽ để AgentUpdateManager nhận IHostApplicationLifetime để tự yêu cầu dừng
                    services.AddSingleton<IAgentUpdateManager>(provider =>
                        new AgentUpdateManager(
                            provider.GetRequiredService<ILogger<AgentUpdateManager>>(),
                            provider.GetRequiredService<IOptions<AppSettings>>(),
                            provider.GetRequiredService<IAgentApiClient>(),
                            provider.GetRequiredService<IAgentSocketClient>(),
                            provider.GetRequiredService<IVersionIgnoreManager>(),
                            provider.GetRequiredService<IRuntimeConfigManager>(),
                            async () => // Đây là Func<Task> requestServiceShutdown
                            {
                                var lifetime = provider.GetRequiredService<IHostApplicationLifetime>();
                                provider.GetRequiredService<ILogger<AgentUpdateManager>>().LogInformation("Yêu cầu dừng service từ AgentUpdateManager...");
                                lifetime.StopApplication(); // Yêu cầu dừng host
                                await Task.CompletedTask;
                            }
                        )
                    );


                    // --- Đăng ký Orchestration & Worker ---
                    services.AddSingleton<IAgentCoreOrchestrator, AgentCoreOrchestrator>();
                    services.AddHostedService<AgentWorker>(); // Đăng ký worker chính

                    Log.Information("Tất cả các dịch vụ đã được đăng ký.");
                })
                .ConfigureHostOptions(options =>
                {
                    // Đặt thời gian timeout cho việc dừng service
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30); // Ví dụ: 30 giây
                })
                .UseWindowsService(options => // Cấu hình để chạy như Windows Service
                {
                    options.ServiceName = AgentConstants.ServiceName;
                });
    }
}
