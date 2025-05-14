using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CMSAgent.Cli;
using CMSAgent.Cli.Commands;
using CMSAgent.Commands;
using CMSAgent.Commands.Handlers;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Communication;
using CMSAgent.Configuration;
using CMSAgent.Core;
using CMSAgent.Logging;
using CMSAgent.Monitoring;
using CMSAgent.Persistence;
using CMSAgent.Security;
using CMSAgent.Update;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Registry;
using Polly.Timeout;
using Serilog;

namespace CMSAgent
{
    public class Program
    {
        private static bool _shouldRunAsWindowsService = true;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                IHost host = CreateHostBuilder(args).Build();
                
                // Xử lý các lệnh CLI
                var cliHandler = host.Services.GetRequiredService<CliHandler>();
                int cliResult = await cliHandler.HandleAsync(args);
                if (cliHandler.IsCliCommandExecuted)
                {
                    return cliResult; // Lệnh CLI đã được xử lý
                }

                // Nếu không có lệnh CLI, chạy dịch vụ
                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ứng dụng gặp lỗi nghiêm trọng và phải kết thúc");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;
                    
                    config.SetBasePath(env.ContentRootPath)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables("CMSAGENT_");

                    // Tạo thư mục dữ liệu nếu cần
                    var configuration = config.Build();
                    var dataDir = configuration["CMSAgent:DataDirectoryPath"] ?? "AppData";
                    var dataPath = Path.Combine(env.ContentRootPath, dataDir);
                    if (!Directory.Exists(dataPath))
                    {
                        Directory.CreateDirectory(dataPath);
                    }
                    
                    // Xác định nếu cần chạy như một Windows Service
                    _shouldRunAsWindowsService = !(args.Length > 0 && args[0].Equals("debug", StringComparison.OrdinalIgnoreCase));
                })
                .ConfigureSerilog()
                .ConfigureServices(ConfigureServices);

            // Cấu hình Windows Service nếu cần
            if (_shouldRunAsWindowsService && OperatingSystem.IsWindows())
            {
                hostBuilder.UseWindowsService(options =>
                {
                    options.ServiceName = "CMSAgent";
                });
            }

            return hostBuilder;
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Đăng ký cấu hình từ appsettings
            services.AddOptions<CmsAgentSettingsOptions>()
                .Bind(context.Configuration.GetSection("CMSAgent"))
                .ValidateDataAnnotations();
            
            services.AddOptions<AgentSpecificSettingsOptions>()
                .Bind(context.Configuration.GetSection("CMSAgent:AgentSpecificSettings"))
                .ValidateDataAnnotations();
            
            services.AddOptions<HttpClientSettingsOptions>()
                .Bind(context.Configuration.GetSection("CMSAgent:HttpClientSettings"))
                .ValidateDataAnnotations();

            // Đăng ký Polly policies
            var policyRegistry = new PolicyRegistry();
            
            // Retry policy
            var httpRetryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: context.Configuration.GetValue<int>("CMSAgent:HttpClientSettings:MaxRetryAttempts", 3),
                    sleepDurationProvider: (retryAttempt) => 
                        TimeSpan.FromMilliseconds(
                            Math.Pow(2, retryAttempt) * context.Configuration.GetValue<int>("CMSAgent:HttpClientSettings:RetryDelayMilliseconds", 200)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        // Log retry attempt
                        Log.Warning("Đang thử lại request HTTP lần {RetryAttempt} sau {RetryInterval}ms",
                            retryAttempt, timespan.TotalMilliseconds);
                    });
            
            // Circuit Breaker policy
            var httpCircuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay) =>
                    {
                        Log.Warning("Circuit Breaker mở trong {BreakDelay}s do lỗi liên tục", breakDelay.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        Log.Information("Circuit Breaker đã đóng, kết nối hoạt động trở lại");
                    });
            
            // Timeout policy
            var httpTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                context.Configuration.GetValue<int>("CMSAgent:HttpClientSettings:TimeoutSeconds", 30));
            
            // Add policies to registry
            policyRegistry.Add("HttpRetryPolicy", httpRetryPolicy);
            policyRegistry.Add("HttpCircuitBreakerPolicy", httpCircuitBreakerPolicy);
            policyRegistry.Add("HttpTimeoutPolicy", httpTimeoutPolicy);
            
            // Add policy registry to services
            services.AddSingleton<IReadOnlyPolicyRegistry<string>>(policyRegistry);

            // Đăng ký HttpClient với Polly
            services.AddHttpClient(HttpClientNames.ApiClient, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<CmsAgentSettingsOptions>>();
                client.BaseAddress = new Uri(options.Value.ServerUrl);
                client.Timeout = TimeSpan.FromSeconds(options.Value.HttpClientSettings.RequestTimeoutSec);
                client.DefaultRequestHeaders.Add("User-Agent", $"CMSAgent/{options.Value.Version}");
            })
            .AddPolicyHandler(httpRetryPolicy)
            .AddPolicyHandler(httpCircuitBreakerPolicy)
            .AddPolicyHandler(httpTimeoutPolicy);

            // Đăng ký singleton services
            services.AddSingleton<StateManager>();
            services.AddSingleton<ConfigLoader>();
            services.AddSingleton<HttpClientWrapper>();
            services.AddSingleton<WebSocketConnector>();
            services.AddSingleton<SystemMonitor>();
            services.AddSingleton<HardwareInfoCollector>();
            services.AddSingleton<UpdateHandler>();
            services.AddSingleton<CommandExecutor>();
            services.AddSingleton<OfflineQueueManager>();
            
            // Đăng ký singleton mutex
            services.AddSingleton<SingletonMutex>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CmsAgentSettingsOptions>>();
                var logger = provider.GetRequiredService<ILogger<SingletonMutex>>();
                return new SingletonMutex(options.Value.AppName, logger);
            });

            // Đăng ký Command Handlers
            services.AddTransient<ConsoleCommandHandler>();
            services.AddTransient<SystemActionCommandHandler>();
            services.AddTransient<GetLogsCommandHandler>();

            // Đăng ký CLI Handlers
            services.AddTransient<ServiceUtils>();
            services.AddTransient<ConfigureCommand>();
            services.AddTransient<StartCommand>();
            services.AddTransient<StopCommand>();
            services.AddTransient<UninstallCommand>();
            services.AddTransient<DebugCommand>();
            services.AddSingleton<CliHandler>();

            // Đăng ký dịch vụ chính
            services.AddHostedService<AgentService>();

            // Thay thế bằng
            services.AddSingleton<TokenProtector>();
            services.AddSingleton<CommandHandlerFactory>();
        }
    }

    /// <summary>
    /// Tên của HttpClient được đăng ký trong DI
    /// </summary>
    public static class HttpClientNames
    {
        /// <summary>
        /// HttpClient được sử dụng cho API requests
        /// </summary>
        public const string ApiClient = "ApiClient";
        
        /// <summary>
        /// HttpClient được sử dụng cho các file lớn
        /// </summary>
        public const string DownloadClient = "DownloadClient";
    }
}
