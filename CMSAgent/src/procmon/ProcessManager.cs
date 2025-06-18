using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;

namespace procmon
{
    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool IsBlocked { get; set; }
        public string Status => IsBlocked ? "BLOCKED" : "RUNNING";
    }

    public class ProcessListConfig
    {
        public string Mode { get; set; } = "blacklist";
        public List<string> Blacklist { get; set; } = new();
        public List<string> Whitelist { get; set; } = new();
    }

    public class ProcessGroupInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<int> PIDs { get; set; } = new();
        public DateTime LatestStartTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
    public class ProcessManager : IDisposable
    {
        private readonly ConcurrentDictionary<int, ProcessInfo> _processes = new();
        private readonly Timer _scanTimer;
        private readonly ProcessBlocker _processBlocker;
        private bool _disposed = false;

        // Configuration
        private string _currentMode = "blacklist";
        private HashSet<string> _blacklist = new();
        private HashSet<string> _whitelist = new();
        private readonly object _configLock = new();

        // Events
        public event Action<ProcessInfo>? ProcessBlocked;
        public event Action<string>? BlockingError; public ProcessManager()
        {
            _processBlocker = new ProcessBlocker();
            _processBlocker.ProcessBlocked += (processInfo) => ProcessBlocked?.Invoke(processInfo);
            _processBlocker.BlockingError += (error) => BlockingError?.Invoke(error);

            _scanTimer = new Timer(ScanProcesses, null, Timeout.Infinite, Timeout.Infinite);
        }        /// <summary>
                 /// Load configuration from JSON string
                 /// </summary>
        public void LoadConfiguration(string jsonConfig)
        {
            try
            {
                Console.WriteLine($"[CONFIG] Deserializing JSON config...");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var config = JsonSerializer.Deserialize<ProcessListConfig>(jsonConfig, options);
                Console.WriteLine($"[CONFIG] Deserialized config - Mode: {config?.Mode}, Blacklist count: {config?.Blacklist?.Count}, Whitelist count: {config?.Whitelist?.Count}");

                lock (_configLock)
                {
                    _blacklist.Clear();
                    _whitelist.Clear();

                    _currentMode = config?.Mode ?? "blacklist";

                    if (config?.Blacklist != null)
                    {
                        foreach (var process in config.Blacklist)
                        {
                            Console.WriteLine($"[CONFIG] Adding to blacklist: {process}");
                            _blacklist.Add(process.ToLowerInvariant());
                        }
                    }

                    if (config?.Whitelist != null)
                    {
                        foreach (var process in config.Whitelist)
                        {
                            Console.WriteLine($"[CONFIG] Adding to whitelist: {process}");
                            _whitelist.Add(process.ToLowerInvariant());
                        }
                    }

                    // Update the blocker configuration
                    _processBlocker.UpdateConfiguration(_currentMode, _blacklist, _whitelist);
                }

                Console.WriteLine($"[CONFIG] Loaded {_currentMode} mode - Black: {_blacklist.Count}, White: {_whitelist.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] Error loading configuration: {ex.Message}");
                Console.WriteLine($"[CONFIG] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Load configuration from JSON file
        /// </summary>
        public void LoadConfigurationFromFile(string filePath)
        {
            try
            {
                Console.WriteLine($"[CONFIG] Attempting to load from: {filePath}");

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[CONFIG] Process list file not found: {filePath}");
                    return;
                }

                var json = File.ReadAllText(filePath);
                Console.WriteLine($"[CONFIG] File content: {json}");
                LoadConfiguration(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] Error loading from file: {ex.Message}");
            }
        }

        /// <summary>
        /// Start process monitoring and blocking
        /// </summary>
        public async Task StartAsync()
        {
            Console.WriteLine("[MANAGER] Starting process monitoring and blocking...");

            // Initial scan
            await Task.Run(() => ScanProcesses());

            // Start periodic scanning every 3 seconds
            _scanTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Stop process monitoring
        /// </summary>
        public void Stop()
        {
            _scanTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Console.WriteLine("[MANAGER] Process monitoring stopped.");
        }

        /// <summary>
        /// Get all current processes as JSON array
        /// </summary>
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
                Console.WriteLine($"[MANAGER] Error serializing processes: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>
        /// Get grouped processes as JSON array
        /// </summary>
        public string GetGroupedProcessesAsJson()
        {
            try
            {
                var runningProcesses = _processes.Values.Where(p => !p.IsBlocked).ToList();
                var blockedProcesses = _processes.Values.Where(p => p.IsBlocked).ToList();

                var groupedRunning = runningProcesses
                    .GroupBy(p => p.ProcessName)
                    .Select(g => new ProcessGroupInfo
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
                    .Select(g => new ProcessGroupInfo
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
                Console.WriteLine($"[MANAGER] Error serializing grouped processes: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// Get simple statistics
        /// </summary>
        public (int running, int blocked, int total) GetStatistics()
        {
            var running = _processes.Values.Count(p => !p.IsBlocked);
            var blocked = _processes.Values.Count(p => p.IsBlocked);
            return (running, blocked, _processes.Count);
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
                Console.WriteLine($"[MANAGER] Scan error: {ex.Message}");
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
        public void Dispose()
        {
            if (_disposed) return;

            _scanTimer?.Dispose();
            _processBlocker?.Dispose();
            _disposed = true;

            Console.WriteLine("[MANAGER] Disposed.");
        }
    }
}
