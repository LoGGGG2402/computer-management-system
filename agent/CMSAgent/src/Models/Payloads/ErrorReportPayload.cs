using System;
using System.Text.Json.Serialization;

namespace CMSAgent.Models.Payloads
{
    public class ErrorDetailsPayload
    {
        [JsonPropertyName("stack_trace")]
        public string? StackTrace { get; set; }

        [JsonPropertyName("agent_version")]
        public string AgentVersion { get; set; } = string.Empty;

        [JsonPropertyName("context_info")]
        public string? ContextInfo { get; set; }
    }

    public class ErrorReportPayload
    {
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty;

        [JsonPropertyName("error_type")]
        public string ErrorType { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        public ErrorDetailsPayload Details { get; set; } = new ErrorDetailsPayload();

        [JsonPropertyName("agent_version")]
        public string AgentVersion { get; set; } = string.Empty;

        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}