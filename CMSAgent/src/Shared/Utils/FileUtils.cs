using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Serilog;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides a comprehensive set of file system utilities for handling file operations, compression,
    /// directory management, and security checks. Includes both synchronous and asynchronous methods
    /// for common file system operations.
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Calculates the SHA-256 cryptographic hash of a file asynchronously.
        /// </summary>
        /// <param name="filePath">The full path to the file to hash.</param>
        /// <returns>
        /// A hexadecimal string representation of the SHA-256 hash, or null if the operation fails.
        /// </returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when file access is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs during reading.</exception>
        /// <remarks>
        /// Uses streaming to efficiently handle large files without loading them entirely into memory.
        /// Returns null if the file doesn't exist or cannot be accessed.
        /// </remarks>
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
        /// Compresses a directory into a ZIP file asynchronously.
        /// </summary>
        /// <param name="sourceDirectoryPath">The path to the directory to compress.</param>
        /// <param name="zipFilePath">The path where the ZIP file will be created.</param>
        /// <param name="includeBaseDirectory">
        /// If true, the base directory is included in the archive; if false, only the directory contents are included.
        /// </param>
        /// <returns>
        /// True if the compression succeeds, false if the source directory doesn't exist, 
        /// the zip file path is invalid, or compression fails.
        /// </returns>
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
        /// Extracts a ZIP file to a specified directory asynchronously.
        /// </summary>
        /// <param name="zipFilePath">The path to the ZIP file to extract.</param>
        /// <param name="extractDirectoryPath">The directory where the ZIP contents will be extracted.</param>
        /// <param name="overwriteFiles">If true, any files in the target directory with the same name will be overwritten.</param>
        /// <returns>
        /// True if the extraction succeeds, false if the ZIP file doesn't exist,
        /// the extract directory path is invalid, or extraction fails.
        /// </returns>
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
        /// Moves a directory from one location to another.
        /// </summary>
        /// <param name="sourceDirName">The source directory path to move.</param>
        /// <param name="destDirName">The destination directory path.</param>
        /// <param name="overwrite">If true and the destination exists, it will be overwritten; otherwise, an exception is thrown.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
        /// <exception cref="IOException">Thrown when the destination directory exists and overwrite is false.</exception>
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
        /// Moves a directory from one location to another asynchronously.
        /// </summary>
        /// <param name="sourceDirName">The source directory path to move.</param>
        /// <param name="destDirName">The destination directory path.</param>
        /// <param name="overwrite">If true and the destination exists, it will be overwritten; otherwise, an exception is thrown.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
        /// <exception cref="IOException">Thrown when the destination directory exists and overwrite is false.</exception>
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
        /// Recursively copies a directory and all its contents to a new location.
        /// </summary>
        /// <param name="sourceDir">The source directory to copy.</param>
        /// <param name="destinationDir">The destination directory path.</param>
        /// <param name="overwrite">If true, existing files will be overwritten; otherwise, an exception is thrown.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
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
        /// Recursively copies a directory and all its contents to a new location asynchronously.
        /// </summary>
        /// <param name="sourceDir">The source directory to copy.</param>
        /// <param name="destinationDir">The destination directory path.</param>
        /// <param name="overwrite">If true, existing files will be overwritten; otherwise, an exception is thrown.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
        public static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, bool overwrite = true)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);            var copyTasks = new List<Task>();

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                copyTasks.Add(Task.Run(() => file.CopyTo(targetFilePath, overwrite)));
            }

            await Task.WhenAll(copyTasks);

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, overwrite);
            }
        }

        /// <summary>
        /// Reads a file's contents as a string asynchronously using UTF-8 encoding.
        /// </summary>
        /// <param name="filePath">The path to the file to read.</param>
        /// <returns>
        /// The file contents as a string if successful; otherwise, null if the file doesn't exist or an error occurs.
        /// </returns>
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
        /// Writes a string to a file asynchronously using UTF-8 encoding.
        /// </summary>
        /// <param name="filePath">The path to the file to write to.</param>
        /// <param name="content">The content to write to the file.</param>
        /// <returns>True if the write operation succeeds; otherwise, false.</returns>
        /// <remarks>
        /// This method creates any necessary directories in the file path if they don't exist.
        /// </remarks>
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
        /// Attempts to delete a file and logs the result, but doesn't throw exceptions if the operation fails.
        /// </summary>
        /// <param name="filePath">The path to the file to delete.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <remarks>
        /// This method silently handles any exceptions during the file deletion process
        /// and logs appropriate information about success, failure, or if the file doesn't exist.
        /// </remarks>
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
        /// Determines whether a file path is contained within an allowed base directory to prevent path traversal attacks.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <param name="allowedBaseDirectory">The allowed base directory.</param>
        /// <returns>
        /// True if the file path is contained within the allowed base directory; otherwise, false.
        /// </returns>
        /// <remarks>
        /// This method helps prevent directory traversal security vulnerabilities by ensuring
        /// that file operations are restricted to an approved directory tree.
        /// </remarks>
        public static bool IsPathSafe(string filePath, string allowedBaseDirectory)
        {
            var fullPath = Path.GetFullPath(filePath);
            var fullAllowedBase = Path.GetFullPath(allowedBaseDirectory);
            return fullPath.StartsWith(fullAllowedBase, StringComparison.OrdinalIgnoreCase);
        }
    }
}
