using System.Net;
using System.Text.Json;

namespace sysmon
{/// <summary>
    /// Unified system monitor that combines network and process monitoring capabilities
    /// </summary>
    public class SystemMonitor : IDisposable
    {
        private readonly NetworkMonitor _networkMonitor;
        private readonly ProcessMonitor _processMonitor;
        private readonly SystemConfig _config;
        private bool _disposed = false;

        // Statistics tracking
        private int _totalProcessesBlocked = 0;
        private int _totalConnectionsBlocked = 0;
        private DateTime _startTime;

        // Events for external display handling
        public event Action<DomainBlockedEventArgs>? DomainBlocked;
        public event Action<string>? DnsQueryBlocked;
        public event Action<ConnectionDetectedEventArgs>? ConnectionDetected;
        public event Action<ProcessBlockedEventArgs>? ProcessBlocked;
        public event Action<string>? BlockingError;

        public SystemMonitor(SystemConfig config)
        {
            _startTime = DateTime.Now;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _networkMonitor = new NetworkMonitor();
            _processMonitor = new ProcessMonitor();

            // Subscribe to events
            SubscribeToEvents();

            // Apply configuration to monitors
            ApplyConfiguration();
        }private void SubscribeToEvents()
        {
            // Network events
            _networkMonitor.DomainBlocked += OnDomainBlocked;
            _networkMonitor.DnsQueryBlocked += OnDnsQueryBlocked;
            _networkMonitor.ConnectionDetected += OnConnectionDetected;

            // Process events
            _processMonitor.ProcessBlocked += OnProcessBlocked;
            _processMonitor.BlockingError += OnBlockingError;
        }

        private void ApplyConfiguration()
        {
            Console.WriteLine("[SYSMON] Applying configuration to monitors...");

            // Apply configuration to monitors synchronously since we're in constructor
            _networkMonitor.LoadConfigurationAsync(_config.NetworkBlocking).Wait();
            _processMonitor.LoadConfigurationAsync(_config.ProcessBlocking).Wait();

            Console.WriteLine("[SYSMON] Configuration applied successfully.");
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[SYSMON] Starting unified monitoring...");

            try
            {
                // Start both monitors
                var networkTask = _networkMonitor.StartAsync(cancellationToken);
                var processTask = _processMonitor.StartAsync(cancellationToken);

                // Wait for both to complete (or cancellation)
                await Task.WhenAll(networkTask, processTask);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[SYSMON] Monitoring stopped by user.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSMON] Error in monitoring: {ex.Message}");
                throw;
            }
        }

        public void StopMonitoring()
        {
            _networkMonitor.Stop();
            _processMonitor.Stop();
        }        // Event handlers - now just fire events instead of displaying
        private void OnDomainBlocked(string domain, IPAddress ip, string processName)
        {
            _totalConnectionsBlocked++;
            DomainBlocked?.Invoke(new DomainBlockedEventArgs 
            { 
                Domain = domain, 
                IpAddress = ip, 
                ProcessName = processName 
            });
        }

        private void OnDnsQueryBlocked(string domain)
        {
            DnsQueryBlocked?.Invoke(domain);
        }

        private void OnConnectionDetected(string domain, IPAddress ip, string processName)
        {
            ConnectionDetected?.Invoke(new ConnectionDetectedEventArgs 
            { 
                Domain = domain, 
                IpAddress = ip, 
                ProcessName = processName 
            });
        }

        private void OnProcessBlocked(string processName, int processId)
        {
            _totalProcessesBlocked++;
            ProcessBlocked?.Invoke(new ProcessBlockedEventArgs 
            { 
                ProcessName = processName, 
                ProcessId = processId 
            });
        }

