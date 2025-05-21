 // CMSAgent.Service/Commands/Handlers/SoftwareInstallCommandHandler.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Service.Communication.Http; // For IAgentApiClient (để tải file)
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Utils; // For FileUtils, ProcessUtils
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Service.Configuration.Models; // For AppSettings
using CMSAgent.Service.Configuration.Manager; // For IRuntimeConfigManager


namespace CMSAgent.Service.Commands.Handlers
{
    public class SoftwareInstallCommandHandler : CommandHandlerBase
    {
        private readonly IAgentApiClient _apiClient;
        private readonly AppSettings _appSettings;
        private readonly string _tempDownloadDir;

        public SoftwareInstallCommandHandler(
            ILogger<SoftwareInstallCommandHandler> logger,
            IAgentApiClient apiClient,
            IOptions<AppSettings> appSettingsOptions,
            IRuntimeConfigManager runtimeConfigManager) : base(logger)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            // Tạo đường dẫn thư mục tạm để tải file cài đặt
            var agentProgramDataPath = runtimeConfigManager.GetAgentProgramDataPath();
            _tempDownloadDir = Path.Combine(agentProgramDataPath, AgentConstants.UpdatesSubFolderName, "temp_install_downloads");
            Directory.CreateDirectory(_tempDownloadDir); // Đảm bảo thư mục tồn tại
        }

        protected override async Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            if (commandRequest.Parameters == null)
                return new CommandOutputResult { ErrorMessage = "Missing required parameters for software installation.", ExitCode = -1 };

            commandRequest.Parameters.TryGetValue("package_url", out var packageUrlObj);
            commandRequest.Parameters.TryGetValue("checksum_sha256", out var checksumObj);
            commandRequest.Parameters.TryGetValue("install_arguments", out var installArgsObj);
            // expected_exit_codes được xử lý ở base class

            string? packageUrl = (packageUrlObj is JsonElement pkgUrlJson) ? pkgUrlJson.GetString() : null;
            string? expectedChecksum = (checksumObj is JsonElement chksumJson) ? chksumJson.GetString() : null;
            string? installArguments = (installArgsObj is JsonElement argsJson) ? argsJson.GetString() : string.Empty; // Mặc định là chuỗi rỗng

            if (string.IsNullOrWhiteSpace(packageUrl) || string.IsNullOrWhiteSpace(expectedChecksum))
            {
                return new CommandOutputResult { ErrorMessage = "package_url and checksum_sha256 are required.", ExitCode = -1 };
            }

            string fileName = Path.GetFileName(new Uri(packageUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                 fileName = $"install_package_{Guid.NewGuid()}.tmp"; // Tên file tạm nếu không lấy được từ URL
            }
            string downloadedFilePath = Path.Combine(_tempDownloadDir, fileName);

            try
            {
                // 1. Tải file cài đặt
                Logger.LogInformation("Đang tải gói cài đặt từ: {PackageUrl} về {FilePath}", packageUrl, downloadedFilePath);
                // Giả sử DownloadAgentPackageAsync có thể tải từ URL bất kỳ nếu được điều chỉnh,
                // hoặc cần một phương thức tải file chung. Hiện tại, DownloadAgentPackageAsync dùng cho agent update.
                // Ta sẽ tạo một phương thức download tạm ở đây hoặc điều chỉnh IAgentApiClient.
                // Vì IAgentApiClient.DownloadAgentPackageAsync nhận filename từ /api/agent/agent-packages/:filename
                // nên nó không phù hợp trực tiếp. Ta cần một HTTP client đơn giản để tải file.
                // Tạm thời dùng HttpClient trực tiếp (không lý tưởng bằng việc có 1 service chuyên dụng).

                using var httpClient = new HttpClient(); // Cân nhắc dùng IHttpClientFactory nếu có nhiều lệnh cần tải file
                bool downloadSuccess = await DownloadFileAsync(httpClient, packageUrl, downloadedFilePath, cancellationToken);

                if (!downloadSuccess)
                {
                    return new CommandOutputResult { ErrorMessage = $"Failed to download package from {packageUrl}.", ExitCode = -2 };
                }
                Logger.LogInformation("Tải file cài đặt thành công: {FilePath}", downloadedFilePath);

                // 2. Xác minh checksum
                Logger.LogInformation("Đang xác minh checksum cho: {FilePath}", downloadedFilePath);
                string? calculatedChecksum = await FileUtils.CalculateSha256ChecksumAsync(downloadedFilePath);
                if (string.IsNullOrWhiteSpace(calculatedChecksum) || !calculatedChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    FileUtils.TryDeleteFile(downloadedFilePath, Logger);
                    return new CommandOutputResult { ErrorMessage = $"Checksum mismatch. Expected: {expectedChecksum}, Calculated: {calculatedChecksum}", ExitCode = -3 };
                }
                Logger.LogInformation("Xác minh checksum thành công.");

                // 3. Thực thi cài đặt
                Logger.LogInformation("Đang thực thi cài đặt: {FilePath} với tham số: {InstallArguments}", downloadedFilePath, installArguments);
                // Timeout cho việc cài đặt có thể rất khác nhau, nên lấy từ tham số hoặc cấu hình riêng
                int installTimeoutSeconds = GetInstallTimeoutSeconds(commandRequest);

                var (stdout, stderr, exitCode) = await ProcessUtils.ExecuteCommandAsync(
                    downloadedFilePath,
                    installArguments ?? string.Empty,
                    workingDirectory: Path.GetDirectoryName(downloadedFilePath), // Chạy từ thư mục chứa file
                    timeoutMilliseconds: installTimeoutSeconds * 1000,
                    cancellationToken: cancellationToken
                );

                // IsExitCodeSuccessful sẽ được gọi bởi base class
                return new CommandOutputResult
                {
                    Stdout = stdout,
                    Stderr = stderr,
                    ExitCode = exitCode,
                    ErrorMessage = string.IsNullOrEmpty(stderr) ? null : stderr
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                 FileUtils.TryDeleteFile(downloadedFilePath, Logger);
                 throw; // Ném lại để base class xử lý
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Lỗi trong quá trình cài đặt phần mềm từ {PackageUrl}", packageUrl);
                return new CommandOutputResult { ErrorMessage = $"Error during software installation: {ex.Message}", Stderr = ex.ToString(), ExitCode = -99 };
            }
            finally
            {
                // Xóa file đã tải về sau khi cài đặt (thành công hoặc thất bại)
                FileUtils.TryDeleteFile(downloadedFilePath, Logger);
            }
        }

        private async Task<bool> DownloadFileAsync(HttpClient client, string url, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await contentStream.CopyToAsync(fileStream, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Lỗi khi tải file từ {Url} về {DestinationPath}", url, destinationPath);
                return false;
            }
        }
         protected override int GetDefaultCommandTimeoutSeconds(CommandRequest commandRequest)
        {
            // Timeout cho việc cài đặt có thể rất dài
            if (commandRequest.Parameters != null &&
                commandRequest.Parameters.TryGetValue("timeout_sec", out var timeoutObj) &&
                timeoutObj is JsonElement timeoutJson &&
                timeoutJson.TryGetInt32(out int timeoutSec) &&
                timeoutSec > 0)
            {
                return timeoutSec;
            }
            // Giá trị timeout lớn hơn cho cài đặt
            return _appSettings.CommandExecution.DefaultCommandTimeoutSeconds * 10; // Ví dụ: gấp 10 lần timeout mặc định
        }
         private int GetInstallTimeoutSeconds(CommandRequest commandRequest) => GetDefaultCommandTimeoutSeconds(commandRequest);

    }
}
