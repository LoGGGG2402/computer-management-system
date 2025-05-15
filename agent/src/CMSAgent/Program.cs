using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Xml.Linq;
using System.Collections.Generic;
using CMSAgent.Cli;
using CMSAgent.Cli.Commands;
using CMSAgent.Commands;
using CMSAgent.Commands.Handlers;
using CMSAgent.Common.Models;
using CMSAgent.Common.Logging;
using CMSAgent.Communication;
using CMSAgent.Configuration;
using CMSAgent.Core;
using CMSAgent.Monitoring;
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
        private static string _applicationName = "CMSAgent";
        private static string _version = "1.0.0";

        public static async Task<int> Main(string[] args)
        {
            // Đọc thông tin từ csproj file
            LoadProjectInfo();

            // Cấu hình Serilog trước khi khởi tạo host
            ConfigureLogging();

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

        private static void LoadProjectInfo()
        {
            try
            {
                string projectFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CMSAgent.csproj");
                
                // Nếu file không tồn tại ở thư mục hiện tại, thử tìm trong thư mục cha
                if (!File.Exists(projectFilePath))
                {
                    string sourcePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                    
                    // Kiểm tra sourcePath không null trước khi gọi Directory.GetParent
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        string? solutionDir = Directory.GetParent(sourcePath)?.Parent?.FullName;
                        
                        if (solutionDir != null)
                        {
                            string srcPath = Path.Combine(solutionDir, "src", "CMSAgent");
                            projectFilePath = Path.Combine(srcPath, "CMSAgent.csproj");
                        }
                    }
                }

                if (File.Exists(projectFilePath))
                {
                    XDocument doc = XDocument.Load(projectFilePath);
                    var propertyGroups = doc.Descendants("PropertyGroup");
                    
                    foreach (var propertyGroup in propertyGroups)
                    {
                        var description = propertyGroup.Element("Description");
                        if (description != null && !string.IsNullOrWhiteSpace(description.Value))
                        {
                            _applicationName = description.Value;
                        }
                        
                        var version = propertyGroup.Element("Version");
                        if (version != null && !string.IsNullOrWhiteSpace(version.Value))
                        {
                            _version = version.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Sử dụng giá trị mặc định nếu không đọc được từ file
                Console.WriteLine($"Không thể đọc thông tin từ project file: {ex.Message}");
            }
        }

        private static void ConfigureLogging()
        {
            // Đọc cấu hình từ appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Lấy thông tin từ assembly
            var assembly = Assembly.GetExecutingAssembly();
            
            // Thêm thông tin từ assembly vào configuration
            var configDictionary = new Dictionary<string, string?>
            {
                { "Application:Name", assembly.GetName().Name ?? _applicationName },
                { "Application:Version", assembly.GetName().Version?.ToString() ?? _version }
            };

            // Tạo configuration mới kết hợp cấu hình hiện tại và thông tin từ assembly
            var combinedConfig = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddInMemoryCollection(configDictionary)
                .Build();

            // Khởi tạo logger với cấu hình kết hợp
            Log.Logger = (Serilog.ILogger)LoggingSetup.CreateLogger(combinedConfig);

            Log.Information("Ứng dụng {ApplicationName} v{Version} đang khởi động...", _applicationName, _version);
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

                    // Thêm thông tin từ project vào configuration
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"Application:Name", _applicationName},
                        {"Application:Version", _version},
                        {"CMSAgent:AppName", _applicationName},
                        {"CMSAgent:Version", _version}
                    });

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
                .UseSerilog() // Sử dụng Serilog đã được cấu hình trước đó
                .ConfigureServices(ConfigureServices);

            // Cấu hình Windows Service nếu cần
            if (_shouldRunAsWindowsService && OperatingSystem.IsWindows())
            {
                hostBuilder.UseWindowsService(options =>
                {
                    options.ServiceName = _applicationName;
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
