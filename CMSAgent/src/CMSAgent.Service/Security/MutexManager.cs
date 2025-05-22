// CMSAgent.Service/Security/MutexManager.cs
using CMSAgent.Shared.Constants; // For AgentConstants
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // For IOptions<AppSettings>
using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using CMSAgent.Service.Configuration.Models; // For AppSettings

namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Manages Mutex to ensure only one instance of Agent Service is running on the system.
    /// </summary>
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

        /// <summary>
        /// Attempt to request Mutex ownership.
        /// </summary>
        /// <returns>True if Mutex ownership is obtained (current instance is unique), False if Mutex is already held by another instance.</returns>
        public bool RequestOwnership()
        {
            if (_hasHandle) // Already holding Mutex
            {
                return true;
            }

            try
            {
                // Configure Mutex security to allow all users on the machine to "see" the global Mutex.
                // This is important because the service runs under LocalSystem, but there might be other instances
                // (e.g., running debug from another user) trying to create a Mutex with the same name.
                var mutexSecurity = new MutexSecurity();
                mutexSecurity.AddAccessRule(new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null), // Everyone
                    MutexRights.FullControl, // Or at least Synchronize | ReadPermissions
                    AccessControlType.Allow
                ));

                _mutex = new Mutex(initiallyOwned: false, name: _mutexName, out bool createdNew, mutexSecurity);

                if (createdNew)
                {
                    // If Mutex is newly created, this instance can obtain ownership.
                    _logger.LogInformation("Mutex {MutexName} was newly created.", _mutexName);
                }
                else
                {
                    // Mutex already exists, try to obtain ownership with a short timeout.
                    _logger.LogInformation("Mutex {MutexName} already exists. Attempting to obtain ownership...", _mutexName);
                }

                // Try to obtain Mutex ownership.
                // WaitOne(0) will return immediately. True if obtained, False if not.
                _hasHandle = _mutex.WaitOne(TimeSpan.Zero, false);

                if (_hasHandle)
                {
                    _logger.LogInformation("Successfully obtained Mutex ownership: {MutexName}.", _mutexName);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Could not obtain Mutex ownership: {MutexName}. Another instance may be running.", _mutexName);
                    _mutex.Close(); // Close handle if ownership not obtained
                    _mutex = null;
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                // Occurs when another process holding the Mutex terminates without releasing it.
                // Current instance can obtain ownership.
                _logger.LogWarning("Mutex {MutexName} was abandoned. Taking ownership.", _mutexName);
                _hasHandle = true; // Take ownership
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogCritical(ex, "UnauthorizedAccessException error when creating or accessing Mutex {MutexName}. Check service/user permissions.", _mutexName);
                // This is a critical error, service may not have permission to create global mutex.
                return false; // Cannot continue if Mutex cannot be created
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error when requesting Mutex {MutexName}.", _mutexName);
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
