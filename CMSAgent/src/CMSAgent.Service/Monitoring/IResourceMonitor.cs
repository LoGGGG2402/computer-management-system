
namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Interface defining methods to monitor system resources (CPU, RAM, Disk).
    /// </summary>
    public interface IResourceMonitor : IDisposable
    {
        /// <summary>
        /// Start monitoring resources.
        /// </summary>
        /// <param name="reportInterval">Time interval between reports (seconds).</param>
        /// <param name="statusUpdateAction">Action to be called to send status updates (CPU, RAM, Disk usage).</param>
        /// <param name="cancellationToken">Token to cancel monitoring.</param>
        Task StartMonitoringAsync(int reportIntervalSeconds, Func<double, double, double, Task> statusUpdateAction, CancellationToken cancellationToken);

        /// <summary>
        /// Stop monitoring resources.
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Get current CPU usage (%).
        /// </summary>
        float GetCurrentCpuUsage();

        /// <summary>
        /// Get current RAM usage (%).
        /// </summary>
        float GetCurrentRamUsage();

        /// <summary>
        /// Get current Disk usage (%) for main drive (usually C:).
        /// </summary>
        float GetCurrentDiskUsage();
    }
}
