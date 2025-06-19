using System.Text.Json;

namespace sysmon
{
    /// <summary>
    /// Main entry point for System Monitor application
    /// Responsibilities:
    /// 1. Load and manage system configuration
    /// 2. Initialize SystemMonitor with configuration
    /// 3. Display real-time monitoring results
    /// 4. Handle graceful shutdown
    /// </summary>
    class Program
    {
        private static SystemMonitor? _systemMonitor;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static SystemConfig? _config;
        private static Timer? _displayTimer;

        static async Task Main(string[] args)
        {
            Console.Title = "System Monitor (sysmon) - Network & Process Monitoring";
            Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("                    SYSTEM MONITOR - NETWORK & PROCESS MONITORING");
            Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("Starting unified system monitoring...");

            _cancellationTokenSource = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancellationTokenSource?.Cancel();
                Console.WriteLine("\nShutting down System Monitor...");
            };            try
            {
                Console.WriteLine("🔧 STEP 1: Loading system configuration...");
                _config = await LoadConfigurationAsync();
                
                Console.WriteLine("🏗️  STEP 2: Initializing System Monitor with configuration...");
                _systemMonitor = new SystemMonitor(_config);

                Console.WriteLine("🔔 STEP 3: Setting up event handlers for monitoring display...");
                SubscribeToSystemMonitorEvents();

                Console.WriteLine("📊 STEP 4: Starting statistics display timer...");
                _displayTimer = new Timer(DisplayStatistics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

                Console.WriteLine("📋 STEP 5: Displaying configuration summary...");
                ShowConfigurationSummary();

                Console.WriteLine("🚀 STEP 6: Starting monitoring services...");
                await _systemMonitor.StartMonitoringAsync(_cancellationTokenSource.Token);

                Console.WriteLine("System Monitor running. Press Ctrl+C to exit.");
                Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════");
                Console.WriteLine("🔍 Process Monitoring: Active blocking based on process blacklist/whitelist");
                Console.WriteLine("🌐 Network Monitoring: DNS-level blocking and connection tracking");
                Console.WriteLine("🛡️ Firewall Integration: Automatic rule creation for blocked domains");
                Console.WriteLine("📊 Real-time Display: Live monitoring statistics and activity logs");
                Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════");

                // Keep the application running
                await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("System monitoring canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Cleaning up resources...");
                await CleanupAsync();
                Console.WriteLine("System Monitor stopped.");
            }
        }        /// <summary>
        /// Display comprehensive configuration summary
        /// Shows what configuration has been loaded and will be used for monitoring
        /// </summary>
        private static void ShowConfigurationSummary()
        {
            if (_config == null) return;

            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("                           📋 LOADED CONFIGURATION SUMMARY");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════");
            
            // General configuration
            Console.WriteLine("⚙️  GENERAL SETTINGS:");
            Console.WriteLine($"    🛡️  Firewall Integration: {(_config.General.EnableFirewallIntegration ? "✅ Enabled" : "❌ Disabled")}");
            Console.WriteLine($"    ⏱️  Real-time Monitoring: {(_config.General.EnableRealTimeMonitoring ? "✅ Enabled" : "❌ Disabled")}");
            Console.WriteLine($"    � Scan Interval: {_config.General.ScanIntervalSeconds} seconds");
            Console.WriteLine($"    🧹 Cleanup Interval: {_config.General.CleanupIntervalMinutes} minutes");
            Console.WriteLine($"    📝 Log Level: {_config.General.LogLevel}");
            
            Console.WriteLine("\n🔧 PROCESS MONITORING:");
            Console.WriteLine($"    📊 Status: {(_config.ProcessBlocking.Enabled ? "✅ Enabled" : "❌ Disabled")}");
            Console.WriteLine($"    🎯 Mode: {_config.ProcessBlocking.Mode.ToUpper()}");
            Console.WriteLine($"    ⚫ Blacklist ({_config.ProcessBlocking.Blacklist.Count} processes):");
            if (_config.ProcessBlocking.Blacklist.Any())
            {
                Console.WriteLine($"       {string.Join(", ", _config.ProcessBlocking.Blacklist.Take(10))}");
                if (_config.ProcessBlocking.Blacklist.Count > 10)
                    Console.WriteLine($"       ... and {_config.ProcessBlocking.Blacklist.Count - 10} more");
            }
            else
            {
                Console.WriteLine("       (No processes in blacklist)");
            }
            
            Console.WriteLine($"    ⚪ Whitelist ({_config.ProcessBlocking.Whitelist.Count} processes):");
            if (_config.ProcessBlocking.Whitelist.Any())
            {
                Console.WriteLine($"       {string.Join(", ", _config.ProcessBlocking.Whitelist.Take(10))}");
                if (_config.ProcessBlocking.Whitelist.Count > 10)
                    Console.WriteLine($"       ... and {_config.ProcessBlocking.Whitelist.Count - 10} more");
            }
            else
            {
                Console.WriteLine("       (No processes in whitelist)");
            }

            Console.WriteLine("\n🌐 NETWORK MONITORING:");
            Console.WriteLine($"    📊 Status: {(_config.NetworkBlocking.Enabled ? "✅ Enabled" : "❌ Disabled")}");
            Console.WriteLine($"    � Mode: {_config.NetworkBlocking.Mode.ToUpper()}");
            Console.WriteLine($"    🚫 Blocked Domains ({_config.NetworkBlocking.Domains.Count} domains):");
            if (_config.NetworkBlocking.Domains.Any())
            {
                Console.WriteLine($"       {string.Join(", ", _config.NetworkBlocking.Domains.Take(10))}");
                if (_config.NetworkBlocking.Domains.Count > 10)
                    Console.WriteLine($"       ... and {_config.NetworkBlocking.Domains.Count - 10} more");
            }
            else
            {
                Console.WriteLine("       (No domains blocked)");
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════\n");
        }private static async Task CleanupAsync()
        {
            try
            {
                _displayTimer?.Dispose();
                _systemMonitor?.Dispose();
                _cancellationTokenSource?.Dispose();
                await Task.Delay(500); // Give cleanup time to complete
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        private static async Task<SystemConfig> LoadConfigurationAsync()
        {
            const string configPath = "config.json";
            
            try
            {
                Console.WriteLine($"[CONFIG] Loading configuration from: {configPath}");

                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"[CONFIG] Configuration file not found, creating default: {configPath}");
                    return await CreateDefaultConfigurationAsync(configPath);
                }

                var json = await File.ReadAllTextAsync(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                var config = JsonSerializer.Deserialize<SystemConfig>(json, options) ?? new SystemConfig();

                Console.WriteLine("[CONFIG] Configuration loaded successfully:");
                Console.WriteLine($"  - Network Blocking: {(config.NetworkBlocking.Enabled ? "Enabled" : "Disabled")} ({config.NetworkBlocking.Mode})");
                Console.WriteLine($"    Blocked Domains: {config.NetworkBlocking.Domains.Count}");
                Console.WriteLine($"  - Process Blocking: {(config.ProcessBlocking.Enabled ? "Enabled" : "Disabled")} ({config.ProcessBlocking.Mode})");
                Console.WriteLine($"    Blacklist: {config.ProcessBlocking.Blacklist.Count}, Whitelist: {config.ProcessBlocking.Whitelist.Count}");
                Console.WriteLine($"  - General Settings: Firewall={config.General.EnableFirewallIntegration}, RealTime={config.General.EnableRealTimeMonitoring}");

                // Check for legacy configurations and migrate if needed
                await MigrateLegacyConfigurationsAsync(config, configPath);

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] Error loading configuration: {ex.Message}");
                Console.WriteLine("[CONFIG] Using default configuration");
                return new SystemConfig();
            }
        }

        private static async Task<SystemConfig> CreateDefaultConfigurationAsync(string configPath)
        {
            var config = new SystemConfig
            {
                NetworkBlocking = new NetworkBlockingConfig
                {
                    Enabled = true,
                    Mode = "blacklist",
                    Domains = new List<string>
                    {
                        "malicious-site.com",
                        "ad-tracker.net",
                        "spyware-domain.org",
                        "facebook.com",
                        "instagram.com",
                        "tiktok.com",
                        "twitter.com",
                        "youtube.com",
                        "netflix.com",
                        "metruyencv.com"
                    }
                },
                ProcessBlocking = new ProcessBlockingConfig
                {
                    Enabled = true,
                    Mode = "blacklist",
                    Blacklist = new List<string> 
                    { 
                        "msedge.exe",
                        "discord.exe",
                        "spotify.exe",
                        "chrome.exe",
                        "firefox.exe"
                    },
                    Whitelist = new List<string> 
                    { 
                        "notepad.exe",
                        "calc.exe",
                        "Code.exe",
                        "cmd.exe",
                        "powershell.exe"
                    }
                },
                General = new GeneralConfig
                {
                    EnableFirewallIntegration = true,
                    EnableRealTimeMonitoring = true,
                    ScanIntervalSeconds = 3,
                    CleanupIntervalMinutes = 5,
                    LogLevel = "info"
                }
            };

            await SaveConfigurationAsync(config, configPath);
            return config;
        }

        private static async Task SaveConfigurationAsync(SystemConfig config, string configPath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(configPath, json);

                Console.WriteLine($"[CONFIG] Configuration saved to: {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] Error saving configuration: {ex.Message}");
            }
        }

