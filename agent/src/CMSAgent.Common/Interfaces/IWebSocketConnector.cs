using System;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface for WebSocket (Socket.IO) connection and communication.
    /// </summary>
    public interface IWebSocketConnector
    {
        /// <summary>
        /// Event triggered when a message is received from WebSocket.
        /// </summary>
        event EventHandler<string> MessageReceived;

        /// <summary>
        /// Event triggered when WebSocket connection is closed.
        /// </summary>
        event EventHandler ConnectionClosed;

        /// <summary>
        /// Checks if WebSocket is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to server via WebSocket and authenticates.
        /// </summary>
        /// <param name="agentToken">Agent authentication token.</param>
        /// <returns>True if connection and authentication successful, False if failed.</returns>
        Task<bool> ConnectAsync(string agentToken);

        /// <summary>
        /// Closes the WebSocket connection.
        /// </summary>
        /// <returns>Task representing the connection closure.</returns>
        Task DisconnectAsync();

        /// <summary>
        /// Sends resource status update to server.
        /// </summary>
        /// <param name="payload">Resource status data.</param>
        /// <returns>Task representing the data transmission.</returns>
        Task SendStatusUpdateAsync(StatusUpdatePayload payload);

        /// <summary>
        /// Sends command execution result to server.
        /// </summary>
        /// <param name="payload">Command execution result data.</param>
        /// <returns>Task representing the data transmission.</returns>
        Task<bool> SendCommandResultAsync(CommandResultPayload payload);
    }
}
