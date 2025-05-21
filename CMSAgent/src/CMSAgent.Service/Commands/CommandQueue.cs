 // CMSAgent.Service/Commands/CommandQueue.cs
using CMSAgent.Service.Commands.Factory;
using CMSAgent.Service.Commands.Models;
using CMSAgent.Service.Communication.WebSocket; // For IAgentSocketClient (để gửi kết quả)
using CMSAgent.Service.Configuration.Models; // For AppSettings
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Channels; // For Channel<T>
using System.Threading.Tasks;
using System.Threading; // For CancellationToken
using System.Collections.Generic; // For List of Tasks

namespace CMSAgent.Service.Commands
{
    /// <summary>
    /// Quản lý hàng đợi và thực thi các lệnh nhận được từ Server.
    /// </summary>
    public class CommandQueue : IAsyncDisposable
    {
        private readonly ILogger<CommandQueue> _logger;
        private readonly ICommandHandlerFactory _handlerFactory;
        private readonly IAgentSocketClient _socketClient;
        private readonly AppSettings _appSettings;
        private readonly Channel<CommandRequest> _queue;
        private readonly List<Task> _workerTasks;
        private CancellationTokenSource? _cts; // Dùng để dừng các worker task

        public CommandQueue(
            ILogger<CommandQueue> logger,
            ICommandHandlerFactory handlerFactory,
            IAgentSocketClient socketClient,
            IOptions<AppSettings> appSettingsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
            _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            // Tạo Channel với giới hạn kích thước từ AppSettings
            var channelOptions = new BoundedChannelOptions(_appSettings.CommandExecution.MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait, // Chờ nếu hàng đợi đầy (hoặc DropWrite)
                SingleReader = false, // Nhiều worker có thể đọc
                SingleWriter = true   // Chỉ có một nguồn ghi (ví dụ: từ WebSocket event)
            };
            _queue = Channel.CreateBounded<CommandRequest>(channelOptions);
            _workerTasks = new List<Task>();
        }

        /// <summary>
        /// Bắt đầu các worker để xử lý lệnh từ hàng đợi.
        /// </summary>
        /// <param name="cancellationToken">Token để dừng các worker.</param>
        public void StartProcessing(CancellationToken cancellationToken)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _logger.LogWarning("CommandQueue processing đã được bắt đầu.");
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int numberOfWorkers = _appSettings.CommandExecution.MaxParallelCommands;
            if (numberOfWorkers <= 0) numberOfWorkers = 1; // Ít nhất 1 worker

            _logger.LogInformation("Bắt đầu {NumberOfWorkers} worker(s) để xử lý hàng đợi lệnh.", numberOfWorkers);

            for (int i = 0; i < numberOfWorkers; i++)
            {
                var workerId = i + 1;
                _workerTasks.Add(Task.Run(async () => await ProcessQueueAsync(workerId, _cts.Token)));
            }
        }

        /// <summary>
        /// Dừng xử lý hàng đợi và chờ các worker hoàn thành.
        /// </summary>
        public async Task StopProcessingAsync()
        {
            _logger.LogInformation("Đang yêu cầu dừng xử lý hàng đợi lệnh...");
            _queue.Writer.Complete(); // Báo hiệu không còn item nào được thêm vào

            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // Gửi tín hiệu hủy cho các worker
            }