        private void OnBlockingError(string error)
        {
            BlockingError?.Invoke(error);
        }        // Statistics accessor - replaces DisplayStatistics
        public SystemStatistics GetStatistics()
        {
            try
            {
                var uptime = DateTime.Now - _startTime;
                var networkStats = _networkMonitor.GetStatistics();
                var processStats = _processMonitor.GetStatistics();

                return new SystemStatistics
                {
                    Uptime = uptime,
                    TotalProcessesBlocked = _totalProcessesBlocked,
                    TotalConnectionsBlocked = _totalConnectionsBlocked,
                    NetworkStats = new NetworkStats
                    {
                        ActiveConnections = networkStats.ActiveConnections,
                        TrackedDomains = networkStats.TrackedDomains
                    },                    ProcessStats = new ProcessStats
                    {
                        Running = processStats.RunningProcesses,
                        Blocked = processStats.BlockedProcesses,
                        Total = processStats.TotalProcesses
                    }
                };
            }
            catch (Exception)
            {
                // Return empty statistics on error
                return new SystemStatistics
                {
                    Uptime = DateTime.Now - _startTime,
                    TotalProcessesBlocked = _totalProcessesBlocked,
                    TotalConnectionsBlocked = _totalConnectionsBlocked
                };
            }
        }        /// <summary>
        /// Get list of monitored processes for display
        /// </summary>
        public List<ProcessInfo> GetMonitoredProcesses()
        {
            return _processMonitor.GetMonitoredProcesses();
        }

        // Configuration accessor - simplified
        public SystemConfig GetCurrentConfiguration()
        {
            return _config;
        }

        /// <summary>
        /// Get basic process and network information as JSON
        /// </summary>
        public string GetMonitoringStatusAsJson()
        {
            try
            {
                var stats = GetStatistics();
                var status = new
                {
                    Uptime = stats.Uptime.ToString(@"hh\:mm\:ss"),
                    NetworkBlocking = new
                    {
                        DomainsCount = _config.NetworkBlocking.Domains.Count,
                        stats.NetworkStats.ActiveConnections,
                        stats.NetworkStats.TrackedDomains
                    },
                    ProcessBlocking = new
                    {
                        _config.ProcessBlocking.Mode,
                        BlacklistCount = _config.ProcessBlocking.Blacklist.Count,
                        WhitelistCount = _config.ProcessBlocking.Whitelist.Count,
                        stats.ProcessStats.Running,
                        stats.ProcessStats.Blocked
                    },
                    TotalBlocked = new
                    {
                        Processes = stats.TotalProcessesBlocked,
                        Connections = stats.TotalConnectionsBlocked
                    }
                };

                return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSMON] Error getting monitoring status: {ex.Message}");
                return "{}";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                Console.WriteLine("[SYSMON] Disposing resources...");
                
                _networkMonitor?.Dispose();
                _processMonitor?.Dispose();

                _disposed = true;
                Console.WriteLine("[SYSMON] Resources disposed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSMON] Error during disposal: {ex.Message}");
            }
        }
        // ===== CORE CONFIGURATION METHODS =====
        
        /// <summary>
        /// Update the entire system configuration
        /// </summary>
        public async Task UpdateSystemConfigurationAsync(SystemConfig newConfig)
        {
            try
            {
                Console.WriteLine("[SYSMON] Updating system configuration...");
                
                _config.NetworkBlocking = newConfig.NetworkBlocking;
                _config.ProcessBlocking = newConfig.ProcessBlocking;
                _config.General = newConfig.General;

                // Apply updated configuration to monitors
                await _networkMonitor.LoadConfigurationAsync(_config.NetworkBlocking);
                await _processMonitor.LoadConfigurationAsync(_config.ProcessBlocking);

                Console.WriteLine("[SYSMON] System configuration updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSMON] Error updating system configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update domains in network blacklist
        /// </summary>
        public async Task UpdateDomainsAsync(List<string> domains)
        {
            try
            {
                Console.WriteLine($"[SYSMON] Updating {domains.Count} domains in blacklist...");
                
                _config.NetworkBlocking.Domains = domains;
                await _networkMonitor.LoadConfigurationAsync(_config.NetworkBlocking);
                
                Console.WriteLine("[SYSMON] Domains updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSMON] Error updating domains: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update processes in blacklist/whitelist
        /// </summary>
        public async Task UpdateProcessListsAsync(List<string> blacklist, List<string> whitelist)
        {
            try
            {
                Console.WriteLine($"[SYSMON] Updating process lists - Blacklist: {blacklist.Count}, Whitelist: {whitelist.Count}");
                
                _config.ProcessBlocking.Blacklist = blacklist;
                _config.ProcessBlocking.Whitelist = whitelist;
                await _processMonitor.LoadConfigurationAsync(_config.ProcessBlocking);
                
                Console.WriteLine("[SYSMON] Process lists updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSMON] Error updating process lists: {ex.Message}");
                throw;
            }
        }

        // ===== CORE INFORMATION METHODS =====
    }
}
