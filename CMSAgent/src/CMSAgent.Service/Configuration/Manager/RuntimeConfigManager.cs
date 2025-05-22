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
    /// Manages reading and writing runtime configuration file (runtime_config.json).
    /// </summary>
    public class RuntimeConfigManager : IRuntimeConfigManager
    {
        private readonly ILogger<RuntimeConfigManager> _logger;
        private readonly string _runtimeConfigFilePath;
        private readonly string _agentProgramDataPath;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1); // Synchronize file access
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public RuntimeConfigManager(ILogger<RuntimeConfigManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Determine path to Agent's ProgramData directory
            // Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) returns "C:\ProgramData" on Windows
            _agentProgramDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AgentConstants.AgentProgramDataFolderName);

            // Ensure runtime_config directory exists
            string runtimeConfigDir = Path.Combine(_agentProgramDataPath, AgentConstants.RuntimeConfigSubFolderName);
            try
            {
                Directory.CreateDirectory(runtimeConfigDir); // Create if not exists
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Cannot create or access runtime configuration directory: {RuntimeConfigDir}", runtimeConfigDir);
                // Throw critical error as we cannot operate without this directory
                throw new InvalidOperationException($"Cannot create or access runtime configuration directory: {runtimeConfigDir}", ex);
            }

            _runtimeConfigFilePath = Path.Combine(runtimeConfigDir, AgentConstants.RuntimeConfigFileName);
            _logger.LogInformation("Runtime configuration file path: {RuntimeConfigFilePath}", _runtimeConfigFilePath);

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true, // Write JSON file for readability
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
                    _logger.LogWarning("Runtime configuration file not found at {FilePath}. Returning default configuration.", _runtimeConfigFilePath);
                    return new RuntimeConfig(); // Return empty/default object
                }

                _logger.LogDebug("Reading runtime_config.json from {FilePath}", _runtimeConfigFilePath);
                string? jsonContent = await FileUtils.ReadFileAsStringAsync(_runtimeConfigFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Runtime configuration file is empty at {FilePath}. Returning default configuration.", _runtimeConfigFilePath);
                    return new RuntimeConfig();
                }

                var config = JsonSerializer.Deserialize<RuntimeConfig>(jsonContent, _jsonSerializerOptions);
                if (config == null)
                {
                     _logger.LogError("Cannot deserialize runtime_config.json from {FilePath}. Content may be invalid. Returning default configuration.", _runtimeConfigFilePath);
                    return new RuntimeConfig();
                }
                _logger.LogInformation("Successfully loaded runtime configuration from {FilePath}", _runtimeConfigFilePath);
                return config;
            }
            catch (JsonException jsonEx)
            {
                 _logger.LogError(jsonEx, "JSON error while reading runtime_config.json from {FilePath}. Returning default configuration.", _runtimeConfigFilePath);
                return new RuntimeConfig(); // Return default if JSON parse error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error while reading runtime_config.json from {FilePath}. Returning default configuration.", _runtimeConfigFilePath);
                return new RuntimeConfig(); // Return default for other errors
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
                _logger.LogDebug("Writing runtime configuration to file: {FilePath}", _runtimeConfigFilePath);
                string jsonContent = JsonSerializer.Serialize(config, _jsonSerializerOptions);

                // Write to temporary file first, then rename to ensure integrity (atomic write)
                string tempFilePath = _runtimeConfigFilePath + ".tmp";
                bool writeSuccess = await FileUtils.WriteStringToFileAsync(tempFilePath, jsonContent);
                if (!writeSuccess)
                {
                    _logger.LogError("Cannot write temporary file {TempFilePath}", tempFilePath);
                    return false;
                }

                File.Move(tempFilePath, _runtimeConfigFilePath, overwrite: true); // Overwrite old file

                _logger.LogInformation("Successfully saved runtime configuration to {FilePath}", _runtimeConfigFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving runtime_config.json to {FilePath}.", _runtimeConfigFilePath);
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
            if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));
            var config = await LoadConfigAsync();
            if (config.AgentId != agentId)
            {
                config.AgentId = agentId;
                await SaveConfigAsync(config);
                _logger.LogInformation("Agent ID has been updated to: {AgentId}", agentId);
            }
        }

        public async Task UpdateEncryptedAgentTokenAsync(string encryptedToken)
        {
            if (string.IsNullOrWhiteSpace(encryptedToken))
            {
                throw new ArgumentException("Encrypted token cannot be empty or null.", nameof(encryptedToken));
            }

            var config = await LoadConfigAsync();
            if (config.AgentTokenEncrypted != encryptedToken)
            {
                config.AgentTokenEncrypted = encryptedToken;
                await SaveConfigAsync(config);
                _logger.LogInformation("Agent's encrypted token has been updated.");
            }
        }

        public async Task UpdatePositionInfoAsync(PositionInfo positionInfo)
        {
            if (positionInfo == null) throw new ArgumentNullException(nameof(positionInfo));
            var config = await LoadConfigAsync();
            // Need an efficient way to compare PositionInfo
            if (config.RoomConfig == null ||
                config.RoomConfig.RoomName != positionInfo.RoomName ||
                config.RoomConfig.PosX != positionInfo.PosX ||
                config.RoomConfig.PosY != positionInfo.PosY)
            {
                config.RoomConfig = positionInfo;
                await SaveConfigAsync(config);
                _logger.LogInformation("Agent's position information has been updated: Room={Room}, X={X}, Y={Y}",
                    positionInfo.RoomName, positionInfo.PosX, positionInfo.PosY);
            }
        }
    }
}
