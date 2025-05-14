using System;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Lớp xử lý lệnh debug để chạy CMSAgent ở chế độ console application.
    /// </summary>
    public class DebugCommand
    {
        private readonly ILogger<DebugCommand> _logger;

        /// <summary>
        /// Khởi tạo một instance mới của DebugCommand.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public DebugCommand(ILogger<DebugCommand> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Thực thi lệnh debug.
        /// </summary>
        /// <returns>Mã lỗi của lệnh.</returns>
        public int Execute()
        {
            try
            {
                // In thông tin về chế độ debug
                Console.WriteLine("--------------------------------");
                Console.WriteLine("| CMSAgent đang chạy ở chế độ DEBUG |");
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Nhấn CTRL+C để dừng.");
                Console.WriteLine();

                // Ghi log
                _logger.LogInformation("CMSAgent đã khởi động ở chế độ debug");

                // Lệnh debug chỉ hiển thị thông báo, không thực sự làm gì
                // Host được quản lý ở Program.cs
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Lỗi trong chế độ debug: {ex.Message}");
                _logger.LogError(ex, "Lỗi khi khởi chạy chế độ debug");
                return -1;
            }
        }
    }
}
