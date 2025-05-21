 // CMSAgent.Service/Configuration/Manager/RuntimeConfigManager.cs
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Shared.Constants; // For AgentConstants
using CMSAgent.Shared.Utils;     // For FileUtils
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Configuration.Manager
{
    /// <summary>
    /// Quản lý việc đọc và ghi file cấu hình runtime (runtime_config.json).
    /// </summary>
    public class RuntimeConfigManager : IRuntimeConfigManager
    {
        private readonly ILogger<RuntimeConfigManager> _logger;
        private readonly string _runtimeConfigFilePath;
        private readonly string _agentProgramDataPath;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1); // Đồng bộ truy cập file
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public RuntimeConfigManager(ILogger<RuntimeConfigManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Xác định đường dẫn đến thư mục ProgramData của Agent
            // Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) trả về "C:\ProgramData" trên Windows
            _agentProgramDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AgentConstants.AgentProgramDataFolderName);

            // Đảm bảo thư mục runtime_config tồn tại
            string runtimeConfigDir = Path.Combine(_agentProgramDataPath, AgentConstants.RuntimeConfigSubFolderName);
            try
            {
                Directory.CreateDirectory(runtimeConfigDir); // Tạo nếu chưa có
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Không thể tạo hoặc truy cập thư mục cấu hình runtime: {RuntimeConfigDir}", runtimeConfigDir);
                // Ném lỗi nghiêm trọng vì không thể hoạt động nếu không có thư mục này
                throw new InvalidOperationException($"Không thể tạo hoặc truy cập thư mục cấu hình runtime: {runtimeConfigDir}", ex);
            }

            _runtimeConfigFilePath = Path.Combine(runtimeConfigDir, AgentConstants.RuntimeConfigFileName);
            _logger.LogInformation("Đường dẫn file cấu hình runtime: {RuntimeConfigFilePath}", _runtimeConfigFilePath);

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true, // Ghi file JSON cho dễ đọc
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public string GetAgentProgramDataPath() => _agentProgramDataPath;
        public string GetRuntimeConfigFilePath() => _runtimeConfigFilePath;


        public async Task<RuntimeConfig> LoadConfigAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_runtimeConfigFilePath))
                {
                    _logger.LogWarning("File runtime_config.json không tìm thấy tại {FilePath}. Trả về cấu hình mặc định.", _runtimeConfigFilePath);
                    return new RuntimeConfig(); // Trả về đối tượng rỗng/mặc định
                }

                _logger.LogDebug("Đang đọc file runtime_config.json từ {FilePath}", _runtimeConfigFilePath);
                string jsonContent = await File.ReadAllTextAsync(_runtimeConfigFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("File runtime_config.json rỗng tại {FilePath}. Trả về cấu hình mặc định.", _runtimeConfigFilePath);
                    return new RuntimeConfig();
                }

                var config = JsonSerializer.Deserialize<RuntimeConfig>(jsonContent, _jsonSerializerOptions);
                if (config == null)
                {
                     _logger.LogError("Không thể deserialize runtime_config.json từ {FilePath}. Nội dung có thể không hợp lệ. Trả về cấu hình mặc định.", _runtimeConfigFilePath);
                    return new RuntimeConfig();
                }
                _logger.LogInformation("Tải cấu hình runtime thành công từ {FilePath}", _runtimeConfigFilePath);
                return config;
            }
            catch (JsonException jsonEx)
            {
                 _logger.LogError(jsonEx, "Lỗi JSON khi đọc runtime_config.json từ {FilePath}. Trả về cấu hình mặc định.", _runtimeConfigFilePath);
                return new RuntimeConfig(); // Trả về mặc định nếu lỗi parse JSON
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi đọc runtime_config.json từ {FilePath}. Trả về cấu hình mặc định.", _runtimeConfigFilePath);
                return new RuntimeConfig(); // Trả về mặc định nếu có lỗi khác
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> SaveConfigAsync(RuntimeConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            await _fileLock.WaitAsync();
            try
            {
                _logger.LogDebug("Đang ghi cấu hình runtime vào file: {FilePath}", _runtimeConfigFilePath);
                string jsonContent = JsonSerializer.Serialize(config, _jsonSerializerOptions);

                // Ghi vào file tạm trước, sau đó rename để đảm bảo tính toàn vẹn (atomic write)
                string tempFilePath = _runtimeConfigFilePath + ".tmp";
                await File.WriteAllTextAsync(tempFilePath, jsonContent);

                // Xóa file backup cũ nếu có (tùy chọn)
                // string backupFilePath = _runtimeConfigFilePath + ".bak";
                // if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
                // if (File.Exists(_runtimeConfigFilePath)) File.Move(_runtimeConfigFilePath, backupFilePath);

                File.Move(tempFilePath, _runtimeConfigFilePath, overwrite: true); // Ghi đè file cũ

                _logger.LogInformation("Lưu cấu hình runtime thành công vào {FilePath}", _runtimeConfigFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu runtime_config.json vào {FilePath}.", _runtimeConfigFilePath);
                return false;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<string?> GetAgentIdAsync()
        {
            var config = await LoadConfigAsync();
            return config.AgentId;
        }

        public async Task<string?> GetEncryptedAgentTokenAsync()
        {
            var config = await LoadConfigAsync();
            return config.AgentTokenEncrypted;
        }

        public async Task<PositionInfo?> GetPositionInfoAsync()
        {
            var config = await LoadConfigAsync();
            return config.RoomConfig;
        }

        public async Task UpdateAgentIdAsync(string agentId)
        {
            if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Agent ID không thể rỗng.", nameof(agentId));
            var config = await LoadConfigAsync();
            if (config.AgentId != agentId)
            {
                config.AgentId = agentId;
                await SaveConfigAsync(config);
                _logger.LogInformation("Agent ID đã được cập nhật thành: {AgentId}", agentId);
            }
        }

        public async Task UpdateEncryptedAgentTokenAsync(string encryptedToken)
        {
            // encryptedToken có thể là null nếu agent bị thu hồi token
            var config = await LoadConfigAsync();
            if (config.AgentTokenEncrypted != encryptedToken)
            {
                config.AgentTokenEncrypted = encryptedToken; // Cho phép gán null
                await SaveConfigAsync(config);
                _logger.LogInformation("Token đã mã hóa của Agent đã được cập nhật.");
            }
        }

        public async Task UpdatePositionInfoAsync(PositionInfo positionInfo)
        {
            if (positionInfo == null) throw new ArgumentNullException(nameof(positionInfo));
            var config = await LoadConfigAsync();
            // Cần một cách so sánh PositionInfo hiệu quả
            if (config.RoomConfig == null ||
                config.RoomConfig.RoomName != positionInfo.RoomName ||
                config.RoomConfig.PosX != positionInfo.PosX ||
                config.RoomConfig.PosY != positionInfo.PosY)
            {
                config.RoomConfig = positionInfo;
                await SaveConfigAsync(config);
                _logger.LogInformation("Thông tin vị trí của Agent đã được cập nhật: Room={Room}, X={X}, Y={Y}",
                    positionInfo.RoomName, positionInfo.PosX, positionInfo.PosY);
            }
        }
    }
}
