using CMSAgent.CommandHandlers;
using CMSAgent.Configuration;
using CMSAgent.Models; 
using CMSAgent.Monitoring;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json; 
using System;
using System.Collections.Generic;
using System.Net.Http; 
using System.Text.Json; 
using System.Threading.Tasks;

namespace CMSAgent.Communication
{
    public class ServerConnector : IAsyncDisposable
    {
        private readonly ILogger<ServerConnector> _logger;
        private readonly HttpClientService _httpClientService;
        private readonly SocketIOClientWrapper _socketWrapper;
        private readonly StateManager _stateManager;
        private readonly SystemMonitor _systemMonitor;
        private readonly ConfigManager _configManager; 
        private string? _deviceId; 
        private string? _agentToken; 

        public event Func<ExecuteCommandEventPayload, Task>? OnCommandExecuteReceived;
        public event Func<NewVersionAvailablePayload, Task>? OnNewVersionAvailableReceived;

        public event Func<Task>? OnSocketAuthenticated
        {
            add => _socketWrapper.Authenticated += value;
            remove => _socketWrapper.Authenticated -= value;
        }
        public event Func<string?, Task>? OnSocketAuthenticationFailed
        {
            add => _socketWrapper.AuthenticationFailed += value;
            remove => _socketWrapper.AuthenticationFailed -= value;
        }
        public event Func<Task>? OnSocketTransportConnected
        {
            add => _socketWrapper.Connected += value;
            remove => _socketWrapper.Connected -= value;
        }
        public event Func<Task>? OnSocketDisconnected
        {
            add => _socketWrapper.Disconnected += value;
            remove => _socketWrapper.Disconnected -= value;
        }
        public event Func<Exception?, Task>? OnSocketConnectError
        {
            add => _socketWrapper.ConnectError += value;
            remove => _socketWrapper.ConnectError -= value;
        }

        public bool IsWebSocketAuthenticated => _socketWrapper.IsAuthenticated;
        public bool IsWebSocketTransportConnected => _socketWrapper.IsTransportConnected;

        public ServerConnector(
            ILogger<ServerConnector> logger,
            HttpClientService httpClientService,
            SocketIOClientWrapper socketWrapper,
            StateManager stateManager,
            SystemMonitor systemMonitor,
            ConfigManager configManager) 
        {
            _logger = logger;
            _httpClientService = httpClientService;
            _socketWrapper = socketWrapper;
            _stateManager = stateManager;
            _systemMonitor = systemMonitor;
            _configManager = configManager; 

            RegisterServerEventHandlers(); 
        }

        private void RegisterServerEventHandlers()
        {
            _socketWrapper.On("command:execute", async (jsonPayloadElement) => 
            {
                _logger.LogInformation("Received 'command:execute' event from server.");
                try
                {
                    string rawJson = jsonPayloadElement.GetRawText();
                    var payload = JsonConvert.DeserializeObject<ExecuteCommandEventPayload>(rawJson);
                    if (payload != null && OnCommandExecuteReceived != null)
                    {
                        await OnCommandExecuteReceived.Invoke(payload);
                    }
                    else if (payload == null)
                    {
                        _logger.LogWarning("'command:execute' payload deserialized to null using Newtonsoft.Json.");
                    }
                }
                catch (Newtonsoft.Json.JsonException jsonEx) 
                {
                    _logger.LogError(jsonEx, "Error deserializing 'command:execute' payload with Newtonsoft.Json.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing 'command:execute' event.");
                }
            });

            _socketWrapper.On("agent:new_version_available", async (jsonPayloadElement) => 
            {
                _logger.LogInformation("Received 'agent:new_version_available' event from server.");
                try
                {
                    string rawJson = jsonPayloadElement.GetRawText();
                    var payload = JsonConvert.DeserializeObject<NewVersionAvailablePayload>(rawJson);
                    if (payload != null && OnNewVersionAvailableReceived != null)
                    {
                        await OnNewVersionAvailableReceived.Invoke(payload);
                    }
                    else if (payload == null)
                    {
                        _logger.LogWarning("'agent:new_version_available' payload deserialized to null using Newtonsoft.Json.");
                    }
                }
                catch (Newtonsoft.Json.JsonException jsonEx) 
                {
                    _logger.LogError(jsonEx, "Error deserializing 'agent:new_version_available' payload with Newtonsoft.Json.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing 'agent:new_version_available' event.");
                }
            });
        }

