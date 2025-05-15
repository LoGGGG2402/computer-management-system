using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands.Handlers
{
    /// <summary>
    /// Handler để thực thi các lệnh hệ thống (khởi động lại, tắt máy, v.v).
    /// </summary>
    public class SystemActionCommandHandler(ILogger<SystemActionCommandHandler> logger) : ICommandHandler
    {
        private readonly ILogger<SystemActionCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Các hành động hệ thống được hỗ trợ
        private enum SystemAction
        {
            Restart,
            Shutdown,
            LogOff,
            Sleep,
            Hibernate
        }

        /// <summary>
        /// Thực thi một lệnh hệ thống.
        /// </summary>
        /// <param name="command">Thông tin lệnh cần thực thi.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Kết quả thực thi lệnh.</returns>
        public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            _logger.LogInformation("Bắt đầu thực thi lệnh hệ thống: {CommandId}", command.commandId);

            var result = new CommandResultPayload
            {
                commandId = command.commandId,
                type = command.commandType,
                success = false,
                result = new CommandResultData
                {
                    stdout = string.Empty,
                    stderr = string.Empty,
                    errorMessage = string.Empty,
                    errorCode = string.Empty
                }
            };

            try
            {
                // Xác định hành động hệ thống cần thực hiện
                if (string.IsNullOrEmpty(command.command))
                {
                    result.result.errorMessage = "Không có hành động hệ thống được chỉ định";
                    return result;
                }

                bool forceAction = false;
                int delaySeconds = 0;

                // Lấy tham số từ command
                if (command.parameters != null)
                {
                    if (command.parameters.TryGetValue("force", out var forceParam) && forceParam is bool forceValue)
                    {
                        forceAction = forceValue;
                    }
                    
                    if (command.parameters.TryGetValue("delay_sec", out var delayParam) && delayParam is int delayValue)
                    {
                        delaySeconds = Math.Max(0, Math.Min(delayValue, 3600)); // giới hạn từ 0s đến 1h
                    }
                }

                // Phân tích hành động cần thực hiện
                if (!Enum.TryParse<SystemAction>(command.command, true, out var action))
                {
                    result.result.errorMessage = $"Hành động hệ thống không hợp lệ: {command.command}";
                    return result;
                }

                // Có delay không?
                if (delaySeconds > 0)
                {
                    _logger.LogInformation("Sẽ thực hiện hành động {Action} sau {Delay} giây", action, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }

                // Thực hiện hành động hệ thống
                _logger.LogWarning("Thực hiện hành động hệ thống: {Action}, Force: {Force}", action, forceAction);
                
                bool actionResult = await ExecuteSystemActionAsync(action, forceAction);
                
                result.success = actionResult;
                if (!actionResult)
                {
                    result.result.errorMessage = $"Không thể thực hiện hành động {action}";
                }
                else
                {
                    result.result.stdout = $"Đã thực hiện hành động {action} thành công";
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Lệnh hệ thống {CommandId} đã bị hủy", command.commandId);
                result.result.errorMessage = "Lệnh đã bị hủy";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực thi lệnh hệ thống {CommandId}", command.commandId);
                result.result.errorMessage = $"Lỗi: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Thực hiện một hành động hệ thống cụ thể.
        /// </summary>
        /// <param name="action">Hành động cần thực hiện.</param>
        /// <param name="force">Buộc thực hiện không cần xác nhận.</param>
        /// <returns>True nếu thành công, False nếu thất bại.</returns>
        private async Task<bool> ExecuteSystemActionAsync(SystemAction action, bool force)
        {
            try
            {
                string arguments = string.Empty;
                
                switch (action)
                {
                    case SystemAction.Restart:
                        arguments = force ? "/r /f /t 0" : "/r /t 60";
                        break;
                    case SystemAction.Shutdown:
                        arguments = force ? "/s /f /t 0" : "/s /t 60";
                        break;
                    case SystemAction.LogOff:
                        arguments = force ? "/l /f" : "/l";
                        break;
                    case SystemAction.Sleep:
                        // Sử dụng powershell để sleep
                        return await RunPowerShellCommandAsync("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Application]::SetSuspendState('Suspend', $false, $false)");
                    case SystemAction.Hibernate:
                        // Sử dụng powershell để hibernate
                        return await RunPowerShellCommandAsync("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Application]::SetSuspendState('Hibernate', $false, $false)");
                    default:
                        _logger.LogError("Hành động hệ thống không được hỗ trợ: {Action}", action);
                        return false;
                }

                if (action != SystemAction.Sleep && action != SystemAction.Hibernate)
                {
                    using var process = new Process();
                    process.StartInfo.FileName = "shutdown.exe";
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    
                    _logger.LogDebug("Thực thi lệnh: shutdown.exe {Arguments}", arguments);
                    process.Start();
                    await process.WaitForExitAsync();
                    
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện hành động hệ thống: {Action}", action);
                return false;
            }
        }

        /// <summary>
        /// Chạy lệnh PowerShell.
        /// </summary>
        /// <param name="command">Lệnh PowerShell cần chạy.</param>
        /// <returns>True nếu thành công, False nếu thất bại.</returns>
        private async Task<bool> RunPowerShellCommandAsync(string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                
                _logger.LogDebug("Thực thi lệnh PowerShell: {Command}", command);
                process.Start();
                await process.WaitForExitAsync();
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực thi lệnh PowerShell: {Command}", command);
                return false;
            }
        }
    }
}
