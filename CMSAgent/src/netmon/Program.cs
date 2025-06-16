namespace netmon
{
    class Program
    {
        private static NetworkMonitor? _monitor;
        private static NetworkBlock? _blocker;
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
            {
                // Initialize components
                _monitor = new NetworkMonitor();
                _blocker = new NetworkBlock();

                // Load configuration
                BlocklistManager.Load("blacklist.json");

                // Show current blocklist
                var blockedDomains = BlocklistManager.GetBlockedDomains();
                Console.WriteLine($"[INIT] Active blocklist contains {blockedDomains.Count} domains:");
                foreach (var domain in blockedDomains)
                {
                    Console.WriteLine($"  - {domain}");
                }
                Console.WriteLine();

                // Subscribe to blocking events
                _blocker.DomainBlocked += OnDomainBlocked;
                _blocker.BlockingError += OnBlockingError;

                // Start background tasks
                var monitorTask = _monitor.StartMonitoringAsync();
                var displayTask = _monitor.DisplayRealTimeConnections(_cancellationTokenSource.Token);
                var blockingTask = _blocker.StartActiveBlockingAsync(_monitor);
                var consoleTask = StartConsoleInterfaceAsync(_cancellationTokenSource.Token);

                Console.WriteLine("Agent running with ACTIVE BLOCKING. Type 'help' for commands or Ctrl+C to exit.");

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
        }

        // ===== CONSOLE INTERFACE =====
        private static async Task StartConsoleInterfaceAsync(CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(100, cancellationToken);
                        if (Console.KeyAvailable)
                        {
                            var input = Console.ReadLine();
                            if (!string.IsNullOrEmpty(input))
                            {
                                ProcessCommand(input.Trim());
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CONSOLE] Error: {ex.Message}");
                    }
                }
            }, cancellationToken);
        }

        private static void ProcessCommand(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "help":
                case "h":
                    ShowHelp();
                    break;

                case "add":
                case "block":
                    if (parts.Length > 1)
                    {
                        var domain = parts[1];
                        AddToBlocklist(domain);
                    }
                    else
                    {
                        Console.WriteLine("[CMD] Usage: add <domain>");
                    }
                    break;

                case "remove":
                case "unblock":
                    if (parts.Length > 1)
                    {
                        var domain = parts[1];
                        RemoveFromBlocklist(domain);
                    }
                    else
                    {
                        Console.WriteLine("[CMD] Usage: remove <domain>");
                    }
                    break;

                case "list":
                case "show":
                    ShowBlocklist();
                    break;

                case "clear":
                    if (parts.Length > 1 && parts[1] == "firewall")
                    {
                        _blocker?.ClearFirewallRules();
                    }
                    else
                    {
                        Console.WriteLine("[CMD] Usage: clear firewall");
                    }
                    break;

                case "status":
                    ShowStatus();
                    break;
                case "test":
                    if (parts.Length > 1)
                    {
                        var domain = parts[1];
                        TestDomain(domain);
                    }
                    else
                    {
                        Console.WriteLine("[CMD] Usage: test <domain>");
                    }
                    break;

                case "testsub":
                    if (parts.Length > 1)
                    {
                        var baseDomain = parts[1];
                        TestSubdomains(baseDomain);
                    }
                    else
                    {
                        Console.WriteLine("[CMD] Usage: testsub <domain>");
                    }
                    break;

                case "hosts":
                    if (parts.Length > 1)
                    {
                        switch (parts[1].ToLower())
                        {
                            case "status":
                                _blocker?.ShowHostsFileStatus();
                                break;
                            case "restore":
                                _blocker?.RestoreHostsFile();
                                break;
                            case "block":
                                if (parts.Length > 2)
                                {
                                    _blocker?.BlockDomainWithHosts(parts[2]);
                                }
                                else
                                {
                                    Console.WriteLine("[CMD] Usage: hosts block <domain>");
                                }
                                break;
                            case "unblock":
                                if (parts.Length > 2)
                                {
                                    _blocker?.UnblockDomainFromHosts(parts[2]);
                                }
                                else
                                {
                                    Console.WriteLine("[CMD] Usage: hosts unblock <domain>");
                                }
                                break;
                            default:
                                Console.WriteLine("[CMD] Usage: hosts [status|restore|block <domain>|unblock <domain>]");
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[CMD] Usage: hosts [status|restore|block <domain>|unblock <domain>]");
                    }
                    break;

                default:
                    Console.WriteLine($"[CMD] Unknown command: {cmd}. Type 'help' for available commands.");
                    break;
            }
        }
        private static void ShowHelp()
        {
            Console.WriteLine("[HELP] Available commands:");
            Console.WriteLine("  add <domain>              - Add domain to blocklist");
            Console.WriteLine("  remove <domain>           - Remove domain from blocklist");
            Console.WriteLine("  list                      - Show current blocklist");
            Console.WriteLine("  status                    - Show firewall status");
            Console.WriteLine("  test <domain>             - Test if domain is blocked");
            Console.WriteLine("  testsub <domain>          - Test subdomain blocking for domain");
            Console.WriteLine("  clear firewall            - Clear all firewall rules");
            Console.WriteLine("  hosts status              - Show hosts file status");
            Console.WriteLine("  hosts restore             - Restore hosts file from backup");
            Console.WriteLine("  hosts block <domain>      - Block domain via hosts file");
            Console.WriteLine("  hosts unblock <domain>    - Unblock domain from hosts file");
            Console.WriteLine("  help                      - Show this help");
            Console.WriteLine("  Ctrl+C                    - Exit application");
            Console.WriteLine();
            Console.WriteLine("[HELP] Subdomain blocking notes:");
            Console.WriteLine("  - Domains are automatically blocked with all subdomains");
            Console.WriteLine("  - Use *.domain.com format to explicitly block all subdomains");
            Console.WriteLine("  - Example: facebook.com blocks www.facebook.com, m.facebook.com, etc.");
        }

        private static void AddToBlocklist(string domain)
        {
            BlocklistManager.AddDomain(domain);
            BlocklistManager.Save("blacklist.json");
            Console.WriteLine($"[BLOCKLIST] Added {domain} to blocklist");
        }

        private static void RemoveFromBlocklist(string domain)
        {
            BlocklistManager.RemoveDomain(domain);
            BlocklistManager.Save("blacklist.json");
            Console.WriteLine($"[BLOCKLIST] Removed {domain} from blocklist");
        }

        private static void ShowBlocklist()
        {
            var domains = BlocklistManager.GetBlockedDomains();
            Console.WriteLine($"[BLOCKLIST] Currently blocking {domains.Count} domains:");
            foreach (var domain in domains)
            {
                Console.WriteLine($"  - {domain}");
            }
        }

        private static void ShowStatus()
        {
            var domains = BlocklistManager.GetBlockedDomains();
            var trackedIPs = _monitor?.GetTrackedDomainCount() ?? 0;
            Console.WriteLine($"[STATUS] Firewall Status:");
            Console.WriteLine($"  Blocked domains: {domains.Count}");
            Console.WriteLine($"  Tracked IP mappings: {trackedIPs}");
            Console.WriteLine($"  Blocking mode: ACTIVE");
            Console.WriteLine($"  Monitor running: {_monitor != null}");
            Console.WriteLine($"  Blocker running: {_blocker != null}");
        }
        private static void TestDomain(string domain)
        {
            bool isBlocked = BlocklistManager.IsBlocked(domain);
            Console.WriteLine($"[TEST] Domain '{domain}' is {(isBlocked ? "BLOCKED" : "ALLOWED")}");
        }

        private static void TestSubdomains(string baseDomain)
        {
            Console.WriteLine($"[TEST] Testing subdomain blocking for '{baseDomain}':");

            // Test the base domain
            bool baseBlocked = BlocklistManager.IsBlocked(baseDomain);
            Console.WriteLine($"  {baseDomain} -> {(baseBlocked ? "BLOCKED" : "ALLOWED")}");

            // Test common subdomains
            var testSubdomains = new[]
            {
            $"www.{baseDomain}",
            $"mail.{baseDomain}",
            $"ftp.{baseDomain}",
            $"blog.{baseDomain}",
            $"shop.{baseDomain}",
            $"api.{baseDomain}",
            $"cdn.{baseDomain}",
            $"app.{baseDomain}",
            $"mobile.{baseDomain}",
            $"m.{baseDomain}",
            $"admin.{baseDomain}",
            $"test.{baseDomain}",
            $"dev.{baseDomain}",
            $"staging.{baseDomain}"
        };

            foreach (var subdomain in testSubdomains)
            {
                bool isBlocked = BlocklistManager.IsBlocked(subdomain);
                Console.WriteLine($"  {subdomain} -> {(isBlocked ? "BLOCKED" : "ALLOWED")}");
            }
            Console.WriteLine($"[TEST] Completed subdomain test for '{baseDomain}'");
        }
    }
}
