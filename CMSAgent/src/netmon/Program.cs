using System.Net.NetworkInformation;

namespace netmon
{    class Program
    {
        private static NetworkManager? _monitor;
        private static NetworkBlocker? _blocker;
        private static CancellationTokenSource? _cancellationTokenSource;

        static async Task Main(string[] args)
        {
            Console.Title = "Network Monitor (netmon)";
            Console.WriteLine("Starting Network Monitor with modular architecture...");

            _cancellationTokenSource = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancellationTokenSource?.Cancel();
                Console.WriteLine("\nShutting down Network Monitor...");
            };

            try
            {                // Initialize components
                _monitor = new NetworkManager();
                _blocker = new NetworkBlocker();                // Load configuration
                BlocklistManager.Load("blacklist.json");

                // Show current blocklist
                var blockedDomains = BlocklistManager.GetBlockedDomains();
                Console.WriteLine($"[INIT] Active blocklist contains {blockedDomains.Count} domains:");
                foreach (var domain in blockedDomains)
                {
                    Console.WriteLine($"  - {domain}");
                }
                Console.WriteLine();                // Subscribe to blocking events
                _blocker.DomainBlocked += OnDomainBlocked;
                _blocker.BlockingError += OnBlockingError;
                
                // Subscribe to DNS blocking events
                _monitor.DnsQueryBlocked += OnDnsQueryBlocked;
                _monitor.DnsQueryAllowed += OnDnsQueryAllowed;
                _monitor.BlockedDomainDetected += OnBlockedDomainDetected;// Start background tasks
                var monitorTask = _monitor.StartMonitoringAsync();
                var displayTask = DisplayRealTimeConnections(_cancellationTokenSource.Token);
                var blockingTask = _blocker.StartActiveBlockingAsync(_monitor);                Console.WriteLine("Agent running with ACTIVE BLOCKING & DNS-LEVEL BLOCKING. Press Ctrl+C to exit.");
                Console.WriteLine("� DNS queries for blocked domains will be redirected or dropped in real-time.");
                Console.WriteLine("✨ No hosts file modification required - works with standard user privileges.");
                Console.WriteLine();

                // Wait for cancellation
                await Task.Delay(-1, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Domain Firewall Agent stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                // Cleanup
                _monitor?.Dispose();
                _blocker?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }

        private static void OnDomainBlocked(string domain, System.Net.IPAddress ip, string process)
        {
            Console.WriteLine($"[EVENT] Blocked: {domain} ({ip}) - Process: {process}");
        }

        private static void OnBlockingError(string error)
        {
            Console.WriteLine($"[ERROR] {error}");
        }        private static void OnDnsQueryBlocked(string domain)
        {
            Console.WriteLine($"[DNS-BLOCK] � DNS query blocked for domain: {domain}");
        }

        private static void OnDnsQueryAllowed(string domain)
        {
            Console.WriteLine($"[DNS-ALLOW] ✅ DNS query allowed for domain: {domain}");
        }

        private static void OnBlockedDomainDetected(string domain)
        {
            Console.WriteLine($"[DNS-DETECT] ⚠️  Blocked domain access attempt: {domain} -> redirected to localhost");
        }// ===== DISPLAY FUNCTIONALITY =====
        private static Task DisplayRealTimeConnections(CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        Console.Clear();
                        Console.WriteLine("Domain Firewall Agent - Realtime Monitor");
                        Console.WriteLine($"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine("{0,-7} {1,-22} {2,-22} {3,-12} {4,-8} {5,-15} {6}",
                            "Proto", "Local Address", "Remote Address", "State", "PID", "Process", "Domain");
                        Console.WriteLine(new string('-', 120));

                        var connections = NetworkUtils.GetTcpConnections();
                        int connectionCount = 0;

                        foreach (var conn in connections)
                        {
                            if (conn.State != TcpState.Established) continue;
                            if (NetworkUtils.IsLoopbackOrPrivate(conn.RemoteAddress)) continue;

                            string domain = _monitor?.GetDomainForIP(conn.RemoteAddress) ?? "(unknown)";
                            string processName = NetworkUtils.GetProcessName(conn.ProcessId);
                            string localAddr = $"{conn.LocalAddress}:{conn.LocalPort}";
                            string remoteAddr = $"{conn.RemoteAddress}:{conn.RemotePort}";

                            Console.WriteLine("{0,-7} {1,-22} {2,-22} {3,-12} {4,-8} {5,-15} {6}",
                                "TCP",
                                localAddr,
                                remoteAddr,
                                conn.State,
                                conn.ProcessId,
                                processName,
                                domain);

                            connectionCount++;
                        }                        Console.WriteLine(new string('-', 120));
                        Console.WriteLine($"Total active external connections: {connectionCount}");
                        Console.WriteLine($"Tracked domains: {_monitor?.GetTrackedDomainCount() ?? 0}");
                        Console.WriteLine("Press Ctrl+C to exit");

                        await Task.Delay(3000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MONITOR] Display error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }                }
            }, cancellationToken);
        }
    }
}
