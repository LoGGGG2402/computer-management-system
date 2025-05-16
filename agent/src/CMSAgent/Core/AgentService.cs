using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Models;
using CMSAgent.Configuration;
using CMSAgent.Common.Interfaces;
using CMSAgent.Monitoring;
using CMSAgent.Commands;
using CMSAgent.Update;
using CMSAgent.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Core
{
    /// <summary>
    /// Main service that coordinates agent operations, connects and manages all modules.
    /// </summary>
    /// <remarks>
        /// Initializes a new instance of AgentService.
    /// </remarks>
        /// <param name="logger">Logger for recording logs.</param>
        /// <param name="stateManager">Manages agent state.</param>
        /// <param name="configLoader">Loads and saves agent configuration.</param>
        /// <param name="webSocketConnector">WebSocket connection to server.</param>
        /// <param name="systemMonitor">Monitors system resources.</param>
        /// <param name="commandExecutor">Executes commands from server.</param>
        /// <param name="updateHandler">Handles agent updates.</param>
        /// <param name="singletonMutex">Ensures only one instance of agent runs.</param>
        /// <param name="tokenProtector">Protects agent token.</param>
        /// <param name="agentSettings">Specific configuration for agent.</param>
        /// <param name="hardwareInfoCollector">Collects hardware information</param>
        /// <param name="httpClient">HTTP connection</param>
    public class AgentService(
            ILogger<AgentService> logger,
            StateManager stateManager,
            IConfigLoader configLoader,
            IWebSocketConnector webSocketConnector,
            SystemMonitor systemMonitor,
            CommandExecutor commandExecutor,
            UpdateHandler updateHandler,
            SingletonMutex singletonMutex,
            TokenProtector tokenProtector,
            IOptions<AgentSpecificSettingsOptions> agentSettings,
            HardwareInfoCollector hardwareInfoCollector,
        IHttpClientWrapper httpClient) : WorkerServiceBase(logger)
    {
        private readonly AgentOperations _agentOperations = new(
            logger,
            stateManager,
            configLoader,
            webSocketConnector,
            systemMonitor,
            commandExecutor,
            updateHandler,
            singletonMutex,
            tokenProtector,
            agentSettings.Value,
            hardwareInfoCollector,
            httpClient);

        /// <summary>
        /// Initialize service, set initial state and check if it can run.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the initialization process.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing AgentService...");
            await _agentOperations.InitializeAsync();
        }

        /// <summary>
        /// Perform the main work of the service.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the work process.</returns>
        protected override async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            await _agentOperations.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Clean up resources when the service stops.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the cleanup process.</returns>
        protected override async Task CleanupAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cleaning up AgentService...");
            await _agentOperations.StopAsync(cancellationToken);
        }
    }
}
