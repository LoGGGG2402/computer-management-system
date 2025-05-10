using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Core;
using Microsoft.Extensions.Logging;

namespace CMSAgent
{
    /// <summary>
    /// Represents the Windows service for the CMS Agent.
    /// This class handles the start and stop events of the service, managing the agent's lifecycle.
    /// </summary>
    public partial class AgentService : ServiceBase
    {
        /// <summary>
        /// The name of the service.
        /// </summary>
        public const string ServiceNameString = "CMSAgent";
        private readonly Microsoft.Extensions.Logging.ILogger<AgentService> _logger;
        private readonly Agent _agent;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentService"/> class.
        /// </summary>
        /// <param name="agent">The core agent logic handler.</param>
        /// <param name="logger">The logger instance for logging messages.</param>
        public AgentService(Agent agent, Microsoft.Extensions.Logging.ILogger<AgentService> logger)
        {
            _agent = agent;
            _logger = logger;
            this.ServiceName = ServiceNameString;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Called when the service is starting.
        /// This method initiates the agent's startup process in a background task.
        /// </summary>
        /// <param name="args">Arguments passed by the service control manager.</param>
        protected override void OnStart(string[] args)
        {
            _logger.LogInformation("CMSAgent Service OnStart called.");
            
            Task.Run(async () => 
            {
                try
                {
                    await _agent.StartAsync(_cancellationTokenSource.Token);
                    _logger.LogInformation("Agent successfully started within the service.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Agent startup was canceled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while starting the agent.");
                }
            });
            
            _logger.LogInformation("CMSAgent Service OnStart completed.");
        }

        /// <summary>
        /// Called when the service is stopping.
        /// This method signals the agent to shut down by cancelling the cancellation token.
        /// </summary>
        protected override void OnStop()
        {
            _logger.LogInformation("CMSAgent Service OnStop called.");
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Attempting to cancel agent tasks.");
                _cancellationTokenSource.Cancel();
                _logger.LogInformation("Agent tasks cancellation requested.");
            }
            _cancellationTokenSource?.Dispose();
            _logger.LogInformation("CMSAgent Service OnStop completed.");
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AgentService"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
