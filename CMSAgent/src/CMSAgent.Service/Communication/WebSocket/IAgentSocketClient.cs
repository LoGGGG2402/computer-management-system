using CMSAgent.Service.Commands.Models; // For CommandRequest, CommandResult
using CMSAgent.Service.Models; // For UpdateNotification (if server sends via WS)

namespace CMSAgent.Service.Communication.WebSocket
{
    /// <summary>
    /// Interface defining methods and events for WebSocket communication with Server.
    /// </summary>
    public interface IAgentSocketClient : IAsyncDisposable
    {
        /// <summary>
        /// Event triggered when WebSocket connection is successfully established and authenticated.
        /// </summary>
        event Func<Task>? Connected;

        /// <summary>
        /// Event triggered when WebSocket connection is disconnected.
        /// </summary>
        event Func<Exception?, Task>? Disconnected; // Exception can be null if disconnected actively

        /// <summary>
        /// Event triggered when WebSocket authentication fails.
        /// </summary>
        event Func<string, Task>? AuthenticationFailed; // string is error message

        /// <summary>
        /// Event triggered when Server sends a command execution request.
        /// Payload is CommandRequest object.
        /// </summary>
        event Func<CommandRequest, Task>? CommandReceived;

        /// <summary>
        /// Event triggered when Server notifies about a new Agent version.
        /// Payload is UpdateNotification object.
        /// </summary>
        event Func<UpdateNotification, Task>? NewVersionAvailableReceived;


        /// <summary>
        /// Start connection to WebSocket server.
        /// </summary>
        /// <param name="agentId">ID of the Agent.</param>
        /// <param name="agentToken">Authentication token of the Agent.</param>
        /// <param name="cancellationToken">Token to cancel the connection attempt.</param>
        Task ConnectAsync(string agentId, string agentToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Actively disconnect WebSocket.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Send Agent's resource status to Server.
        /// Event: agent:status_update
        /// </summary>
        /// <param name="cpuUsage">CPU usage percentage.</param>
        /// <param name="ramUsage">RAM usage percentage.</param>
        /// <param name="diskUsage">Disk usage percentage.</param>
        Task SendStatusUpdateAsync(double cpuUsage, double ramUsage, double diskUsage);

        /// <summary>
        /// Send command execution result to Server.
        /// Event: agent:command_result
        /// </summary>
        /// <param name="commandResult">Object containing command result.</param>
        Task SendCommandResultAsync(CommandResult commandResult);

        /// <summary>
        /// Check if client is currently connected.
        /// </summary>
        bool IsConnected { get; }
    }
}
