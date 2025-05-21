 // CMSAgent.Service/Communication/WebSocket/IAgentSocketClient.cs
using System;
using System.Threading.Tasks;
using CMSAgent.Service.Commands.Models; // For CommandRequest, CommandResult
using CMSAgent.Service.Models; // For UpdateNotification (nếu server gửi qua WS)

namespace CMSAgent.Service.Communication.WebSocket
{
    /// <summary>
    /// Interface định nghĩa các phương thức và sự kiện cho việc giao tiếp WebSocket với Server.
    /// </summary>
    public interface IAgentSocketClient : IAsyncDisposable
    {
        /// <summary>
        /// Sự kiện được kích hoạt khi kết nối WebSocket được thiết lập thành công và xác thực.
        /// </summary>
        event Func<Task>? Connected;

        /// <summary>
        /// Sự kiện được kích hoạt khi kết nối WebSocket bị ngắt.
        /// </summary>
        event Func<Exception?, Task>? Disconnected; // Exception có thể là null nếu ngắt kết nối chủ động

        /// <summary>
        /// Sự kiện được kích hoạt khi xác thực WebSocket thất bại.
        /// </summary>
        event Func<string, Task>? AuthenticationFailed; // string là thông điệp lỗi

        /// <summary>
        /// Sự kiện được kích hoạt khi Server gửi một yêu cầu thực thi lệnh.
        /// Payload là đối tượng CommandRequest.
        /// </summary>
        event Func<CommandRequest, Task>? CommandReceived;

        /// <summary>
        /// Sự kiện được kích hoạt khi Server thông báo có phiên bản Agent mới.
        /// Payload là đối tượng UpdateNotification.
        /// </summary>
        event Func<UpdateNotification, Task>? NewVersionAvailableReceived;


        /// <summary>
        /// Bắt đầu kết nối đến WebSocket server.
        /// </summary>
        /// <param name="agentId">ID của Agent.</param>
        /// <param name="agentToken">Token xác thực của Agent.</param>
        /// <param name="cancellationToken">Token để hủy bỏ việc cố gắng kết nối.</param>
        Task ConnectAsync(string agentId, string agentToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ngắt kết nối WebSocket một cách chủ động.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Gửi trạng thái tài nguyên của Agent lên Server.
        /// Sự kiện: agent:status_update
        /// </summary>
        /// <param name="cpuUsage">Phần trăm sử dụng CPU.</param>
        /// <param name="ramUsage">Phần trăm sử dụng RAM.</param>
        /// <param name="diskUsage">Phần trăm sử dụng Disk.</param>
        Task SendStatusUpdateAsync(double cpuUsage, double ramUsage, double diskUsage);

        /// <summary>
        /// Gửi kết quả thực thi lệnh về Server.
        /// Sự kiện: agent:command_result
        /// </summary>
        /// <param name="commandResult">Đối tượng chứa kết quả lệnh.</param>
        Task SendCommandResultAsync(CommandResult commandResult);

        /// <summary>
        /// Gửi thông báo về trạng thái quá trình cập nhật của agent.
        /// Ví dụ: agent:update_status với payload { "status": "update_started", "target_version": "<new_version>" }
        /// </summary>
        /// <param name="statusPayload">Đối tượng chứa thông tin trạng thái cập nhật.</param>
        Task SendUpdateStatusAsync(object statusPayload);


        /// <summary>
        /// Kiểm tra xem client có đang kết nối hay không.
        /// </summary>
        bool IsConnected { get; }
    }
}
