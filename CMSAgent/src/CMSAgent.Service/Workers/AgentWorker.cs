// CMSAgent.Service/Workers/AgentWorker.cs
using CMSAgent.Service.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Workers
{
    /// <summary>
    /// Main worker of Agent Service, inherits from BackgroundService.
    /// Responsible for launching and managing the lifecycle of AgentCoreOrchestrator.
    /// </summary>
    public class AgentWorker : BackgroundService
    {
        private readonly ILogger<AgentWorker> _logger;
        private readonly IAgentCoreOrchestrator _orchestrator;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public AgentWorker(
            ILogger<AgentWorker> logger,
            IAgentCoreOrchestrator orchestrator,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _hostApplicationLifetime = hostApplicationLifetime ?? throw new ArgumentNullException(nameof(hostApplicationLifetime));
        }

        /// <summary>
        /// Main method called when HostedService starts.
        /// It will launch AgentCoreOrchestrator and keep the worker running until a stop signal is received.
        /// </summary>
        /// <param name="stoppingToken">Token triggered when service is requested to stop.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AgentWorker is starting at: {time}", DateTimeOffset.Now);

            // Register a callback when application is requested to stop (e.g., Ctrl+C, shutdown)
            // to safely call StopAsync of orchestrator.
            stoppingToken.Register(async () =>
            {
                _logger.LogInformation("AgentWorker received stop signal from stoppingToken.");
                await StopOrchestratorAsync();
            });

            try
            {
                // Launch main Agent logic through Orchestrator
                // Orchestrator.StartAsync will contain the main loop or long-running background tasks.
                // It should also respect the stoppingToken passed in.
                await _orchestrator.StartAsync(stoppingToken);

                // If orchestrator's StartAsync ends without error and stoppingToken hasn't been requested,
                // it might mean orchestrator completed its work abnormally (if it's designed to run indefinitely).
                // Or, if orchestrator is designed to run once and finish, then this is normal behavior.
                // In the case of a continuously running background agent, StartAsync typically shouldn't end unless there's an error or stoppingToken is triggered.
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("AgentCoreOrchestrator.StartAsync ended without stop request. Service may stop.");
                    // If orchestrator ends early, we may want to stop the entire host application.
                    _hostApplicationLifetime.StopApplication();
                }
            }
            catch (OperationCanceledException)
            {
                // This occurs when stoppingToken is triggered while StartAsync is running.
                _logger.LogInformation("AgentWorker operation cancelled (OperationCanceledException).");
                // StopOrchestratorAsync is already registered with stoppingToken.Register, so no need to call it here.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled critical error in AgentWorker.ExecuteAsync. Service will stop.");
                // In case of critical error, stop the entire application.
                _hostApplicationLifetime.StopApplication();
            }
            finally
            {
                _logger.LogInformation("AgentWorker.ExecuteAsync has ended.");
            }
        }

        /// <summary>
        /// Called when service starts.
        /// </summary>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentWorker.StartAsync called.");
            // Perform additional initialization tasks if needed, before ExecuteAsync is called.
            // For example: check prerequisites.
            // However, main agent initialization logic should be in _orchestrator.StartAsync().
            await base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Called when service is requested to stop.
        /// This method should release resources and safely stop running tasks.
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AgentWorker.StopAsync called.");

            // Call StopOrchestratorAsync to ensure orchestrator is stopped properly.
            // This is already registered in ExecuteAsync with stoppingToken, but calling here to be sure.
            // Pass StopAsync's cancellationToken to have a time limit for stopping.
            await StopOrchestratorAsync(cancellationToken);

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("AgentWorker has completely stopped.");
        }

        /// <summary>
        /// Helper method to safely stop Orchestrator.
        /// </summary>
        private async Task StopOrchestratorAsync(CancellationToken externalToken = default)
        {
            _logger.LogInformation("Attempting to stop AgentCoreOrchestrator...");
            try
            {
                // Create a CancellationTokenSource with timeout if needed,
                // to ensure stopping doesn't hang indefinitely.
                // Or use externalToken if provided.
                CancellationToken effectiveToken = externalToken == default ? new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token : externalToken;
                await _orchestrator.StopAsync(effectiveToken);
                _logger.LogInformation("AgentCoreOrchestrator has been stopped.");
            }
            catch (OperationCanceledException)
            {
                 _logger.LogWarning("AgentCoreOrchestrator stop process cancelled (timeout or external request).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping AgentCoreOrchestrator.");
            }
        }
    }
}
