using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands.Handlers
{
    /// <summary>
    /// Handler để thực thi các lệnh console (CMD hoặc PowerShell).
    /// </summary>
    public class ConsoleCommandHandler : ICommandHandler
    {
        private readonly ILogger<ConsoleCommandHandler> _logger;
        private readonly CommandExecutorSettingsOptions _settings;

        /// <summary>
        /// Khởi tạo một instance mới của ConsoleCommandHandler.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="options">Cấu hình thực thi lệnh.</param>
        public ConsoleCommandHandler(
            ILogger<ConsoleCommandHandler> logger,
            IOptions<CommandExecutorSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Thực thi một lệnh console.
        /// </summary>
        /// <param name="command">Thông tin lệnh cần thực thi.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Kết quả thực thi lệnh.</returns>
        public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            _logger.LogInformation("Bắt đầu thực thi lệnh console: {CommandId}", command.commandId);

            // Mặc định sử dụng CMD, nhưng có thể thay đổi dựa trên tham số
            bool usePowerShell = false;
            int timeoutSeconds = _settings.DefaultTimeoutSec;

            // Xác định loại shell và timeout từ tham số
            if (command.parameters != null)
            {
                if (command.parameters.TryGetValue("use_powershell", out var pshellParam) && pshellParam is bool usePsParam)
                {
                    usePowerShell = usePsParam;
                }
                
                if (command.parameters.TryGetValue("timeout_sec", out var timeoutParam) && timeoutParam is int timeoutValue)
                {
                    timeoutSeconds = Math.Max(30, Math.Min(timeoutValue, 3600)); // giới hạn từ 30s đến 1h
                }
            }

            var result = new CommandResultPayload
            {
                commandId = command.commandId,
                type = command.commandType,
                success = false,
                result = new CommandResultData()
            };

            try
            {
                using var process = new Process();
                
                if (usePowerShell)
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.command}\"";
                }
                else
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c {command.command}";
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                
                // Thiết lập encoding cho đầu ra
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(_settings.ConsoleEncoding);
                process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding(_settings.ConsoleEncoding);

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                _logger.LogDebug("Khởi động process {FileName} với tham số: {Arguments}", 
                    process.StartInfo.FileName, process.StartInfo.Arguments);
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Chờ process hoàn thành với timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
                var processExitTask = process.WaitForExitAsync(cancellationToken);
                
                // Task nào hoàn thành trước?
                if (await Task.WhenAny(processExitTask, timeoutTask) == timeoutTask)
                {
                    // Timeout xảy ra, kill process
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning("Lệnh đã vượt quá thời gian chờ {Timeout} giây, đang hủy", timeoutSeconds);
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi hủy process");
                    }

                    result.success = false;
                    result.result.exitCode = -1;
                    result.result.errorMessage = $"Lệnh đã vượt quá thời gian {timeoutSeconds} giây và bị hủy";
                    result.result.stdout = outputBuilder.ToString();
                    result.result.stderr = errorBuilder.ToString();
                    return result;
                }

                // Process đã thoát bình thường
                result.success = process.ExitCode == 0;
                result.result.exitCode = process.ExitCode;
                result.result.stdout = outputBuilder.ToString();
                result.result.stderr = errorBuilder.ToString();

                _logger.LogInformation("Lệnh console {CommandId} đã hoàn thành với mã thoát {ExitCode}", 
                    command.commandId, process.ExitCode);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Lệnh console {CommandId} đã bị hủy", command.commandId);
                result.success = false;
                result.result.errorMessage = "Lệnh đã bị hủy";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực thi lệnh console {CommandId}", command.commandId);
                result.success = false;
                result.result.errorMessage = $"Lỗi: {ex.Message}";
                return result;
            }
        }
    }
}
