// CMSAgent.Service/Orchestration/IAgentCoreOrchestrator.cs
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Orchestration
{
    /// <summary>
    /// Interface định nghĩa các phương thức chính để điều phối các hoạt động của Agent.
    /// </summary>
    public interface IAgentCoreOrchestrator
    {
        /// <summary>
        /// Bắt đầu các hoạt động chính của Agent, bao gồm kết nối server,
        /// khởi chạy các module giám sát, xử lý lệnh, và kiểm tra cập nhật.
        /// Phương thức này sẽ chạy cho đến khi nhận được tín hiệu dừng.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy bỏ các hoạt động.</param>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Dừng tất cả các hoạt động của Agent một cách an toàn.
        /// </summary>
        /// <param name="cancellationToken">Token để giới hạn thời gian dừng.</param>
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Chạy quy trình cấu hình ban đầu cho Agent.
        /// Được gọi khi Agent chạy với tham số "configure".
        /// </summary>
        /// <returns>True nếu cấu hình thành công, ngược lại False.</returns>
        Task<bool> RunInitialConfigurationAsync(CancellationToken cancellationToken = default);
    }
}
