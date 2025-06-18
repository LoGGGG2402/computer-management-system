using System.Diagnostics;
using System.Collections.Concurrent;

namespace procmon
{
    public class ProcessBlocker : IDisposable
    {
        private readonly ConcurrentDictionary<string, DateTime> _recentBlocks = new();
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        // System protection
        private static readonly HashSet<string> _systemPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            @"c:\windows\system32\",
            @"c:\windows\syswow64\",
            @"c:\windows\",
            @"c:\program files\windows defender\"
        };

        private static readonly HashSet<string> _systemProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "system", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe",
            "services.exe", "lsass.exe", "svchost.exe", "explorer.exe",
            "dwm.exe", "conhost.exe", "sihost.exe", "taskhostw.exe"
        };

        // Configuration
        private string _currentMode = "blacklist";
        private HashSet<string> _blacklist = new();
        private HashSet<string> _whitelist = new();
        private readonly object _configLock = new();

        // Events
        public event Action<ProcessInfo>? ProcessBlocked;
        public event Action<string>? BlockingError;

        public ProcessBlocker()
        {
            _cleanupTimer = new Timer(CleanupOldBlocks, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }        /// <summary>
                 /// Update blocking configuration
                 /// </summary>
        public void UpdateConfiguration(string mode, HashSet<string> blacklist, HashSet<string> whitelist)
        {
            lock (_configLock)
            {
                _currentMode = mode;
                _blacklist = new HashSet<string>(blacklist);
                _whitelist = new HashSet<string>(whitelist);

                Console.WriteLine($"[BLOCKER] Configuration updated - Mode: {mode}");
                Console.WriteLine($"[BLOCKER] Blacklist ({_blacklist.Count}): {string.Join(", ", _blacklist)}");
                Console.WriteLine($"[BLOCKER] Whitelist ({_whitelist.Count}): {string.Join(", ", _whitelist)}");
            }
        }        /// <summary>
                 /// Check if a process should be blocked based on current configuration
                 /// </summary>
        public bool ShouldBlock(string processName, string executablePath = "")
        {
            if (string.IsNullOrEmpty(processName)) return false;

            var lowerName = processName.ToLowerInvariant();
            var lowerPath = executablePath?.ToLowerInvariant() ?? "";

            // Never block system processes/paths
            if (IsSystemProcess(lowerName, lowerPath))
            {
                return false;
            }

            lock (_configLock)
            {
                bool shouldBlock = _currentMode == "whitelist"
                    ? !_whitelist.Contains(lowerName)  // Whitelist: block if NOT in list
                    : _blacklist.Contains(lowerName);  // Blacklist: block if IN list

                if (shouldBlock)
                {
                    Console.WriteLine($"[BLOCKER] Process {processName} should be BLOCKED (mode: {_currentMode})");
                }

                return shouldBlock;
            }
        }

        /// <summary>
        /// Attempt to block a process
        /// </summary>
        public void BlockProcess(ProcessInfo processInfo)
        {
            try
            {
                var key = $"{processInfo.ProcessName}:{processInfo.ProcessId}";

                // Avoid spamming the same process
                if (_recentBlocks.ContainsKey(key) &&
                    _recentBlocks[key] > DateTime.Now.AddSeconds(-30))
                    return;

                _recentBlocks[key] = DateTime.Now;

                // Terminate the process
                if (TerminateProcess(processInfo.ProcessId, processInfo.ProcessName))
                {
                    ProcessBlocked?.Invoke(processInfo);
                }
                else
                {
                    BlockingError?.Invoke($"Failed to terminate: {processInfo.ProcessName} (PID: {processInfo.ProcessId})");
                }
            }
            catch (Exception ex)
            {
                BlockingError?.Invoke($"Error blocking {processInfo.ProcessName}: {ex.Message}");
            }
        }

        private static bool IsSystemProcess(string processName, string executablePath)
        {
            if (_systemProcesses.Contains(processName)) return true;
            if (string.IsNullOrEmpty(executablePath)) return false;
            return _systemPaths.Any(path => executablePath.StartsWith(path));
        }

        private bool TerminateProcess(int processId, string processName)
        {
            try
            {
                using var process = Process.GetProcessById(processId);

                // Try graceful close first
                if (process.CloseMainWindow() && process.WaitForExit(3000))
                    return true;

                // Force terminate
                process.Kill();
                bool success = process.WaitForExit(5000);

                if (success)
                    Console.WriteLine($"[BLOCKER] Terminated: {processName} (PID: {processId})");

                return success;
            }
            catch (ArgumentException)
            {
                // Process already exited
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BLOCKER] Error terminating {processName}: {ex.Message}");
                return false;
            }
        }

        private void CleanupOldBlocks(object? state)
        {
            try
            {
                var cutoff = DateTime.Now.AddMinutes(-10);
                var oldKeys = _recentBlocks.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList(); foreach (var key in oldKeys)
                    _recentBlocks.TryRemove(key, out _);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BLOCKER] Cleanup error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cleanupTimer?.Dispose();
            _disposed = true;

            Console.WriteLine("[BLOCKER] Disposed.");
        }
    }
}
