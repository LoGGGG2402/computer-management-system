using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using CMSAgent.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Cli
{
    /// <summary>
    /// Xử lý và điều phối các lệnh CLI của ứng dụng.
    /// </summary>
    public class CliHandler
    {
        /// <summary>
        /// Cờ xác định một lệnh CLI đã được thực thi.
        /// </summary>
        public bool IsCliCommandExecuted { get; private set; } = false;

        private readonly ILogger<CliHandler> _logger;
        private readonly RootCommand _rootCommand;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Khởi tạo một instance mới của CliHandler.
        /// </summary>
        /// <param name="serviceProvider">Service provider để resolve các lớp xử lý lệnh.</param>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public CliHandler(IServiceProvider serviceProvider, ILogger<CliHandler> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Tạo root command
            _rootCommand = new RootCommand("CMSAgent - Hệ thống quản lý máy tính");
            
            // Đăng ký các lệnh con
            RegisterCommands();
        }

        /// <summary>
        /// Xử lý lệnh từ command line.
        /// </summary>
        /// <param name="args">Các tham số từ command line.</param>
        /// <returns>Mã lỗi của lệnh.</returns>
        public async Task<int> HandleAsync(string[] args)
        {
            try
            {
                return await _rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xử lý được khi thực thi lệnh CLI");
                return -1;
            }
        }

        /// <summary>
        /// Đăng ký các lệnh con vào root command.
        /// </summary>
        private void RegisterCommands()
        {
            // Lệnh configure
            var configureCmd = new Command("configure", "Cấu hình ban đầu cho agent (tương tác)");
            configureCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<ConfigureCommand>();
                context.ExitCode = await cmd.ExecuteAsync(context.GetCancellationToken());
            });
            _rootCommand.AddCommand(configureCmd);

            // Lệnh start
            var startCmd = new Command("start", "Khởi động CMSAgent Windows service");
            startCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<StartCommand>();
                context.ExitCode = await cmd.ExecuteAsync();
            });
            _rootCommand.AddCommand(startCmd);

            // Lệnh stop
            var stopCmd = new Command("stop", "Dừng CMSAgent Windows service");
            stopCmd.SetHandler(async (InvocationContext context) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<StopCommand>();
                context.ExitCode = await cmd.ExecuteAsync();
            });
            _rootCommand.AddCommand(stopCmd);

            // Lệnh uninstall
            var uninstallCmd = new Command("uninstall", "Gỡ bỏ CMSAgent Windows service");
            var removeDataOption = new Option<bool>(
                aliases: new[] { "--remove-data", "-r" },
                description: "Xóa toàn bộ dữ liệu của agent từ ProgramData"
            );
            uninstallCmd.AddOption(removeDataOption);
            uninstallCmd.SetHandler(async (bool removeData) => 
            {
                IsCliCommandExecuted = true;
                var cmd = _serviceProvider.GetRequiredService<UninstallCommand>();
                Environment.ExitCode = await cmd.ExecuteAsync(removeData);
            }, removeDataOption);
            _rootCommand.AddCommand(uninstallCmd);

            // Lệnh debug
            var debugCmd = new Command("debug", "Chạy agent ở chế độ console application");
            debugCmd.SetHandler((InvocationContext context) => 
            {
                // Lưu ý: IsCliCommandExecuted KHÔNG được set true cho lệnh debug
                // Vì lệnh debug sẽ tiếp tục thực thi như bình thường thay vì thoát
                var cmd = _serviceProvider.GetRequiredService<DebugCommand>();
                context.ExitCode = cmd.Execute();
            });
            _rootCommand.AddCommand(debugCmd);
        }
    }
}
