 // CMSAgent.Service/Commands/Handlers/SystemActionCommandHandler.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Shared.Utils; // For ProcessUtils
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices; // For OSPlatform

namespace CMSAgent.Service.Commands.Handlers
{
    public class SystemActionCommandHandler : CommandHandlerBase
    {
        public SystemActionCommandHandler(ILogger<SystemActionCommandHandler> logger)
            : base(logger)
        {
        }

        protected override async Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            string action = commandRequest.Command?.Trim().ToLowerInvariant() ?? string.Empty;
            bool force = false;
            int delaySec = 0;

            if (commandRequest.Parameters != null)
            {
                if (commandRequest.Parameters.TryGetValue("force", out var forceObj) &&
                    forceObj is JsonElement forceJson &&
                    forceJson.TryGetBoolean(out bool forceFlag))
                {
                    force = forceFlag;
                }
                if (commandRequest.Parameters.TryGetValue("delay_sec", out var delayObj) &&
                    delayObj is JsonElement delayJson &&
                    delayJson.TryGetInt32(out int delayVal) &&
                    delayVal >= 0)
                {
                    delaySec = delayVal;
                }
            }

            if (delaySec > 0)
            {
                Logger.LogInformation("System action '{Action}' sẽ được thực thi sau {DelaySeconds} giây.", action, delaySec);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested(); // Kiểm tra trước khi thực hiện hành động

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new CommandOutputResult { ErrorMessage = "System actions (shutdown, restart) are only supported on Windows.", ExitCode = -1 };
            }

            string arguments;
            switch (action)
            {
                case "restart":
                    arguments = $"/r /t 0 {(force ? "/f" : "")}";
                    Logger.LogInformation("Thực hiện Restart. Force: {Force}", force);
                    Process.Start("shutdown.exe", arguments);
                    return new CommandOutputResult { Stdout = "Restart command issued.", ExitCode = 0 };

                case "shutdown":
                    arguments = $"/s /t 0 {(force ? "/f" : "")}";
                    Logger.LogInformation("Thực hiện Shutdown. Force: {Force}", force);
                    Process.Start("shutdown.exe", arguments);
                    return new CommandOutputResult { Stdout = "Shutdown command issued.", ExitCode = 0 };

                case "logoff":
                    // logoff không có cờ /f trực tiếp, nó sẽ force close apps không lưu
                    // Để logoff user hiện tại (nếu service chạy dưới quyền user), dùng "logoff"
                    // Để logoff một session cụ thể, cần session ID.
                    // Service chạy LocalSystem không có user session để logoff trực tiếp.
                    // Cần xem xét kỹ kịch bản này.
                    // Tạm thời:
                    // Process.Start("logoff.exe"); // Có thể cần chỉ định session ID
                    Logger.LogWarning("Logoff action is complex when run as LocalSystem and might not work as expected for specific users.");
                    return new CommandOutputResult { ErrorMessage = "Logoff action is not fully supported in this context.", ExitCode = -1 };

                default:
                    Logger.LogWarning("Hành động hệ thống không được hỗ trợ: {Action}", action);
                    return new CommandOutputResult { ErrorMessage = $"Unsupported system action: {action}", ExitCode = -1 };
            }
            // Các lệnh shutdown/restart sẽ không cho phép process này trả về kết quả nếu thành công
            // vì máy sẽ tắt hoặc khởi động lại.
        }
    }
}
