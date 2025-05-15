using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Core
{
    /// <summary>
    /// Lớp cơ sở cho các dịch vụ worker, cung cấp xử lý lỗi và cơ chế thử lại.
    /// </summary>
    /// <param name="logger">Logger để ghi nhật ký.</param>
    public abstract class WorkerServiceBase(ILogger logger) : BackgroundService
    {
        protected readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private bool _isStopping;

        /// <summary>
        /// Thực thi dịch vụ worker theo chu kỳ với cơ chế thử lại khi gặp lỗi.
        /// </summary>
        /// <param name="stoppingToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình chạy dịch vụ.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ {ServiceName} đang khởi động...", GetType().Name);

            try
            {
                await InitializeAsync(stoppingToken);
                
                stoppingToken.Register(() => 
                {
                    _isStopping = true;
                    _logger.LogInformation("Dịch vụ {ServiceName} đang dừng...", GetType().Name);
                });

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await DoWorkAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        if (_isStopping)
                        {
                            _logger.LogInformation("Tác vụ trong {ServiceName} đã bị hủy do dịch vụ đang dừng", GetType().Name);
                        }
                        else
                        {
                            _logger.LogWarning("Tác vụ trong {ServiceName} đã bị hủy khi đang chạy", GetType().Name);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isStopping)
                        {
                            _logger.LogWarning(ex, "Lỗi trong {ServiceName} khi đang dừng, bỏ qua", GetType().Name);
                            break;
                        }

                        _logger.LogError(ex, "Lỗi không xử lý được trong dịch vụ {ServiceName}", GetType().Name);

                        // Chờ giây trước khi thử lại để tránh vòng lặp quá nhanh khi có lỗi
                        try
                        {
                            await Task.Delay(GetRetryDelay(), stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // Bị hủy trong khi chờ thử lại
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Lỗi nghiêm trọng trong {ServiceName}, dịch vụ dừng hoạt động", GetType().Name);
                throw;
            }
            finally
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong quá trình dọn dẹp {ServiceName}", GetType().Name);
                }

                _logger.LogInformation("Dịch vụ {ServiceName} đã dừng", GetType().Name);
            }
        }

        /// <summary>
        /// Khởi tạo các tài nguyên cần thiết trước khi bắt đầu thực thi công việc chính.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình khởi tạo.</returns>
        protected virtual Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Thực hiện công việc chính của worker.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình thực hiện công việc.</returns>
        protected abstract Task DoWorkAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Dọn dẹp tài nguyên khi dịch vụ dừng lại.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho tiến trình dọn dẹp.</returns>
        protected virtual Task CleanupAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Lấy thời gian chờ trước khi thử lại khi có lỗi.
        /// </summary>
        /// <returns>Thời gian chờ (milliseconds).</returns>
        protected virtual TimeSpan GetRetryDelay()
        {
            return TimeSpan.FromSeconds(5);
        }
    }
}