        private static async Task MigrateLegacyConfigurationsAsync(SystemConfig config, string configPath)
        {
            bool migrationOccurred = false;

            try
            {
                // Try to load from legacy blacklist.json
                if (File.Exists("blacklist.json"))
                {
                    Console.WriteLine("[CONFIG] Migrating from legacy blacklist.json");
                    var blacklistJson = await File.ReadAllTextAsync("blacklist.json");
                    var blacklistConfig = JsonSerializer.Deserialize<LegacyBlacklistConfig>(blacklistJson);
                    
                    if (blacklistConfig?.blockedDomains != null)
                    {
                        foreach (var domain in blacklistConfig.blockedDomains)
                        {
                            if (!config.NetworkBlocking.Domains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                            {
                                config.NetworkBlocking.Domains.Add(domain);
                            }
                        }
                        migrationOccurred = true;
                    }
                }

                // Try to load from legacy processlist.json
                if (File.Exists("processlist.json"))
                {
                    Console.WriteLine("[CONFIG] Migrating from legacy processlist.json");
                    var processlistJson = await File.ReadAllTextAsync("processlist.json");
                    var processConfig = JsonSerializer.Deserialize<LegacyProcessConfig>(processlistJson);
                    
                    if (processConfig != null)
                    {
                        if (!string.IsNullOrEmpty(processConfig.mode))
                            config.ProcessBlocking.Mode = processConfig.mode;

                        if (processConfig.blacklist != null)
                        {
                            foreach (var process in processConfig.blacklist)
                            {
                                if (!config.ProcessBlocking.Blacklist.Contains(process, StringComparer.OrdinalIgnoreCase))
                                {
                                    config.ProcessBlocking.Blacklist.Add(process);
                                }
                            }
                        }

                        if (processConfig.whitelist != null)
                        {
                            foreach (var process in processConfig.whitelist)
                            {
                                if (!config.ProcessBlocking.Whitelist.Contains(process, StringComparer.OrdinalIgnoreCase))
                                {
                                    config.ProcessBlocking.Whitelist.Add(process);
                                }
                            }
                        }
                        migrationOccurred = true;
                    }
                }

                if (migrationOccurred)
                {
                    await SaveConfigurationAsync(config, configPath);
                    Console.WriteLine("[CONFIG] Legacy configuration migration completed and saved");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] Error during legacy migration: {ex.Message}");
            }
        }        /// <summary>
        /// Subscribe to SystemMonitor events for real-time display
        /// This is where we receive and display monitoring results
        /// </summary>
        private static void SubscribeToSystemMonitorEvents()
        {
            if (_systemMonitor == null) return;

            Console.WriteLine("[EVENT] 🔔 Subscribing to monitoring events for real-time display...");
            
            // Subscribe to events for display
            _systemMonitor.DomainBlocked += OnDomainBlocked;
            _systemMonitor.DnsQueryBlocked += OnDnsQueryBlocked;
            _systemMonitor.ConnectionDetected += OnConnectionDetected;
            _systemMonitor.ProcessBlocked += OnProcessBlocked;
            _systemMonitor.BlockingError += OnBlockingError;
            
            Console.WriteLine("[EVENT] ✅ Event handlers configured for real-time monitoring display");
        }

        // === REAL-TIME MONITORING RESULT DISPLAY HANDLERS ===
        
        /// <summary>
        /// Display domain blocking results
        /// </summary>
        private static void OnDomainBlocked(DomainBlockedEventArgs args)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] 🚫 NETWORK BLOCK: {args.Domain} ({args.IpAddress}) ← {args.ProcessName}");
        }

