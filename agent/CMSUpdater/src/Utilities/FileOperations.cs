using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CMSUpdater.Utilities
{
    /// <summary>
    /// Helper class for file operations used during the update process
    /// </summary>
    public static class FileOperations
    {
        /// <summary>
        /// Creates a backup of files and directories
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="sourceDir">Source directory (current agent)</param>
        /// <param name="backupDir">Backup directory</param>
        /// <param name="version">Version to include in backup name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task BackupFilesAsync(ILogger logger, string sourceDir, string backupDir, string version, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Creating backup directory: {BackupDir}", backupDir);
                Directory.CreateDirectory(backupDir);

                logger.LogInformation("Copying files from {SourceDir} to {BackupDir}", sourceDir, backupDir);
                
                // Create version.txt file in the backup directory
                string versionFilePath = Path.Combine(backupDir, "version.txt");
                await File.WriteAllTextAsync(versionFilePath, version, cancellationToken);
                
                // Copy all files and directories
                foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                {
                    string destPath = dirPath.Replace(sourceDir, backupDir);
                    Directory.CreateDirectory(destPath);
                }
                
                foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    string destPath = filePath.Replace(sourceDir, backupDir);
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    await Task.Run(() => File.Copy(filePath, destPath, true), cancellationToken);
                }
                
                logger.LogInformation("Backup completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during backup creation");
                throw;
            }
        }

        /// <summary>
        /// Replaces files in the target directory with new ones, preserving specified files
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="newSourcePath">Path to new files</param>
        /// <param name="targetDir">Target directory</param>
        /// <param name="filesToPreserve">Array of files/directories to preserve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ReplaceFilesAsync(ILogger logger, string newSourcePath, string targetDir, string[] filesToPreserve, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Replacing files");
                
                // Delete all files and directories in the target directory except those to preserve
                foreach (string filePath in Directory.GetFiles(targetDir, "*", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (Array.Exists(filesToPreserve, f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)) ||
                        Array.Exists(filesToPreserve, f => fileName.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        logger.LogInformation("Preserving file: {FileName}", fileName);
                        continue;
                    }
                    
                    logger.LogInformation("Deleting file: {FilePath}", filePath);
                    File.Delete(filePath);
                }

                foreach (string dirPath in Directory.GetDirectories(targetDir, "*", SearchOption.TopDirectoryOnly))
                {
                    string dirName = new DirectoryInfo(dirPath).Name;
                    if (Array.Exists(filesToPreserve, f => f.Equals(dirName, StringComparison.OrdinalIgnoreCase)) ||
                        Array.Exists(filesToPreserve, f => dirName.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        logger.LogInformation("Preserving directory: {DirName}", dirName);
                        continue;
                    }
                    
                    logger.LogInformation("Deleting directory: {DirPath}", dirPath);
                    Directory.Delete(dirPath, true);
                }
                
                // Copy all files from the new source to the target directory
                foreach (string dirPath in Directory.GetDirectories(newSourcePath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = dirPath.Substring(newSourcePath.Length).TrimStart('\\', '/');
                    string destPath = Path.Combine(targetDir, relativePath);
                    
                    logger.LogInformation("Creating directory: {DestPath}", destPath);
                    Directory.CreateDirectory(destPath);
                }

                foreach (string filePath in Directory.GetFiles(newSourcePath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = filePath.Substring(newSourcePath.Length).TrimStart('\\', '/');
                    string destPath = Path.Combine(targetDir, relativePath);
                    
                    logger.LogInformation("Copying file: {SourcePath} to {DestPath}", filePath, destPath);
                    await Task.Run(() => File.Copy(filePath, destPath, true), cancellationToken);
                }
                
                logger.LogInformation("File replacement completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during file replacement");
                throw;
            }
        }

        /// <summary>
        /// Cleans up a directory by deleting it and its contents
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="path">Path to clean up</param>
        public static void CleanupDirectory(ILogger logger, string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    logger.LogInformation("Cleaning up directory: {Path}", path);
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during cleanup");
                // We don't throw here as this is not critical to the update process
            }
        }
    }
}
