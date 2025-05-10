using CMSAgent.Models;
using CMSAgent.Utilities;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging; // Added ILogger using
using System.IO; // Added System.IO using

namespace CMSAgent.Configuration
{
    public class DataManager
    {
        private readonly ILogger<DataManager> _logger;
        private readonly FileUtilities _fileUtils;
        private readonly string _storagePath;
        private readonly string _stateFilePath;
        private readonly string _tokenFilePath; 
        private AgentRuntimeData _runtimeState;

        // Used for DPAPI encryption
        private static readonly byte[] s_additionalEntropy = { 0x4c, 0x6f, 0x6e, 0x67, 0x50, 0x50, 0x48 }; // "LongPPH"
        private const string ApplicationName = "CMSAgent"; // Added constant for ApplicationName

        public DataManager(ILogger<DataManager> logger, FileUtilities fileUtils, ConfigManager configManager)
        {
            _logger = logger;
            _fileUtils = fileUtils;
            // Use the hardcoded ApplicationName constant
            _storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ApplicationName);
            Directory.CreateDirectory(_storagePath); // Ensure it exists

            _stateFilePath = Path.Combine(_storagePath, "agent_state.json");
            _tokenFilePath = Path.Combine(_storagePath, "auth.token");

            _runtimeState = LoadStateFileAsync().Result ?? new AgentRuntimeData();
        }

        private async Task<AgentRuntimeData?> LoadStateFileAsync()
        {
            if (File.Exists(_stateFilePath))
            {
                try
                {
                    return await _fileUtils.LoadJsonFromFileAsync<AgentRuntimeData>(_stateFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading agent state from {StateFilePath}. A new state file will be created if necessary.", _stateFilePath);
                    return null;
                }
            }
            return null;
        }

        private async Task SaveStateFileAsync()
        {
            try
            {
                await _fileUtils.SaveJsonToFileAsync(_runtimeState, _stateFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving agent state to {StateFilePath}.", _stateFilePath);
            }
        }

        public async Task<string> EnsureDeviceIdAsync()
        {
            if (string.IsNullOrWhiteSpace(_runtimeState.DeviceId))
            {
                _runtimeState.DeviceId = Guid.NewGuid().ToString();
                await SaveStateFileAsync();
                _logger.LogInformation("New DeviceId generated and saved: {DeviceId}", _runtimeState.DeviceId);
            }
            return _runtimeState.DeviceId;
        }

        public async Task SaveRoomConfigAsync(RoomPosition roomConfig)
        {
            _runtimeState.RoomConfig = roomConfig;
            await SaveStateFileAsync();
            _logger.LogInformation("Room configuration saved: {RoomName}, X={PosX}, Y={PosY}", roomConfig.RoomName, roomConfig.PosX, roomConfig.PosY);
        }

        public RoomPosition? GetRoomConfig()
        {
            return _runtimeState.RoomConfig;
        }

        public async Task SaveTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                if (File.Exists(_tokenFilePath))
                {
                    File.Delete(_tokenFilePath);
                    _logger.LogInformation("Auth token cleared.");
                }
                return;
            }

            try
            {
                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                byte[] encryptedTokenBytes = ProtectedData.Protect(tokenBytes, s_additionalEntropy, DataProtectionScope.LocalMachine);
                await File.WriteAllBytesAsync(_tokenFilePath, encryptedTokenBytes);
                _logger.LogInformation("Auth token saved securely.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save auth token securely.");
            }
        }

        public async Task<string?> LoadTokenAsync()
        {
            if (!File.Exists(_tokenFilePath))
            {
                return null;
            }

            try
            {
                byte[] encryptedTokenBytes = await File.ReadAllBytesAsync(_tokenFilePath);
                byte[] tokenBytes = ProtectedData.Unprotect(encryptedTokenBytes, s_additionalEntropy, DataProtectionScope.LocalMachine);
                string token = Encoding.UTF8.GetString(tokenBytes);
                _logger.LogInformation("Auth token loaded successfully.");
                return token;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt auth token. The token might be corrupted or from a different machine/user context. Clearing token.");
                // If decryption fails, the token is likely invalid or corrupted. Delete it.
                File.Delete(_tokenFilePath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load auth token.");
                return null;
            }
        }
    }
}