        /// <summary>
        /// Display DNS query blocking results
        /// </summary>
        private static void OnDnsQueryBlocked(string domain)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] 🔍 DNS BLOCK: Query for '{domain}' was blocked");
        }

        /// <summary>
        /// Display connection detection results
        /// </summary>
        private static void OnConnectionDetected(ConnectionDetectedEventArgs args)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] 🔗 CONNECTION: {args.ProcessName} → {args.Domain} ({args.IpAddress})");
        }

        /// <summary>
        /// Display process blocking results
        /// </summary>
        private static void OnProcessBlocked(ProcessBlockedEventArgs args)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] ⛔ PROCESS BLOCK: '{args.ProcessName}' (PID: {args.ProcessId}) was terminated");
        }

        /// <summary>
        /// Display blocking errors
        /// </summary>
        private static void OnBlockingError(string error)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] 🔴 BLOCKING ERROR: {error}");
        }/// <summary>
        /// Display real-time monitoring statistics
        /// This is the main display function that shows monitoring results
        /// </summary>
        private static void DisplayStatistics(object? state)
        {
            try
            {
                if (_systemMonitor == null) return;

                var stats = _systemMonitor.GetStatistics();
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                Console.WriteLine($"\n╔══════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║                    📊 SYSTEM MONITOR LIVE STATISTICS [{timestamp}]                    ║");
                Console.WriteLine($"╠══════════════════════════════════════════════════════════════════════════════════╣");
                
                // System uptime
                Console.WriteLine($"║ ⏱️  System Uptime: {stats.Uptime:dd\\:hh\\:mm\\:ss} (Days:Hours:Minutes:Seconds)        ║");
                
                // Network statistics
                Console.WriteLine($"║                                                                                  ║");
                Console.WriteLine($"║ 🌐 NETWORK MONITORING:                                                          ║");
                Console.WriteLine($"║    📡 Active Connections: {stats.NetworkStats.ActiveConnections,-3} (external connections)                    ║");
                Console.WriteLine($"║    🔍 Tracked Domains: {stats.NetworkStats.TrackedDomains,-6} (DNS resolved domains)                  ║");
                Console.WriteLine($"║    🚫 Blocked Connections: {stats.TotalConnectionsBlocked,-4} (total since start)                      ║");
                
                // Process statistics  
                Console.WriteLine($"║                                                                                  ║");
                Console.WriteLine($"║ � PROCESS MONITORING:                                                          ║");
                Console.WriteLine($"║    ✅ Running Processes: {stats.ProcessStats.Running,-5} (allowed processes)                       ║");
                Console.WriteLine($"║    ⛔ Blocked Processes: {stats.ProcessStats.Blocked,-5} (currently blocked)                      ║");                Console.WriteLine($"║    📊 Total Processes: {stats.ProcessStats.Total,-6} (total monitored)                        ║");
                Console.WriteLine($"║    🚫 Total Blocked: {stats.TotalProcessesBlocked,-8} (total since start)                        ║");
                Console.WriteLine($"║                                                                                  ║");
                
                // Display detailed process information
                DisplayProcessDetails();
                
                // Summary
                Console.WriteLine($"║                                                                                  ║");
                Console.WriteLine($"║ 📈 BLOCKING EFFECTIVENESS:                                                      ║");
                var processBlockRate = stats.ProcessStats.Total > 0 ? (stats.ProcessStats.Blocked * 100.0 / stats.ProcessStats.Total) : 0;
                Console.WriteLine($"║    Process Block Rate: {processBlockRate:F1}% ({stats.ProcessStats.Blocked}/{stats.ProcessStats.Total})                                   ║");
                Console.WriteLine($"║    Total Actions: {stats.TotalProcessesBlocked + stats.TotalConnectionsBlocked,-8} (processes + network blocks)               ║");
                
                Console.WriteLine($"╚══════════════════════════════════════════════════════════════════════════════════╝\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DISPLAY ERROR] ❌ Error displaying statistics: {ex.Message}");
            }
        }        /// <summary>
        /// Display detailed information about monitored processes
        /// </summary>
        private static void DisplayProcessDetails()
        {
            try
            {
                if (_systemMonitor == null) return;

                var processes = _systemMonitor.GetMonitoredProcesses();
                if (processes.Count == 0)
                {
                    Console.WriteLine("║    🔍 No processes currently being monitored                                    ║");
                    return;
                }

                var runningProcesses = processes.Where(p => !p.IsBlocked).ToList();
                var blockedProcesses = processes.Where(p => p.IsBlocked).ToList();

                // Display running processes grouped by name
                if (runningProcesses.Count > 0)
                {
                    Console.WriteLine("║    ✅ RUNNING PROCESSES BY NAME:                                                ║");
                    var runningGroups = runningProcesses
                        .GroupBy(p => p.ProcessName)
                        .Select(g => new 
                        { 
                            Name = g.Key, 
                            Count = g.Count(),
                            PIDs = g.Select(p => p.ProcessId).OrderBy(p => p).ToList()
                        })
                        .OrderBy(g => g.Name)
                        .Take(8)
                        .ToList();

                    foreach (var group in runningGroups)
                    {
                        var pidsDisplay = group.Count > 3
                            ? $"{string.Join(",", group.PIDs.Take(3))}... (+{group.Count - 3})"
                            : string.Join(",", group.PIDs);
                        
                        var displayLine = $"       {group.Name} [{group.Count}] PIDs: {pidsDisplay}";
                        if (displayLine.Length > 78) displayLine = displayLine.Substring(0, 75) + "...";
                        Console.WriteLine($"║{displayLine,-82}║");
                    }
                    
                    if (runningProcesses.GroupBy(p => p.ProcessName).Count() > 8)
                    {
                        var remaining = runningProcesses.GroupBy(p => p.ProcessName).Count() - 8;
                        Console.WriteLine($"║       ... and {remaining} more process types                                       ║");
                    }
                }

                // Display blocked processes
                if (blockedProcesses.Count > 0)
                {
                    Console.WriteLine("║    ⛔ BLOCKED PROCESSES:                                                        ║");
                    var blockedGroups = blockedProcesses
                        .GroupBy(p => p.ProcessName)
                        .Select(g => new 
                        { 
                            Name = g.Key, 
                            Count = g.Count(),
                            LastBlocked = g.OrderByDescending(p => p.StartTime).FirstOrDefault()?.StartTime ?? DateTime.MinValue
                        })
                        .OrderBy(g => g.Name)
                        .Take(5)
                        .ToList();

                    foreach (var group in blockedGroups)
                    {
                        var timeAgo = group.LastBlocked != DateTime.MinValue ? 
                            $"last: {(DateTime.Now - group.LastBlocked).TotalMinutes:F0}m ago" : "time: unknown";
                        var displayLine = $"       {group.Name} [{group.Count} blocked] {timeAgo}";
                        if (displayLine.Length > 78) displayLine = displayLine.Substring(0, 75) + "...";
                        Console.WriteLine($"║{displayLine,-82}║");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"║    ❌ Error displaying process details: {ex.Message}                            ║");
            }
        }

        // Legacy configuration models for migration
        private class LegacyBlacklistConfig
        {
            public List<string>? blockedDomains { get; set; }
        }        private class LegacyProcessConfig
        {
            public string? mode { get; set; }
            public List<string>? blacklist { get; set; }
            public List<string>? whitelist { get; set; }
        }
    }
}
