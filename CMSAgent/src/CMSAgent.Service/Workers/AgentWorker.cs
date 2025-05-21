 // CMSAgent.Service/Workers/AgentWorker.cs
using CMSAgent.Service.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Workers
{
    /// <summary>
    /// Worker chính của Agent Service, kế thừa từ BackgroundService.
    /// Chịu trách nhiệm khởi chạy và quản lý vòng đời của AgentCoreOrchestrator.
    /// </summary>
    public class AgentWorker : BackgroundService
    {
        private readonly ILogger<AgentWorker> _logger;
        private readonly IAgentCoreOrchestrator _orchestrator;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public AgentWorker(
            ILogger<AgentWorker> logger,
            IAgentCoreOrchestrator orchestrator,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _hostApplicationLifetime = hostApplicationLifetime ?? throw new ArgumentNullException(nameof(hostApplicationLifetime));
        }

        /// <summary>
        /// Phương thức chính được gọi khi HostedService bắt đầu.
        /// Nó sẽ khởi chạy AgentCoreOrchestrator và giữ cho worker chạy cho đến khi nhận được tín hiệu dừng.
        /// </summary>
        /// <param name="stoppingToken">Token được kích hoạt khi service được yêu cầu dừng.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AgentWorker đang bắt đầu lúc: {time}", DateTimeOffset.Now);

            // Đăng ký một callback khi ứng dụng được yêu cầu dừng (ví dụ: Ctrl+C, shutdown)
            // để có thể gọi StopAsync của orchestrator một cách an toàn.
            stoppingToken.Register(async () =>
            {
                _logger.LogInformation("AgentWorker nhận được tín hiệu dừng từ stoppingToken.");
                await StopOrchestratorAsync();
            });

            try
            {
                // Khởi chạy logic chính của Agent thông qua Orchestrator
                // Orchestrator.StartAsync sẽ chứa vòng lặp chính hoặc các tác vụ nền dài hạn.
                // Nó cũng nên tôn trọng stoppingToken được truyền vào.
                await _orchestrator.StartAsync(stoppingToken);

                // Nếu StartAsync của orchestrator kết thúc mà không có lỗi và stoppingToken chưa được yêu cầu,
                // điều đó có thể có nghĩa là orchestrator đã hoàn thành công việc của nó một cách bất thường (nếu nó được thiết kế để chạy vô hạn).
                // Hoặc, nếu orchestrator được thiết kế để chạy một lần rồi kết thúc, thì đây là hành vi bình thường.
                // Trong trường hợp của một agent chạy nền liên tục, StartAsync thường không nên kết thúc trừ khi có lỗi hoặc stoppingToken được kích hoạt.
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("AgentCoreOrchestrator.StartAsync đã kết thúc mà không có yêu cầu dừng. Service có thể sẽ dừng.");
                    // Nếu orchestrator kết thúc sớm, ta có thể muốn dừng toàn bộ host application.
                    _hostApplicationLifetime.StopApplication();
                }
            }
            catch (OperationCanceledException)
            {
                // Điều này xảy ra khi stoppingToken được kích hoạt trong khi StartAsync đang chạy.
                _logger.LogInformation("Hoạt động của AgentWorker bị hủy bỏ (OperationCanceledException).");
                // StopOrchestratorAsync đã được đăng ký với stoppingToken.Register, nên không cần gọi lại ở đây.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Lỗi nghiêm trọng không xử lý được trong AgentWorker.ExecuteAsync. Service sẽ dừng.");
                // Trong trường hợp lỗi nghiêm trọng, dừng toàn bộ ứng dụng.
                _hostApplicationLifetime.StopApplication();
            }
            finally
            {
                _logger.LogInformation("AgentWorker.ExecuteAsync đã kết thúc.");
            }
        }

        /// <summary>
        /// Được gọi khi service bắt đầu.
        /// </summary>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentWorker.StartAsync được gọi.");
            // Thực hiện các tác vụ khởi tạo bổ sung nếu cần, trước khi ExecuteAsync được gọi.
            // Ví dụ: kiểm tra các điều kiện tiên quyết.
            // Tuy nhiên, logic khởi tạo chính của agent nên nằm trong _orchestrator.StartAsync().
            await base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Được gọi khi service được yêu cầu dừng.
        /// Phương thức này nên giải phóng tài nguyên và dừng các tác vụ đang chạy một cách an toàn.
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentWorker.StopAsync được gọi.");

            // Gọi StopOrchestratorAsync để đảm bảo orchestrator được dừng đúng cách.
            // stoppingToken trong ExecuteAsync đã đăng ký việc này, nhưng gọi ở đây để chắc chắn.
            // Truyền cancellationToken của StopAsync vào để có giới hạn thời gian cho việc dừng.
            await StopOrchestratorAsync(cancellationToken);

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("AgentWorker đã dừng hoàn toàn.");
        }

        /// <summary>
        /// Phương thức helper để dừng Orchestrator một cách an toàn.
        /// </summary>
        private async Task StopOrchestratorAsync(CancellationToken externalToken = default)
        {
            _logger.LogInformation("Đang cố gắng dừng AgentCoreOrchestrator...");
            try
            {
                // Tạo một CancellationTokenSource với timeout nếu cần,
                // để đảm bảo việc dừng không bị treo vô hạn.
                // Hoặc sử dụng externalToken nếu được cung cấp.
                CancellationToken effectiveToken = externalToken == default ? new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token : externalToken;
                await _orchestrator.StopAsync(effectiveToken);
                _logger.LogInformation("AgentCoreOrchestrator đã được dừng.");
            }
            catch (OperationCanceledException)
            {
                 _logger.LogWarning("Quá trình dừng AgentCoreOrchestrator bị hủy (timeout hoặc yêu cầu từ bên ngoài).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dừng AgentCoreOrchestrator.");
            }
        }
    }
}
