using CMSAgent.Utilities;
using CMSAgent.Configuration;
using Serilog;
using System.Management;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CMSAgent.Configuration
{
    /// <summary>
    /// Manages the runtime state of the agent
    /// </summary>
    public class RuntimeStateManager
    {
        private readonly string _runtimeConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CMSAgent", "runtime_config");
        private readonly string _runtimeConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CMSAgent", "runtime_config", "runtime_config.json");
        private RuntimeConfigPoco _currentConfig = new RuntimeConfigPoco();
        private string _deviceId = string.Empty;
        private static readonly object _lockObject = new();

        /// <summary>
        /// The default runtime configuration filename
        /// </summary>
        public const string RuntimeConfigFileName = "runtime_config.json";

        /// <summary>
        /// Gets the unique device ID for this agent
        /// </summary>
        public string DeviceId
        {
            get
            {
                // Only generate the device ID once per instance
                if (string.IsNullOrEmpty(_deviceId))
                {
                    lock (_lockObject)
                    {
                        if (string.IsNullOrEmpty(_deviceId))
                        {
                            _deviceId = GetOrCreateDeviceId();
                        }
                    }
                }
                return _deviceId;
            }
        }

        /// <summary>
        /// Initializes a new instance of the RuntimeStateManager class
        /// </summary>
        public RuntimeStateManager()
        {
            _currentConfig = LoadConfig();
        }

        /// <summary>
        /// Initializes the runtime state manager
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Load the runtime configuration
                _currentConfig = LoadConfig();

                // Ensure device_id is populated
                if (string.IsNullOrEmpty(_currentConfig.DeviceId))
                {
                    _currentConfig.DeviceId = DeviceId;
                    SaveConfig(_currentConfig);
                }

                Log.Information("Runtime state manager initialized. Device ID: {DeviceId}", DeviceId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing runtime state manager: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Loads the runtime configuration
        /// </summary>
        public RuntimeConfigPoco LoadConfig()
        {
            Log.Information("Attempting to load runtime configuration from {FilePath}", _runtimeConfigFile);
            if (!File.Exists(_runtimeConfigFile))
            {
                Log.Warning("Runtime configuration file not found at {FilePath}. Returning default/empty config.", _runtimeConfigFile);
                _currentConfig = new RuntimeConfigPoco(); // Return empty config if file does not exist
                return _currentConfig;
            }

            try
            {
                var json = File.ReadAllText(_runtimeConfigFile);
                var config = JsonSerializer.Deserialize<RuntimeConfigPoco>(json);
                if (config != null)
                {
                    _currentConfig = config;
                    Log.Information("Successfully loaded runtime configuration.");
                    // Log DeviceId and RoomConfig values for verification
                    Log.Debug("Device ID from runtime_config: {DeviceId}", _currentConfig.DeviceId);
                    if (_currentConfig.RoomConfig != null)
                    {
                        Log.Debug("RoomConfig from runtime_config: RoomName={RoomName}, PosX={PosX}, PosY={PosY}",
                                  _currentConfig.RoomConfig.RoomName, _currentConfig.RoomConfig.PosX, _currentConfig.RoomConfig.PosY);
                    }
                    else
                    {
                        Log.Debug("RoomConfig is null in runtime_config.");
                    }
                }
                else
                {
                    Log.Warning("Failed to deserialize runtime configuration. Returning default/empty config.");
                    _currentConfig = new RuntimeConfigPoco();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading runtime configuration: {Message}", ex.Message);
                _currentConfig = new RuntimeConfigPoco();
            }

            return _currentConfig;
        }

        /// <summary>
        /// Saves the runtime configuration
        /// </summary>
        public void SaveConfig(RuntimeConfigPoco config)
        {
            if (config == null)
            {
                Log.Warning("Attempted to save a null runtime configuration. Operation aborted.");
                return;
            }

            try
            {
                Log.Information("Attempting to save runtime configuration to {FilePath}", _runtimeConfigFile);
                Directory.CreateDirectory(_runtimeConfigDir); // Ensure directory exists
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(_runtimeConfigFile, json);
                _currentConfig = config; // Update current config in memory
                Log.Information("Runtime configuration saved successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving runtime configuration: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the current version of the application
        /// </summary>
        public string GetCurrentVersion()
        {
            try
            {
                // Get the version from the assembly
                Version? version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    return version.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting current version: {Message}", ex.Message);
            }

            // Default version
            return "1.0.0";
        }

        /// <summary>
        /// Updates the room configuration
        /// </summary>
        public void SetRoomConfig(RoomConfig roomConfig)
        {
            _currentConfig.RoomConfig = roomConfig;
            SaveConfig(_currentConfig);
        }

        public RoomConfig? GetRoomConfig()
        {
            return _currentConfig.RoomConfig;
        }

        /// <summary>
        /// Gets the decrypted agent token
        /// </summary>
        public string GetEncryptedAgentToken()
        {
            return _currentConfig.AgentTokenEncrypted ?? string.Empty;
        }

        /// <summary>
        /// Saves the agent token (encrypts it first)
        /// </summary>
        public void SaveAgentToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Log.Warning("Attempted to save empty agent token");
                return;
            }

            try
            {
                _currentConfig.AgentTokenEncrypted = SystemOperations.CryptoHelper.EncryptAgentToken(token);
                SaveConfig(_currentConfig);
                Log.Information("Agent token saved successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving agent token");
                throw;
            }
        }

        /// <summary>
        /// Gets or creates a device ID for this agent
        /// </summary>
        private string GetOrCreateDeviceId()
        {
            try
            {
                string savedDeviceId = LoadDeviceId();
                if (!string.IsNullOrEmpty(savedDeviceId))
                {
                    return savedDeviceId;
                }

                // Generate a new device ID
                string newDeviceId = GenerateDeviceId();
                SaveDeviceId(newDeviceId);

                return newDeviceId;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting or creating device ID: {Message}", ex.Message);

                // Generate a fallback device ID
                string fallbackId = $"FALLBACK-{Environment.MachineName}-{Guid.NewGuid():N}";
                return fallbackId;
            }
        }

        /// <summary>
        /// Loads the device ID from the device ID file
        /// </summary>
        private string LoadDeviceId()
        {
            string deviceIdPath = Path.Combine(DirectoryUtils.GetAppDataDirectory(), "device_id.txt");
            if (File.Exists(deviceIdPath))
            {
                return File.ReadAllText(deviceIdPath).Trim();
            }
            return string.Empty;
        }

        /// <summary>
        /// Saves the device ID to the device ID file
        /// </summary>
        private void SaveDeviceId(string deviceId)
        {
            string deviceIdPath = Path.Combine(DirectoryUtils.GetAppDataDirectory(), "device_id.txt");
            File.WriteAllText(deviceIdPath, deviceId);
        }

        /// <summary>
        /// Generates a unique device ID based on hardware information
        /// </summary>
        private string GenerateDeviceId()
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                // Try to get the processor ID
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                    {
                        sb.Append(obj["ProcessorId"]?.ToString() ?? "");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting processor ID: {Message}", ex.Message);
            }

            try
            {
                // Try to get the motherboard serial number
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                    {
                        sb.Append(obj["SerialNumber"]?.ToString() ?? "");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting motherboard serial number: {Message}", ex.Message);
            }

            // Add machine name
            sb.Append(Environment.MachineName);

            // If we have no hardware info, add a random GUID
            if (sb.Length < 10)
            {
                sb.Append(Guid.NewGuid().ToString("N"));
            }

            // Generate a hash of the hardware info
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                byte[] hash = md5.ComputeHash(bytes);

                // Format the hash as a hex string
                return "CMS-" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}