            // Chờ tất cả các worker task hoàn thành
            // Có thể thêm timeout ở đây nếu cần
            if (_workerTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(_workerTasks);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Một hoặc nhiều command worker đã bị hủy.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi chờ các command worker dừng.");
                }
                _workerTasks.Clear();
            }
            _logger.LogInformation("Xử lý hàng đợi lệnh đã dừng.");
        }

        /// <summary>
        /// Thêm một yêu cầu lệnh vào hàng đợi.
        /// </summary>
        /// <param name="commandRequest">Yêu cầu lệnh.</param>
        /// <returns>True nếu thêm thành công, False nếu hàng đợi đã đóng hoặc bị hủy.</returns>
        public async Task<bool> EnqueueCommandAsync(CommandRequest commandRequest)
        {
            if (commandRequest == null)
            {
                _logger.LogWarning("Không thể thêm command request null vào hàng đợi.");
                return false;
            }

            if (_queue.Writer.Completion.IsCompleted)
            {
                _logger.LogWarning("Không thể thêm lệnh vào hàng đợi đã đóng. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }

            try
            {
                // Thử ghi vào channel, có thể chờ nếu hàng đợi đầy (tùy theo BoundedChannelFullMode)
                await _queue.Writer.WriteAsync(commandRequest, _cts?.Token ?? CancellationToken.None);
                _logger.LogInformation("Đã thêm lệnh ID: {CommandId}, Type: {CommandType} vào hàng đợi.", commandRequest.CommandId, commandRequest.CommandType);
                return true;
            }
            catch (ChannelClosedException)
            {
                _logger.LogWarning("Không thể thêm lệnh vào hàng đợi đã đóng. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }
            catch (OperationCanceledException)
            {
                 _logger.LogWarning("Thao tác thêm lệnh vào hàng đợi bị hủy. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm lệnh vào hàng đợi. CommandId: {CommandId}", commandRequest.CommandId);
                return false;
            }
        }

        private async Task ProcessQueueAsync(int workerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Command worker {WorkerId} đã bắt đầu.", workerId);
            try
            {
                // Đọc từ channel cho đến khi nó bị đóng và rỗng
                await foreach (var commandRequest in _queue.Reader.ReadAllAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    _logger.LogInformation("Worker {WorkerId} đang xử lý lệnh ID: {CommandId}, Type: {CommandType}",
                        workerId, commandRequest.CommandId, commandRequest.CommandType);

                    ICommandHandler? handler = _handlerFactory.CreateHandler(commandRequest.CommandType);
                    if (handler == null)
                    {
                        _logger.LogWarning("Worker {WorkerId}: Không tìm thấy handler cho CommandType: {CommandType}, CommandId: {CommandId}. Bỏ qua lệnh.",
                            workerId, commandRequest.CommandType, commandRequest.CommandId);
                        // Gửi kết quả lỗi về server
                        var errorResult = new CommandResult
                        {
                            CommandId = commandRequest.CommandId,
                            CommandType = commandRequest.CommandType,
                            Success = false,
                            Result = new CommandOutputResult { ErrorMessage = $"Unsupported command type: {commandRequest.CommandType}", ExitCode = -100 }
                        };
                        await SendResultToServerAsync(errorResult);
                        continue;
                    }

                    CommandResult result = await handler.ExecuteAsync(commandRequest, cancellationToken);
                    await SendResultToServerAsync(result);

                    _logger.LogInformation("Worker {WorkerId} đã xử lý xong lệnh ID: {CommandId}. Success: {Success}",
                        workerId, commandRequest.CommandId, result.Success);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Command worker {WorkerId} bị hủy bỏ.", workerId);
            }
            catch (ChannelClosedException) // Channel đã bị đóng khi đang đọc (ReadAllAsync)
            {
                _logger.LogInformation("Command worker {WorkerId}: Channel đã đóng, kết thúc xử lý.", workerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng trong command worker {WorkerId}.", workerId);
                // Cân nhắc có nên dừng toàn bộ service nếu worker bị lỗi không thể phục hồi
            }
            finally
            {
                _logger.LogInformation("Command worker {WorkerId} đã dừng.", workerId);
            }
        }

        private async Task SendResultToServerAsync(CommandResult result)
        {
            if (_socketClient.IsConnected)
            {
                try
                {
                    await _socketClient.SendCommandResultAsync(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gửi kết quả lệnh ID: {CommandId} về server.", result.CommandId);
                    // Cân nhắc lưu kết quả vào hàng đợi offline nếu gửi thất bại
                }
            }
            else
            {
                _logger.LogWarning("WebSocket không kết nối. Không thể gửi kết quả lệnh ID: {CommandId} về server.", result.CommandId);
                // Lưu kết quả vào hàng đợi offline
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing CommandQueue...");
            await StopProcessingAsync(); // Đảm bảo các worker đã dừng
            _cts?.Dispose();
            _logger.LogInformation("CommandQueue disposed.");
            GC.SuppressFinalize(this);
        }
    }
}
