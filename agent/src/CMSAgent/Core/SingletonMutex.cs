using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Core
{
    /// <summary>
    /// Class that ensures only one instance of the application is running at a time.
    /// </summary>
    public class SingletonMutex
    {
        private readonly string _mutexName;
        private readonly Mutex _mutex;
        private readonly ILogger<SingletonMutex> _logger;
        private bool _ownsHandle = false;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of SingletonMutex.
        /// </summary>
        /// <param name="mutexName">Name of the mutex, which determines its unique identity.</param>
        /// <param name="logger">Logger for logging.</param>
        public SingletonMutex(string mutexName, ILogger<SingletonMutex> logger)
        {
            _mutexName = string.IsNullOrEmpty(mutexName) 
                ? "Global\\CMSAgentSingleInstance" 
                : $"Global\\{mutexName}SingleInstance";
            _logger = logger;
            
            try
            {
                // Try to create and own the global mutex
                _mutex = new Mutex(true, _mutexName, out _ownsHandle);
                
                if (_ownsHandle)
                {
                    _logger.LogInformation("Acquired singleton lock with name {MutexName}", _mutexName);
                }
                else
                {
                    _logger.LogWarning("Unable to acquire singleton lock. Another instance of the application may be running.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when creating singleton mutex");
                throw;
            }
        }

        /// <summary>
        /// Checks if this instance owns the mutex.
        /// </summary>
        /// <returns>True if this instance is the only one, otherwise False.</returns>
        public bool IsSingleInstance()
        {
            return _ownsHandle;
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if called from user code, False if from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_ownsHandle)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                        _logger.LogInformation("Released singleton lock {MutexName}", _mutexName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error when releasing singleton lock");
                    }
                }

                _mutex.Dispose();
            }

            _disposed = true;
        }
    }
}
