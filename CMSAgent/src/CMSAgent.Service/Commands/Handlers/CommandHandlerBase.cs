 // CMSAgent.Service/Commands/Handlers/CommandHandlerBase.cs
using CMSAgent.Service.Commands.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Commands.Handlers
{
    /// <summary>
    /// Lớp cơ sở trừu tượng cho các command handler, cung cấp các chức năng chung.
    /// </summary>
    public abstract class CommandHandlerBase : ICommandHandler
    {
        protected readonly ILogger Logger;

        protected CommandHandlerBase(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Phương thức trừu tượng mà các lớp con phải triển khai để xử lý logic cụ thể của lệnh.
        /// </summary>
        protected abstract Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Thực thi lệnh và đóng gói kết quả.
        /// </summary>
        public async Task<CommandResult> ExecuteAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Bắt đầu thực thi lệnh ID: {CommandId}, Type: {CommandType}, Command: {CommandText}",
                commandRequest.CommandId, commandRequest.CommandType, commandRequest.Command);

            var commandResult = new CommandResult
            {
                CommandId = commandRequest.CommandId,
                CommandType = commandRequest.CommandType,
                Success = false // Mặc định là thất bại
            };

            try
            {
                // Áp dụng timeout nếu có
                int defaultTimeoutSeconds = GetDefaultCommandTimeoutSeconds(commandRequest); // Lấy từ AppSettings hoặc command params
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(defaultTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                commandResult.Result = await ExecuteInternalAsync(commandRequest, linkedCts.Token);
                commandResult.Success = string.IsNullOrEmpty(commandResult.Result.ErrorMessage) && (commandResult.Result.ExitCode == 0 || IsExitCodeSuccessful(commandRequest, commandResult.Result.ExitCode));

                if (commandResult.Success)
                {
                    Logger.LogInformation("Thực thi lệnh ID: {CommandId} thành công. ExitCode: {ExitCode}",
                        commandRequest.CommandId, commandResult.Result.ExitCode);
                }
                else
                {
                    Logger.LogWarning("Thực thi lệnh ID: {CommandId} thất bại. ExitCode: {ExitCode}, Error: {ErrorMessage}",
                        commandRequest.CommandId, commandResult.Result.ExitCode, commandResult.Result.ErrorMessage);
                }
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex, "Lệnh ID: {CommandId} bị timeout sau {TimeoutSeconds} giây.", commandRequest.CommandId, defaultTimeoutSeconds);
                commandResult.Result = new CommandOutputResult
                {
                    ErrorMessage = $"Command timed out after {defaultTimeoutSeconds} seconds.",
                    ExitCode = -1 // Mã lỗi cho timeout
                };
                commandResult.Success = false;
            }
            catch (OperationCanceledException ex) // Bị hủy bởi token bên ngoài
            {
                Logger.LogWarning(ex, "Lệnh ID: {CommandId} bị hủy bỏ.", commandRequest.CommandId);
                commandResult.Result = new CommandOutputResult
                {
                    ErrorMessage = "Command execution was canceled.",
                    ExitCode = -2 // Mã lỗi cho cancellation
                };
                commandResult.Success = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Lỗi không mong muốn khi thực thi lệnh ID: {CommandId}.", commandRequest.CommandId);
                commandResult.Result = new CommandOutputResult
                {
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    Stderr = ex.ToString(), // Ghi stack trace vào stderr để debug
                    ExitCode = -99 // Mã lỗi chung
                };
                commandResult.Success = false;
            }

            return commandResult;
        }

        /// <summary>
        /// Lấy giá trị timeout mặc định cho lệnh.
        /// Có thể được override bởi các lớp con nếu cần logic phức tạp hơn.
        /// </summary>
        protected virtual int GetDefaultCommandTimeoutSeconds(CommandRequest commandRequest)
        {
            // Mặc định lấy từ commandRequest.Parameters nếu có, nếu không thì từ AppSettings
            if (commandRequest.Parameters != null &&
                commandRequest.Parameters.TryGetValue("timeout_sec", out var timeoutObj) &&
                timeoutObj is JsonElement timeoutJson &&
                timeoutJson.TryGetInt32(out int timeoutSec) &&
                timeoutSec > 0)
            {
                return timeoutSec;
            }
            // Giả sử có AppSettings được inject vào lớp con và có DefaultCommandTimeoutSeconds
            // Đây là ví dụ, cần triển khai thực tế
            return 60; // Giá trị mặc định cứng nếu không lấy được từ đâu
        }

        /// <summary>
        /// Kiểm tra xem exit code có được coi là thành công hay không, dựa trên 'expected_exit_codes' trong parameters.
        /// </summary>
        protected virtual bool IsExitCodeSuccessful(CommandRequest commandRequest, int exitCode)
        {
            if (commandRequest.Parameters != null &&
                commandRequest.Parameters.TryGetValue("expected_exit_codes", out var expectedCodesObj) &&
                expectedCodesObj is JsonElement expectedCodesJson &&
                expectedCodesJson.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    var expectedCodes = expectedCodesJson.Deserialize<List<int>>();
                    return expectedCodes?.Contains(exitCode) ?? (exitCode == 0);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, "Không thể parse 'expected_exit_codes' cho lệnh ID: {CommandId}", commandRequest.CommandId);
                    return exitCode == 0; // Mặc định về kiểm tra exit code 0 nếu parse lỗi
                }
            }
            return exitCode == 0; // Mặc định là thành công nếu exit code là 0
        }
    }
}
