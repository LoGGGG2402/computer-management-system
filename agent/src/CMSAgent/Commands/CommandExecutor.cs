using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Models;
using CMSAgent.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands
{
    /// <summary>
    /// Quản lý hàng đợi lệnh và thực thi lệnh.
    /// </summary>
    public class CommandExecutor
    {
        private readonly ILogger<CommandExecutor> _logger;
        private readonly CommandHandlerFactory _commandHandlerFactory;
        private readonly IWebSocketConnector _webSocketConnector;
        private readonly CommandExecutorSettingsOptions _settings;
        
        private readonly ConcurrentQueue<CommandPayload> _commandQueue = new();
        private readonly SemaphoreSlim _executionSemaphore;
        private bool _isProcessing = false;

        /// <summary>
        /// Khởi tạo một instance mới của CommandExecutor.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="commandHandlerFactory">Factory để tạo handler thực thi lệnh.</param>
        /// <param name="webSocketConnector">WebSocket connector để gửi kết quả lệnh.</param>
        /// <param name="settingsOptions">Cấu hình thực thi lệnh.</param>
        public CommandExecutor(
            ILogger<CommandExecutor> logger,
            CommandHandlerFactory commandHandlerFactory,
            IWebSocketConnector webSocketConnector,
            IOptions<CommandExecutorSettingsOptions> settingsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _commandHandlerFactory = commandHandlerFactory ?? throw new ArgumentNullException(nameof(commandHandlerFactory));
            _webSocketConnector = webSocketConnector ?? throw new ArgumentNullException(nameof(webSocketConnector));
            _settings = settingsOptions?.Value ?? throw new ArgumentNullException(nameof(settingsOptions));
            
            // Khởi tạo semaphore với số lượng lệnh có thể thực thi đồng thời
            _executionSemaphore = new SemaphoreSlim(_settings.MaxParallelCommands, _settings.MaxParallelCommands);
        }

        /// <summary>
        /// Thử thêm một lệnh vào hàng đợi.
        /// </summary>
        /// <param name="command">Lệnh cần thêm vào hàng đợi.</param>
        /// <returns>True nếu thêm thành công, False nếu hàng đợi đã đầy.</returns>
        public bool TryEnqueueCommand(CommandPayload command)
        {
            ArgumentNullException.ThrowIfNull(command);

            // Kiểm tra xem hàng đợi đã đầy chưa
            if (_commandQueue.Count >= _settings.MaxQueueSize)
            {
                _logger.LogWarning("Không thể thêm lệnh vào hàng đợi: Hàng đợi đã đầy ({QueueSize}/{MaxSize})",
                    _commandQueue.Count, _settings.MaxQueueSize);
                return false;
            }

            _commandQueue.Enqueue(command);
            _logger.LogInformation("Đã thêm lệnh {CommandType} vào hàng đợi: {CommandId} (kích thước hàng đợi: {QueueSize})",
                command.commandType, command.commandId, _commandQueue.Count);
            
            return true;
        }

        /// <summary>
        /// Bắt đầu xử lý các lệnh trong hàng đợi.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho quá trình xử lý lệnh.</returns>
        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Đã có một quá trình xử lý lệnh đang chạy");
                return;
            }

            _isProcessing = true;
            _logger.LogInformation("Bắt đầu xử lý hàng đợi lệnh");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Không có lệnh nào trong hàng đợi, đợi một chút rồi kiểm tra lại
                    if (_commandQueue.IsEmpty)
                    {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    // Lấy lệnh từ hàng đợi
                    if (!_commandQueue.TryDequeue(out var command))
                    {
                        continue;
                    }

                    // Chờ semaphore để đảm bảo không vượt quá số lượng lệnh thực thi đồng thời
                    await _executionSemaphore.WaitAsync(cancellationToken);

                    // Thực thi lệnh trong task riêng
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteCommandAsync(command, cancellationToken);
                        }
                        finally
                        {
                            // Giải phóng semaphore khi hoàn thành
                            _executionSemaphore.Release();
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Quá trình xử lý hàng đợi lệnh đã bị hủy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý hàng đợi lệnh");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Thực thi một lệnh cụ thể.
        /// </summary>
        /// <param name="command">Lệnh cần thực thi.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho việc thực thi lệnh.</returns>
        private async Task ExecuteCommandAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Bắt đầu thực thi lệnh {CommandType}: {CommandId}", 
                command.commandType, command.commandId);

            CommandResultPayload result;

            try
            {
                // Lấy handler phù hợp từ factory
                var handler = _commandHandlerFactory.GetHandler(command.commandType);
                
                // Thực thi lệnh
                result = await handler.ExecuteAsync(command, cancellationToken);
                
                _logger.LogInformation("Đã thực thi lệnh {CommandId} với kết quả: {Success}", 
                    command.commandId, result.success ? "Thành công" : "Thất bại");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực thi lệnh {CommandId}", command.commandId);
                
                // Tạo kết quả lỗi
                result = new CommandResultPayload
                {
                    commandId = command.commandId,
                    type = command.commandType,
                    success = false,
                    result = new CommandResultData
                    {
                        stdout = string.Empty,
                        stderr = string.Empty,
                        errorMessage = $"Lỗi không xử lý được: {ex.Message}",
                        errorCode = string.Empty
                    }
                };
            }
            
            // Gửi kết quả lên server
            await SendCommandResultAsync(result);
        }

        /// <summary>
        /// Gửi kết quả lệnh lên server.
        /// </summary>
        /// <param name="result">Kết quả lệnh cần gửi.</param>
        /// <returns>Task đại diện cho việc gửi kết quả.</returns>
        private async Task SendCommandResultAsync(CommandResultPayload result)
        {
            try
            {
                // Thử gửi kết quả qua WebSocket trước
                if (await _webSocketConnector.SendCommandResultAsync(result))
                {
                    _logger.LogInformation("Đã gửi kết quả lệnh {CommandId} thành công qua WebSocket", 
                        result.commandId);
                    return;
                }
                
                _logger.LogWarning("Không thể gửi kết quả lệnh {CommandId} qua WebSocket (server có thể không khả dụng)", 
                    result.commandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi kết quả lệnh {CommandId}", result.commandId);
            }
        }
    }
}
