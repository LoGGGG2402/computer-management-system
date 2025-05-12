using CMSAgent.Core;
using CMSAgent.SystemOperations;
using CMSAgent.Utilities;
using Serilog;
using System.ServiceProcess;

namespace CMSAgent
{
    public class AgentWindowsService : ServiceBase
    {
        private readonly ICoreAgent _agent;
        private readonly MutexHelper _mutexHelper;
        private bool _disposed = false;

        public AgentWindowsService()
        {
            ServiceName = "CMSAgentService";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;

            _agent = new CoreAgent();
            _mutexHelper = new MutexHelper("Global\\CMSAgentServiceMutex");
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Ensure we're the only instance running
                if (!_mutexHelper.TryAcquire())
                {
                    Log.Error("Another instance of the agent is already running. Service cannot start.");
                    Stop();
                    return;
                }                // Setup logging for the service
                LoggingSetup.Initialize(false);
                Log.Information("Starting CMSAgentService...");

                // Ensure required directories exist
                DirectoryUtils.EnsureRequiredDirectoriesExist();

                // Start the agent
                _agent.StartAsync().Wait();

                Log.Information("CMSAgentService started successfully.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error starting agent service: {Message}", ex.Message);
                Stop();
            }
        }

        protected override void OnStop()
        {
            Log.Information("Attempting to stop CMSAgentService...");
            try
            {
                // Signal the agent to stop and wait for it to complete.
                // CoreAgent.Dispose() should handle this.
                if (_agent is IDisposable disposableAgent)
                {
                    disposableAgent.Dispose(); // This should trigger CoreAgent's shutdown logic
                }
                Log.Information("CoreAgent disposed/stopped.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during CoreAgent shutdown: {Message}", ex.Message);
            }
            finally
            {
                // Release and dispose the mutex
                // _mutexHelper.Dispose() will release if acquired by this instance.
                _mutexHelper?.Dispose();
                Log.Information("Mutex released and disposed.");

                Log.Information("CMSAgentService stop process completed.");
                Log.CloseAndFlush(); // Ensure all logs are written before service exits
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    _mutexHelper.Dispose();
                }

                _disposed = true;
            }
            
            base.Dispose(disposing);
        }
    }
}