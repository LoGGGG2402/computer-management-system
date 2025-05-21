 // CMSAgent.Service/Models/HardwareInfo.cs
using System.Text.Json.Serialization;
// Không cần using System.Collections.Generic; nữa nếu không có List

namespace CMSAgent.Service.Models
{
    /// <summary>
    /// Model chứa thông tin phần cứng của client machine, được điều chỉnh để phù hợp với API.
    /// Được gửi đến Server qua API /api/agent/hardware-info.
    /// Tham khảo: CMSAgent_Doc.md mục 5.1 và agent_api.md.
    /// </summary>
    public class HardwareInfo
    {
        /// <summary>
        /// Thông tin hệ điều hành dưới dạng một chuỗi tóm tắt.
        /// Ví dụ: "Windows 10 Pro 64-bit, Version 22H2, Build 19045.2006"
        /// </summary>
        [JsonPropertyName("os_info")]
        public string? OsInfo { get; set; } // API spec nói là "string"

        /// <summary>
        /// Thông tin CPU dưới dạng một chuỗi tóm tắt.
        /// Ví dụ: "Intel(R) Core(TM) i7-8700 CPU @ 3.20GHz, 6 Cores, 12 Threads, 3192 MHz"
        /// </summary>
        [JsonPropertyName("cpu_info")]
        public string? CpuInfo { get; set; } // API spec nói là "string"

        /// <summary>
        /// Thông tin GPU dưới dạng một chuỗi tóm tắt.
        /// Ví dụ: "NVIDIA GeForce RTX 3070, VRAM: 8192 MB, Driver: 30.0.15.1234"
        /// Nếu có nhiều GPU, có thể nối chuỗi hoặc chỉ lấy thông tin GPU chính.
        /// </summary>
        [JsonPropertyName("gpu_info")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GpuInfo { get; set; } // API spec nói là "string"

        /// <summary>
        /// Tổng dung lượng RAM vật lý (MB).
        /// API spec: "total_ram": "integer"
        /// </summary>
        [JsonPropertyName("total_ram")] // Đổi tên JSON property
        public long TotalRamMb { get; set; }

        /// <summary>
        /// Tổng dung lượng ổ đĩa (thường là ổ C: hoặc ổ đĩa hệ thống chính) tính bằng MB.
        /// API spec: "total_disk_space": "integer (required)"
        /// </summary>
        [JsonPropertyName("total_disk_space")]
        public long TotalDiskSpaceMb { get; set; } // Đổi từ List<DiskDriveInfo> thành một giá trị long

        // Các lớp OsInfo, CpuInfo, GpuInfo, DiskDriveInfo đã bị loại bỏ
        // vì API chỉ yêu cầu các chuỗi tóm tắt hoặc giá trị đơn giản.
        // Logic để tạo các chuỗi tóm tắt này sẽ nằm trong HardwareCollector.cs.
    }
}
