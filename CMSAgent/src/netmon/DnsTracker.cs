using System.Net;
using System.Collections.Concurrent;
using SharpPcap;
using PacketDotNet;
using System.Text;

namespace netmon
{
    public class DnsTracker
    {
        public ConcurrentDictionary<IPAddress, string> IpToDomain { get; } = new();
        private ConcurrentDictionary<string, DateTime> _recentQueries = new();
        private ICaptureDevice? _device;

        public async Task StartCaptureAsync(CancellationToken cancellationToken = default)
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

                    _device = devices[0]; // Use first device, could be made configurable
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
                            Console.WriteLine($"[DNS] Captured DNS query packet");
                            ParseDnsQuery(dnsData);
                        }
                        else if (udpPacket.SourcePort == 53)
                        {
                            // DNS Response - extract domain-IP mappings
                            Console.WriteLine($"[DNS] Captured DNS response packet");
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
                        Console.WriteLine($"[DNS] Query: {domain}");
                    }
                }
            }
            catch (Exception)
            {
                // Ignore parsing errors
            }
        }
        private void ParseDnsResponse(byte[] dnsData)
        {
            try
            {
                if (dnsData.Length < 12) return; // DNS header is at least 12 bytes

                // Check if this is actually a response (QR bit = 1)
                if ((dnsData[2] & 0x80) == 0) return; // Not a response

                // Skip DNS header (12 bytes)
                int offset = 12;

                // Skip questions section
                int questionCount = (dnsData[4] << 8) | dnsData[5];
                for (int i = 0; i < questionCount && offset < dnsData.Length; i++)
                {
                    string questionDomain = ReadDnsName(dnsData, offset, out int nameLength);
                    offset += nameLength;
                    offset += 4; // Skip QTYPE and QCLASS
                }

                // Parse answers section
                int answerCount = (dnsData[6] << 8) | dnsData[7];
                Console.WriteLine($"[DNS] Processing {answerCount} answers");
                for (int i = 0; i < answerCount && offset < dnsData.Length; i++)
                {
                    string domain = ReadDnsName(dnsData, offset, out int nameLength);
                    Console.WriteLine($"[DNS] Answer {i + 1}: domain='{domain}', nameLength={nameLength}");
                    offset += nameLength;

                    if (offset + 10 > dnsData.Length)
                    {
                        Console.WriteLine($"[DNS] Not enough data for answer {i + 1}");
                        break;
                    }

                    int type = (dnsData[offset] << 8) | dnsData[offset + 1];
                    Console.WriteLine($"[DNS] Answer {i + 1}: type={type}");
                    offset += 8; // Skip TYPE, CLASS, TTL

                    int dataLength = (dnsData[offset] << 8) | dnsData[offset + 1];
                    Console.WriteLine($"[DNS] Answer {i + 1}: dataLength={dataLength}");
                    offset += 2;

                    if (offset + dataLength > dnsData.Length)
                    {
                        Console.WriteLine($"[DNS] Data length exceeds packet for answer {i + 1}");
                        break;
                    }

                    if (type == 1 && dataLength == 4) // A record
                    {
                        var ip = new IPAddress(new byte[] {
                        dnsData[offset], dnsData[offset + 1],
                        dnsData[offset + 2], dnsData[offset + 3]
                    });

                        if (!string.IsNullOrEmpty(domain))
                        {
                            // Clean up domain name (remove trailing dots, convert to lowercase)
                            domain = domain.TrimEnd('.').ToLowerInvariant();

                            IpToDomain[ip] = domain;
                            Console.WriteLine($"[DNS] Response: {domain} -> {ip}");
                        }
                    }
                    else if (type == 5) // CNAME record
                    {
                        string cname = ReadDnsName(dnsData, offset, out int cnameLength);
                        if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(cname))
                        {
                            domain = domain.TrimEnd('.').ToLowerInvariant();
                            cname = cname.TrimEnd('.').ToLowerInvariant();
                            Console.WriteLine($"[DNS] CNAME: {domain} -> {cname}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DNS] Skipping answer {i + 1}: type={type}, dataLength={dataLength}");
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
            length = 0; // Initialize length

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

                    // Follow the pointer
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

        private int SkipDnsName(byte[] data, int offset)
        {
            while (offset < data.Length)
            {
                byte len = data[offset];
                if (len == 0)
                {
                    return offset + 1;
                }

                if ((len & 0xC0) == 0xC0) // Compression pointer
                {
                    return offset + 2;
                }

                offset += len + 1;
            }
            return offset;
        }

        // Method to manually add domain-IP mappings (for testing)
        public void AddMapping(string domain, IPAddress ip)
        {
            IpToDomain[ip] = domain;
            Console.WriteLine($"[DNS] Manual mapping: {domain} => {ip}");
        }

        // Method to resolve domain for testing
        public async Task<IPAddress?> ResolveDomainAsync(string domain)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(domain);
                if (addresses.Length > 0)
                {
                    var ip = addresses[0];
                    IpToDomain[ip] = domain;
                    Console.WriteLine($"[DNS] Resolved: {domain} => {ip}");
                    return ip;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DNS] Failed to resolve {domain}: {ex.Message}");
            }
            return null;
        }

        // Method to clean up old queries (older than 5 minutes)
        public void CleanupOldQueries()
        {
            var cutoff = DateTime.Now.AddMinutes(-5);
            var keysToRemove = _recentQueries.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();

            foreach (var key in keysToRemove)
            {
                _recentQueries.TryRemove(key, out _);
            }
        }        // Get the most recently queried domain for an IP
        public string? GetMostRecentDomainForIP(IPAddress ip)
        {
            if (IpToDomain.TryGetValue(ip, out var domain))
            {
                return domain;
            }

            // If we don't have direct mapping, check if any recent queries might match
            // This is a fallback mechanism
            return null;
        }
    }
}
