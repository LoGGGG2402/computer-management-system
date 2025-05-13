using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands
{
    /// <summary>
    /// Quản lý hàng đợi lệnh và thực thi lệnh.
    /// </summary>
    public class CommandExecutor : ICommandExecutor
    {
        private readonly ILogger<CommandExecutor> _logger;
        private readonly ICommandHandlerFactory _handlerFactory;
        private readonly IWebSocketConnector _webSocketConnector;
        private readonly IOfflineQueueManager _offlineQueueManager;
        private readonly CommandExecutorSettingsOptions _settings;
        
        private readonly ConcurrentQueue<CommandPayload> _commandQueue = new ConcurrentQueue<CommandPayload>();
        private readonly SemaphoreSlim _executionSemaphore;
        private bool _isProcessing = false;

        /// <summary>
        /// Khởi tạo một instance mới của CommandExecutor.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="handlerFactory">Factory để tạo command handler.</param>
        /// <param name="webSocketConnector">WebSocket connector để gửi kết quả lệnh.</param>
        /// <param name="offlineQueueManager">Manager của queue offline.</param>
        /// <param name="options">Cấu hình thực thi lệnh.</param>
        public CommandExecutor(
            ILogger<CommandExecutor> logger,
            ICommandHandlerFactory handlerFactory,
            IWebSocketConnector webSocketConnector,
            IOfflineQueueManager offlineQueueManager,
            IOptions<CommandExecutorSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
            _webSocketConnector = webSocketConnector ?? throw new ArgumentNullException(nameof(webSocketConnector));
            _offlineQueueManager = offlineQueueManager ?? throw new ArgumentNullException(nameof(offlineQueueManager));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            
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
            if (command == null)
                throw new ArgumentNullException(nameof(command));

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

            CommandResultPayload result = null;

            try
            {
                // Lấy handler phù hợp cho loại lệnh
                var handler = _handlerFactory.GetHandler(command.commandType);
                
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
                        errorMessage = $"Lỗi không xử lý được: {ex.Message}"
                    }
                };
            }
            
            // Gửi kết quả lên server (hoặc thêm vào queue offline nếu mất kết nối)
            await SendCommandResultAsync(result);
        }

        /// <summary>
        /// Gửi kết quả lệnh lên server hoặc thêm vào queue offline.
        /// </summary>
        /// <param name="result">Kết quả lệnh cần gửi.</param>
        /// <returns>Task đại diện cho việc gửi kết quả.</returns>
        private async Task SendCommandResultAsync(CommandResultPayload result)
        {
            try
            {
                // Gửi qua WebSocket nếu đang kết nối
                if (_webSocketConnector.IsConnected)
                {
                    await _webSocketConnector.SendCommandResultAsync(result);
                    return;
                }
                
                // Không có kết nối, thêm vào queue offline
                _logger.LogInformation("WebSocket không được kết nối, thêm kết quả lệnh {CommandId} vào hàng đợi offline", 
                    result.commandId);
                
                await _offlineQueueManager.EnqueueCommandResultAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi kết quả lệnh {CommandId}", result.commandId);
                
                // Thử thêm vào queue offline trong trường hợp lỗi
                try
                {
                    await _offlineQueueManager.EnqueueCommandResultAsync(result);
                }
                catch (Exception queueEx)
                {
                    _logger.LogError(queueEx, "Không thể thêm kết quả lệnh {CommandId} vào hàng đợi offline", 
                        result.commandId);
                }
            }
        }
    }
}
