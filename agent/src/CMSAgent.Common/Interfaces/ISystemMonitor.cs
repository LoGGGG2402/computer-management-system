using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho việc giám sát tài nguyên hệ thống.
    /// </summary>
    public interface ISystemMonitor
    {
        /// <summary>
        /// Khởi tạo các bộ đếm hiệu suất và thành phần giám sát.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Lấy trạng thái sử dụng tài nguyên hiện tại.
        /// </summary>
        /// <returns>Dữ liệu về mức sử dụng CPU, RAM và disk.</returns>
        Task<StatusUpdatePayload> GetCurrentStatusAsync();
    }
}
