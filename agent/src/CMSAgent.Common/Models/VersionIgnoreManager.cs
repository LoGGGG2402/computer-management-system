using System.Text.Json;
using CMSAgent.Common.DTOs;

namespace CMSAgent.Common.Models
{
    /// <summary>
    /// Manages the list of ignored update versions
    /// </summary>
    public static class VersionIgnoreManager
    {
        private static readonly string _ignoredVersionsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ignored_versions.json");
        private static readonly SemaphoreSlim _fileLock = new(1, 1);
        
        // Tạo và lưu trữ JsonSerializerOptions để tái sử dụng
        private static readonly JsonSerializerOptions _deserializeOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions _serializeOptions = new() 
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        /// <summary>
        /// Checks if a version is in the ignore list
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>True if version is ignored, False otherwise</returns>
        public static async Task<bool> IsVersionIgnoredAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }
            
            try
            {
                var versionInfoList = await LoadIgnoredVersionsFromFileAsync();
                return versionInfoList.Any(v => v.Version == version);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Adds a version to the ignore list
        /// </summary>
        /// <param name="version">Version to add to ignore list</param>
        /// <param name="reason">Reason for ignoring</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task AddVersionToIgnoreListAsync(string version, string? reason = null)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return;
            }
            
            try
            {
                var versionInfoList = await LoadIgnoredVersionsFromFileAsync();
                
                // Kiểm tra phiên bản đã tồn tại chưa
                var existingVersion = versionInfoList.FirstOrDefault(v => v.Version == version);
                
                if (existingVersion != null)
                {
                    // Cập nhật thông tin nếu phiên bản đã tồn tại
                    existingVersion.FailedAttempts++;
                    if (!string.IsNullOrWhiteSpace(reason))
                    {
                        existingVersion.Reason = reason;
                    }
                }
                else
                {
                    // Thêm phiên bản mới
                    versionInfoList.Add(new IgnoredVersionInfo
                    {
                        Version = version,
                        AddedTime = DateTime.UtcNow,
                        FailedAttempts = 1,
                        Reason = reason
                    });
                }
                
                // Lưu danh sách vào file
                await SaveIgnoredVersionsToFileAsync(versionInfoList);
            }
            catch
            {
                // Ignore errors
            }
        }
        
        /// <summary>
        /// Reads the list of ignored versions from file
        /// </summary>
        /// <returns>List of ignored version information</returns>
        private static async Task<List<IgnoredVersionInfo>> LoadIgnoredVersionsFromFileAsync()
        {
            var result = new List<IgnoredVersionInfo>();
            
            await _fileLock.WaitAsync();
            try
            {
                if (File.Exists(_ignoredVersionsFilePath))
                {
                    string jsonContent = await File.ReadAllTextAsync(_ignoredVersionsFilePath);
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var loadedVersions = JsonSerializer.Deserialize<List<IgnoredVersionInfo>>(
                            jsonContent,
                            _deserializeOptions);
                        
                        if (loadedVersions != null)
                        {
                            result = loadedVersions;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            finally
            {
                _fileLock.Release();
            }
            
            return result;
        }
        
        /// <summary>
        /// Saves the list of ignored versions to file
        /// </summary>
        /// <param name="versionInfoList">List of ignored version information</param>
        private static async Task SaveIgnoredVersionsToFileAsync(List<IgnoredVersionInfo> versionInfoList)
        {
            await _fileLock.WaitAsync();
            try
            {
                string jsonContent = JsonSerializer.Serialize(
                    versionInfoList,
                    _serializeOptions);
                
                await File.WriteAllTextAsync(_ignoredVersionsFilePath, jsonContent);
            }
            catch
            {
                // Ignore errors
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
} 