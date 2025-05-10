using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Utilities
{
    /// <summary>
    /// Provides utility methods for file operations such as JSON serialization/deserialization,
    /// checksum calculation, ZIP file extraction, and file/directory manipulation.
    /// </summary>
    public class FileUtilities
    {
        private readonly ILogger<FileUtilities> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileUtilities"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging messages.</param>
        public FileUtilities(ILogger<FileUtilities> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Asynchronously serializes the given data to a JSON string and saves it to the specified file path.
        /// </summary>
        /// <typeparam name="T">The type of the data to serialize.</typeparam>
        /// <param name="data">The data to serialize.</param>
        /// <param name="filePath">The path to the file where the JSON data will be saved.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="Exception">Thrown if any error occurs during file writing or serialization.</exception>
        public async Task SaveJsonToFileAsync<T>(T data, string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                _logger.LogDebug("Successfully saved JSON to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving JSON to file {FilePath}", filePath);
                throw; // Re-throw to allow caller to handle
            }
        }

        /// <summary>
        /// Asynchronously loads JSON data from the specified file path and deserializes it to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the JSON data to.</typeparam>
        /// <param name="filePath">The path to the file from which to load the JSON data.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the deserialized object,
        /// or default(T) if the file does not exist.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="JsonSerializationException">Thrown if an error occurs during JSON deserialization, indicating corrupted or malformed data.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during file reading.</exception>
        public async Task<T?> LoadJsonFromFileAsync<T>(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("JSON file not found at {FilePath}", filePath);
                    return default(T);
                }
                string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                T? result = JsonConvert.DeserializeObject<T>(json);
                _logger.LogDebug("Successfully loaded JSON from {FilePath}", filePath);
                return result;
            }
            catch (JsonSerializationException ex)
            {
                _logger.LogError(ex, "Error deserializing JSON from file {FilePath}. File might be corrupted or not in the expected format.", filePath);
                throw; // Re-throw as this indicates a data issue
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading JSON from file {FilePath}", filePath);
                throw; // Re-throw for other IO issues
            }
        }

        /// <summary>
        /// Asynchronously calculates the SHA256 hash of the file at the specified path.
        /// Note: This method seems to be duplicated by CalculateSHA256Async. Consider removing one.
        /// </summary>
        /// <param name="filePath">The path to the file for which to calculate the hash.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the SHA256 hash as a lowercase hexadecimal string.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file specified in <paramref name="filePath"/> does not exist.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during hash calculation.</exception>
        public async Task<string> CalculateSha256Async(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hash = await sha256.ComputeHashAsync(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SHA256 for file {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously calculates the SHA256 hash of the file at the specified path.
        /// </summary>
        /// <param name="filePath">The path to the file for which to calculate the hash.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the SHA256 hash as a lowercase hexadecimal string.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file specified in <paramref name="filePath"/> does not exist.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during hash calculation.</exception>
        public async Task<string> CalculateSHA256Async(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("Cannot calculate SHA256 for non-existent file: {FilePath}", filePath);
                throw new FileNotFoundException($"File not found for SHA256 calculation: {filePath}");
            }

            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = await sha256.ComputeHashAsync(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SHA256 for file {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Extracts the contents of a ZIP file to the specified extraction path.
        /// </summary>
        /// <param name="zipPath">The path to the ZIP file.</param>
        /// <param name="extractPath">The path where the contents will be extracted.</param>
        /// <param name="overwriteFiles">True to overwrite existing files; false otherwise. Defaults to true.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="zipPath"/> or <paramref name="extractPath"/> is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified in <paramref name="extractPath"/> does not exist and cannot be created.</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs during extraction.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during extraction.</exception>
        public void ExtractZipFile(string zipPath, string extractPath, bool overwriteFiles = true)
        {
            try
            {
                Directory.CreateDirectory(extractPath);
                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles);
                _logger.LogInformation("Successfully extracted {ZipPath} to {ExtractPath}", zipPath, extractPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting ZIP file {ZipPath} to {ExtractPath}", zipPath, extractPath);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously moves a file from the source path to the destination path, with retries on failure.
        /// </summary>
        /// <param name="sourcePath">The path of the file to move.</param>
        /// <param name="destPath">The destination path for the file.</param>
        /// <param name="retries">The number of times to retry the move operation. Defaults to 3.</param>
        /// <param name="delay">The time to wait between retries. Defaults to 1 second.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourcePath"/> or <paramref name="destPath"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the <paramref name="sourcePath"/> does not exist.</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs during the move operation after all retries.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during the move operation.</exception>
        public async Task MoveFileWithRetryAsync(string sourcePath, string destPath, int retries = 3, TimeSpan delay = default)
        {
            if (delay == default)
                delay = TimeSpan.FromSeconds(1);

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    File.Move(sourcePath, destPath, true); // true to overwrite if exists
                    _logger.LogInformation("Successfully moved file from {SourcePath} to {DestPath}", sourcePath, destPath);
                    return;
                }
                catch (IOException ex) when (i < retries - 1)
                {
                    _logger.LogWarning(ex, "Error moving file from {SourcePath} to {DestPath}. Attempt {AttemptNumber}/{TotalAttempts}. Retrying in {Delay}s...", 
                        sourcePath, destPath, i + 1, retries, delay.TotalSeconds);
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move file from {SourcePath} to {DestPath} after {Retries} retries.", sourcePath, destPath, retries);
                    throw;
                }
            }
        }

        /// <summary>
        /// Recursively deletes the specified directory and all its contents.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory does not exist (though this method logs a warning instead of throwing for this specific case if the directory is already gone).</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs during deletion.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during deletion.</exception>
        public void DeleteDirectoryRecursive(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    _logger.LogInformation("Successfully deleted directory {Path}", path);
                }
                else
                {
                    _logger.LogWarning("Attempted to delete directory {Path}, but it does not exist.", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting directory {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// Copies a directory from a source location to a destination location.
        /// </summary>
        /// <param name="sourceDir">The path of the source directory.</param>
        /// <param name="destinationDir">The path of the destination directory.</param>
        /// <param name="recursive">True to copy subdirectories recursively; false otherwise. Defaults to true.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceDir"/> or <paramref name="destinationDir"/> is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the <paramref name="sourceDir"/> does not exist.</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs during copying.</exception>
        /// <exception cref="Exception">Thrown if any other error occurs during copying.</exception>
        public void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
        {
            try
            {
                var dir = new DirectoryInfo(sourceDir);

                if (!dir.Exists)
                {
                    _logger.LogError("Source directory not found: {SourceDir}", sourceDir);
                    throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
                }

                DirectoryInfo[] dirs = dir.GetDirectories();

                Directory.CreateDirectory(destinationDir);

                foreach (FileInfo file in dir.GetFiles())
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath, true);
                    _logger.LogDebug("Copied: {SourceFile} -> {TargetFile}", file.FullName, targetFilePath);
                }

                if (recursive)
                {
                    foreach (DirectoryInfo subDir in dirs)
                    {
                        string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                        CopyDirectory(subDir.FullName, newDestinationDir, true);
                    }
                }
                
                _logger.LogInformation("Successfully copied directory {SourceDir} to {DestinationDir}", sourceDir, destinationDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying directory from {SourceDir} to {DestinationDir}", sourceDir, destinationDir);
                throw;
            }
        }
    }
}
