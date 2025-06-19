using System.Net;
using System.Net.NetworkInformation;

namespace sysmon
{
    // ===== CONFIGURATION MODELS =====
    public class SystemConfig
    {
        public NetworkBlockingConfig NetworkBlocking { get; set; } = new();
        public ProcessBlockingConfig ProcessBlocking { get; set; } = new();
        public GeneralConfig General { get; set; } = new();
    }

    public class NetworkBlockingConfig
    {
        public bool Enabled { get; set; } = true;
        public string Mode { get; set; } = "blacklist";
        public List<string> Domains { get; set; } = new();
    }

    public class ProcessBlockingConfig
    {
        public bool Enabled { get; set; } = true;
        public string Mode { get; set; } = "blacklist";
        public List<string> Blacklist { get; set; } = new();
        public List<string> Whitelist { get; set; } = new();
    }

    public class GeneralConfig
    {
        public bool EnableFirewallIntegration { get; set; } = true;
        public bool EnableRealTimeMonitoring { get; set; } = true;
        public int ScanIntervalSeconds { get; set; } = 3;
        public int CleanupIntervalMinutes { get; set; } = 5;
        public string LogLevel { get; set; } = "info";
    }

    public class ProcessListConfig
    {
        public string Mode { get; set; } = "blacklist";
        public List<string> Blacklist { get; set; } = new();
        public List<string> Whitelist { get; set; } = new();
    }

    // ===== PROCESS MODELS =====
    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool IsBlocked { get; set; }
        public string Status => IsBlocked ? "BLOCKED" : "RUNNING";
    }

    // ===== NETWORK MODELS =====
    public class TcpConnection
    {
        public IPAddress LocalAddress { get; set; } = IPAddress.Any;
        public ushort LocalPort { get; set; }
        public IPAddress RemoteAddress { get; set; } = IPAddress.Any;
        public ushort RemotePort { get; set; }
        public TcpState State { get; set; }
        public int ProcessId { get; set; }
    }

    public class ConnectionInfo
    {
        public string LocalAddress { get; set; } = string.Empty;
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string State { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }

    // ===== EVENT MODELS =====
    public class DomainBlockedEventArgs
    {
        public string Domain { get; set; } = string.Empty;
        public IPAddress IpAddress { get; set; } = IPAddress.None;
        public string ProcessName { get; set; } = string.Empty;
    }

    public class ProcessBlockedEventArgs
    {
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
    }

    public class ConnectionDetectedEventArgs
    {
        public string Domain { get; set; } = string.Empty;
        public IPAddress IpAddress { get; set; } = IPAddress.None;
        public string ProcessName { get; set; } = string.Empty;
    }

    // ===== STATISTICS MODELS =====
    public class SystemStatistics
    {
        public TimeSpan Uptime { get; set; }
        public int TotalProcessesBlocked { get; set; }
        public int TotalConnectionsBlocked { get; set; }
        public NetworkStats NetworkStats { get; set; } = new();
        public ProcessStats ProcessStats { get; set; } = new();
    }

    public class NetworkStats
    {
        public int ActiveConnections { get; set; }
        public int TrackedDomains { get; set; }
    }

    public class ProcessStats
    {
        public int Running { get; set; }
        public int Blocked { get; set; }
        public int Total { get; set; }
    }

    // ===== STATUS MODELS =====
    public class SystemMonitorStatus
    {
        public bool IsRunning { get; set; }
        public TimeSpan Uptime { get; set; }
        public int TotalProcessesBlocked { get; set; }
        public int TotalConnectionsBlocked { get; set; }
        public NetworkStats NetworkStats { get; set; } = new();
        public ProcessStats ProcessStats { get; set; } = new();
        public ProcessConfigInfo ProcessConfiguration { get; set; } = new();
        public NetworkConfigInfo NetworkConfiguration { get; set; } = new();
        public DateTime LastUpdate { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ProcessMonitorStatus
    {
        public bool IsRunning { get; set; }
        public string Mode { get; set; } = string.Empty;
        public int TotalProcesses { get; set; }
        public int BlockedProcesses { get; set; }
        public int RunningProcesses { get; set; }
        public DateTime LastUpdate { get; set; }
        public List<ProcessInfo> RecentProcesses { get; set; } = new();
        public List<string> BlacklistSample { get; set; } = new();
        public List<string> WhitelistSample { get; set; } = new();
    }

    public class NetworkMonitorStatus
    {
        public bool IsRunning { get; set; }
        public int ActiveConnections { get; set; }
        public int TrackedDomains { get; set; }
        public int BlockedDomains { get; set; }
        public DateTime LastUpdate { get; set; }
        public List<ConnectionInfo> RecentConnections { get; set; } = new();
        public List<string> BlockedDomainsSample { get; set; } = new();
    }

    public class ProcessConfigInfo
    {
        public string Mode { get; set; } = string.Empty;
        public int BlacklistCount { get; set; }
        public int WhitelistCount { get; set; }
        public string[] BlacklistSample { get; set; } = Array.Empty<string>();
        public string[] WhitelistSample { get; set; } = Array.Empty<string>();
    }

    public class NetworkConfigInfo
    {
        public int BlockedDomainCount { get; set; }
        public string[] BlockedDomainsSample { get; set; } = Array.Empty<string>();
    }

    // ===== RESPONSE MODELS =====
    public class ProcessConfigurationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ProcessBlockingConfig? CurrentConfig { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class NetworkConfigurationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public NetworkBlockingConfig? CurrentConfig { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
