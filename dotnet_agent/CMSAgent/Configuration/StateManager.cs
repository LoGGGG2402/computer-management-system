using CMSAgent.Models; // For AgentRuntimeData, RoomPosition
using CMSAgent.Utilities; // For FileUtilities
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CMSAgent.Configuration
{
    public class StateManager
    {
        private readonly ILogger<StateManager> _logger;
        private readonly FileUtilities _fileUtils;
        private readonly string _storagePath; // Root storage path for CMSAgent data
        private readonly string _stateFilePath; // Path to agent_state.json
        private AgentRuntimeData _runtimeState; // Holds deserialized content of agent_state.json

        public StateManager(ILogger<StateManager> logger, FileUtilities fileUtils, ConfigManager configManager)
        {
            _logger = logger;
            _fileUtils = fileUtils;
            
            const string appName = "CMSAgent"; 
            _storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), appName);
            
            try
            {
                Directory.CreateDirectory(_storagePath); // Ensure directory exists
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent storage directory at {StoragePath}. State persistence may fail.", _storagePath);
            }

            _stateFilePath = Path.Combine(_storagePath, "agent_state.json");
            _logger.LogInformation("Agent state file path: {StateFilePath}", _stateFilePath);

            _runtimeState = LoadStateFileAsync().Result ?? new AgentRuntimeData();
            
            if (string.IsNullOrEmpty(_runtimeState.DeviceId))
            {
                InitializeDeviceIdAsync().Wait(); // Synchronously wait for critical init step
            }
        }

        private async Task<AgentRuntimeData?> LoadStateFileAsync()
        {
            if (File.Exists(_stateFilePath))
            {
                try
                {
                    var loadedState = await _fileUtils.LoadJsonFromFileAsync<AgentRuntimeData>(_stateFilePath);
                    if (loadedState != null)
                    {
                        _logger.LogInformation("Agent state loaded successfully from {StateFilePath}. DeviceId: {DeviceId}", _stateFilePath, loadedState.DeviceId);
                    }
                    else
                    {
                        _logger.LogWarning("Agent state file {StateFilePath} loaded as null. A new state will be initialized.", _stateFilePath);
                    }
                    return loadedState;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading agent state from {StateFilePath}. A new state file will be created if necessary.", _stateFilePath);
                    return null;
                }
            }
            _logger.LogInformation("Agent state file {StateFilePath} not found. A new state will be initialized.", _stateFilePath);
            return null;
        }

        private async Task SaveStateFileAsync()
        {
            try
            {
                await _fileUtils.SaveJsonToFileAsync(_runtimeState, _stateFilePath);
                _logger.LogDebug("Agent state saved to {StateFilePath}.", _stateFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving agent state to {StateFilePath}.", _stateFilePath);
            }
        }

        public string? GetDeviceId()
        {
            return _runtimeState.DeviceId;
        }

        public async Task InitializeDeviceIdAsync()
        {
            if (string.IsNullOrEmpty(_runtimeState.DeviceId))
            {
                _runtimeState.DeviceId = Guid.NewGuid().ToString();
                _logger.LogInformation("New Device ID (device_id) generated: {DeviceId}", _runtimeState.DeviceId);
                await SaveStateFileAsync();
            }
        }

        public Task EnsureDeviceIdAsync() => InitializeDeviceIdAsync();

        public Task<string?> LoadTokenAsync()
        {
            return Task.FromResult(_runtimeState.AgentToken);
        }

        public async Task ClearTokenAsync()
        {
            if (_runtimeState.AgentToken != null)
            {
                _runtimeState.AgentToken = null;
                await SaveStateFileAsync();
                _logger.LogInformation("Agent token (agent_token) cleared from state.");
            }
        }

        public async Task SaveTokenAsync(string agentToken) 
        {
            _runtimeState.AgentToken = agentToken;
            await SaveStateFileAsync();
            _logger.LogInformation("Agent token (agent_token) updated in state.");
        }

        public async Task SaveDeviceIdAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.LogWarning("Attempted to save a null or empty Device ID. Operation aborted.");
                return;
            }
            if (_runtimeState.DeviceId != deviceId)
            {
                 _logger.LogInformation("Device ID in state changing from {OldDeviceId} to {NewDeviceId}.", _runtimeState.DeviceId ?? "null", deviceId);
                _runtimeState.DeviceId = deviceId;
                await SaveStateFileAsync();
            }
        }

        public RoomPosition? GetRoomConfig()
        {
            return _runtimeState.RoomConfig;
        }

        public async Task UpdateRoomPositionAsync(RoomPosition roomPosition)
        {
            if (roomPosition == null || string.IsNullOrEmpty(roomPosition.RoomName))
            {
                _logger.LogWarning("Attempted to update room position with null or invalid data.");
                return;
            }
            _runtimeState.RoomConfig = roomPosition;
            await SaveStateFileAsync();
            _logger.LogInformation("Room position updated in state: Room='{RoomName}', X='{PosX}', Y='{PosY}'", 
                roomPosition.RoomName, roomPosition.PosX, roomPosition.PosY);
        }
        
        public string? GetAuthToken()
        {
            return _runtimeState.AgentToken;
        }

        public async Task SetAuthTokenAsync(string agentToken)
        {
            await SaveTokenAsync(agentToken);
        }
    }
}
