// Filepath: c:\Users\longpph\Desktop\computer-management-system\dotnet_agent\CMSAgent\SystemOperations\DirectoryUtils.cs
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Provides utility methods for directory operations such as creating, deleting, copying, and calculating size.
    /// </summary>
    public class DirectoryUtils
    {
        private readonly ILogger<DirectoryUtils> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryUtils"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging messages.</param>
        public DirectoryUtils(ILogger<DirectoryUtils> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Ensures that the specified directory exists. If it does not exist, it is created.
        /// </summary>
        /// <param name="path">The path of the directory to check or create.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="Exception">Rethrows exceptions that occur during directory creation.</exception>
        public void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path), "Directory path cannot be null or whitespace.");
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation("Created directory: {Path}", Path.GetFullPath(path));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure directory exists: {Path}", Path.GetFullPath(path));
                throw;
            }
        }

        /// <summary>
        /// Asynchronously deletes the specified directory and all its contents recursively.
        /// If the directory does not exist, the method logs a warning and returns.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        /// <returns>A task that represents the asynchronous deletion operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="Exception">Rethrows exceptions that occur during directory deletion.</exception>
        public async Task DeleteDirectoryRecursiveAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path), "Directory path cannot be null or whitespace.");
            }

            if (!Directory.Exists(path))
            {
                _logger.LogWarning("Attempted to delete a non-existent directory: {Path}", Path.GetFullPath(path));
                return;
            }

            _logger.LogInformation("Recursively deleting directory: {Path}", Path.GetFullPath(path));
            try
            {
                await Task.Run(() => Directory.Delete(path, true));
                _logger.LogInformation("Successfully deleted directory: {Path}", Path.GetFullPath(path));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete directory recursively: {Path}", Path.GetFullPath(path));
                throw;
            }
        }

        /// <summary>
        /// Asynchronously copies a directory from a source location to a destination location.
        /// </summary>
        /// <param name="sourceDir">The path of the source directory.</param>
        /// <param name="destDir">The path of the destination directory. It will be created if it doesn't exist.</param>
        /// <param name="recursive">True to copy subdirectories recursively; false otherwise.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceDir"/> or <paramref name="destDir"/> is null or whitespace.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the <paramref name="sourceDir"/> does not exist.</exception>
        /// <exception cref="Exception">Rethrows exceptions that occur during the copy operation.</exception>
        public async Task CopyDirectoryAsync(string sourceDir, string destDir, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(sourceDir)) throw new ArgumentNullException(nameof(sourceDir));
            if (string.IsNullOrWhiteSpace(destDir)) throw new ArgumentNullException(nameof(destDir));

            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            _logger.LogInformation("Copying directory from {Source} to {Destination}. Recursive: {Recursive}", Path.GetFullPath(sourceDir), Path.GetFullPath(destDir), recursive);

            try
            {
                EnsureDirectoryExists(destDir);

                foreach (FileInfo file in dir.GetFiles())
                {
                    string targetFilePath = Path.Combine(destDir, file.Name);
                    await Task.Run(() => file.CopyTo(targetFilePath, true));
                    _logger.LogTrace("Copied file {SourceFile} to {DestinationFile}", file.FullName, targetFilePath);
                }

                if (recursive)
                {
                    DirectoryInfo[] dirs = dir.GetDirectories();
                    foreach (DirectoryInfo subDir in dirs)
                    {
                        string newDestDir = Path.Combine(destDir, subDir.Name);
                        await CopyDirectoryAsync(subDir.FullName, newDestDir, true);
                    }
                }
                _logger.LogInformation("Successfully copied directory from {Source} to {Destination}", Path.GetFullPath(sourceDir), Path.GetFullPath(destDir));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy directory from {Source} to {Destination}", Path.GetFullPath(sourceDir), Path.GetFullPath(destDir));
                throw;
            }
        }

        /// <summary>
        /// Asynchronously calculates the total size of a directory, including all its files and subdirectories.
        /// </summary>
        /// <param name="path">The path of the directory.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the total size of the directory in bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified by <paramref name="path"/> does not exist.</exception>
        /// <exception cref="Exception">Rethrows exceptions that occur during size calculation.</exception>
        public async Task<long> GetDirectorySizeAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path)) 
                throw new DirectoryNotFoundException($"Directory not found: {Path.GetFullPath(path)}");

            _logger.LogDebug("Calculating size of directory: {Path}", Path.GetFullPath(path));
            long size = 0;

            try
            {
                size = await Task.Run(() => 
                {
                    var dirInfo = new DirectoryInfo(path);
                    long currentSize = dirInfo.GetFiles().Sum(file => file.Length);
                    foreach (var subDirInfo in dirInfo.GetDirectories())
                    {
                        currentSize += GetDirectorySizeNonAsync(subDirInfo.FullName);
                    }
                    return currentSize;
                });
                _logger.LogInformation("Calculated size of directory {Path}: {Size} bytes", Path.GetFullPath(path), size);
                return size;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate size of directory: {Path}", Path.GetFullPath(path));
                throw;
            }
        }

        /// <summary>
        /// Recursively calculates the size of a directory. This is a non-asynchronous helper method.
        /// </summary>
        /// <param name="path">The path of the directory.</param>
        /// <returns>The total size of the directory in bytes.</returns>
        private long GetDirectorySizeNonAsync(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            long size = dirInfo.GetFiles().Sum(file => file.Length);
            foreach (var subDirInfo in dirInfo.GetDirectories())
            {
                size += GetDirectorySizeNonAsync(subDirInfo.FullName);
            }
            return size;
        }
    }
}
