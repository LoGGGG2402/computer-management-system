using CMSAgent.Models.Payloads;

namespace CMSAgent.Communication
{
    public delegate void CommandRequestHandler(CommandRequest commandRequest);
    public delegate void UpdateAvailableHandler(UpdateCheckResponse updateInfo);

    public interface IServerConnector
    {
        bool IsConnected { get; }

        event CommandRequestHandler OnCommandExecutionRequested;
        event UpdateAvailableHandler OnUpdateAvailable;

        /// <summary>
        /// Initializes the server connector
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Connects to the server
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Tries to identify the agent with the server
        /// </summary>
        Task<bool> TryIdentifyWithServerAsync(bool forceRenewToken);

        /// <summary>
        /// Sends hardware information to the server
        /// </summary>
        Task SendHardwareInfoAsync(HardwareInfoPayload hardwareInfo);

        /// <summary>
        /// Sends a status update to the server
        /// </summary>
        Task SendStatusUpdateAsync(AgentStatusPayload statusPayload);

        /// <summary>
        /// Sends a command execution result to the server
        /// </summary>
        Task<CommandResult> SendCommandResultAsync(CommandResult result);

        /// <summary>
        /// Reports an error to the server
        /// </summary>
        Task ReportErrorAsync(ErrorReport errorReport);

        /// <summary>
        /// Checks if there's an update available
        /// </summary>
        Task<UpdateCheckResponse?> CheckForUpdateAsync(string currentVersion);

        /// <summary>
        /// Downloads an update package
        /// </summary>
        Task<Stream?> DownloadUpdatePackageAsync(string downloadUrl);

        /// <summary>
        /// Sends an error report to the server
        /// </summary>
        Task<bool> SendErrorReportAsync(ErrorReportPayload errorReport);
    }
}