        public async Task<bool> InitializeAsync(RoomPosition positionConfig, string? mfaCode = null, bool forceRenewToken = false)
        {
            await _stateManager.EnsureDeviceIdAsync();
            _deviceId = _stateManager.GetDeviceId();

            if (string.IsNullOrEmpty(_deviceId))
            {
                _logger.LogError("Failed to ensure DeviceId. Cannot initialize connection.");
                return false;
            }
            _httpClientService.UpdateAgentIdHeader(_deviceId);

            _agentToken = await _stateManager.LoadTokenAsync();

            if (string.IsNullOrEmpty(_agentToken) || forceRenewToken)
            {
                _logger.LogInformation(forceRenewToken ? "Force renewing token." : "No existing agent_token found. Attempting to identify agent.");
                return await AuthenticateAndConnectAsync(positionConfig, mfaCode); 
            }
            else
            {
                _logger.LogInformation("Existing agent_token loaded. Attempting WebSocket connection.");
                await _socketWrapper.ConnectAsync(_deviceId, _agentToken);
                
                await Task.Delay(1000); 
                if (IsWebSocketAuthenticated) 
                {
                    _logger.LogInformation("Successfully connected and authenticated WebSocket with existing token.");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to establish authenticated WebSocket connection with existing token, or token might be invalid/expired. Re-authenticating via HTTP.");
                    await _stateManager.ClearTokenAsync(); 
                    _agentToken = null;
                    return await AuthenticateAndConnectAsync(positionConfig, mfaCode);
                }
            }
        }

