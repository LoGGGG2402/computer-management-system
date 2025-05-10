using CMSAgent.Models; // This should bring in AgentConfig and its nested classes
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json; // For deserializing agent_config.json

namespace CMSAgent.Configuration
{
    public class ConfigManager
    {
        private readonly ILogger<ConfigManager> _logger;
        // _configuration is primarily used for appsettings.json, not agent_config.json directly anymore
        private readonly IConfiguration? _appSettingsConfiguration; 
        public AgentConfig AgentConfig { get; private set; } // This is the strongly-typed config from agent_config.json

        public string AgentVersion { get; private set; } = "0.0.0"; // Default, should be set by Agent.cs

        public ConfigManager(ILogger<ConfigManager> logger, string agentConfigFileName = "agent_config.json")
        {
            _logger = logger;

            // Load appsettings.json (for logging, etc., if needed by other parts of the app)
            var appSettingsBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            _appSettingsConfiguration = appSettingsBuilder.Build();
            
            // Load and parse agent_config.json
            string agentConfigPath = Path.Combine(AppContext.BaseDirectory, agentConfigFileName);
            try
            {
                if (File.Exists(agentConfigPath))
                {
                    string jsonContent = File.ReadAllText(agentConfigPath);
                    // Using System.Text.Json to deserialize into our AgentConfig model
                    // Ensure AgentConfig and its sub-models have System.Text.Json.Serialization.JsonPropertyName attributes
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false }; // Be strict with casing from file
                    AgentConfig = JsonSerializer.Deserialize<AgentConfig>(jsonContent, options) 
                                  ?? GetDefaultAgentConfig(); // Fallback if deserialization returns null
                    _logger.LogInformation("Agent configuration loaded successfully from {ConfigFileName}.", agentConfigFileName);
                }
                else
                {
                    _logger.LogWarning("Agent configuration file ({ConfigFileName}) not found. Using default configuration.", agentConfigFileName);
                    AgentConfig = GetDefaultAgentConfig();
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error deserializing agent configuration from {ConfigFileName}. Using default configuration.", agentConfigFileName);
                AgentConfig = GetDefaultAgentConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading agent configuration from {ConfigFileName}. Using default configuration.", agentConfigFileName);
                AgentConfig = GetDefaultAgentConfig();
            }
        }

        private AgentConfig GetDefaultAgentConfig() 
        {
            _logger.LogInformation("Providing default agent configuration as per agent_standard.md.");
            // This structure must exactly match agent_standard.md for agent_config.json
            return new AgentConfig
            {
                ServerUrl = "http://localhost:3000/api/agent/", // Default Server URL from standard example
                Agent = new AgentSettings
                {
                    StatusReportIntervalSec = 30,
                    EnableAutoUpdate = true,
                    AutoUpdateIntervalSec = 86400 // 1 day
                },
                HttpClient = new HttpClientSettings
                {
                    RequestTimeoutSec = 15
                },
                Websocket = new WebSocketSettings
                {
                    ReconnectDelayInitialSec = 5,
                    ReconnectDelayMaxSec = 60,
                    ReconnectAttemptsMax = null // unlimited
                },
                CommandExecutor = new CommandExecutorSettings
                {
                    DefaultTimeoutSec = 300, // 5 minutes
                    MaxParallelCommands = 2,
                    MaxQueueSize = 100,
                    ConsoleEncoding = "utf-8" // Standard default
                }
            };
        }

        public string GetServerUrl() => AgentConfig.ServerUrl;

        public string GetAgentVersion() => AgentVersion;

        // Generic Get<T> method to access values from the strongly-typed AgentConfig
        // This simplifies access and ensures type safety.
        // Example: _configManager.Get(c => c.Agent.StatusReportIntervalSec, 30);
        public T Get<T>(Func<AgentConfig, T> valueSelector, T defaultValue = default!)
        {
            try
            {
                return valueSelector(AgentConfig);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error accessing configuration value. Returning default.");
                return defaultValue;
            }
        }
        
        // Specific Get<T> for appsettings.json if needed by other parts of the application (e.g., logging config)
        public T? GetAppSetting<T>(string keyPath, T? defaultValue = default)
        {
            if (_appSettingsConfiguration == null)
            {
                _logger.LogWarning("_appSettingsConfiguration is null, cannot get value for key {KeyPath}. Returning default.", keyPath);
                return defaultValue;
            }
            try
            {
                return _appSettingsConfiguration.GetValue<T>(keyPath, defaultValue!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting app setting value for key {KeyPath}. Returning default value.", keyPath);
                return defaultValue;
            }
        }


        public void SetAgentVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                _logger.LogWarning("Attempted to set empty or null agent version.");
                return;
            }
            AgentVersion = version;
            _logger.LogInformation("Agent version set in ConfigManager to: {AgentVersion}", AgentVersion);
        }
    }
}
