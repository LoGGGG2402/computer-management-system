using CMSAgent.Models.Payloads;

namespace CMSAgent.Monitoring
{
    public interface ISystemMonitor
    {
        /// <summary>
        /// Gets hardware information about the system
        /// </summary>
        Task<HardwareInfoPayload> GetHardwareInfoAsync();

        /// <summary>
        /// Gets current system metrics (CPU, RAM, disk usage)
        /// </summary>
        Task<SystemMetrics> GetSystemMetricsAsync();
    }
}