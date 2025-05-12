using Serilog;
using System.IO.Compression;

namespace CMSAgent.Utilities
{
    public static class DirectoryUtils
    {
        /// <summary>
        /// Ensures that a directory exists, creating it if necessary
        /// </summary>
        public static void EnsureDirectoryExists(string? directoryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    return;
                }

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Log.Debug("Created directory: {DirectoryPath}", directoryPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating directory {DirectoryPath}: {Message}", directoryPath, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Safely deletes a directory and all its contents, with retry logic
        /// </summary>
        public static bool SafeDeleteDirectory(string directoryPath, bool recursive = true, int maxRetries = 3)
        {
            if (!Directory.Exists(directoryPath))
            {
                return true;
            }

            int currentRetry = 0;
            while (currentRetry < maxRetries)
            {
                try
                {
                    Directory.Delete(directoryPath, recursive);
                    Log.Debug("Deleted directory: {DirectoryPath}", directoryPath);
                    return true;
                }
                catch (IOException ioEx)
                {
                    currentRetry++;
                    Log.Warning(ioEx, "Failed to delete directory {DirectoryPath} (attempt {Attempt}/{MaxRetries}): {Message}", 
                        directoryPath, currentRetry, maxRetries, ioEx.Message);
                    
                    if (currentRetry >= maxRetries)
                    {
                        Log.Error("Max retries reached. Could not delete directory: {DirectoryPath}", directoryPath);
                        return false;
                    }
                    
                    // Wait before retrying
                    Thread.Sleep(500 * currentRetry);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error deleting directory {DirectoryPath}: {Message}", directoryPath, ex.Message);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Safely copies a directory and all its contents to a destination
        /// </summary>
        public static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = true)
        {
            try
            {
                // Create destination directory if it doesn't exist
                EnsureDirectoryExists(destinationDir);

                // Get all files in the source directory
                foreach (string filePath in Directory.GetFiles(sourceDir))
                {
                    // Get filename only
                    string fileName = Path.GetFileName(filePath);
                    
                    // Combine destination directory with filename
                    string destFile = Path.Combine(destinationDir, fileName);
                    
                    // Copy file
                    File.Copy(filePath, destFile, overwrite);
                }

                // Recursively copy subdirectories
                foreach (string subDirPath in Directory.GetDirectories(sourceDir))
                {
                    // Get subdirectory name
                    string subDirName = Path.GetFileName(subDirPath);
                    
                    // Combine destination directory with subdirectory name
                    string destSubDir = Path.Combine(destinationDir, subDirName);
                    
                    // Recursively copy subdirectory
                    CopyDirectory(subDirPath, destSubDir, overwrite);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error copying directory from {SourceDir} to {DestinationDir}: {Message}", 
                    sourceDir, destinationDir, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Extracts a ZIP file to a destination directory
        /// </summary>
        public static void ExtractZipFile(string zipFilePath, string destinationDirectoryPath, bool overwrite = true)
        {
            try
            {
                // Ensure destination directory exists
                EnsureDirectoryExists(destinationDirectoryPath);

                // If overwrite is true, delete the destination directory contents first
                if (overwrite && Directory.Exists(destinationDirectoryPath))
                {
                    foreach (string filePath in Directory.GetFiles(destinationDirectoryPath))
                    {
                        File.Delete(filePath);
                    }

                    foreach (string subDirPath in Directory.GetDirectories(destinationDirectoryPath))
                    {
                        SafeDeleteDirectory(subDirPath, true);
                    }
                }

                // Extract the ZIP file
                ZipFile.ExtractToDirectory(zipFilePath, destinationDirectoryPath, overwrite);
                
                Log.Information("Extracted ZIP file {ZipFilePath} to {DestinationDirectoryPath}", 
                    zipFilePath, destinationDirectoryPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting ZIP file {ZipFilePath} to {DestinationDirectoryPath}: {Message}", 
                    zipFilePath, destinationDirectoryPath, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Creates a ZIP file from a directory
        /// </summary>
        public static void CreateZipFromDirectory(string sourceDirectoryPath, string zipFilePath)
        {
            try
            {
                // Ensure parent directory of zip file exists
                EnsureDirectoryExists(Path.GetDirectoryName(zipFilePath));

                // Delete existing ZIP file if it exists
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                // Create the ZIP file
                ZipFile.CreateFromDirectory(sourceDirectoryPath, zipFilePath);
                
                Log.Information("Created ZIP file {ZipFilePath} from directory {SourceDirectoryPath}", 
                    zipFilePath, sourceDirectoryPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating ZIP file {ZipFilePath} from directory {SourceDirectoryPath}: {Message}", 
                    zipFilePath, sourceDirectoryPath, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the size of a directory in bytes
        /// </summary>
        public static long GetDirectorySize(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return 0;
            }

            try
            {
                // Calculate the size of all files in the directory
                long size = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Sum(filePath => new FileInfo(filePath).Length);
                
                return size;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calculating directory size for {DirectoryPath}: {Message}", 
                    directoryPath, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Gets the path to the CMSAgent application data directory
        /// </summary>
        public static string GetAppDataDirectory()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "CMSAgent");
            
            EnsureDirectoryExists(appDataPath);
            return appDataPath;
        }

        /// <summary>
        /// Gets a subdirectory within the app data directory
        /// </summary>
        public static string GetAppDataSubdirectory(string subdirectoryName)
        {
            string subdirectoryPath = Path.Combine(GetAppDataDirectory(), subdirectoryName);
            EnsureDirectoryExists(subdirectoryPath);
            return subdirectoryPath;
        }

        public static string GetProgramDataPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CMSAgent");
        }

        public static string GetLogsPath()
        {
            return Path.Combine(GetProgramDataPath(), "logs");
        }

        public static string GetErrorReportsPath()
        {
            return Path.Combine(GetProgramDataPath(), "error_reports");
        }

        public static string GetUpdaterPath()
        {
            // Assuming updater is in a subdirectory of the agent's installation or a known relative path
            // This might need adjustment based on actual deployment structure
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CMSUpdater");
        }
        
        public static string GetTempPath()
        {
            return Path.Combine(GetProgramDataPath(), "temp");
        }

        /// <summary>
        /// Ensures all required directories for the agent are created.
        /// </summary>
        public static void EnsureRequiredDirectoriesExist()
        {
            Log.Debug("Ensuring required directories exist...");
            EnsureDirectoryExists(GetProgramDataPath());
            EnsureDirectoryExists(GetLogsPath());
            EnsureDirectoryExists(GetErrorReportsPath()); // Added error_reports
            EnsureDirectoryExists(GetTempPath()); // Added temp as per Standard.md III.2.c
            // The updater path is trickier as it's part of the installation, not a data directory.
            // We might not need to "ensure" it here unless we're writing to it.
            Log.Information("Required directories checked/created.");
        }
    }
}