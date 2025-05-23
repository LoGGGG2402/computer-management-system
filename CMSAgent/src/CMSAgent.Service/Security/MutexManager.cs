// CMSAgent.Service/Security/MutexManager.cs
using CMSAgent.Shared.Constants;
using Microsoft.Extensions.Options; 
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using CMSAgent.Service.Configuration.Models; // For AppSettings

namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Manages Mutex to ensure only one instance of Agent Service is running on the system.
    /// </summary>
    /// 
    [SupportedOSPlatform("windows")]
    public class MutexManager : IDisposable
    {
        private readonly ILogger<MutexManager> _logger;
        private Mutex? _mutex;
        private bool _hasHandle = false;
        private readonly string _mutexName;

        /// <summary>
        /// Initialize MutexManager.
        /// </summary>
        /// <param name="appSettingsOptions">Application configuration to get AgentInstanceGuid.</param>
        /// <param name="logger">Logger.</param>
        public MutexManager(IOptions<AppSettings> appSettingsOptions, ILogger<MutexManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            if (string.IsNullOrWhiteSpace(appSettings.AgentInstanceGuid))
            {
                var errorMessage = "AgentInstanceGuid is not configured in appsettings. Cannot create unique Mutex.";
                _logger.LogCritical(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _mutexName = $"{AgentConstants.AgentServiceMutexNamePrefix}{appSettings.AgentInstanceGuid}";
            _logger.LogInformation("Using Mutex name: {MutexName}", _mutexName);
        }
        public bool RequestOwnership()
        {
            // Configure Mutex security to allow all users on the machine to "see" the global Mutex.
            var mutexSecurity = new MutexSecurity();
            mutexSecurity.AddAccessRule(new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null), // Everyone
                MutexRights.FullControl,
                AccessControlType.Allow
            ));

            _mutex = new Mutex(false, _mutexName, out bool createdNew);
            _mutex.SetAccessControl(mutexSecurity);

            if (createdNew)
            {
                _logger.LogInformation("Mutex {MutexName} was newly created.", _mutexName);
            }
            else
            {
                _logger.LogInformation("Mutex {MutexName} already exists. Attempting to obtain ownership...", _mutexName);
            }

            _hasHandle = _mutex.WaitOne(TimeSpan.Zero, false);

            if (_hasHandle)
            {
                _logger.LogInformation("Successfully obtained Mutex ownership: {MutexName}.", _mutexName);
                return true;
            }
            else
            {
                _logger.LogWarning("Could not obtain Mutex ownership: {MutexName}. Another instance may be running.", _mutexName);
                _mutex.Close();
                _mutex = null;
                return false;
            }
        }

        /// <summary>
        /// Release Mutex if held.
        /// </summary>
        public void ReleaseOwnership()
        {
            if (_hasHandle && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                    _logger.LogInformation("Released Mutex: {MutexName}.", _mutexName);
                }
                catch (ApplicationException ex) // Can occur if thread not owning mutex tries to release
                {
                    _logger.LogError(ex, "ApplicationException error when releasing Mutex {MutexName}. This thread may not own the Mutex.", _mutexName);
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Unknown error when releasing Mutex {MutexName}.", _mutexName);
                }
                _hasHandle = false;
            }
        }

        public void Dispose()
        {
            ReleaseOwnership();
            _mutex?.Dispose();
            _mutex = null;
            GC.SuppressFinalize(this);
            _logger.LogDebug("MutexManager has been disposed.");
        }
    }
}
