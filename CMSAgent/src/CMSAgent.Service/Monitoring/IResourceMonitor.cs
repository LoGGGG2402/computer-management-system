 // CMSAgent.Service/Monitoring/IResourceMonitor.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Interface định nghĩa các phương thức để giám sát tài nguyên hệ thống (CPU, RAM, Disk).
    /// </summary>
    public interface IResourceMonitor : IDisposable
    {
        /// <summary>
        /// Bắt đầu giám sát tài nguyên.
        /// </summary>
        /// <param name="reportInterval">Khoảng thời gian giữa các lần báo cáo (giây).</param>
        /// <param name="statusUpdateAction">Hành động được gọi để gửi thông tin cập nhật trạng thái (CPU, RAM, Disk usage). </param>
        /// <param name="cancellationToken">Token để hủy bỏ việc giám sát.</param>
        Task StartMonitoringAsync(int reportIntervalSeconds, Func<double, double, double, Task> statusUpdateAction, CancellationToken cancellationToken);

        /// <summary>
        /// Dừng giám sát tài nguyên.
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Lấy giá trị sử dụng CPU hiện tại (%).
        /// </summary>
        float GetCurrentCpuUsage();

        /// <summary>
        /// Lấy giá trị sử dụng RAM hiện tại (%).
        /// </summary>
        float GetCurrentRamUsage();

        /// <summary>
        /// Lấy giá trị sử dụng Disk hiện tại (%) cho ổ đĩa chính (thường là C:).
        /// </summary>
        float GetCurrentDiskUsage();
    }
}
