// Filepath: c:\Users\longpph\Desktop\computer-management-system\dotnet_agent\CMSAgent\SystemOperations\WindowsSync.cs
using Microsoft.Extensions.Logging;
using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Provides synchronization primitives for Windows environments, such as Mutex and EventWaitHandle.
    /// This class handles the creation, acquisition, and release of these synchronization objects.
    /// </summary>
    public class WindowsSync : IDisposable
    {
        private readonly ILogger<WindowsSync> _logger;
        private Mutex? _mutex;
        private EventWaitHandle? _eventWaitHandle;
        private const string DefaultMutexPrefix = "Global\\";
        private const string DefaultEventPrefix = "Global\\";

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSync"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging messages.</param>
        public WindowsSync(ILogger<WindowsSync> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Tries to acquire a system-wide or local mutex with a specified name and timeout.
        /// </summary>
        /// <param name="mutexName">The name of the mutex. If not prefixed with "Global\\" or "Local\\", "Global\\" will be used.</param>
        /// <param name="timeout">The time to wait to acquire the mutex.</param>
        /// <param name="acquiredMutex">When this method returns, contains the acquired <see cref="Mutex"/> object if the acquisition was successful, or null otherwise.</param>
        /// <returns>True if the mutex was acquired successfully; otherwise, false.</returns>
        /// <remarks>
        /// Default security descriptors are used for the mutex. If the mutex is acquired due to an AbandonedMutexException,
        /// this method still returns true and provides the acquired mutex.
        /// </remarks>
        public bool TryAcquireMutex(string mutexName, TimeSpan timeout, out Mutex? acquiredMutex)
        {
            acquiredMutex = null;
            if (string.IsNullOrWhiteSpace(mutexName))
            {
                _logger.LogError("Mutex name cannot be null or empty.");
                return false;
            }

            string fullMutexName = mutexName.StartsWith(DefaultMutexPrefix, StringComparison.OrdinalIgnoreCase) || mutexName.StartsWith("Local\\", StringComparison.OrdinalIgnoreCase) 
                                   ? mutexName 
                                   : DefaultMutexPrefix + mutexName;

            _logger.LogDebug("Attempting to acquire mutex: {MutexName}", fullMutexName);

            try
            {
                _mutex = new Mutex(false, fullMutexName, out bool createdNew);
                
                if (createdNew)
                {
                    _logger.LogInformation("Created new mutex: {MutexName}", fullMutexName);
                }
                else
                {
                    _logger.LogInformation("Opened existing mutex: {MutexName}", fullMutexName);
                }

                if (_mutex.WaitOne(timeout))
                {
                    _logger.LogInformation("Successfully acquired mutex: {MutexName}", fullMutexName);
                    acquiredMutex = _mutex;
                    return true;
                }
                else
                {
                    _logger.LogWarning("Timeout while waiting to acquire mutex: {MutexName}", fullMutexName);
                    _mutex.Dispose();
                    _mutex = null;
                    return false;
                }
            }
            catch (AbandonedMutexException ex)
            {
                _logger.LogWarning(ex, "Mutex {MutexName} was abandoned. Acquired successfully but previous owner terminated without releasing.", fullMutexName);
                acquiredMutex = _mutex; 
                return true; 
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access while trying to create or open mutex: {MutexName}. Try running with elevated privileges or check permissions.", fullMutexName);
                _mutex?.Dispose();
                _mutex = null;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring mutex: {MutexName}", fullMutexName);
                _mutex?.Dispose();
                _mutex = null;
                return false;
            }
        }

        /// <summary>
        /// Releases the specified mutex. If no mutex is specified, releases the mutex held by this instance.
        /// </summary>
        /// <param name="mutexToRelease">The mutex to release. If null, the instance's _mutex is used.</param>
        public void ReleaseMutex(Mutex? mutexToRelease = null)
        {
            var mtx = mutexToRelease ?? _mutex;
            if (mtx != null)
            {
                try
                {
                    mtx.ReleaseMutex();
                    _logger.LogInformation("Mutex {MutexName} released.", GetMutexName(mtx)); 
                }
                catch (ApplicationException ex)
                {
                    _logger.LogWarning(ex, "Attempted to release mutex {MutexName} more times than it was acquired or by a non-owner thread.", GetMutexName(mtx));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing mutex {MutexName}.", GetMutexName(mtx));
                }
            }
            else
            {
                _logger.LogWarning("Attempted to release a null mutex.");
            }
        }

        /// <summary>
        /// Placeholder method to get a mutex name for logging purposes.
        /// The actual name used for creation is not directly retrievable from the Mutex object.
        /// </summary>
        /// <param name="mtx">The mutex instance.</param>
        /// <returns>A generic identifier string, as the actual name is not retrievable.</returns>
        private string GetMutexName(Mutex mtx)
        {
            return "(name not directly retrievable)";
        }

        /// <summary>
        /// Creates a new system-wide or local named <see cref="EventWaitHandle"/> or opens an existing one.
        /// </summary>
        /// <param name="eventName">The name of the event. If not prefixed with "Global\\" or "Local\\", "Global\\" will be used.</param>
        /// <param name="mode">One of the <see cref="EventResetMode"/> values that determines the reset behavior of the event.</param>
        /// <param name="initialState">True to set the initial state to signaled; false to set it to nonsignaled.</param>
        /// <param name="createdNew">When this method returns, contains true if a new event was created; false if an existing event was opened.</param>
        /// <returns>The created or opened <see cref="EventWaitHandle"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventName"/> is null or empty.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if access is denied when creating or opening the event.</exception>
        /// <remarks>Default security descriptors are used for the event wait handle.</remarks>
        public EventWaitHandle CreateOrOpenEventWaitHandle(string eventName, EventResetMode mode, bool initialState, out bool createdNew)
        {
            createdNew = false;
            if (string.IsNullOrWhiteSpace(eventName))
            {
                _logger.LogError("EventWaitHandle name cannot be null or empty.");
                throw new ArgumentNullException(nameof(eventName));
            }

            string fullEventName = eventName.StartsWith(DefaultEventPrefix, StringComparison.OrdinalIgnoreCase) || eventName.StartsWith("Local\\", StringComparison.OrdinalIgnoreCase)
                                   ? eventName
                                   : DefaultEventPrefix + eventName;

            _logger.LogDebug("Creating or opening EventWaitHandle: {EventName}", fullEventName);
            try
            {
                _eventWaitHandle = new EventWaitHandle(initialState, mode, fullEventName, out createdNew);
                _logger.LogInformation("EventWaitHandle {EventName} {Action}. Mode: {Mode}, InitialState: {InitialState}", 
                                     fullEventName, createdNew ? "created" : "opened", mode, initialState);
                return _eventWaitHandle;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access while trying to create or open EventWaitHandle: {EventName}. Try running with elevated privileges or check permissions.", fullEventName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or opening EventWaitHandle: {EventName}", fullEventName);
                throw;
            }
        }

        /// <summary>
        /// Sets the state of the specified <see cref="EventWaitHandle"/> to signaled, allowing one or more waiting threads to proceed.
        /// If no handle is specified, sets the event handle held by this instance.
        /// </summary>
        /// <param name="ewh">The <see cref="EventWaitHandle"/> to set. If null, the instance's _eventWaitHandle is used.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public bool SetEvent(EventWaitHandle? ewh = null)
        {
            var handle = ewh ?? _eventWaitHandle;
            if (handle != null)
            {
                try
                {
                    handle.Set();
                    _logger.LogDebug("EventWaitHandle signaled (Set).");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting EventWaitHandle.");
                    return false;
                }
            }
            _logger.LogWarning("Attempted to Set a null EventWaitHandle.");
            return false;
        }

        /// <summary>
        /// Sets the state of the specified <see cref="EventWaitHandle"/> to nonsignaled, causing threads to block.
        /// If no handle is specified, resets the event handle held by this instance.
        /// </summary>
        /// <param name="ewh">The <see cref="EventWaitHandle"/> to reset. If null, the instance's _eventWaitHandle is used.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public bool ResetEvent(EventWaitHandle? ewh = null)
        {
            var handle = ewh ?? _eventWaitHandle;
            if (handle != null)
            {
                try
                {
                    handle.Reset();
                    _logger.LogDebug("EventWaitHandle reset.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting EventWaitHandle.");
                    return false;
                }
            }
            _logger.LogWarning("Attempted to Reset a null EventWaitHandle.");
            return false;
        }

        /// <summary>
        /// Blocks the current thread until the specified <see cref="EventWaitHandle"/> receives a signal or the timeout elapses.
        /// If no handle is specified, waits on the event handle held by this instance.
        /// </summary>
        /// <param name="ewh">The <see cref="EventWaitHandle"/> to wait on. If null, the instance's _eventWaitHandle is used.</param>
        /// <param name="timeout">The <see cref="TimeSpan"/> to wait, or <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <returns>True if the <see cref="EventWaitHandle"/> was signaled within the timeout; otherwise, false.</returns>
        public bool WaitOne(EventWaitHandle? ewh, TimeSpan timeout)
        {
            var handle = ewh ?? _eventWaitHandle;
            if (handle != null)
            {
                try
                {
                    _logger.LogTrace("Waiting on EventWaitHandle for {Timeout}ms...", timeout.TotalMilliseconds);
                    bool signaled = handle.WaitOne(timeout);
                    if (signaled) _logger.LogTrace("EventWaitHandle was signaled.");
                    else _logger.LogTrace("EventWaitHandle wait timed out.");
                    return signaled;
                }
                catch (AbandonedMutexException ex)
                {
                    _logger.LogWarning(ex, "WaitOne on EventWaitHandle resulted in AbandonedMutexException. This is unexpected.");
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting on EventWaitHandle.");
                    return false;
                }
            }
            _logger.LogWarning("Attempted to WaitOne on a null EventWaitHandle.");
            return false;
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="WindowsSync"/> class.
        /// This includes releasing any held mutex and disposing of the mutex and event wait handle.
        /// </summary>
        public void Dispose()
        {
            _logger.LogDebug("Disposing WindowsSync resources.");
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch { }
            _mutex?.Dispose();
            _mutex = null;

            _eventWaitHandle?.Dispose();
            _eventWaitHandle = null;
            GC.SuppressFinalize(this);
        }
    }
}
