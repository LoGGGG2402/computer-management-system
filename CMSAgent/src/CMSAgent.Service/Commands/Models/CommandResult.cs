 // CMSAgent.Service/Commands/Models/CommandResult.cs
using System.Text.Json.Serialization;

namespace CMSAgent.Service.Commands.Models
{
    /// <summary>
    /// Model đại diện cho kết quả của một lệnh được Agent gửi về Server.
    /// Tham khảo: agent_api.md, sự kiện "agent:command_result" và CMSAgent_Doc.md mục 7.6.
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// ID của lệnh gốc mà kết quả này tương ứng.
        /// </summary>
        [JsonPropertyName("commandId")]
        public string CommandId { get; set; } = string.Empty;

        /// <summary>
        /// Loại lệnh đã được thực thi.
        /// </summary>
        [JsonPropertyName("commandType")]
        public string CommandType { get; set; } = string.Empty;

        /// <summary>
        /// Cho biết lệnh có thực thi thành công hay không.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Chứa chi tiết kết quả của lệnh.
        /// </summary>
        [JsonPropertyName("result")]
        public CommandOutputResult Result { get; set; } = new CommandOutputResult();
    }

    /// <summary>
    /// Chi tiết kết quả đầu ra của một lệnh.
    /// </summary>
    public class CommandOutputResult
    {
        /// <summary>
        /// Standard output của lệnh (nếu có).
        /// </summary>
        [JsonPropertyName("stdout")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Stdout { get; set; }

        /// <summary>
        /// Standard error của lệnh (nếu có).
        /// </summary>
        [JsonPropertyName("stderr")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Stderr { get; set; }

        /// <summary>
        /// Mã thoát (exit code) của lệnh (nếu có).
        /// 0 thường có nghĩa là thành công.
        /// </summary>
        [JsonPropertyName("exitCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] // Không gửi nếu là giá trị mặc định (0)
        public int ExitCode { get; set; }

        /// <summary>
        /// Thông điệp lỗi nếu success=false.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Mã lỗi nội bộ của Agent (nếu có).
        /// </summary>
        [JsonPropertyName("errorCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorCode { get; set; }
    }
}
