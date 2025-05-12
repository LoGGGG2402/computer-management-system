using Serilog;
using System.Threading;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Helper class for process synchronization using mutex
    /// </summary>
    public static class MutexHelper
    {
        private static Mutex? _mutex;
        private static bool _hasMutex = false;

        /// <summary>
        /// Tries to acquire a global mutex with the specified name
        /// </summary>
        /// <param name="mutexName">Name of the mutex</param>
        /// <returns>True if the mutex was acquired, false otherwise</returns>
        public static bool TryAcquireMutex(string mutexName)
        {
            try
            {
                // Release any existing mutex
                ReleaseMutex();

                // Try to create and acquire a new mutex
                _mutex = new Mutex(false, mutexName);
                _hasMutex = _mutex.WaitOne(TimeSpan.Zero, false);

                if (_hasMutex)
                {
                    Log.Debug("Acquired mutex: {MutexName}", mutexName);
                }
                else
                {
                    Log.Debug("Failed to acquire mutex: {MutexName} (already owned)", mutexName);
                }

                return _hasMutex;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error acquiring mutex {MutexName}: {Message}", mutexName, ex.Message);
                
                // Clean up
                _mutex?.Dispose();
                _mutex = null;
                _hasMutex = false;
                
                return false;
            }
        }

        /// <summary>
        /// Releases the acquired mutex
        /// </summary>
        public static void ReleaseMutex()
        {
            if (_hasMutex && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                    Log.Debug("Released mutex");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error releasing mutex: {Message}", ex.Message);
                }
                finally
                {
                    _mutex.Dispose();
                    _mutex = null;
                    _hasMutex = false;
                }
            }
        }

        /// <summary>
        /// Checks if the process already has an instance running
        /// </summary>
        /// <returns>True if this is the first instance, false if another instance is already running</returns>
        public static bool IsFirstInstance()
        {
            return TryAcquireMutex("Global\\CMSAgent");
        }
    }
}