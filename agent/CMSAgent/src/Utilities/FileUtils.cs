using Serilog;
using System.Text.Json;

namespace CMSAgent.Utilities
{
    public static class FileUtils
    {
        /// <summary>
        /// Safely writes a JSON object to a file
        /// </summary>
        public static async Task<bool> WriteJsonObjectToFileAsync<T>(string filePath, T data, bool createBackup = true)
        {
            try
            {
                string tempFilePath = $"{filePath}.temp";
                string backupFilePath = $"{filePath}.bak";

                // Ensure directory exists
                DirectoryUtils.EnsureDirectoryExists(Path.GetDirectoryName(filePath));

                // Serialize the object with indentation for readability
                string jsonContent = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // First write to a temporary file
                await File.WriteAllTextAsync(tempFilePath, jsonContent);

                // Create backup of the existing file if necessary
                if (createBackup && File.Exists(filePath))
                {
                    if (File.Exists(backupFilePath))
                    {
                        File.Delete(backupFilePath);
                    }
                    File.Copy(filePath, backupFilePath);
                }

                // Replace the original file with the temp file
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempFilePath, filePath);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error writing JSON to file {FilePath}: {Message}", filePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Safely reads a JSON object from a file
        /// </summary>
        public static async Task<T?> ReadJsonObjectFromFileAsync<T>(string filePath, T? defaultValue = default)
        {
            try
            {
                // Check if file exists
                if (!File.Exists(filePath))
                {
                    Log.Warning("File not found: {FilePath}", filePath);
                    return defaultValue;
                }

                // Read the file content
                string jsonContent = await File.ReadAllTextAsync(filePath);

                // If file is empty, return default
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Log.Warning("File is empty: {FilePath}", filePath);
                    return defaultValue;
                }

                // Deserialize the JSON content
                T? result = JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? defaultValue;
            }
            catch (JsonException jsonEx)
            {
                Log.Error(jsonEx, "Error parsing JSON from file {FilePath}: {Message}", filePath, jsonEx.Message);

                // Try to recover from backup if it exists
                string backupFilePath = $"{filePath}.bak";
                if (File.Exists(backupFilePath))
                {
                    Log.Information("Attempting to recover from backup file: {BackupPath}", backupFilePath);
                    try
                    {
                        string backupContent = await File.ReadAllTextAsync(backupFilePath);
                        return JsonSerializer.Deserialize<T>(backupContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? defaultValue;
                    }
                    catch (Exception backupEx)
                    {
                        Log.Error(backupEx, "Error recovering from backup file: {Message}", backupEx.Message);
                    }
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading JSON from file {FilePath}: {Message}", filePath, ex.Message);
                return defaultValue;
            }
        }
    }
}