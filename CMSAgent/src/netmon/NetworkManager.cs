using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace netmon
{
    // ===== MODELS =====
    public class TcpConnection
    {
        public IPAddress LocalAddress { get; set; } = IPAddress.Any;
        public ushort LocalPort { get; set; }
        public IPAddress RemoteAddress { get; set; } = IPAddress.Any;
        public ushort RemotePort { get; set; }
        public TcpState State { get; set; }
        public int ProcessId { get; set; }
    }    // ===== NETWORK MANAGEMENT =====
    public class NetworkManager
    {
        private readonly DnsTracker _dnsTracker;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;

        public event Action<TcpConnection, string>? ConnectionDetected;
        
        // Expose DNS events
        public event Action<string>? DnsQueryBlocked;
        public event Action<string>? DnsQueryAllowed;
        public event Action<string>? BlockedDomainDetected;

        public NetworkManager()
        {
            _dnsTracker = new DnsTracker();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Subscribe to DNS tracker events
            _dnsTracker.DnsQueryBlocked += OnDnsQueryBlocked;
            _dnsTracker.DnsQueryAllowed += OnDnsQueryAllowed;
            _dnsTracker.BlockedDomainDetected += OnBlockedDomainDetected;
        }

        private void OnDnsQueryBlocked(string domain) => DnsQueryBlocked?.Invoke(domain);
        private void OnDnsQueryAllowed(string domain) => DnsQueryAllowed?.Invoke(domain);
        private void OnBlockedDomainDetected(string domain) => BlockedDomainDetected?.Invoke(domain);

        public async Task StartMonitoringAsync()
        {
            if (_isRunning) return;

            _isRunning = true;
            Console.WriteLine("[MONITOR] Starting network monitoring...");

            try
            {
                // Start DNS tracking
                var dnsTask = _dnsTracker.StartCaptureAsync(_cancellationTokenSource.Token);

                // Start connection monitoring
                var connectionTask = StartConnectionMonitoringAsync(_cancellationTokenSource.Token);

                // Wait for both tasks
                await Task.WhenAll(dnsTask, connectionTask);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[MONITOR] Network monitoring stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MONITOR] Error in network monitoring: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void StopMonitoring()
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
                            if (NetworkUtils.IsLoopbackOrPrivate(conn.RemoteAddress)) continue;

                            string domain = _dnsTracker.GetMostRecentDomainForIP(conn.RemoteAddress) ?? "(unknown)";

                            // Notify subscribers about the connection
                            ConnectionDetected?.Invoke(conn, domain);
                        }

                        // Clean up old DNS queries periodically
                        _dnsTracker.CleanupOldQueries();

                        await Task.Delay(3000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MONITOR] Error in connection monitoring: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }, cancellationToken);
        }

        public List<TcpConnection> GetActiveConnections()
        {
            return NetworkUtils.GetTcpConnections()
                .Where(conn => conn.State == TcpState.Established &&
                              !NetworkUtils.IsLoopbackOrPrivate(conn.RemoteAddress))
                .ToList();
        }

        public string? GetDomainForIP(IPAddress ip)
        {
            return _dnsTracker.GetMostRecentDomainForIP(ip);
        }

        public int GetTrackedDomainCount()
        {
            return _dnsTracker.IpToDomain.Count;
        }

        public void AddManualDomainMapping(string domain, IPAddress ip)
        {
            _dnsTracker.AddMapping(domain, ip);
        }

        public async Task<IPAddress?> ResolveDomainAsync(string domain)
        {
            return await _dnsTracker.ResolveDomainAsync(domain);
        }

        public void Dispose()
        {
            StopMonitoring();
            _cancellationTokenSource.Dispose();
        }
    }

    // ===== NETWORK UTILITIES =====
    public static class NetworkUtils
    {
        // Process Helper Methods
        public static string GetProcessName(int pid)
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

        public static string GetProcessPath(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                return p.MainModule?.FileName ?? "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        // TCP Table Helper Methods
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

        public static List<TcpConnection> GetTcpConnections()
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

        public static bool IsLoopbackOrPrivate(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;

            byte[] b = ip.GetAddressBytes();
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (
                b[0] == 10 ||                                  // 10.0.0.0/8
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||   // 172.16.0.0/12
                (b[0] == 192 && b[1] == 168) ||                // 192.168.0.0/16
                (b[0] == 127)                                  // loopback
            );
        }
    }
}
