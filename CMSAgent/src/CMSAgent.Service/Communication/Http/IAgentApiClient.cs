 // CMSAgent.Service/Communication/Http/IAgentApiClient.cs
using CMSAgent.Service.Models; // For HardwareInfo, UpdateNotification
using CMSAgent.Service.Configuration.Models; // For PositionInfo
using CMSAgent.Shared.Models; // For AgentErrorReport

namespace CMSAgent.Service.Communication.Http
{
    /// <summary>
    /// Interface định nghĩa các phương thức giao tiếp HTTP với Server của CMS.
    /// </summary>
    public interface IAgentApiClient
    {
        /// <summary>
        /// Xác định Agent với Server và lấy token hoặc yêu cầu MFA.
        /// API: POST /api/agent/identify
        /// </summary>
        /// <param name="agentId">ID của Agent.</param>
        /// <param name="positionInfo">Thông tin vị trí của Agent.</param>
        /// <param name="forceRenewToken">True nếu muốn yêu cầu Server cấp token mới (cho việc làm mới token định kỳ).</param>
        /// <param name="cancellationToken">Token để hủy bỏ yêu cầu.</param>
        /// <returns>Một tuple chứa trạng thái (ví dụ: "mfa_required", "success", "position_error") và agentToken (nếu có).</returns>
        Task<(string Status, string? AgentToken, string? ErrorMessage)> IdentifyAgentAsync(string agentId, PositionInfo positionInfo, bool forceRenewToken = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Xác minh mã MFA cho Agent.
        /// API: POST /api/agent/verify-mfa
        /// </summary>
        /// <param name="agentId">ID của Agent.</param>
        /// <param name="mfaCode">Mã MFA do người dùng nhập.</param>
        /// <param name="cancellationToken">Token để hủy bỏ yêu cầu.</param>
        /// <returns>Một tuple chứa trạng thái ("success" hoặc lỗi) và agentToken (nếu thành công).</returns>
        Task<(string Status, string? AgentToken, string? ErrorMessage)> VerifyMfaAsync(string agentId, string mfaCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi thông tin phần cứng của Agent lên Server.
        /// API: POST /api/agent/hardware-info
        /// </summary>
        /// <param name="hardwareInfo">Đối tượng chứa thông tin phần cứng.</param>
        /// <param name="cancellationToken">Token để hủy bỏ yêu cầu.</param>
        /// <returns>True nếu gửi thành công, ngược lại False.</returns>
        Task<bool> ReportHardwareInfoAsync(HardwareInfo hardwareInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi báo cáo lỗi từ Agent lên Server.
        /// API: POST /api/agent/report-error
        /// </summary>
        /// <param name="errorReport">Đối tượng chứa thông tin lỗi.</param>
        /// <param name="cancellationToken">Token để hủy bỏ yêu cầu.</param>
        /// <returns>True nếu gửi thành công, ngược lại False.</returns>
        Task<bool> ReportErrorAsync(AgentErrorReport errorReport, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra phiên bản cập nhật mới cho Agent.
        /// API: GET /api/agent/check-update?current_version={current_version}
        /// </summary>
        /// <param name="currentVersion">Phiên bản hiện tại của Agent.</param>
        /// <param name="cancellationToken">Token để hủy bỏ yêu cầu.</param>
        /// <returns>Đối tượng UpdateNotification chứa thông tin cập nhật nếu có, hoặc null nếu không có hoặc lỗi.</returns>
        Task<UpdateNotification?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tải gói cập nhật Agent từ Server.
        /// API: GET /api/agent/agent-packages/{filename}
        /// </summary>
        /// <param name="filename">Tên file của gói cập nhật.</param>
        /// <param name="destinationPath">Đường dẫn đầy đủ để lưu file tải về.</param>
        /// <param name="cancellationToken">Token để hủy bỏ yêu cầu.</param>
        /// <returns>True nếu tải và lưu file thành công, ngược lại False.</returns>
        Task<bool> DownloadAgentPackageAsync(string filename, string destinationPath, CancellationToken cancellationToken = default);
    }
}
