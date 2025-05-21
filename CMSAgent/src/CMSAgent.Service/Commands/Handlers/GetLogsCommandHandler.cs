 // CMSAgent.Service/Commands/Handlers/GetLogsCommandHandler.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Service.Communication.Http; // For IAgentApiClient (để upload log)
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Utils; // For FileUtils
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Service.Configuration.Manager; // For IRuntimeConfigManager
using CMSAgent.Service.Configuration.Models; // For AppSettings


namespace CMSAgent.Service.Commands.Handlers
{
    public class GetLogsCommandHandler : CommandHandlerBase
    {
        private readonly IAgentApiClient _apiClient;
        private readonly string _agentProgramDataPath;
        private readonly AppSettings _appSettings;


        public GetLogsCommandHandler(
            ILogger<GetLogsCommandHandler> logger,
            IAgentApiClient apiClient,
            IRuntimeConfigManager runtimeConfigManager,
            IOptions<AppSettings> appSettingsOptions) : base(logger)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _agentProgramDataPath = runtimeConfigManager.GetAgentProgramDataPath();
            _appSettings = appSettingsOptions.Value;
            if (string.IsNullOrWhiteSpace(_agentProgramDataPath))
            {
                throw new InvalidOperationException("AgentProgramDataPath không thể rỗng.");
            }
        }

        protected override async Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            if (commandRequest.Parameters == null)
                return new CommandOutputResult { ErrorMessage = "Missing parameters for GetLogs command.", ExitCode = -1 };

            commandRequest.Parameters.TryGetValue("log_type", out var logTypeObj);
            commandRequest.Parameters.TryGetValue("date_from", out var dateFromObj);
            commandRequest.Parameters.TryGetValue("date_to", out var dateToObj);
            commandRequest.Parameters.TryGetValue("file_path", out var filePathObj);

            string logType = (logTypeObj is JsonElement ltJson) ? ltJson.GetString()?.ToLowerInvariant() ?? "agent" : "agent";
            DateTime? dateFrom = (dateFromObj is JsonElement dfJson && DateTime.TryParse(dfJson.GetString(), out var parsedFrom)) ? parsedFrom : null;
            DateTime? dateTo = (dateToObj is JsonElement dtJson && DateTime.TryParse(dtJson.GetString(), out var parsedTo)) ? parsedTo.Date.AddDays(1).AddTicks(-1) : null; // Đến cuối ngày
            string? specificFilePath = (filePathObj is JsonElement fpJson) ? fpJson.GetString() : null;

            string logsDir = Path.Combine(_agentProgramDataPath, AgentConstants.LogsSubFolderName);
            if (!Directory.Exists(logsDir))
            {
                return new CommandOutputResult { ErrorMessage = $"Log directory not found: {logsDir}", ExitCode = -2 };
            }

            List<string> filesToCollect = new List<string>();

            switch (logType)
            {
                case "agent":
                case "updater":
                    string filePrefix = logType == "agent" ? AgentConstants.AgentLogFilePrefix : AgentConstants.UpdaterLogFilePrefix;
                    filesToCollect.AddRange(
                        Directory.EnumerateFiles(logsDir, $"{filePrefix}*.log")
                                 .Where(f => IsFileInDateRange(f, dateFrom, dateTo, filePrefix))
                    );
                    break;
                case "specific_file":
                    if (string.IsNullOrWhiteSpace(specificFilePath) || !File.Exists(specificFilePath))
                    {
                        return new CommandOutputResult { ErrorMessage = $"Specific file path is invalid or file not found: {specificFilePath}", ExitCode = -3 };
                    }
                    // Kiểm tra an toàn đường dẫn, không cho phép truy cập file ngoài thư mục log hoặc các thư mục được phép khác
                    if (!IsPathSafe(specificFilePath, logsDir)) {
                         return new CommandOutputResult { ErrorMessage = $"Access to file path is restricted: {specificFilePath}", ExitCode = -4 };
                    }
                    filesToCollect.Add(specificFilePath);
                    break;
                default:
                    return new CommandOutputResult { ErrorMessage = $"Unsupported log_type: {logType}", ExitCode = -5 };
            }

            if (!filesToCollect.Any())
            {
                return new CommandOutputResult { Stdout = "No log files found matching the criteria.", ExitCode = 0 };
            }

