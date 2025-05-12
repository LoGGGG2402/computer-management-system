using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CMSAgent.Models.Payloads
{
    /// <summary>
    /// Agent status payload sent to the server via WebSocket
    /// </summary>
    public class AgentStatusPayload // Sent via WebSocket agent:status_update
    {
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty; // device_id

        [JsonPropertyName("cpuUsage")]
        public double CpuUsage { get; set; } // Percentage

        [JsonPropertyName("ramUsage")]
        public double RamUsage { get; set; } // Percentage or MB, ensure consistency with server

        [JsonPropertyName("diskUsage")]
        public List<DiskUsageInfo> DiskUsage { get; set; } = new List<DiskUsageInfo>();

        // Optional fields based on Standard.md VI.C
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("networkInfo")]
        public NetworkInfoPayload? NetworkInfo { get; set; }

        [JsonPropertyName("activeProcesses")]
        public List<ProcessInfoPayload>? ActiveProcesses { get; set; }
    }

    public class DiskUsageInfo
    {
        [JsonPropertyName("driveLetter")]
        public string DriveLetter { get; set; } = string.Empty;

        [JsonPropertyName("usedSpaceGB")]
        public double UsedSpaceGB { get; set; }

        [JsonPropertyName("totalSpaceGB")]
        public double TotalSpaceGB { get; set; }

        [JsonPropertyName("freeSpacePercentage")]
        public double FreeSpacePercentage { get; set; }
    }

    public class NetworkInfoPayload
    {
        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }
        [JsonPropertyName("macAddress")]
        public string? MacAddress { get; set; }
    }

    public class ProcessInfoPayload
    {
        [JsonPropertyName("processName")]
        public string? ProcessName { get; set; }
        [JsonPropertyName("cpuUsage")]
        public double? CpuUsage { get; set; }
        [JsonPropertyName("memoryUsageMB")]
        public double? MemoryUsageMB { get; set; }
    }
}