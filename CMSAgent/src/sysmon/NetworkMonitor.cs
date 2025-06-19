using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using PacketDotNet;
using SharpPcap;

namespace sysmon
{
    // ===== UNIFIED NETWORK MONITOR CLASS =====
    public class NetworkMonitor : IDisposable
    {
        // Main components
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;

        // Configuration management
        private NetworkBlockingConfig _currentConfig = new();
        private readonly List<ConnectionInfo> _recentConnections = new();
        private readonly object _connectionLock = new();
        private DateTime _lastStatusUpdate = DateTime.Now;

        // DNS Tracking components (merged from DnsTracker)
        public ConcurrentDictionary<IPAddress, string> IpToDomain { get; } = new();
        private ConcurrentDictionary<string, DateTime> _recentQueries = new();
        private ICaptureDevice? _device;

        // Network Blocking components (merged from NetworkBlocker)
        private bool _isBlockingActive = false;

        // Blocklist management (merged from BlocklistManager)
        private static HashSet<string> _blocked = new();
        private static readonly object _lockObject = new();

        // Events
        public event Action<string, IPAddress, string>? DomainBlocked;
        public event Action<string>? DnsQueryBlocked;
        public event Action<string, IPAddress, string>? ConnectionDetected;
        public event Action<string>? DnsQueryAllowed;
        public event Action<string>? BlockedDomainDetected;
        public event Action<string>? BlockingError;

        public NetworkMonitor()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }        public async Task LoadConfigurationAsync(NetworkBlockingConfig config)
        {
            await Task.Run(() =>
            {
                LoadFromConfig(config);
                Console.WriteLine($"[NETWORK] Loaded {config.Domains.Count} blocked domains");
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isRunning) return;

            _isRunning = true;
            Console.WriteLine("[NETWORK] Starting network monitoring...");

            try
            {
                // Start DNS tracking
                var dnsTask = StartDnsCaptureAsync(cancellationToken);

                // Start connection monitoring
                var connectionTask = StartConnectionMonitoringAsync(cancellationToken);

                // Start active blocking
                var blockingTask = StartActiveBlockingAsync(cancellationToken);

                // Wait for all tasks
                await Task.WhenAll(dnsTask, connectionTask, blockingTask);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[NETWORK] Network monitoring stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Error in network monitoring: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _isRunning = false;
        }

        private async Task StartConnectionMonitoringAsync(CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var connections = NetworkUtils.GetTcpConnections();

                        foreach (var conn in connections)
                        {
                            if (conn.State != TcpState.Established) continue;
                            if (NetworkUtils.IsLoopbackOrPrivate(conn.RemoteAddress)) continue;                            string domain = GetMostRecentDomainForIP(conn.RemoteAddress) ?? "(unknown)";
                            string processName = GetProcessName(conn.ProcessId);

                            // Notify subscribers about the connection
                            ConnectionDetected?.Invoke(domain, conn.RemoteAddress, processName);
                        }

                        // Clean up old DNS queries periodically
                        CleanupOldQueries();

                        await Task.Delay(3000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NETWORK] Error in connection monitoring: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }, cancellationToken);
        }        // Event handlers
        private void OnDnsQueryBlocked(string domain) => DnsQueryBlocked?.Invoke(domain);
        private void OnDnsQueryAllowed(string domain) { /* Log if needed */ }
        private void OnBlockedDomainDetected(string domain) { /* Already handled by DnsQueryBlocked */ }
        private void OnDomainBlocked(string domain, IPAddress ip, string processName) => DomainBlocked?.Invoke(domain, ip, processName);
        private void OnBlockingError(string error) => Console.WriteLine($"[NETWORK-ERROR] {error}");

        // Public methods for SystemMonitor
        public List<TcpConnection> GetActiveConnections()
        {
            return GetTcpConnections()
                .Where(conn => conn.State == TcpState.Established &&
                              !IsLoopbackOrPrivate(conn.RemoteAddress))
                .ToList();
        }

