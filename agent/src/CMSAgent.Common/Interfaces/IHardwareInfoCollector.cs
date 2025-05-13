using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho việc thu thập thông tin phần cứng của hệ thống.
    /// </summary>
    public interface IHardwareInfoCollector
    {
        /// <summary>
        /// Thu thập thông tin phần cứng của hệ thống.
        /// </summary>
        /// <returns>Thông tin phần cứng đã thu thập.</returns>
        Task<HardwareInfoPayload> CollectHardwareInfoAsync();
    }
} 