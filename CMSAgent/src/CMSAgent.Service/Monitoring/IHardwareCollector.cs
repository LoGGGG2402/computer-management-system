// CMSAgent.Service/Monitoring/IHardwareCollector.cs
using CMSAgent.Service.Models; // For HardwareInfo
using System.Threading.Tasks;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Interface defining methods to collect detailed hardware information of the machine.
    /// </summary>
    public interface IHardwareCollector
    {
        /// <summary>
        /// Collect detailed hardware information of the client machine.
        /// This information includes OS, CPU, GPU, RAM, Disk.
        /// </summary>
        /// <returns>A HardwareInfo object containing the collected information, or null if there is an error.</returns>
        Task<HardwareInfo?> CollectHardwareInfoAsync();
    }
}
