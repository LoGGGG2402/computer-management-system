using System.Text.Json.Serialization;

namespace CMSAgent.Shared.Models
{
    /// <summary>
    /// Represents an error report sent from the Agent to the Server.
    /// </summary>
    public class AgentErrorReport
    {
        /// <summary>
        /// The type of error encountered (e.g., "DownloadFailed", "ChecksumMismatch", "GeneralException").
        /// Required. Length: 2-50 characters.
        /// </summary>
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        /// <summary>
        /// The error message description.
        /// Required. Length: 5-255 characters.
        /// </summary>
        [JsonPropertyName("message")]
        public required string Message { get; set; }

        /// <summary>
        /// Additional details about the error. Can be a JSON object or JSON string. Optional. Up to 2KB.
        /// </summary>
        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Details { get; set; }

        public AgentErrorReport() { }
    }
}
