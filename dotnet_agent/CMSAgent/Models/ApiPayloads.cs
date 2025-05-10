using Newtonsoft.Json; // Standardizing on Newtonsoft.Json for all payloads
using System.Collections.Generic; // For Dictionary in ReportErrorRequestPayload

namespace CMSAgent.Models
{
    /// <summary>
    /// Represents a common error response payload from the API.
    /// Conforms to agent_standard.md for error responses.
    /// </summary>
    public class ErrorResponsePayload
    {
        [JsonProperty("status")] // Added status as per standard examples
        public string? Status { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Payload for POST /identify request.
    /// </summary>
    public class IdentifyRequestPayload
    {
        [JsonProperty("unique_agent_id")]
        public string DeviceId { get; set; } = null!;

        [JsonProperty("positionInfo")]
        public RoomPositionPayload PositionInfo { get; set; } = null!;
    }

    /// <summary>
    /// Structure for positionInfo within IdentifyRequestPayload.
    /// </summary>
    public class RoomPositionPayload
    {
        [JsonProperty("roomName")]
        public string? RoomName { get; set; }

        [JsonProperty("posX")]
        public string? PosX { get; set; }

        [JsonProperty("posY")]
        public string? PosY { get; set; }
    }

    /// <summary>
    /// Response for POST /identify.
    /// </summary>
    public class IdentifyResponsePayload
    {
        [JsonProperty("status")]
        public string Status { get; set; } = null!; // "success", "mfa_required", "position_error", "error"

        [JsonProperty("agentId", NullValueHandling = NullValueHandling.Ignore)]
        public string? AgentId { get; set; } // Server-assigned/confirmed agent ID

        [JsonProperty("agentToken", NullValueHandling = NullValueHandling.Ignore)]
        public string? AgentToken { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string? Message { get; set; } // For errors or mfa_required message
    }

    /// <summary>
    /// Payload for POST /verify-mfa request.
    /// </summary>
    public class VerifyMfaRequestPayload
    {
        [JsonProperty("unique_agent_id")]
        public string DeviceId { get; set; } = null!;

        [JsonProperty("mfaCode")]
        public string MfaCode { get; set; } = null!;
    }

    /// <summary>
    /// Response for POST /verify-mfa.
    /// </summary>
    public class VerifyMfaResponsePayload
    {
        [JsonProperty("status")]
        public string Status { get; set; } = null!; // "success", "error" (or specific MFA error like "mfa_invalid_code")

        [JsonProperty("agentId", NullValueHandling = NullValueHandling.Ignore)]
        public string? AgentId { get; set; }

        [JsonProperty("agentToken", NullValueHandling = NullValueHandling.Ignore)]
        public string? AgentToken { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Payload for POST /hardware-info request.
    /// </summary>
    public class HardwareInfoPayload
    {
        [JsonProperty("os_info")]
        public string? OsInfo { get; set; }

        [JsonProperty("cpu_info")]
        public string? CpuInfo { get; set; }

        [JsonProperty("gpu_info")]
        public string? GpuInfo { get; set; }

        [JsonProperty("total_ram_bytes")]
        public long TotalRamBytes { get; set; }

        [JsonProperty("total_disk_space_bytes")]
        public long TotalDiskSpaceBytes { get; set; }

        [JsonProperty("ip_address")]
        public string? IpAddress { get; set; }
    }

    /// <summary>
    /// Response for POST /hardware-info.
    /// </summary>
    public class HardwareInfoResponsePayload
    {
        [JsonProperty("status")]
        public string Status { get; set; } = null!; // "success", "error"

        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Response for GET /check-update.
    /// </summary>
    public class CheckUpdateResponsePayload
    {
        [JsonProperty("version")]
        public string Version { get; set; } = null!;

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = null!;

        [JsonProperty("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = null!;
    }

    /// <summary>
    /// Payload for POST /report-error request.
    /// </summary>
    public class ReportErrorRequestPayload
    {
        [JsonProperty("error_type")]
        public string ErrorType { get; set; } = null!;

        [JsonProperty("error_message")]
        public string ErrorMessage { get; set; } = null!;

        [JsonProperty("error_details", NullValueHandling = NullValueHandling.Ignore)]
        public object? ErrorDetails { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = null!;
    }

    /// <summary>
    /// Response for POST /report-error.
    /// </summary>
    public class ReportErrorResponsePayload
    {
        [JsonProperty("status")]
        public string Status { get; set; } = null!; // "success", "error"

        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Payload for 'command:execute' event from server.
    /// </summary>
    public class ExecuteCommandEventPayload
    {
        [JsonProperty("commandId")]
        public string CommandId { get; set; } = null!;

        [JsonProperty("command")]
        public string Command { get; set; } = null!;

        [JsonProperty("commandType")]
        public string CommandType { get; set; } = null!;
    }

    /// <summary>
    /// Payload for 'agent:new_version_available' event from server.
    /// </summary>
    public class NewVersionAvailablePayload
    {
        [JsonProperty("version")]
        public string Version { get; set; } = null!;

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = null!;

        [JsonProperty("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = null!;
    }

    /// <summary>
    /// Payload for 'agent:command_result' event to server.
    /// </summary>
    public class AgentCommandResultPayload
    {
        [JsonProperty("agentId")]
        public string AgentId { get; set; } = null!;

        [JsonProperty("commandId")]
        public string CommandId { get; set; } = null!;

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("result")]
        public CommandResultDetailPayload Result { get; set; } = null!;
    }

    /// <summary>
    /// Detailed result part of AgentCommandResultPayload.
    /// </summary>
    public class CommandResultDetailPayload
    {
        [JsonProperty("stdout")]
        public string Stdout { get; set; } = string.Empty;

        [JsonProperty("stderr")]
        public string Stderr { get; set; } = string.Empty;

        [JsonProperty("exitCode")]
        public int ExitCode { get; set; }
    }

    /// <summary>
    /// Payload for 'agent:status_update' event to server.
    /// </summary>
    public class AgentStatusUpdatePayload
    {
        [JsonProperty("agentId")]
        public string AgentId { get; set; } = null!;

        [JsonProperty("cpuUsage")]
        public double CpuUsage { get; set; }

        [JsonProperty("ramUsage")]
        public double RamUsage { get; set; }

        [JsonProperty("diskUsage")]
        public double DiskUsage { get; set; }
    }
}
