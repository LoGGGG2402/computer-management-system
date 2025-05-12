namespace CMSAgent.Communication
{
    public interface ISocketIOClientWrapper : IDisposable
    {
        bool IsConnected { get; }

        event Action? OnConnected;
        event Action<string>? OnDisconnected;
        event Action<int>? OnReconnecting;
        event Action? OnReconnectFailed;
        event Action<string, string>? OnMessage;

        /// <summary>
        /// Connects to the Socket.IO server
        /// </summary>
        Task<bool> ConnectAsync(Dictionary<string, string> authData);

        /// <summary>
        /// Disconnects from the Socket.IO server
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Emits an event to the Socket.IO server
        /// </summary>
        Task EmitAsync(string eventName, object data);
    }
}