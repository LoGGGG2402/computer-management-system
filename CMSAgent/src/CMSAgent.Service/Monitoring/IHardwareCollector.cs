 // CMSAgent.Service/Monitoring/IHardwareCollector.cs
using CMSAgent.Service.Models; // For HardwareInfo
using System.Threading.Tasks;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Interface định nghĩa phương thức để thu thập thông tin phần cứng chi tiết của máy.
    /// </summary>
    public interface IHardwareCollector
    {
        /// <summary>
        /// Thu thập thông tin phần cứng chi tiết của máy client.
        /// Thông tin này bao gồm OS, CPU, GPU, RAM, Disk.
        /// </summary>
        /// <returns>Một đối tượng HardwareInfo chứa các thông tin đã thu thập, hoặc null nếu có lỗi.</returns>
        Task<HardwareInfo?> CollectHardwareInfoAsync();
    }
}
