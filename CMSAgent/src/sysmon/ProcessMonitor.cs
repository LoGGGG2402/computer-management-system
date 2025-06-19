using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace sysmon
{
    // ===== PROCESS MONITOR =====
    public class ProcessMonitor : IDisposable
    {
        private readonly ConcurrentDictionary<int, ProcessInfo> _processes = new();
        private readonly Timer _scanTimer;
        private readonly ProcessBlocker _processBlocker;
        private bool _disposed = false;
        private bool _isRunning = false;

        // Configuration
        private ProcessBlockingConfig _currentConfig = new();
        private string _currentMode = "blacklist";
        private HashSet<string> _blacklist = new();
        private HashSet<string> _whitelist = new();
        private readonly object _configLock = new();
        private DateTime _lastStatusUpdate = DateTime.Now;

        // Events
        public event Action<string, int>? ProcessBlocked;
        public event Action<string>? BlockingError;

        public ProcessMonitor()
        {
            _processBlocker = new ProcessBlocker();
            _processBlocker.ProcessBlocked += OnProcessBlockedInternal;
            _processBlocker.BlockingError += (error) => BlockingError?.Invoke(error);

            _scanTimer = new Timer(ScanProcesses, null, Timeout.Infinite, Timeout.Infinite);
        }        public async Task LoadConfigurationAsync(ProcessBlockingConfig config)
        {
            await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine($"[PROCESS] Loading configuration from unified config...");
                    
                    // Store current config
                    _currentConfig = config;

                    lock (_configLock)
                    {
                        _blacklist.Clear();
                        _whitelist.Clear();

                        _currentMode = config.Mode;

                        foreach (var process in config.Blacklist)
                        {
                            _blacklist.Add(process.ToLowerInvariant());
                        }

                        foreach (var process in config.Whitelist)
                        {
                            _whitelist.Add(process.ToLowerInvariant());
                        }

                        // Update the blocker configuration
                        _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                    }

                    Console.WriteLine($"[PROCESS] Loaded {_currentMode} mode - Black: {_blacklist.Count}, White: {_whitelist.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PROCESS] Error loading unified configuration: {ex.Message}");
                }
            });
        }

        private void LoadConfigurationFromJson(string jsonConfig)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var config = JsonSerializer.Deserialize<ProcessListConfig>(jsonConfig, options);

                lock (_configLock)
                {
                    _blacklist.Clear();
                    _whitelist.Clear();

                    _currentMode = config?.Mode ?? "blacklist";

                    if (config?.Blacklist != null)
                    {
                        foreach (var process in config.Blacklist)
                        {
                            _blacklist.Add(process.ToLowerInvariant());
                        }
                    }

                    if (config?.Whitelist != null)
                    {
                        foreach (var process in config.Whitelist)
                        {
                            _whitelist.Add(process.ToLowerInvariant());
                        }
                    }

                    // Update the blocker configuration
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }

                Console.WriteLine($"[PROCESS] Loaded {_currentMode} mode - Black: {_blacklist.Count}, White: {_whitelist.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error parsing configuration: {ex.Message}");
            }
        }        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[PROCESS] Starting process monitoring...");
            _isRunning = true;

            // Initial scan
            await Task.Run(() => ScanProcesses());

            // Start periodic scanning every 3 seconds
            _scanTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(3));

            // Keep running until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[PROCESS] Process monitoring stopped.");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void Stop()
        {
            _scanTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isRunning = false;
            Console.WriteLine("[PROCESS] Process monitoring stopped.");
        }

        private void ScanProcesses(object? state = null)
        {
            try
            {
                var currentProcessIds = new HashSet<int>();
                var processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    try
                    {
                        currentProcessIds.Add(process.Id);

                        if (!_processes.ContainsKey(process.Id))
                        {
                            var processInfo = CreateProcessInfo(process);
                            if (processInfo != null)
                            {
                                _processes.TryAdd(processInfo.ProcessId, processInfo);
                                if (processInfo.IsBlocked)
                                    _processBlocker.BlockProcess(processInfo);
                            }
                        }
                    }
                    catch { /* Ignore inaccessible processes */ }
                    finally
                    {
                        process.Dispose();
                    }
                }

                // Remove stopped processes
                var stoppedProcesses = _processes.Keys.Except(currentProcessIds).ToList();
                foreach (var stoppedId in stoppedProcesses)
                    _processes.TryRemove(stoppedId, out _);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Scan error: {ex.Message}");
            }
        }

        private ProcessInfo? CreateProcessInfo(Process process)
        {
            try
            {
                var processInfo = new ProcessInfo
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName + ".exe",
                    StartTime = process.StartTime
                };

                try { processInfo.ExecutablePath = process.MainModule?.FileName ?? ""; }
                catch { /* Process may not have main module */ }

                processInfo.IsBlocked = _processBlocker.ShouldBlock(processInfo.ProcessName, processInfo.ExecutablePath);
                return processInfo;
            }
            catch
            {
                return null;
            }
        }

        private void OnProcessBlockedInternal(ProcessInfo processInfo)
        {
            ProcessBlocked?.Invoke(processInfo.ProcessName, processInfo.ProcessId);
        }        // Public methods for SystemMonitor
        public (int RunningProcesses, int BlockedProcesses, int TotalProcesses) GetStatistics()
        {
            var running = _processes.Values.Count(p => !p.IsBlocked);
            var blocked = _processes.Values.Count(p => p.IsBlocked);
            return (running, blocked, _processes.Count);
        }

        public (string Mode, int BlacklistCount, int WhitelistCount, string[] BlacklistSample, string[] WhitelistSample) GetConfigurationSummary()
        {
            lock (_configLock)
            {
                var blacklistSample = _blacklist.Take(5).ToArray();
                var whitelistSample = _whitelist.Take(5).ToArray();
                return (_currentMode, _blacklist.Count, _whitelist.Count, blacklistSample, whitelistSample);
            }
        }        /// <summary>
        /// Get list of monitored processes for display
        /// </summary>
        public List<ProcessInfo> GetMonitoredProcesses()
        {
            try
            {
                return _processes.Values.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error getting monitored processes: {ex.Message}");
                return new List<ProcessInfo>();
            }
        }

        public string GetProcessesAsJson()
        {
            try
            {
                var processes = _processes.Values.ToList();
                return JsonSerializer.Serialize(processes, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error serializing processes: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>
        /// Get grouped processes as JSON string
        /// </summary>
        public string GetGroupedProcessesAsJson()
        {
            try
            {
                var runningProcesses = _processes.Values.Where(p => !p.IsBlocked).ToList();
                var blockedProcesses = _processes.Values.Where(p => p.IsBlocked).ToList();

                var groupedRunning = runningProcesses
                    .GroupBy(p => p.ProcessName)
                    .Select(g => new
                    {
                        ProcessName = g.Key,
                        Count = g.Count(),
                        PIDs = g.Select(p => p.ProcessId).OrderBy(p => p).ToList(),
                        LatestStartTime = g.OrderByDescending(p => p.StartTime).First().StartTime,
                        Status = "RUNNING"
                    })
                    .OrderBy(g => g.ProcessName)
                    .ToList();

                var groupedBlocked = blockedProcesses
                    .GroupBy(p => p.ProcessName)
                    .Select(g => new
                    {
                        ProcessName = g.Key,
                        Count = g.Count(),
                        PIDs = g.Select(p => p.ProcessId).OrderBy(p => p).ToList(),
                        LatestStartTime = g.OrderByDescending(p => p.StartTime).First().StartTime,
                        Status = "BLOCKED"
                    })
                    .OrderBy(g => g.ProcessName)
                    .ToList();

                var result = new
                {
                    Running = groupedRunning,
                    Blocked = groupedBlocked,
                    Summary = new
                    {
                        TotalRunning = runningProcesses.Count,
                        TotalBlocked = blockedProcesses.Count,
                        UniqueRunning = groupedRunning.Count,
                        UniqueBlocked = groupedBlocked.Count,
                        Total = _processes.Count
                    }
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error serializing grouped processes: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>        /// Get configuration summary for status display
        /// </summary>
        public string GetConfigurationSummaryJson()
        {
            try
            {
                var summary = new
                {
                    Enabled = _currentConfig.Enabled,
                    Mode = _currentConfig.Mode,
                    BlacklistCount = _currentConfig.Blacklist.Count,
                    WhitelistCount = _currentConfig.Whitelist.Count,
                    SampleBlacklist = _currentConfig.Blacklist.Take(5).ToArray(),
                    SampleWhitelist = _currentConfig.Whitelist.Take(5).ToArray()
                };

                return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error getting configuration summary: {ex.Message}");
                return "{}";
            }
        }

        // Legacy support method for backward compatibility
        public void LoadConfiguration(string filePath)
        {
            try
            {
                Console.WriteLine($"[PROCESS] Loading configuration from legacy file: {filePath}");

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[PROCESS] Process list file not found: {filePath}");
                    return;
                }

                var json = File.ReadAllText(filePath);
                LoadConfigurationFromJson(json);
            }            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error loading legacy configuration: {ex.Message}");
            }
        }

        // ===== NEW CONFIGURATION AND JSON SUPPORT METHODS =====
        
        /// <summary>
        /// Updates the process monitoring configuration
        /// </summary>
        /// <param name="config">New process blocking configuration</param>
        /// <returns>JSON response indicating success/failure</returns>
        public string UpdateConfiguration(ProcessBlockingConfig config)
        {
            try
            {
                _currentConfig = config ?? throw new ArgumentNullException(nameof(config));
                
                lock (_configLock)
                {
                    _blacklist.Clear();
                    _whitelist.Clear();
                    
                    _currentMode = config.Mode;
                    
                    foreach (var process in config.Blacklist)
                    {
                        _blacklist.Add(process.ToLowerInvariant());
                    }
                    
                    foreach (var process in config.Whitelist)
                    {
                        _whitelist.Add(process.ToLowerInvariant());
                    }
                    
                    // Update the blocker configuration
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }
                
                var response = new ProcessConfigurationResponse
                {
                    Success = true,
                    Message = $"Configuration updated successfully. Mode: {config.Mode}, Blacklist: {config.Blacklist.Count}, Whitelist: {config.Whitelist.Count}",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[PROCESS] Configuration updated - Mode: {config.Mode}, Black: {config.Blacklist.Count}, White: {config.Whitelist.Count}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new ProcessConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to update configuration: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[PROCESS-ERROR] Configuration update failed: {ex.Message}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Gets current process monitoring configuration as JSON
        /// </summary>
        /// <returns>JSON string of current configuration</returns>
        public string GetConfigurationAsJson()
        {
            var response = new ProcessConfigurationResponse
            {
                Success = true,
                Message = "Configuration retrieved successfully",
                CurrentConfig = _currentConfig,
                UpdatedAt = DateTime.Now
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Gets current process monitoring status as JSON
        /// </summary>
        /// <returns>JSON string of current status and statistics</returns>
        public string GetStatusAsJson()
        {
            try
            {
                lock (_configLock)
                {
                    var allProcesses = _processes.Values.ToList();
                    var blockedProcesses = allProcesses.Where(p => p.IsBlocked).ToList();
                    var runningProcesses = allProcesses.Where(p => !p.IsBlocked).ToList();

                    var status = new ProcessMonitorStatus
                    {
                        IsRunning = _isRunning,
                        Mode = _currentMode,
                        TotalProcesses = allProcesses.Count,
                        BlockedProcesses = blockedProcesses.Count,
                        RunningProcesses = runningProcesses.Count,
                        LastUpdate = DateTime.Now,
                        RecentProcesses = allProcesses.OrderByDescending(p => p.StartTime).Take(20).ToList(),
                        BlacklistSample = _blacklist.Take(10).ToList(),
                        WhitelistSample = _whitelist.Take(10).ToList()
                    };

                    _lastStatusUpdate = DateTime.Now;
                    return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS-ERROR] Failed to get status: {ex.Message}");                var errorStatus = new ProcessMonitorStatus
                {
                    IsRunning = false,
                    Mode = _currentMode,
                    LastUpdate = DateTime.Now
                };
                return JsonSerializer.Serialize(errorStatus, new JsonSerializerOptions { WriteIndented = true });
            }
        }        /// <summary>
        /// Adds a process to the blacklist at runtime
        /// </summary>
        /// <param name="processName">Process name to block</param>
        /// <returns>JSON response</returns>
        public string AddBlockedProcess(string processName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName))
                    throw new ArgumentException("Process name cannot be empty");

                var lowerName = processName.ToLowerInvariant();
                
                if (_currentConfig.Blacklist.Contains(lowerName))
                    throw new InvalidOperationException($"Process '{processName}' is already in blacklist");

                _currentConfig.Blacklist.Add(lowerName);
                
                lock (_configLock)
                {
                    _blacklist.Add(lowerName);
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }

                var response = new ProcessConfigurationResponse
                {
                    Success = true,
                    Message = $"Process '{processName}' added to blacklist",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[PROCESS] Added process to blacklist: {processName}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new ProcessConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to add process to blacklist: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }        /// <summary>
        /// Removes a process from the blacklist at runtime
        /// </summary>
        /// <param name="processName">Process name to unblock</param>
        /// <returns>JSON response</returns>
        public string RemoveBlockedProcess(string processName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName))
                    throw new ArgumentException("Process name cannot be empty");

                var lowerName = processName.ToLowerInvariant();
                bool removed = _currentConfig.Blacklist.Remove(lowerName);
                if (!removed)
                    throw new InvalidOperationException($"Process '{processName}' not found in blacklist");

                lock (_configLock)
                {
                    _blacklist.Remove(lowerName);
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }

                var response = new ProcessConfigurationResponse
                {
                    Success = true,
                    Message = $"Process '{processName}' removed from blacklist",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[PROCESS] Removed process from blacklist: {processName}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new ProcessConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to remove process from blacklist: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Adds a process to the whitelist at runtime
        /// </summary>
        /// <param name="processName">Process name to whitelist</param>
        /// <returns>JSON response</returns>
        public string AddToWhitelist(string processName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName))
                    throw new ArgumentException("Process name cannot be empty");

                var lowerName = processName.ToLowerInvariant();
                _currentConfig.Whitelist.Add(lowerName);
                
                lock (_configLock)
                {
                    _whitelist.Add(lowerName);
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }

                var response = new ProcessConfigurationResponse
                {
                    Success = true,
                    Message = $"Process '{processName}' added to whitelist",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[PROCESS] Added process to whitelist: {processName}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new ProcessConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to add process to whitelist: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Removes a process from the whitelist at runtime
        /// </summary>
        /// <param name="processName">Process name to remove from whitelist</param>
        /// <returns>JSON response</returns>
        public string RemoveFromWhitelist(string processName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName))
                    throw new ArgumentException("Process name cannot be empty");

                var lowerName = processName.ToLowerInvariant();
                bool removed = _currentConfig.Whitelist.Remove(lowerName);
                if (!removed)
                    throw new InvalidOperationException($"Process '{processName}' not found in whitelist");

                lock (_configLock)
                {
                    _whitelist.Remove(lowerName);
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }

                var response = new ProcessConfigurationResponse
                {
                    Success = true,
                    Message = $"Process '{processName}' removed from whitelist",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[PROCESS] Removed process from whitelist: {processName}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new ProcessConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to remove process from whitelist: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Gets all blocked processes as a list
        /// </summary>
        /// <returns>List of blocked processes</returns>
        public List<string> GetBlockedProcessesList()
        {
            lock (_configLock)
            {
                return _blacklist.ToList();
            }
        }

        /// <summary>
        /// Gets all whitelisted processes as a list
        /// </summary>
        /// <returns>List of whitelisted processes</returns>
        public List<string> GetWhitelistedProcessesList()
        {
            lock (_configLock)
            {
                return _whitelist.ToList();
            }
        }

        /// <summary>
        /// Validates if a process name format is correct
        /// </summary>
        /// <param name="processName">Process name to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private bool IsValidProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            
            // Basic process name validation
            return !processName.Contains('/') && 
                   !processName.Contains('\\') && 
                   processName.Length > 0 &&
                   processName.Length < 260; // MAX_PATH
        }

        public void Dispose()
        {
            if (_disposed) return;

            _scanTimer?.Dispose();
            _processBlocker?.Dispose();
            _disposed = true;

            Console.WriteLine("[PROCESS] Process monitor disposed.");
        }
    }

    // ===== PROCESS BLOCKER =====
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
        }

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
        }

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

                return shouldBlock;
            }
        }

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
                var oldKeys = _recentBlocks.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();

                foreach (var key in oldKeys)
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

            Console.WriteLine("[BLOCKER] Process blocker disposed.");
        }
    }
}
