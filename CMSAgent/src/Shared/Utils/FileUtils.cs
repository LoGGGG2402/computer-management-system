using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Serilog;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides utility functions related to file and directory handling.
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Calculate SHA256 checksum for a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>SHA256 checksum string (hex string lowercase) or null if error.</returns>
        public static async Task<string?> CalculateSha256ChecksumAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Log.Error("CalculateSha256ChecksumAsync: File path is invalid or file does not exist: {FilePath}", filePath);
                return null;
            }

            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hashBytes = await sha256.ComputeHashAsync(stream);
                var sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CalculateSha256ChecksumAsync: Error calculating SHA256 for file {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Compress a directory into a ZIP file.
        /// </summary>
        /// <param name="sourceDirectoryPath">Path to the directory to compress.</param>
        /// <param name="zipFilePath">Path to the ZIP file to be created.</param>
        /// <param name="includeBaseDirectory">True to include the base directory in the ZIP, False to only compress contents.</param>
        /// <returns>True if compression is successful, False if failed.</returns>
        public static async Task<bool> CompressDirectoryAsync(string sourceDirectoryPath, string zipFilePath, bool includeBaseDirectory = false)
        {
            if (string.IsNullOrEmpty(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
            {
                Log.Error("CompressDirectoryAsync: Source directory path is invalid or does not exist: {SourceDirectoryPath}", sourceDirectoryPath);
                return false;
            }
            if (string.IsNullOrEmpty(zipFilePath))
            {
                Log.Error("CompressDirectoryAsync: ZIP file path is invalid.");
                return false;
            }

            try
            {
                string? zipDirectory = Path.GetDirectoryName(zipFilePath);
                if (!string.IsNullOrEmpty(zipDirectory) && !Directory.Exists(zipDirectory))
                {
                    Directory.CreateDirectory(zipDirectory);
                }

                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
                
                await Task.Run(() => ZipFile.CreateFromDirectory(sourceDirectoryPath, zipFilePath, CompressionLevel.Optimal, includeBaseDirectory));
                Log.Information("CompressDirectoryAsync: Successfully compressed directory {SourceDirectoryPath} to {ZipFilePath}", sourceDirectoryPath, zipFilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CompressDirectoryAsync: Error compressing directory {SourceDirectoryPath} to {ZipFilePath}", sourceDirectoryPath, zipFilePath);
                return false;
            }
        }

        /// <summary>
        /// Decompress a ZIP file into a directory.
        /// </summary>
        /// <param name="zipFilePath">Path to the ZIP file.</param>
        /// <param name="extractDirectoryPath">Path to the directory to contain the extracted contents.</param>
        /// <param name="overwriteFiles">True to overwrite files if they exist.</param>
        /// <returns>True if decompression is successful, False if failed.</returns>
        public static async Task<bool> DecompressZipFileAsync(string zipFilePath, string extractDirectoryPath, bool overwriteFiles = true)
        {
            if (string.IsNullOrEmpty(zipFilePath) || !File.Exists(zipFilePath))
            {
                Log.Error("DecompressZipFileAsync: ZIP file path is invalid or does not exist: {ZipFilePath}", zipFilePath);
                return false;
            }
            if (string.IsNullOrEmpty(extractDirectoryPath))
            {
                Log.Error("DecompressZipFileAsync: Extract directory path is invalid.");
                return false;
            }

            try
            {
                if (!Directory.Exists(extractDirectoryPath))
                {
                    Directory.CreateDirectory(extractDirectoryPath);
                }

                await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, extractDirectoryPath, overwriteFiles));
                Log.Information("DecompressZipFileAsync: Successfully decompressed {ZipFilePath} to {ExtractDirectoryPath}", zipFilePath, extractDirectoryPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DecompressZipFileAsync: Error decompressing {ZipFilePath} to {ExtractDirectoryPath}", zipFilePath, extractDirectoryPath);
                return false;
            }
        }

        /// <summary>
        /// Safely move a directory, including deleting the destination directory if it exists.
        /// </summary>
        /// <param name="sourceDirName">Source directory path.</param>
        /// <param name="destDirName">Destination directory path.</param>
        /// <param name="overwrite">If true, delete the destination directory if it exists.</param>
        public static void DirectoryMove(string sourceDirName, string destDirName, bool overwrite = true)
        {
            if (!Directory.Exists(sourceDirName))
            {
                Log.Error("DirectoryMove: Source directory not found: {SourceDir}", sourceDirName);
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirName}");
            }

            if (Directory.Exists(destDirName))
            {
                if (overwrite)
                {
                    Log.Warning("DirectoryMove: Destination directory {DestDir} exists and will be overwritten.", destDirName);
                    Directory.Delete(destDirName, true);
                }
                else
                {
                    Log.Error("DirectoryMove: Destination directory {DestDir} already exists and overwrite is false.", destDirName);
                    throw new IOException($"Destination directory already exists: {destDirName}");
                }
            }
            var parentDir = Path.GetDirectoryName(destDirName);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            Directory.Move(sourceDirName, destDirName);
            Log.Information("DirectoryMove: Moved directory from {SourceDir} to {DestDir}", sourceDirName, destDirName);
        }

        /// <summary>
        /// Safely move a directory asynchronously, including deleting the destination directory if it exists.
        /// </summary>
        /// <param name="sourceDirName">Source directory path.</param>
        /// <param name="destDirName">Destination directory path.</param>
        /// <param name="overwrite">If true, delete the destination directory if it exists.</param>
        public static async Task DirectoryMoveAsync(string sourceDirName, string destDirName, bool overwrite = true)
        {
            if (!Directory.Exists(sourceDirName))
            {
                Log.Error("DirectoryMoveAsync: Source directory not found: {SourceDir}", sourceDirName);
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirName}");
            }

            if (Directory.Exists(destDirName))
            {
                if (overwrite)
                {
                    Log.Warning("DirectoryMoveAsync: Destination directory {DestDir} exists and will be overwritten.", destDirName);
                    await Task.Run(() => Directory.Delete(destDirName, true));
                }
                else
                {
                    Log.Error("DirectoryMoveAsync: Destination directory {DestDir} already exists and overwrite is false.", destDirName);
                    throw new IOException($"Destination directory already exists: {destDirName}");
                }
            }

            var parentDir = Path.GetDirectoryName(destDirName);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            await Task.Run(() => Directory.Move(sourceDirName, destDirName));
            Log.Information("DirectoryMoveAsync: Moved directory from {SourceDir} to {DestDir}", sourceDirName, destDirName);
        }

        /// <summary>
        /// Recursively copy a directory.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destinationDir">Destination directory path.</param>
        /// <param name="overwrite">True to overwrite files if they exist.</param>
        public static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = true)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, overwrite);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, overwrite);
            }
        }

        /// <summary>
        /// Recursively copy a directory asynchronously.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destinationDir">Destination directory path.</param>
        /// <param name="overwrite">True to overwrite files if they exist.</param>
        public static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, bool overwrite = true)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            var copyTasks = new List<Task>();

            // Copy files
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                copyTasks.Add(Task.Run(() => file.CopyTo(targetFilePath, overwrite)));
            }

            // Wait for all file copies to complete
            await Task.WhenAll(copyTasks);

            // Copy subdirectories
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, overwrite);
            }
        }

        /// <summary>
        /// Read file content as string.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>File content or null if error.</returns>
        public static async Task<string?> ReadFileAsStringAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Log.Warning("ReadFileAsStringAsync: File not found at {FilePath}", filePath);
                return null;
            }
            try
            {
                return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ReadFileAsStringAsync: Error reading file {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Write a string to a file. Will create directory if not exists and overwrite file if exists.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="content">Content to write.</param>
        /// <returns>True if write is successful, False if failed.</returns>
        public static async Task<bool> WriteStringToFileAsync(string filePath, string content)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WriteStringToFileAsync: Error writing to file {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Safely delete a file with logging.
        /// </summary>
        /// <param name="filePath">Path to the file to delete.</param>
        /// <param name="logger">Logger instance for logging operations.</param>
        public static void TryDeleteFile(string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                logger.LogError("TryDeleteFile: File path is null or empty");
                return;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    logger.LogInformation("TryDeleteFile: Successfully deleted file {FilePath}", filePath);
                }
                else
                {
                    logger.LogDebug("TryDeleteFile: File does not exist {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TryDeleteFile: Error deleting file {FilePath}", filePath);
            }
        }

        /// <summary>
        /// Check if a file path is safe to access by ensuring it is within an allowed base directory.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <param name="allowedBaseDirectory">The base directory that the file path must be within.</param>
        /// <returns>True if the file path is within the allowed base directory, false otherwise.</returns>
        public static bool IsPathSafe(string filePath, string allowedBaseDirectory)
        {
            var fullPath = Path.GetFullPath(filePath); // Chuẩn hóa đường dẫn
            var fullAllowedBase = Path.GetFullPath(allowedBaseDirectory);
            return fullPath.StartsWith(fullAllowedBase, StringComparison.OrdinalIgnoreCase);
        }
    }
}
