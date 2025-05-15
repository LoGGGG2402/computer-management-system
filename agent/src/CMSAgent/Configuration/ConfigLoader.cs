using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Configuration
{
    /// <summary>
    /// Class that loads and saves agent configuration
    /// </summary>
    public class ConfigLoader : IConfigLoader
    {
        private readonly ILogger<ConfigLoader> _logger;
        private readonly IOptionsMonitor<CmsAgentSettingsOptions> _settingsMonitor;
        private RuntimeConfig? _runtimeConfigCache = null;
        private readonly string _runtimeConfigPath;
        private readonly string _installPath = string.Empty;
        private readonly string _dataPath;
        private readonly string _runtimeConfigFileName = "runtime_config.json";
        
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Current agent configuration from appsettings.json
        /// </summary>
        public CmsAgentSettingsOptions Settings => _settingsMonitor.CurrentValue;

        /// <summary>
        /// Agent-specific configuration (AgentSettings section in CmsAgentSettingsOptions)
        /// </summary>
        public AgentSpecificSettingsOptions AgentSettings => _settingsMonitor.CurrentValue.AgentSettings;

        /// <summary>
        /// Initializes a new instance of ConfigLoader
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <param name="settingsMonitor">Configuration monitor from appsettings.json</param>
        public ConfigLoader(ILogger<ConfigLoader> logger, IOptionsMonitor<CmsAgentSettingsOptions> settingsMonitor)
        {
            _logger = logger;
            _settingsMonitor = settingsMonitor;
            _installPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Settings.AppName ?? "CMSAgent");
            _runtimeConfigPath = Path.Combine(_dataPath, "runtime_config", _runtimeConfigFileName);
        }

        /// <summary>
        /// Loads runtime configuration from file
        /// </summary>
        /// <param name="forceReload">Whether to force reload from disk even if already in memory cache</param>
        /// <returns>RuntimeConfig object or default configuration if not found/error</returns>
        public async Task<RuntimeConfig> LoadRuntimeConfigAsync(bool forceReload = false)
        {
            if (_runtimeConfigCache != null && !forceReload)
                return _runtimeConfigCache;

            try
            {
                if (!File.Exists(_runtimeConfigPath))
                {
                    _logger.LogWarning("Runtime configuration file not found at {Path}", _runtimeConfigPath);
                    return CreateDefaultRuntimeConfig();
                }

                var json = await File.ReadAllTextAsync(_runtimeConfigPath);
                _runtimeConfigCache = JsonSerializer.Deserialize<RuntimeConfig>(json);
                
                if (_runtimeConfigCache == null)
                {
                    _logger.LogError("Cannot deserialize runtime configuration, creating default configuration");
                    return CreateDefaultRuntimeConfig();
                }

                // Validate and ensure AgentId is always set
                if (string.IsNullOrWhiteSpace(_runtimeConfigCache.AgentId))
                {
                    _logger.LogWarning("Loaded configuration has empty AgentId, generating new one");
                    _runtimeConfigCache.AgentId = GenerateNewAgentId();
                    await SaveRuntimeConfigAsync(_runtimeConfigCache);
                }

                return _runtimeConfigCache;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot load runtime configuration from {Path}", _runtimeConfigPath);
                return CreateDefaultRuntimeConfig();
            }
        }

        /// <summary>
        /// Creates default runtime configuration
        /// </summary>
        private RuntimeConfig CreateDefaultRuntimeConfig()
        {
            var config = new RuntimeConfig
            {
                AgentId = GenerateNewAgentId(),
                RoomConfig = new RoomConfig
                {
                    RoomName = "Default",
                    PosX = 0,
                    PosY = 0
                },
                AgentTokenEncrypted = string.Empty
            };
            
            _runtimeConfigCache = config;
            return config;
        }

        /// <summary>
        /// Generates a new unique AgentId
        /// </summary>
        private string GenerateNewAgentId()
        {
            return "AGENT-" + Guid.NewGuid().ToString("N")[..8];
        }

        /// <summary>
        /// Saves runtime configuration to file
        /// </summary>
        /// <param name="config">Configuration object to save</param>
        public async Task SaveRuntimeConfigAsync(RuntimeConfig config)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(_runtimeConfigPath);
                if (directoryPath != null)
                {
                    Directory.CreateDirectory(directoryPath);
                    var json = JsonSerializer.Serialize(config, _jsonOptions);
                    await File.WriteAllTextAsync(_runtimeConfigPath, json);
                    _runtimeConfigCache = config;
                    _logger.LogInformation("Runtime configuration saved to {Path}", _runtimeConfigPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot save runtime configuration to {Path}", _runtimeConfigPath);
            }
        }

        /// <summary>
        /// Gets agent ID from runtime configuration
        /// </summary>
        /// <returns>AgentId or empty string if not set</returns>
        public string GetAgentId() => _runtimeConfigCache?.AgentId ?? string.Empty;

        /// <summary>
        /// Gets encrypted agent token from runtime configuration
        /// </summary>
        /// <returns>Encrypted token or empty string if not set</returns>
        public string GetEncryptedAgentToken() => _runtimeConfigCache?.AgentTokenEncrypted ?? string.Empty;

        /// <summary>
        /// Gets the installation path of the agent
        /// </summary>
        /// <returns>Installation path</returns>
        public string GetInstallPath() => _installPath;

        /// <summary>
        /// Gets the data directory path of the agent
        /// </summary>
        /// <returns>Data directory path</returns>
        public string GetDataPath() => _dataPath;

        /// <summary>
        /// Gets the current version of the agent.
        /// </summary>
        /// <returns>Agent version.</returns>
        public string GetAgentVersion() => Settings.Version;
    }
}
