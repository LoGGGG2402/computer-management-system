using CMSAgent.Utilities;
using Serilog;
using System.Reflection;

namespace CMSAgent.Configuration
{
    /// <summary>
    /// Provides access to the static agent configuration
    /// </summary>
    public class StaticConfigProvider
    {
        private readonly string _configFilePath;
        private AgentConfigPoco _config;
        
        /// <summary>
        /// The default configuration filename
        /// </summary>
        public const string ConfigFileName = "agent_config.json";

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        public AgentConfigPoco Config => _config;

        /// <summary>
        /// Initializes a new instance of the StaticConfigProvider class
        /// </summary>
        public StaticConfigProvider(string? customConfigPath = null)
        {
            _configFilePath = GetConfigFilePath(customConfigPath);
            _config = LoadConfigOrDefault();
        }

        /// <summary>
        /// Initializes the configuration provider
        /// </summary>
        public async Task InitializeAsync()
        {
            // Ensure we have the latest configuration
            _config = await LoadConfigAsync();
        }

        /// <summary>
        /// Loads the configuration from the config file
        /// </summary>
        public async Task<AgentConfigPoco> LoadConfigAsync()
        {
            try
            {
                Log.Debug("Loading configuration from: {ConfigPath}", _configFilePath);

                // Check if the config file exists
                if (!File.Exists(_configFilePath))
                {
                    Log.Warning("Configuration file not found: {ConfigPath}", _configFilePath);
                    
                    // Create the default configuration and save it
                    var defaultConfig = GetDefaultConfig();
                    await SaveConfigAsync(defaultConfig);
                    
                    return defaultConfig;
                }

                // Read the config file
                var config = await FileUtils.ReadJsonObjectFromFileAsync<AgentConfigPoco>(_configFilePath);
                if (config == null)
                {
                    Log.Warning("Failed to parse configuration file, using default configuration");
                    config = GetDefaultConfig();
                    await SaveConfigAsync(config);
                }

                // Set the current configuration
                _config = config;

                Log.Information("Configuration loaded successfully from: {ConfigPath}", _configFilePath);
                return config;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading configuration: {Message}", ex.Message);
                
                // Return default configuration
                return GetDefaultConfig();
            }
        }

        /// <summary>
        /// Saves the configuration to the config file
        /// </summary>
        public async Task SaveConfigAsync(AgentConfigPoco config)
        {
            try
            {
                Log.Debug("Saving configuration to: {ConfigPath}", _configFilePath);

                // Ensure the directory exists
                string? directory = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save the config file
                await FileUtils.WriteJsonObjectToFileAsync(_configFilePath, config);

                // Update the current configuration
                _config = config;

                Log.Information("Configuration saved successfully to: {ConfigPath}", _configFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving configuration: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the default configuration
        /// </summary>
        private AgentConfigPoco GetDefaultConfig()
        {
            return new AgentConfigPoco
            {
                // Default values are already set in the class definition
            };
        }

        /// <summary>
        /// Loads the configuration from the config file or returns the default configuration
        /// </summary>
        private AgentConfigPoco LoadConfigOrDefault()
        {
            try
            {
                // Check if the config file exists
                if (!File.Exists(_configFilePath))
                {
                    Log.Warning("Configuration file not found: {ConfigPath}", _configFilePath);
                    return GetDefaultConfig();
                }

                // Read the config file
                string json = File.ReadAllText(_configFilePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<AgentConfigPoco>(json);
                
                if (config == null)
                {
                    Log.Warning("Failed to parse configuration file, using default configuration");
                    return GetDefaultConfig();
                }

                Log.Debug("Configuration loaded successfully from: {ConfigPath}", _configFilePath);
                return config;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading configuration: {Message}", ex.Message);
                return GetDefaultConfig();
            }
        }

        /// <summary>
        /// Gets the configuration file path
        /// </summary>
        private string GetConfigFilePath(string? customConfigPath)
        {
            // If a custom path is provided, use it
            if (!string.IsNullOrEmpty(customConfigPath))
            {
                return customConfigPath;
            }

            // First, check in the application directory
            string executablePath = Assembly.GetExecutingAssembly().Location;
            string executableDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
            string applicationConfigPath = Path.Combine(executableDirectory, ConfigFileName);
            
            if (File.Exists(applicationConfigPath))
            {
                return applicationConfigPath;
            }

            // Next, check in the app data directory
            string appDataDirectory = DirectoryUtils.GetAppDataDirectory();
            string appDataConfigPath = Path.Combine(appDataDirectory, ConfigFileName);
            
            if (File.Exists(appDataConfigPath))
            {
                return appDataConfigPath;
            }

            // Finally, check in the resource directory
            string resourcesDirectory = Path.Combine(executableDirectory, "Resources");
            string resourcesConfigPath = Path.Combine(resourcesDirectory, ConfigFileName);
            
            if (File.Exists(resourcesConfigPath))
            {
                return resourcesConfigPath;
            }

            // If all else fails, use the app data directory
            return appDataConfigPath;
        }
    }
}