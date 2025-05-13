using System;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface để kết nối và giao tiếp qua WebSocket (Socket.IO).
    /// </summary>
    public interface IWebSocketConnector
    {
        /// <summary>
        /// Sự kiện khi nhận được thông điệp từ WebSocket.
        /// </summary>
        event EventHandler<string> MessageReceived;

        /// <summary>
        /// Sự kiện khi kết nối WebSocket bị ngắt.
        /// </summary>
        event EventHandler ConnectionClosed;

        /// <summary>
        /// Kiểm tra xem WebSocket có đang kết nối không.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Kết nối đến server qua WebSocket và xác thực.
        /// </summary>
        /// <param name="agentToken">Token xác thực của agent.</param>
        /// <returns>True nếu kết nối và xác thực thành công, False nếu thất bại.</returns>
        Task<bool> ConnectAsync(string agentToken);

        /// <summary>
        /// Đóng kết nối WebSocket.
        /// </summary>
        /// <returns>Task đại diện cho việc đóng kết nối.</returns>
        Task DisconnectAsync();

        /// <summary>
        /// Gửi cập nhật trạng thái tài nguyên lên server.
        /// </summary>
        /// <param name="payload">Dữ liệu trạng thái tài nguyên.</param>
        /// <returns>Task đại diện cho việc gửi dữ liệu.</returns>
        Task SendStatusUpdateAsync(StatusUpdatePayload payload);

        /// <summary>
        /// Gửi kết quả thực thi lệnh lên server.
        /// </summary>
        /// <param name="payload">Dữ liệu kết quả thực thi lệnh.</param>
        /// <returns>Task đại diện cho việc gửi dữ liệu.</returns>
        Task SendCommandResultAsync(CommandResultPayload payload);
    }
}