        public async Task<bool> AuthenticateAndConnectAsync(RoomPosition positionConfig, string? mfaCode = null)
        {
            if (string.IsNullOrEmpty(_deviceId)) 
            {
                _logger.LogError("DeviceId is not set. Cannot authenticate.");
                return false;
            }

            try
            {
                var identifyPayload = new IdentifyRequestPayload
                {
                    DeviceId = _deviceId,
                    PositionInfo = new RoomPositionPayload 
                    {
                        RoomName = positionConfig.RoomName,
                        PosX = positionConfig.PosX,
                        PosY = positionConfig.PosY
                    }
                };

                IdentifyResponsePayload? identifyResponse = await _httpClientService.IdentifyAgentAsync(identifyPayload);

                if (identifyResponse == null)
                {
                    _logger.LogError("Identify agent request failed or returned null response.");
                    return false;
                }

                if (identifyResponse.Status == "mfa_required")
                {
                    _logger.LogWarning("MFA is required by server. Message: {Message}", identifyResponse.Message);
                    throw new MfaRequiredException(identifyResponse.Message ?? "MFA code is required.");
                }

                if (identifyResponse.Status == "success" && !string.IsNullOrEmpty(identifyResponse.AgentToken) && !string.IsNullOrEmpty(identifyResponse.AgentId))
                {
                    _agentToken = identifyResponse.AgentToken;
                    if (_deviceId != identifyResponse.AgentId)
                    {
                        _logger.LogWarning("DeviceID from server ({ServerDeviceId}) differs from current ({CurrentDeviceId}). Updating to server's version.", identifyResponse.AgentId, _deviceId);
                        _deviceId = identifyResponse.AgentId; 
                        await _stateManager.SaveDeviceIdAsync(_deviceId); 
                        _httpClientService.UpdateAgentIdHeader(_deviceId); 
                    }
                    
                    await _stateManager.SaveTokenAsync(_agentToken);
                    _logger.LogInformation("Successfully authenticated via HTTP and received new agent_token for DeviceId: {DeviceId}", _deviceId);

                    await _socketWrapper.ConnectAsync(_deviceId, _agentToken); 
                    return true;
                }
                else
                {
                    _logger.LogError("Authentication failed. Status: {Status}, Message: {Message}", identifyResponse.Status, identifyResponse.Message);
                    return false;
                }
            }
            catch (MfaRequiredException) { throw; } 
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error during agent identification/authentication process. Message: {ErrorMessage}", httpEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error during agent identification/authentication process.");
                return false;
            }
        }
        
        public async Task<bool> VerifyMfaAndConnectAsync(string mfaCode)
        {
            if (string.IsNullOrEmpty(_deviceId))
            {
                _logger.LogError("DeviceId is not set. Cannot verify MFA.");
                return false;
            }

            try
            {
                var verifyPayload = new VerifyMfaRequestPayload { DeviceId = _deviceId, MfaCode = mfaCode };
                VerifyMfaResponsePayload? verifyResponse = await _httpClientService.VerifyMfaAsync(verifyPayload);

                if (verifyResponse != null && verifyResponse.Status == "success" && !string.IsNullOrEmpty(verifyResponse.AgentToken) && !string.IsNullOrEmpty(verifyResponse.AgentId))
                {
                    _agentToken = verifyResponse.AgentToken;
                    if (_deviceId != verifyResponse.AgentId)
                    {
                        _logger.LogWarning("DeviceID from MFA verify response ({ServerDeviceId}) differs from current ({CurrentDeviceId}). Updating.", verifyResponse.AgentId, _deviceId);
                        _deviceId = verifyResponse.AgentId;
                        await _stateManager.SaveDeviceIdAsync(_deviceId);
                        _httpClientService.UpdateAgentIdHeader(_deviceId);
                    }

                    await _stateManager.SaveTokenAsync(_agentToken);
                    _logger.LogInformation("MFA verified successfully. New agent_token for DeviceId: {DeviceId} received.", _deviceId);

                    await _socketWrapper.ConnectAsync(_deviceId, _agentToken);
                    return true;
                }
                else
                {
                    _logger.LogError("MFA verification failed. Status: {Status}, Message: {Message}", verifyResponse?.Status, verifyResponse?.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MFA verification process.");
                return false;
            }
        }

        public void RegisterWebSocketHandler(string eventName, Func<JsonElement, Task> callback)
        {
            _logger.LogInformation("Registering WebSocket handler for event: {EventName}", eventName);
            _socketWrapper.On(eventName, callback);
        }

        public async Task SendStatusUpdateAsync(SystemUsageStats internalStats) 
        {
            if (!_socketWrapper.IsAuthenticated || string.IsNullOrEmpty(_deviceId))
            {
                _logger.LogWarning("Cannot send status update: WebSocket not authenticated or DeviceId is missing.");
                return;
            }

            var payload = new AgentStatusUpdatePayload
            {
                AgentId = _deviceId, 
                CpuUsage = internalStats.CpuUsagePercentage, 
                RamUsage = internalStats.RamUsagePercentage, 
                DiskUsage = internalStats.DiskUsagePercentage 
            };

            try
            {
                await _socketWrapper.EmitAsync("agent:status_update", payload);
                _logger.LogDebug("agent:status_update sent: CPU {CpuUsage}%, RAM {RamUsage}%, Disk {DiskUsage}%", payload.CpuUsage, payload.RamUsage, payload.DiskUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending agent:status_update");
            }
        }
        
        public async Task SendCommandExecutionResultAsync(AgentCommandResultPayload payload) 
        {
            if (!_socketWrapper.IsAuthenticated || string.IsNullOrEmpty(_deviceId))
            {
                _logger.LogWarning("Cannot send command result: WebSocket not authenticated or DeviceId is missing.");
                return;
            }

            if (payload.AgentId != _deviceId)
            {
                _logger.LogWarning("Payload.AgentId ({PayloadAgentId}) does not match ServerConnector's _deviceId ({ConnectorDeviceId}). Overriding.", payload.AgentId, _deviceId);
                payload.AgentId = _deviceId; 
            }

            try
            {
                _logger.LogInformation("Sending 'agent:command_result' for CommandId: {CommandId}, Success: {Success}", payload.CommandId, payload.Success);
                await _socketWrapper.EmitAsync("agent:command_result", payload); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending 'agent:command_result' for CommandId: {CommandId}", payload.CommandId);
            }
        }

        public async Task SendInitialHardwareInfoAsync()
        {
            if (string.IsNullOrEmpty(_deviceId) || string.IsNullOrEmpty(_agentToken))
            {
                _logger.LogWarning("Cannot send hardware info, agent not fully authenticated (DeviceId or AgentToken missing).");
                return;
            }
            try
            {
                var internalHwInfo = await _systemMonitor.GetHardwareInfoAsync();
                
                var payload = new HardwareInfoPayload
                {
                    OsInfo = internalHwInfo.OsInfo,
                    CpuInfo = internalHwInfo.CpuInfo,
                    GpuInfo = internalHwInfo.GpuInfo,
                    TotalRamBytes = internalHwInfo.TotalRamBytes,
                    TotalDiskSpaceBytes = internalHwInfo.TotalDiskSpaceBytes,
                    IpAddress = internalHwInfo.IpAddress
                };

                var response = await _httpClientService.SendHardwareInfoAsync(payload); 
                if (response != null && response.Status == "success")
                {
                    _logger.LogInformation("Successfully sent initial hardware information. Server message: {Message}", response.Message);
                }
                else
                {
                    _logger.LogWarning("Sent initial hardware information, but server response indicated an error or was unexpected. Status: {Status}, Message: {Message}", response?.Status, response?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send initial hardware information.");
            }
        }

        public async Task<CheckUpdateResponsePayload?> CheckForUpdateAsync(string currentVersion)
        {
            if (string.IsNullOrEmpty(_deviceId) || string.IsNullOrEmpty(_agentToken))
            {
                _logger.LogWarning("Cannot check for updates, agent not fully authenticated (DeviceId or AgentToken missing).");
                return null;
            }
            try
            {
                return await _httpClientService.CheckForUpdateAsync(currentVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates.");
                return null;
            }
        }

        public async Task ReportCommandResultAsync(string commandId, string commandType, bool success, string stdout, string stderr, int exitCode)
        {
            if (string.IsNullOrEmpty(_deviceId))
            {
                _logger.LogError("Cannot report command result: DeviceId is not set. CommandId: {CommandId}", commandId);
                return;
            }

            var payload = new AgentCommandResultPayload 
            {
                AgentId = _deviceId, 
                CommandId = commandId,
                Success = success,
                Type = commandType, 
                Result = new CommandResultDetailPayload 
                {
                    Stdout = stdout ?? string.Empty, 
                    Stderr = stderr ?? string.Empty,   
                    ExitCode = exitCode
                }
            };
            await SendCommandExecutionResultAsync(payload); 
        }

        public async Task ReportErrorToBackendAsync(string errorType, string errorMessage, Dictionary<string, object?>? errorDetails = null)
        {
            var errorPayload = new ReportErrorRequestPayload
            {
                ErrorType = errorType,
                ErrorMessage = errorMessage, 
                ErrorDetails = errorDetails, 
                Timestamp = DateTime.UtcNow.ToString("o") 
            };
            await _httpClientService.ReportErrorAsync(errorPayload);
        }

        public async ValueTask DisposeAsync()
        {
            await _socketWrapper.DisposeAsync();
            _httpClientService.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class MfaRequiredException : Exception
    {
        public MfaRequiredException(string message) : base(message) { }
    }
}