        public string? GetDomainForIP(IPAddress ip)
        {
            return GetMostRecentDomainForIP(ip);
        }        public (int ActiveConnections, int TrackedDomains) GetStatistics()
        {
            var activeConnections = GetActiveConnections().Count;
            var trackedDomains = IpToDomain.Count;
            return (activeConnections, trackedDomains);
        }

        public (int BlockedDomainCount, string[] BlockedDomainsSample) GetConfigurationSummary()
        {
            var blockedDomains = GetBlockedDomains();
            var sample = blockedDomains.Take(5).ToArray();
            return (blockedDomains.Count, sample);
        }

        // ===== NEW CONFIGURATION AND JSON SUPPORT METHODS =====
        
        /// <summary>
        /// Updates the network monitoring configuration
        /// </summary>
        /// <param name="config">New network blocking configuration</param>
        /// <returns>JSON response indicating success/failure</returns>
        public string UpdateConfiguration(NetworkBlockingConfig config)
        {
            try
            {
                _currentConfig = config ?? throw new ArgumentNullException(nameof(config));
                  // Update the blocklist manager
                LoadFromConfig(config);
                
                var response = new NetworkConfigurationResponse
                {
                    Success = true,
                    Message = $"Configuration updated successfully. {config.Domains.Count} domains loaded.",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[NETWORK] Configuration updated - Mode: {config.Mode}, Domains: {config.Domains.Count}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new NetworkConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to update configuration: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[NETWORK-ERROR] Configuration update failed: {ex.Message}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Gets current network monitoring configuration as JSON
        /// </summary>
        /// <returns>JSON string of current configuration</returns>
        public string GetConfigurationAsJson()
        {
            var response = new NetworkConfigurationResponse
            {
                Success = true,
                Message = "Configuration retrieved successfully",
                CurrentConfig = _currentConfig,
                UpdatedAt = DateTime.Now
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Gets current network monitoring status as JSON
        /// </summary>
        /// <returns>JSON string of current status and statistics</returns>
        public string GetStatusAsJson()
        {
            try
            {
                lock (_connectionLock)
                {
                    var activeConnections = GetActiveConnections();
                    
                    // Update recent connections list (keep last 20)
                    var newConnections = activeConnections.Select(conn => new ConnectionInfo
                    {
                        LocalAddress = conn.LocalAddress.ToString(),
                        LocalPort = conn.LocalPort,
                        RemoteAddress = conn.RemoteAddress.ToString(),
                        RemotePort = conn.RemotePort,
                        State = conn.State.ToString(),
                        ProcessId = conn.ProcessId,
                        ProcessName = GetProcessName(conn.ProcessId),
                        Domain = GetMostRecentDomainForIP(conn.RemoteAddress) ?? "(unknown)",
                        DetectedAt = DateTime.Now
                    }).ToList();

                    // Keep only recent unique connections
                    _recentConnections.Clear();
                    _recentConnections.AddRange(newConnections.Take(20));

                    var status = new NetworkMonitorStatus
                    {
                        IsRunning = _isRunning,
                        ActiveConnections = activeConnections.Count,                        TrackedDomains = IpToDomain.Count,
                        BlockedDomains = GetBlockedDomains().Count,
                        LastUpdate = DateTime.Now,
                        RecentConnections = _recentConnections.ToList(),
                        BlockedDomainsSample = GetBlockedDomains().Take(10).ToList()
                    };

                    _lastStatusUpdate = DateTime.Now;
                    return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK-ERROR] Failed to get status: {ex.Message}");
                var errorStatus = new NetworkMonitorStatus
                {
                    IsRunning = false,
                    LastUpdate = DateTime.Now
                };
                return JsonSerializer.Serialize(errorStatus, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Get detailed status as JSON string
        /// </summary>
        public string GetDetailedStatusAsJson()
        {
            try
            {
                var status = new NetworkMonitorStatus
                {
                    IsRunning = _isRunning,
                    LastUpdate = DateTime.Now,
                    BlockedDomains = _currentConfig.Domains.Count,
                    BlockedDomainsSample = _currentConfig.Domains.Take(10).ToList()
                };

                lock (_connectionLock)
                {
                    status.RecentConnections = _recentConnections.Take(20).ToList();
                    status.ActiveConnections = _recentConnections.Count;
                    status.TrackedDomains = _recentConnections.Select(c => c.Domain).Distinct().Count();
                }

                return JsonSerializer.Serialize(status, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Error getting detailed status: {ex.Message}");
                return JsonSerializer.Serialize(new NetworkMonitorStatus 
                { 
                    IsRunning = false, 
                    LastUpdate = DateTime.Now 
                });
            }
        }        /// <summary>
        /// Get configuration summary for status display
        /// </summary>
        public string GetConfigurationSummaryJson()
        {
            try
            {
                var summary = new
                {
                    Enabled = _currentConfig.Enabled,
                    Mode = _currentConfig.Mode,
                    BlockedDomainsCount = _currentConfig.Domains.Count,
                    SampleDomains = _currentConfig.Domains.Take(5).ToArray()
                };

                return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Error getting configuration summary: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// Adds a domain to the blocklist at runtime        /// <summary>
        /// Adds a domain to the blocklist at runtime
        /// </summary>
        /// <param name="domain">Domain to block</param>
        /// <returns>JSON response</returns>
        public string AddBlockedDomain(string domain)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(domain))
                    throw new ArgumentException("Domain cannot be empty");

                if (!IsValidDomain(domain))
                    throw new ArgumentException($"Invalid domain format: {domain}");

                if (_currentConfig.Domains.Contains(domain))
                    throw new InvalidOperationException($"Domain '{domain}' is already in blocklist");                _currentConfig.Domains.Add(domain);
                BlocklistManager.LoadFromConfig(_currentConfig);

                var response = new NetworkConfigurationResponse
                {
                    Success = true,
                    Message = $"Domain '{domain}' added to blocklist",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[NETWORK] Added domain to blocklist: {domain}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new NetworkConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to add domain: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Removes a domain from the blocklist at runtime
        /// </summary>
        /// <param name="domain">Domain to unblock</param>
        /// <returns>JSON response</returns>
        public string RemoveBlockedDomain(string domain)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(domain))
                    throw new ArgumentException("Domain cannot be empty");                bool removed = _currentConfig.Domains.Remove(domain);
                if (!removed)
                    throw new InvalidOperationException($"Domain '{domain}' not found in blocklist");

                LoadFromConfig(_currentConfig);

                var response = new NetworkConfigurationResponse
                {
                    Success = true,
                    Message = $"Domain '{domain}' removed from blocklist",
                    CurrentConfig = _currentConfig,
                    UpdatedAt = DateTime.Now
                };

                Console.WriteLine($"[NETWORK] Removed domain from blocklist: {domain}");
                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var response = new NetworkConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to remove domain: {ex.Message}",
                    UpdatedAt = DateTime.Now
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
        }        /// <summary>
        /// Gets all blocked domains as a list
        /// </summary>
        /// <returns>List of blocked domains</returns>
        public List<string> GetBlockedDomainsList()
        {
            return _currentConfig.Domains.ToList();
        }

        /// <summary>
        /// Validates if a domain format is correct
        /// </summary>
        /// <param name="domain">Domain to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return false;
            
            // Basic domain validation
            return domain.Contains('.') && 
                   !domain.StartsWith('.') && 
                   !domain.EndsWith('.') &&
                   domain.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-');
        }

        // ===== DNS TRACKING METHODS (merged from DnsTracker) =====
        
        public async Task StartDnsCaptureAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Get the first available network device
                    var devices = CaptureDeviceList.Instance;
                    if (devices.Count == 0)
                    {
                        Console.WriteLine("[DNS] No network devices found for DNS capture");
                        return;
                    }

                    _device = devices[0];
                    _device.OnPacketArrival += OnPacketArrival;

                    // Set filter for DNS traffic (port 53)
                    _device.Open(DeviceModes.Promiscuous, 1000);
                    _device.Filter = "udp port 53";

                    Console.WriteLine($"[DNS] Starting DNS capture on device: {_device.Description}");
                    _device.StartCapture();

                    // Keep running until cancellation
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(100);
                    }

                    _device.StopCapture();
                    _device.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DNS] Error in DNS capture: {ex.Message}");
                }
            }, cancellationToken);
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
                var udpPacket = packet.Extract<UdpPacket>();
                if (udpPacket != null && (udpPacket.SourcePort == 53 || udpPacket.DestinationPort == 53))
                {
                    var ipPacket = packet.Extract<IPPacket>();
                    if (ipPacket != null)
                    {
                        var dnsData = udpPacket.PayloadData;

                        if (udpPacket.DestinationPort == 53)
                        {
                            // DNS Query - extract domain being queried
                            ParseDnsQuery(dnsData);
                        }
                        else if (udpPacket.SourcePort == 53)
                        {
                            // DNS Response - extract domain-IP mappings
                            ParseDnsResponse(dnsData);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore packet parsing errors
            }
        }

        private void ParseDnsQuery(byte[] dnsData)
        {
            try
            {
                if (dnsData.Length < 12) return;

                // Skip DNS header (12 bytes)
                int offset = 12;

                // Parse questions section
                int questionCount = (dnsData[4] << 8) | dnsData[5];
                for (int i = 0; i < questionCount && offset < dnsData.Length; i++)
                {
                    string domain = ReadDnsName(dnsData, offset, out int nameLength);
                    offset += nameLength;

                    if (offset + 4 > dnsData.Length) break;

                    int qtype = (dnsData[offset] << 8) | dnsData[offset + 1];
                    offset += 4; // Skip QTYPE and QCLASS

                    if (qtype == 1 && !string.IsNullOrEmpty(domain)) // A record query
                    {
                        _recentQueries[domain] = DateTime.Now;
                        
                        // Check if domain should be blocked
                        if (IsBlocked(domain))
                        {
                            Console.WriteLine($"[DNS-BLOCK] ❌ Blocking DNS query for: {domain}");
                            DnsQueryBlocked?.Invoke(domain);
                            BlockedDomainDetected?.Invoke(domain);
                        }
                        else
                        {
                            DnsQueryAllowed?.Invoke(domain);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DNS] Error parsing DNS query: {ex.Message}");
            }
        }

        private void ParseDnsResponse(byte[] dnsData)
        {
            try
            {
                if (dnsData.Length < 12) return;

                // Check if this is actually a response (QR bit = 1)
                if ((dnsData[2] & 0x80) == 0) return;

                // Skip DNS header (12 bytes)
                int offset = 12;

                // Skip questions section
                int questionCount = (dnsData[4] << 8) | dnsData[5];
                for (int i = 0; i < questionCount && offset < dnsData.Length; i++)
                {
                    ReadDnsName(dnsData, offset, out int nameLength);
                    offset += nameLength;
                    offset += 4; // Skip QTYPE and QCLASS
                }

                // Parse answers section
                int answerCount = (dnsData[6] << 8) | dnsData[7];
                for (int i = 0; i < answerCount && offset < dnsData.Length; i++)
                {
                    string domain = ReadDnsName(dnsData, offset, out int nameLength);
                    offset += nameLength;

                    if (offset + 10 > dnsData.Length) break;

                    int type = (dnsData[offset] << 8) | dnsData[offset + 1];
                    offset += 8; // Skip TYPE, CLASS, TTL

                    int dataLength = (dnsData[offset] << 8) | dnsData[offset + 1];
                    offset += 2;

                    if (offset + dataLength > dnsData.Length) break;

                    if (type == 1 && dataLength == 4) // A record
                    {
                        var ip = new IPAddress(new byte[] {
                            dnsData[offset], dnsData[offset + 1],
                            dnsData[offset + 2], dnsData[offset + 3]
                        });

                        if (!string.IsNullOrEmpty(domain))
                        {
                            domain = domain.TrimEnd('.').ToLowerInvariant();
                            IpToDomain[ip] = domain;
                        }
                    }

                    offset += dataLength;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DNS] Error parsing response: {ex.Message}");
            }
        }

        private string ReadDnsName(byte[] data, int offset, out int length)
        {
            var parts = new List<string>();
            int originalOffset = offset;
            bool jumped = false;
            length = 0;

            while (offset < data.Length)
            {
                byte len = data[offset];
                if (len == 0)
                {
                    offset++;
                    break;
                }

                if ((len & 0xC0) == 0xC0) // Compression pointer
                {
                    if (!jumped)
                    {
                        length = offset - originalOffset + 2;
                        jumped = true;
                    }

                    int pointer = ((len & 0x3F) << 8) | data[offset + 1];
                    offset = pointer;
                    continue;
                }

                offset++;
                if (offset + len > data.Length) break;

                parts.Add(System.Text.Encoding.ASCII.GetString(data, offset, len));
                offset += len;
            }

            if (!jumped)
            {
                length = offset - originalOffset;
            }

            return string.Join(".", parts);
        }

        public void CleanupOldQueries()
        {
            var cutoff = DateTime.Now.AddMinutes(-5);
            var keysToRemove = _recentQueries.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();

            foreach (var key in keysToRemove)
            {
                _recentQueries.TryRemove(key, out _);
            }
        }

        public string? GetMostRecentDomainForIP(IPAddress ip)
        {
            if (IpToDomain.TryGetValue(ip, out var domain))
            {
                return domain;
            }
            return null;
        }

        // ===== NETWORK BLOCKING METHODS (merged from NetworkBlocker) =====
        
        public async Task StartActiveBlockingAsync(CancellationToken cancellationToken)
        {
            if (_isBlockingActive) return;

            _isBlockingActive = true;
            Console.WriteLine("[BLOCK] Starting active blocking engine...");

            try
            {
                await Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            CheckAndBlockConnections();
                            await Task.Delay(5000, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BLOCK] Error in active blocking: {ex.Message}");
                            await Task.Delay(1000, cancellationToken);
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[BLOCK] Active blocking stopped.");
            }
            finally
            {
                _isBlockingActive = false;
            }
        }

        private void CheckAndBlockConnections()
        {
            try
            {
                var connections = GetActiveConnections();

                foreach (var conn in connections)
                {
                    var domain = GetDomainForIP(conn.RemoteAddress);
                    if (!string.IsNullOrEmpty(domain) && IsBlocked(domain))
                    {
                        string processName = GetProcessName(conn.ProcessId);
                        Console.WriteLine($"[BLOCK] Blocking connection to {domain} ({conn.RemoteAddress}:{conn.RemotePort}) - PID: {conn.ProcessId}");

                        try
                        {
                            BlockDomainWithFirewall(domain, conn.RemoteAddress.ToString());
                            DomainBlocked?.Invoke(domain, conn.RemoteAddress, processName);
                        }
                        catch (Exception blockEx)
                        {
                            var errorMsg = $"Failed to block {domain}: {blockEx.Message}";
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

        private void BlockDomainWithFirewall(string domain, string ipAddress)
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREWALL] Error adding firewall rule: {ex.Message}");
            }
        }

        // ===== BLOCKLIST MANAGEMENT METHODS (merged from BlocklistManager) =====
        
        public void LoadFromConfig(NetworkBlockingConfig config)
        {
            lock (_lockObject)
            {
                try
                {
                    _blocked = new HashSet<string>(config.Domains, StringComparer.OrdinalIgnoreCase);
                    Console.WriteLine($"[BLOCKLIST] Loaded {config.Domains.Count} blocked domains from unified config");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BLOCKLIST] Failed to load from config: {ex.Message}");
                    _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public void Load(string path)
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

        public bool IsBlocked(string domain)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(domain)) return false;

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

        private string CleanDomain(string domain)
        {
            if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                domain = domain.Substring(7);
            else if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                domain = domain.Substring(8);

            var pathIndex = domain.IndexOf('/');
            if (pathIndex > 0)
                domain = domain.Substring(0, pathIndex);

            var portIndex = domain.LastIndexOf(':');
            if (portIndex > 0 && portIndex < domain.Length - 1)
            {
                if (int.TryParse(domain.Substring(portIndex + 1), out _))
                    domain = domain.Substring(0, portIndex);
            }

            return domain.Trim().ToLowerInvariant();
        }

        private bool IsSubdomainMatch(string domain, string blockedDomain)
        {
            blockedDomain = blockedDomain.ToLowerInvariant();
            domain = domain.ToLowerInvariant();

            if (domain == blockedDomain) return true;
            if (domain.EndsWith("." + blockedDomain, StringComparison.OrdinalIgnoreCase)) return true;

            if (blockedDomain.StartsWith("*."))
            {
                var rootDomain = blockedDomain.Substring(2);
                if (domain == rootDomain || domain.EndsWith("." + rootDomain))
                    return true;
            }

            return false;
        }

        public List<string> GetBlockedDomains()
        {
            lock (_lockObject)
            {
                return new List<string>(_blocked);
            }
        }

        // ===== NETWORK UTILITIES METHODS (merged from NetworkUtils) =====
        
        public string GetProcessName(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                return p.ProcessName;
            }
            catch
            {
                return "N/A";
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable, ref int dwOutBufLen, bool sort,
            int ipVersion, TcpTableClass tblClass, uint reserved = 0);

        enum TcpTableClass
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public byte localPort1, localPort2, localPort3, localPort4;
            public uint remoteAddr;
            public byte remotePort1, remotePort2, remotePort3, remotePort4;
            public int pid;

            public ushort LocalPort => BitConverter.ToUInt16(new byte[] { localPort2, localPort1 }, 0);
            public ushort RemotePort => BitConverter.ToUInt16(new byte[] { remotePort2, remotePort1 }, 0);
        }

        public List<TcpConnection> GetTcpConnections()
        {
            var list = new List<TcpConnection>();
            int buffSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, 2, TcpTableClass.TCP_TABLE_OWNER_PID_ALL);
            IntPtr tablePtr = Marshal.AllocHGlobal(buffSize);

            try
            {
                uint result = GetExtendedTcpTable(tablePtr, ref buffSize, true, 2, TcpTableClass.TCP_TABLE_OWNER_PID_ALL);
                if (result != 0) return list;

                int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                int numEntries = Marshal.ReadInt32(tablePtr);
                IntPtr rowPtr = IntPtr.Add(tablePtr, 4);

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                    list.Add(new TcpConnection
                    {
                        LocalAddress = new IPAddress(row.localAddr),
                        RemoteAddress = new IPAddress(row.remoteAddr),
                        LocalPort = row.LocalPort,
                        RemotePort = row.RemotePort,
                        State = (TcpState)row.state,
                        ProcessId = row.pid
                    });
                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tablePtr);
            }

            return list;
        }

        public bool IsLoopbackOrPrivate(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;

            byte[] b = ip.GetAddressBytes();
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (
                b[0] == 10 ||
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                (b[0] == 192 && b[1] == 168) ||
                (b[0] == 127)
            );
        }

        // ===== HELPER CLASSES =====
        
        private class BlockConfig
        {
            public List<string> blockedDomains { get; set; } = new();
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
            _device?.Close();
        }
    }
}
