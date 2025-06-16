using System.Net;
using System.Diagnostics;
using System.Text.Json;

namespace netmon
{
    // ===== BLOCKLIST MANAGER =====
    public static class BlocklistManager
    {
        private static HashSet<string> _blocked = new();
        private static readonly object _lockObject = new();

        public static void Load(string path)
        {
            lock (_lockObject)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var model = JsonSerializer.Deserialize<BlockConfig>(json);
                        if (model?.blockedDomains != null)
                        {
                            _blocked = new HashSet<string>(model.blockedDomains, StringComparer.OrdinalIgnoreCase);
                            Console.WriteLine($"[BLOCKLIST] Loaded {model.blockedDomains.Count} blocked domains from {path}");
                        }
                    }
                    else
                    {
                        // No default blocklist creation - only read from existing blacklist.json
                        Console.WriteLine($"[BLOCKLIST] File {path} not found. No domains will be blocked.");
                        _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BLOCKLIST] Failed to load blocklist: {ex.Message}");
                }
            }
        }
        public static bool IsBlocked(string domain)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(domain)) return false;

                // Clean the domain (remove protocol, path, etc.)
                domain = CleanDomain(domain);

                foreach (var entry in _blocked)
                {
                    if (IsSubdomainMatch(domain, entry))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private static string CleanDomain(string domain)
        {
            // Remove protocol if present
            if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                domain = domain.Substring(7);
            else if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                domain = domain.Substring(8);

            // Remove path and query parameters
            var pathIndex = domain.IndexOf('/');
            if (pathIndex > 0)
                domain = domain.Substring(0, pathIndex);

            // Remove port
            var portIndex = domain.LastIndexOf(':');
            if (portIndex > 0 && portIndex < domain.Length - 1)
            {
                if (int.TryParse(domain.Substring(portIndex + 1), out _))
                    domain = domain.Substring(0, portIndex);
            }

            return domain.Trim().ToLowerInvariant();
        }

        private static bool IsSubdomainMatch(string domain, string blockedDomain)
        {
            blockedDomain = blockedDomain.ToLowerInvariant();
            domain = domain.ToLowerInvariant();

            // Exact match
            if (domain == blockedDomain)
                return true;

            // Subdomain match - domain ends with .blockedDomain
            if (domain.EndsWith("." + blockedDomain, StringComparison.OrdinalIgnoreCase))
                return true;

            // If blocked domain starts with *, match any subdomain
            if (blockedDomain.StartsWith("*."))
            {
                var rootDomain = blockedDomain.Substring(2);
                if (domain == rootDomain || domain.EndsWith("." + rootDomain))
                    return true;
            }

            return false;
        }

        public static void AddDomain(string domain)
        {
            lock (_lockObject)
            {
                if (!string.IsNullOrEmpty(domain))
                {
                    _blocked.Add(domain);
                    Console.WriteLine($"[BLOCKLIST] Added domain: {domain}");
                }
            }
        }

        public static void RemoveDomain(string domain)
        {
            lock (_lockObject)
            {
                if (_blocked.Remove(domain))
                {
                    Console.WriteLine($"[BLOCKLIST] Removed domain: {domain}");
                }
            }
        }

        public static List<string> GetBlockedDomains()
        {
            lock (_lockObject)
            {
                return new List<string>(_blocked);
            }
        }

        public static void Save(string path)
        {
            lock (_lockObject)
            {
                try
                {
                    var config = new BlockConfig { blockedDomains = new List<string>(_blocked) };
                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                    Console.WriteLine($"[BLOCKLIST] Saved {_blocked.Count} domains to {path}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BLOCKLIST] Failed to save blocklist: {ex.Message}");
                }
            }
        }

        private class BlockConfig
        {
            public List<string> blockedDomains { get; set; } = new();
        }
    }

    // ===== NETWORK BLOCKING ENGINE =====
    public class NetworkBlock
    {
        private static readonly string HostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        private static readonly string HostsBackupPath = HostsFilePath + ".backup";
        private static readonly string BlockMarker = "# CMS Domain Firewall";

        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isActive = false;

        public event Action<string, IPAddress, string>? DomainBlocked;
        public event Action<string>? BlockingError;

        public NetworkBlock()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartActiveBlockingAsync(NetworkMonitor monitor)
        {
            if (_isActive) return;

            _isActive = true;
            Console.WriteLine("[BLOCK] Starting active blocking engine...");

            try
            {
                await Task.Run(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            CheckAndBlockConnections(monitor);
                            await Task.Delay(5000, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BLOCK] Error in active blocking: {ex.Message}");
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[BLOCK] Active blocking stopped.");
            }
            finally
            {
                _isActive = false;
            }
        }

        public void StopActiveBlocking()
        {
            _cancellationTokenSource.Cancel();
            _isActive = false;
        }

        private void CheckAndBlockConnections(NetworkMonitor monitor)
        {
            try
            {
                var connections = monitor.GetActiveConnections();

                foreach (var conn in connections)
                {
                    var domain = monitor.GetDomainForIP(conn.RemoteAddress);
                    if (!string.IsNullOrEmpty(domain) && BlocklistManager.IsBlocked(domain))
                    {
                        Console.WriteLine($"[BLOCK] Blocking connection to {domain} ({conn.RemoteAddress}:{conn.RemotePort}) - PID: {conn.ProcessId}");

                        try
                        {
                            // Try multiple blocking methods
                            BlockDomainWithFirewall(domain, conn.RemoteAddress.ToString());

                            if (ShouldTerminateProcess(conn.ProcessId))
                            {
                                TerminateProcessConnection(conn.ProcessId, domain);
                            }

                            ResetTcpConnection(conn);

                            DomainBlocked?.Invoke(domain, conn.RemoteAddress, NetworkUtils.GetProcessName(conn.ProcessId));
                            Console.WriteLine($"[BLOCK] Successfully blocked {domain}");
                        }
                        catch (Exception blockEx)
                        {
                            var errorMsg = $"Failed to block {domain}: {blockEx.Message}";
                            Console.WriteLine($"[BLOCK] {errorMsg}");
                            BlockingError?.Invoke(errorMsg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BLOCK] Error checking connections: {ex.Message}");
            }
        }

        // ===== FIREWALL BLOCKING METHODS =====
        public void BlockDomainWithFirewall(string domain, string ipAddress)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"Block_{domain}_{DateTime.Now.Ticks}\" dir=out action=block remoteip={ipAddress}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                };

                using (var process = Process.Start(processInfo))
                {
                    process?.WaitForExit(5000);
                    if (process?.ExitCode == 0)
                    {
                        Console.WriteLine($"[FIREWALL] Added blocking rule for {domain} ({ipAddress})");
                    }
                    else
                    {
                        Console.WriteLine($"[FIREWALL] Failed to add rule for {domain}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREWALL] Error adding firewall rule: {ex.Message}");
            }
        }

        public void ClearFirewallRules()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall delete rule name=all dir=out",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                };

                using (var process = Process.Start(processInfo))
                {
                    process?.WaitForExit(5000);
                    Console.WriteLine("[FIREWALL] Cleared firewall rules");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREWALL] Error clearing rules: {ex.Message}");
            }
        }        // ===== HOSTS FILE BLOCKING METHODS =====
        public void BlockDomainWithHosts(string domain)
        {
            try
            {
                if (!File.Exists(HostsBackupPath))
                {
                    File.Copy(HostsFilePath, HostsBackupPath);
                    Console.WriteLine($"[HOSTS] Created backup of hosts file");
                }

                var lines = new List<string>();
                if (File.Exists(HostsFilePath))
                {
                    lines.AddRange(File.ReadAllLines(HostsFilePath));
                }

                bool alreadyBlocked = lines.Any(line =>
                    line.Contains(domain) && line.StartsWith("127.0.0.1"));

                if (!alreadyBlocked)
                {
                    lines.Add($"{BlockMarker} - {domain} and subdomains");

                    // Block main domain
                    lines.Add($"127.0.0.1 {domain}");
                    lines.Add($"::1 {domain}");

                    // Block www subdomain
                    lines.Add($"127.0.0.1 www.{domain}");
                    lines.Add($"::1 www.{domain}");

                    // Block common subdomains
                    var commonSubdomains = new[] { "mail", "ftp", "blog", "shop", "api", "cdn", "app", "mobile", "m", "admin" };
                    foreach (var subdomain in commonSubdomains)
                    {
                        lines.Add($"127.0.0.1 {subdomain}.{domain}");
                        lines.Add($"::1 {subdomain}.{domain}");
                    }

                    // Add wildcard entry (works for some applications)
                    lines.Add($"127.0.0.1 *.{domain}");
                    lines.Add($"::1 *.{domain}");

                    File.WriteAllLines(HostsFilePath, lines);
                    FlushDnsCache();

                    Console.WriteLine($"[HOSTS] Blocked {domain} and subdomains via hosts file");
                }
                else
                {
                    Console.WriteLine($"[HOSTS] {domain} already blocked in hosts file");
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[HOSTS] Access denied. Run as Administrator to modify hosts file");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HOSTS] Error blocking {domain}: {ex.Message}");
            }
        }

        public void UnblockDomainFromHosts(string domain)
        {
            try
            {
                if (!File.Exists(HostsFilePath)) return;

                var lines = File.ReadAllLines(HostsFilePath).ToList();
                var originalCount = lines.Count;

                lines.RemoveAll(line =>
                    line.Contains(domain) &&
                    (line.StartsWith("127.0.0.1") || line.StartsWith("::1") || line.Contains(BlockMarker)));

                if (lines.Count < originalCount)
                {
                    File.WriteAllLines(HostsFilePath, lines);
                    FlushDnsCache();
                    Console.WriteLine($"[HOSTS] Unblocked {domain} from hosts file");
                }
                else
                {
                    Console.WriteLine($"[HOSTS] {domain} was not found in hosts file");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HOSTS] Error unblocking {domain}: {ex.Message}");
            }
        }

        public void RestoreHostsFile()
        {
            try
            {
                if (File.Exists(HostsBackupPath))
                {
                    File.Copy(HostsBackupPath, HostsFilePath, true);
                    FlushDnsCache();
                    Console.WriteLine("[HOSTS] Restored hosts file from backup");
                }
                else
                {
                    if (File.Exists(HostsFilePath))
                    {
                        var lines = File.ReadAllLines(HostsFilePath).ToList();
                        var originalCount = lines.Count;

                        lines.RemoveAll(line => line.Contains(BlockMarker) ||
                            (line.StartsWith("127.0.0.1") && BlocklistManager.GetBlockedDomains().Any(domain => line.Contains(domain))) ||
                            (line.StartsWith("::1") && BlocklistManager.GetBlockedDomains().Any(domain => line.Contains(domain))));

                        if (lines.Count < originalCount)
                        {
                            File.WriteAllLines(HostsFilePath, lines);
                            FlushDnsCache();
                            Console.WriteLine("[HOSTS] Removed all blocking entries from hosts file");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HOSTS] Error restoring hosts file: {ex.Message}");
            }
        }

        public void ShowHostsFileStatus()
        {
            try
            {
                if (!File.Exists(HostsFilePath))
                {
                    Console.WriteLine("[HOSTS] Hosts file not found");
                    return;
                }

                var lines = File.ReadAllLines(HostsFilePath);
                var blockedEntries = lines.Where(line =>
                    line.Contains(BlockMarker) ||
                    (line.StartsWith("127.0.0.1") && !line.Contains("localhost"))).ToList();

                Console.WriteLine($"[HOSTS] Hosts file status:");
                Console.WriteLine($"  Total lines: {lines.Length}");
                Console.WriteLine($"  Blocked entries: {blockedEntries.Count}");
                Console.WriteLine($"  Backup exists: {File.Exists(HostsBackupPath)}");

                if (blockedEntries.Any())
                {
                    Console.WriteLine("  Current blocked domains:");
                    foreach (var entry in blockedEntries.Take(10))
                    {
                        Console.WriteLine($"    {entry}");
                    }
                    if (blockedEntries.Count > 10)
                    {
                        Console.WriteLine($"    ... and {blockedEntries.Count - 10} more");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HOSTS] Error reading hosts file: {ex.Message}");
            }
        }

        // ===== PROCESS TERMINATION METHODS =====
        private bool ShouldTerminateProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                var processName = process.ProcessName.ToLower();

                var terminableProcesses = new[] { "chrome", "firefox", "edge", "msedge", "opera", "brave", "iexplore" };
                return terminableProcesses.Contains(processName);
            }
            catch
            {
                return false;
            }
        }

        private void TerminateProcessConnection(int processId, string domain)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                Console.WriteLine($"[PROCESS] Terminating process {process.ProcessName} (PID: {processId}) accessing {domain}");

                process.CloseMainWindow();

                if (!process.WaitForExit(3000))
                {
                    process.Kill();
                    Console.WriteLine($"[PROCESS] Force killed process {processId}");
                }
                else
                {
                    Console.WriteLine($"[PROCESS] Gracefully closed process {processId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESS] Error terminating process {processId}: {ex.Message}");
            }
        }

        private void ResetTcpConnection(TcpConnection conn)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netstat -ano | findstr {conn.RemoteAddress}:{conn.RemotePort}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process?.WaitForExit(2000);
                    Console.WriteLine($"[CONNECTION] Reset connection to {conn.RemoteAddress}:{conn.RemotePort}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONNECTION] Error resetting connection: {ex.Message}");
            }
        }

        // ===== UTILITY METHODS =====
        private void FlushDnsCache()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process?.WaitForExit(5000);
                    Console.WriteLine("[DNS] DNS cache flushed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DNS] Error flushing DNS cache: {ex.Message}");
            }
        }

        public void BlockConnection(IPAddress remoteIP, int remotePort, string domain)
        {
            Console.WriteLine($"[BLOCK] Blocking {domain} ({remoteIP}:{remotePort})");
            BlockDomainWithFirewall(domain, remoteIP.ToString());
        }

        public void Dispose()
        {
            StopActiveBlocking();
            _cancellationTokenSource.Dispose();
        }
    }
}
