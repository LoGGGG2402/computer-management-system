using System.Net;
using System.Net.NetworkInformation;
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
        }        private static bool IsSubdomainMatch(string domain, string blockedDomain)
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

        public static List<string> GetBlockedDomains()
        {
            lock (_lockObject)
            {
                return new List<string>(_blocked);
            }
        }

        private class BlockConfig
        {
            public List<string> blockedDomains { get; set; } = new();
        }
    }    // ===== NETWORK BLOCKING ENGINE =====
    public class NetworkBlocker
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isActive = false;

        public event Action<string, IPAddress, string>? DomainBlocked;
        public event Action<string>? BlockingError;

        public NetworkBlocker()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }        public async Task StartActiveBlockingAsync(NetworkManager monitor)
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

        private void CheckAndBlockConnections(NetworkManager monitor)
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
            }        }

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
