 // CMSAgent.Service/Commands/Models/CommandRequest.cs
using System.Text.Json.Serialization;
using System.Collections.Generic; // For Dictionary

namespace CMSAgent.Service.Commands.Models
{
    /// <summary>
    /// Model đại diện cho một yêu cầu lệnh từ Server gửi đến Agent.
    /// Tham khảo: agent_api.md, sự kiện "command:execute" và CMSAgent_Doc.md mục 7.1.
    /// </summary>
    public class CommandRequest
    {
        /// <summary>
        /// ID duy nhất của lệnh (UUID string).
        /// </summary>
        [JsonPropertyName("commandId")]
        public string CommandId { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung chính của lệnh.
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Loại lệnh (ví dụ: "CONSOLE", "SYSTEM_ACTION", "SOFTWARE_INSTALL", "SOFTWARE_UNINSTALL", "GET_LOGS").
        /// </summary>
        [JsonPropertyName("commandType")]
        public string CommandType { get; set; } = string.Empty; // Nên có giá trị mặc định hoặc enum

        /// <summary>
        /// Các tham số bổ sung cho lệnh, dưới dạng một đối tượng JSON (dictionary).
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; } // Sử dụng object để linh hoạt, sẽ parse sau
                                                                    // Hoặc có thể dùng JsonElement
    }
}
