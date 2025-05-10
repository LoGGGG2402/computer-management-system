using CMSAgent.Models; // For AgentConfig
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression; // For ZipFile
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Utilities; // For FileUtilities (SHA256)
using Microsoft.Extensions.Logging;

namespace CMSAgent.Core
{
    public class UpdateHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UpdateHandler> _logger;
        private readonly FileUtilities _fileUtilities; 
        private readonly string _updaterExecutableName = "CMSUpdater.exe"; // Standardized name
        private readonly string _agentRootDirectory;
        private readonly string _updaterDirectory;

        public UpdateHandler(HttpClient httpClient, AgentConfig agentConfig, ILogger<UpdateHandler> logger, FileUtilities fileUtilities)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileUtilities = fileUtilities ?? throw new ArgumentNullException(nameof(fileUtilities));
            
            _agentRootDirectory = AppContext.BaseDirectory;
            _updaterDirectory = Path.Combine(_agentRootDirectory, "CMSUpdater");
        }

        public async Task<bool> PerformUpdateAsync(string downloadUrl, string expectedSha256Checksum, string? releaseNotes, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting agent update process from URL: {UpdateUrl}", downloadUrl);
            if (!string.IsNullOrEmpty(releaseNotes))
            {
                _logger.LogInformation("Release Notes (if provided): {ReleaseNotes}", releaseNotes);
            }

            string tempDownloadDir = Path.Combine(_agentRootDirectory, "TempDownload");
            string downloadedUpdatePackagePath = Path.Combine(tempDownloadDir, "update_package.zip");

            try
            {
                if (Directory.Exists(tempDownloadDir))
                {
                    Directory.Delete(tempDownloadDir, true);
                    _logger.LogDebug("Cleaned up existing temporary download folder: {TempDownloadDir}", tempDownloadDir);
                }
                Directory.CreateDirectory(tempDownloadDir);
                _logger.LogDebug("Created temporary download folder: {TempDownloadDir}", tempDownloadDir);

                _logger.LogInformation("Downloading update package to {FilePath}...", downloadedUpdatePackagePath);
                await DownloadFileAsync(downloadUrl, downloadedUpdatePackagePath, cancellationToken);
                _logger.LogInformation("Update package downloaded successfully.");

                _logger.LogInformation("Verifying SHA256 checksum of the downloaded file...");
                string actualSha256 = await _fileUtilities.CalculateSha256Async(downloadedUpdatePackagePath);
                if (!string.Equals(actualSha256, expectedSha256Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("SHA256 checksum mismatch. Expected: {ExpectedSha256}, Actual: {ActualSha256}. Update aborted.", 
                                     expectedSha256Checksum, actualSha256);
                    return false;
                }
                _logger.LogInformation("SHA256 checksum verified successfully.");

                _logger.LogInformation("Extracting update package to agent root directory: {AgentRootDir}...", _agentRootDirectory);
                try
                {
                    ZipFile.ExtractToDirectory(downloadedUpdatePackagePath, _agentRootDirectory, true);
                    _logger.LogInformation("Update package extracted successfully to agent root.");
                }
                catch (IOException ioEx)
                {
                    _logger.LogError(ioEx, "IOException during extraction (files might be in use). Updater will need to handle this. Path: {Path}", _agentRootDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract update package to {AgentRootDir}. Update aborted.", _agentRootDirectory);
                    return false;
                }

                string updaterFullPath = Path.Combine(_updaterDirectory, _updaterExecutableName);
                _logger.LogInformation("Attempting to launch updater: {UpdaterPath}", updaterFullPath);
                if (!File.Exists(updaterFullPath))
                {
                    _logger.LogError("CMSUpdater.exe not found at {UpdaterPath}. Update cannot proceed.", updaterFullPath);
                    return false;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterFullPath,
                    Arguments = $"--agent-pid {Process.GetCurrentProcess().Id} --agent-path \"{_agentRootDirectory}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Starting CMSUpdater.exe with arguments: {Arguments}", startInfo.Arguments);
                Process? updaterProcess = Process.Start(startInfo);

                if (updaterProcess != null)
                {
                    _logger.LogInformation("Updater process started (PID: {UpdaterPid}). Agent will now allow itself to be terminated by the updater.", updaterProcess.Id);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to start updater process. Update cannot be finalized.");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Update process was canceled by agent shutdown or timeout.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the agent update process.");
                return false;
            }
            finally
            {
                if (Directory.Exists(tempDownloadDir))
                {
                    try
                    {
                        Directory.Delete(tempDownloadDir, true);
                        _logger.LogDebug("Cleaned up temporary download folder: {TempDownloadDir}", tempDownloadDir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up temporary download folder: {TempDownloadDir}", tempDownloadDir);
                    }
                }
            }
        }

        private async Task DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Attempting to download file from {Url} to {FilePath}", url, filePath);
            using HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            await contentStream.CopyToAsync(fileStream, cancellationToken);
            _logger.LogDebug("File downloaded successfully from {Url} to {FilePath}", url, filePath);
        }
    }
}