            // Nén các file log
            string tempZipDir = Path.Combine(Path.GetTempPath(), $"CMSAgent_Logs_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempZipDir);
            string zipFileName = $"CMSAgent_Logs_{logType}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            string zipFilePath = Path.Combine(Path.GetTempPath(), zipFileName); // Lưu zip vào thư mục tạm của hệ thống

            try
            {
                // Sao chép các file cần thu thập vào thư mục tạm để nén (giữ nguyên cấu trúc nếu cần)
                foreach (var filePath in filesToCollect)
                {
                    // Để đơn giản, chỉ copy file, không giữ cấu trúc thư mục phức tạp bên trong zip
                    File.Copy(filePath, Path.Combine(tempZipDir, Path.GetFileName(filePath)), true);
                }

                bool compressSuccess = await FileUtils.CompressDirectoryAsync(tempZipDir, zipFilePath, false); // Nén nội dung, không nén thư mục gốc tempZipDir
                if (!compressSuccess)
                {
                    return new CommandOutputResult { ErrorMessage = "Failed to compress log files.", ExitCode = -6 };
                }

                Logger.LogInformation("Log files compressed to: {ZipFilePath}", zipFilePath);

                // Tải file zip lên server
                // API spec không có endpoint /api/agent/upload-log.
                // CMSAgent_Doc.md mục 6.2.3 đề cập "(Or using report-error with error_type: "LOG_UPLOAD_REQUESTED")"
                // Ta sẽ thử dùng report-error với thông tin file đính kèm (nếu server hỗ trợ)
                // Hoặc, nếu có endpoint upload riêng, cần dùng nó.
                // Hiện tại, IAgentApiClient không có UploadLogFileAsync nữa.
                // => Ta sẽ trả về thông báo rằng file đã được nén, và cần cơ chế upload riêng.
                // Hoặc, nếu lệnh này chỉ nhằm mục đích thu thập và chuẩn bị, không tự upload.

                // Giả sử lệnh này chỉ thu thập và nén, không tự upload.
                // Kết quả sẽ là đường dẫn đến file zip (cục bộ trên agent).
                // Server có thể cần một lệnh khác để yêu cầu agent upload file từ đường dẫn đó.

                // Nếu muốn agent tự upload, cần khôi phục/thêm UploadLogFileAsync vào IAgentApiClient
                // và AgentApiClient, đồng thời server phải có endpoint tương ứng.

                // Hiện tại, trả về thông tin file đã nén.
                return new CommandOutputResult
                {
                    Stdout = $"Log files collected and compressed to: {zipFilePath}. Upload mechanism needs to be implemented separately or via another command.",
                    ExitCode = 0
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during log collection and compression.");
                return new CommandOutputResult { ErrorMessage = $"Error during log collection: {ex.Message}", Stderr=ex.ToString(), ExitCode = -99 };
            }
            finally
            {
                // Xóa thư mục tạm và file zip tạm nếu không cần nữa
                if (Directory.Exists(tempZipDir)) Directory.Delete(tempZipDir, true);
                // if (File.Exists(zipFilePath)) File.Delete(zipFilePath); // Giữ lại file zip để trả về đường dẫn
            }
        }

        private bool IsFileInDateRange(string filePath, DateTime? from, DateTime? to, string filePrefix)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!fileName.StartsWith(filePrefix)) return false;

                string datePart = fileName.Substring(filePrefix.Length);
                // Agent log: agent_YYYYMMDD.log -> YYYYMMDD
                // Updater log: updater_YYYYMMDD_HHMMSS.log -> YYYYMMDD_HHMMSS
                if (filePrefix == AgentConstants.UpdaterLogFilePrefix && datePart.Contains("_"))
                {
                    datePart = datePart.Substring(0, datePart.IndexOf('_')); // Lấy phần YYYYMMDD
                }

                if (DateTime.TryParseExact(datePart, AgentConstants.LogFileDateFormat, null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                {
                    if (from.HasValue && fileDate.Date < from.Value.Date) return false;
                    if (to.HasValue && fileDate.Date > to.Value.Date) return false; // to.Value đã là cuối ngày
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not parse date from log file name: {FilePath}", filePath);
            }
            return false; // Mặc định không khớp nếu không parse được
        }

        private bool IsPathSafe(string filePath, string allowedBaseDirectory)
        {
            var fullPath = Path.GetFullPath(filePath); // Chuẩn hóa đường dẫn
            var fullAllowedBase = Path.GetFullPath(allowedBaseDirectory);
            return fullPath.StartsWith(fullAllowedBase, StringComparison.OrdinalIgnoreCase);
        }
    }
}
