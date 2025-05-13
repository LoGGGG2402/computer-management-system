using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Commands.Handlers
{
    /// <summary>
    /// Handler để thu thập và gửi log của agent.
    /// </summary>
    public class GetLogsCommandHandler : ICommandHandler
    {
        private readonly ILogger<GetLogsCommandHandler> _logger;
        private readonly IConfigLoader _configLoader;
        private readonly IHttpClientWrapper _httpClient;
        private readonly CommandExecutorSettingsOptions _settings;

        /// <summary>
        /// Khởi tạo một instance mới của GetLogsCommandHandler.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="configLoader">ConfigLoader để tải cấu hình.</param>
        /// <param name="httpClient">HttpClient để gửi request HTTP.</param>
        /// <param name="options">Cấu hình thực thi lệnh.</param>
        public GetLogsCommandHandler(
            ILogger<GetLogsCommandHandler> logger,
            IConfigLoader configLoader,
            IHttpClientWrapper httpClient,
            IOptions<CommandExecutorSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Xử lý lệnh thu thập và gửi log.
        /// </summary>
        /// <param name="command">Thông tin lệnh cần thực thi.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Kết quả thực thi lệnh.</returns>
        public async Task<CommandResultPayload> ExecuteAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            _logger.LogInformation("Bắt đầu thu thập log: {CommandId}", command.commandId);

            var result = new CommandResultPayload
            {
                commandId = command.commandId,
                type = command.commandType,
                success = false,
                result = new CommandResultData()
            };

            try
            {
                // Phân tích tham số
                int maxLogAgeDays = 7;
                bool includeAllLogFiles = false;
                bool compressLogs = true;

                if (command.parameters != null)
                {
                    if (command.parameters.TryGetValue("max_age_days", out var ageParam) && ageParam is int ageValue)
                    {
                        maxLogAgeDays = Math.Max(1, Math.Min(ageValue, 30)); // giới hạn từ 1 đến 30 ngày
                    }
                    
                    if (command.parameters.TryGetValue("include_all_files", out var allFilesParam) && allFilesParam is bool allFilesValue)
                    {
                        includeAllLogFiles = allFilesValue;
                    }
                    
                    if (command.parameters.TryGetValue("compress_logs", out var compressParam) && compressParam is bool compressValue)
                    {
                        compressLogs = compressValue;
                    }
                }

                // Lấy thư mục log
                string logDirectory = GetLogDirectory();
                if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory))
                {
                    _logger.LogError("Không tìm thấy thư mục log: {LogDirectory}", logDirectory);
                    result.result.errorMessage = "Không tìm thấy thư mục log";
                    return result;
                }

                // Thu thập các file log
                var logFiles = CollectLogFiles(logDirectory, maxLogAgeDays, includeAllLogFiles);
                if (logFiles.Count == 0)
                {
                    _logger.LogWarning("Không tìm thấy file log nào trong thư mục: {LogDirectory}", logDirectory);
                    result.result.errorMessage = "Không tìm thấy file log nào";
                    return result;
                }

                _logger.LogInformation("Đã tìm thấy {Count} file log", logFiles.Count);

                // Tải lên từng file hoặc file nén
                bool uploadSuccess = false;
                
                if (compressLogs)
                {
                    // Tạo file ZIP tạm thời
                    string tempZipPath = Path.Combine(Path.GetTempPath(), $"cmsagent_logs_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip");
                    
                    try
                    {
                        using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
                        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                        {
                            foreach (var logFile in logFiles)
                            {
                                var fileInfo = new FileInfo(logFile);
                                var entry = archive.CreateEntry(fileInfo.Name, CompressionLevel.Optimal);
                                
                                using var entryStream = entry.Open();
                                using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                await fileStream.CopyToAsync(entryStream, cancellationToken);
                            }
                        }
                        
                        // Tải lên file ZIP
                        uploadSuccess = await UploadLogFileAsync(tempZipPath, cancellationToken);
                        result.success = uploadSuccess;
                        
                        if (uploadSuccess)
                        {
                            result.result.stdout = $"Đã tải lên {logFiles.Count} file log dưới dạng nén";
                        }
                        else
                        {
                            result.result.errorMessage = "Không thể tải lên file log nén";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi nén và tải lên file log");
                        result.result.errorMessage = $"Lỗi khi nén file log: {ex.Message}";
                    }
                    finally
                    {
                        // Xóa file ZIP tạm
                        if (File.Exists(tempZipPath))
                        {
                            try
                            {
                                File.Delete(tempZipPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Không thể xóa file ZIP tạm: {FilePath}", tempZipPath);
                            }
                        }
                    }
                }
                else
                {
                    // Tải lên từng file log riêng lẻ
                    int successCount = 0;
                    
                    foreach (var logFile in logFiles)
                    {
                        try
                        {
                            if (await UploadLogFileAsync(logFile, cancellationToken))
                            {
                                successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi tải lên file log: {FilePath}", logFile);
                        }
                    }
                    
                    uploadSuccess = successCount > 0;
                    result.success = uploadSuccess;
                    
                    if (uploadSuccess)
                    {
                        result.result.stdout = $"Đã tải lên {successCount}/{logFiles.Count} file log";
                    }
                    else
                    {
                        result.result.errorMessage = "Không thể tải lên file log nào";
                    }
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Lệnh thu thập log {CommandId} đã bị hủy", command.commandId);
                result.result.errorMessage = "Lệnh đã bị hủy";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập log {CommandId}", command.commandId);
                result.result.errorMessage = $"Lỗi: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Lấy thư mục chứa file log.
        /// </summary>
        /// <returns>Đường dẫn đến thư mục log.</returns>
        private string GetLogDirectory()
        {
            // Thường log nằm cùng thư mục với ứng dụng
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logDirectory = Path.Combine(baseDirectory, "logs");
            
            return Directory.Exists(logDirectory) ? logDirectory : baseDirectory;
        }

        /// <summary>
        /// Thu thập danh sách các file log cần gửi.
        /// </summary>
        /// <param name="logDirectory">Thư mục chứa log.</param>
        /// <param name="maxLogAgeDays">Số ngày tối đa của log.</param>
        /// <param name="includeAllLogFiles">Có bao gồm tất cả các file log không.</param>
        /// <returns>Danh sách đường dẫn đến các file log.</returns>
        private List<string> CollectLogFiles(string logDirectory, int maxLogAgeDays, bool includeAllLogFiles)
        {
            var logFiles = new List<string>();
            var cutoffDate = DateTime.UtcNow.AddDays(-maxLogAgeDays);
            
            try
            {
                var directory = new DirectoryInfo(logDirectory);
                var files = directory.GetFiles("*.log", SearchOption.TopDirectoryOnly)
                    .Where(f => f.LastWriteTimeUtc >= cutoffDate)
                    .OrderByDescending(f => f.LastWriteTimeUtc);
                
                // Nếu không bao gồm tất cả file, chỉ lấy những file gần đây nhất (tối đa 10 file)
                if (!includeAllLogFiles)
                {
                    files = files.Take(10);
                }
                
                logFiles.AddRange(files.Select(f => f.FullName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thu thập các file log từ {LogDirectory}", logDirectory);
            }
            
            return logFiles;
        }

        /// <summary>
        /// Tải lên một file log lên server.
        /// </summary>
        /// <param name="filePath">Đường dẫn đến file log.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>True nếu tải lên thành công, ngược lại là False.</returns>
        private async Task<bool> UploadLogFileAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File log không tồn tại: {FilePath}", filePath);
                    return false;
                }

                string agentId = _configLoader.GetAgentId();
                string encryptedToken = _configLoader.GetEncryptedAgentToken();
                
                if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(encryptedToken))
                {
                    _logger.LogError("Thiếu thông tin cần thiết để tải lên log");
                    return false;
                }

                // Đọc file và chuyển đổi thành base64
                var fileInfo = new FileInfo(filePath);
                byte[] fileBytes;
                
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileBytes = new byte[fileStream.Length];
                    await fileStream.ReadAsync(fileBytes, 0, fileBytes.Length, cancellationToken);
                }
                
                string base64Content = Convert.ToBase64String(fileBytes);
                
                // Tạo payload
                var logUploadPayload = new LogUploadPayload
                {
                    log_filename = fileInfo.Name,
                    log_content_base64 = base64Content
                };
                
                // Gửi lên server
                await _httpClient.PostAsync(
                    ApiRoutes.LogUpload, 
                    logUploadPayload, 
                    agentId, 
                    encryptedToken);
                
                _logger.LogInformation("Đã tải lên thành công file log: {FileName} ({Size:N0} bytes)", 
                    fileInfo.Name, fileInfo.Length);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải lên file log: {FilePath}", filePath);
                return false;
            }
        }
    }
}
