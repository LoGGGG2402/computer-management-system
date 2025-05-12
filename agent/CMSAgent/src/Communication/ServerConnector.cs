using CMSAgent.Configuration;
using CMSAgent.Models.Payloads;
using CMSAgent.SystemOperations;
using Serilog;
using System;
using System.IO; // Required for Path, Directory, File
using System.Net; // Required for HttpStatusCode
using System.Text.Json; // Required for JsonSerializer
using System.Threading.Tasks;

namespace CMSAgent.Communication
{
    public class ServerConnector : IServerConnector
    {
        private readonly StaticConfigProvider _configProvider;
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly string _errorReportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CMSAgent", "error_reports");

        public IHttpChannel HttpChannel { get; private set; }
        public IWebSocketChannel WebSocketChannel { get; private set; }

        private string? _agentToken;
        private DateTime _lastConnectAttempt = DateTime.MinValue;
        private int _reconnectAttempts = 0;
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        public bool IsConnected => WebSocketChannel?.IsConnected ?? false;

        public ServerConnector(
            StaticConfigProvider configProvider,
            RuntimeStateManager runtimeStateManager)
        {
            _configProvider = configProvider;
            _runtimeStateManager = runtimeStateManager;

            HttpChannel = new HttpChannel(
                _configProvider.Config.server_url,
                _configProvider.Config.http_client_settings.request_timeout_sec);

            WebSocketChannel = new WebSocketChannel(
                _configProvider.Config.server_url,
                _configProvider.Config.websocket_settings);

            if (WebSocketChannel is WebSocketChannel concreteWebSocketChannel)
            {
                concreteWebSocketChannel.OnInternalConnected += HandleWebSocketConnected;
                concreteWebSocketChannel.OnInternalDisconnected += HandleWebSocketDisconnected;
                concreteWebSocketChannel.OnInternalReconnecting += HandleWebSocketReconnecting;
                concreteWebSocketChannel.OnInternalReconnectFailed += HandleWebSocketReconnectFailed;
                concreteWebSocketChannel.OnInternalMessage += HandleWebSocketMessage;
            }
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                Log.Information("Initializing ServerConnector...");
                var runtimeConfig = await _runtimeStateManager.GetRuntimeConfigAsync();
                if (!string.IsNullOrEmpty(runtimeConfig.agent_token_encrypted))
                {
                    try
                    {
                        _agentToken = CryptoHelper.DecryptAgentToken(runtimeConfig.agent_token_encrypted);
                        HttpChannel.SetAuthHeader("Bearer", _agentToken);
                        Log.Information("Agent token loaded and HTTP auth header set.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to decrypt agent token: {Message}", ex.Message);
                        _agentToken = null;
                        HttpChannel.ClearAuthHeader();
                    }
                }
                await WebSocketChannel.InitializeAsync();
                _isInitialized = true;
                Log.Information("ServerConnector initialized successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing ServerConnector: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<bool> ConnectAsync()
        {
            if (!_isInitialized)
            {
                Log.Error("Cannot connect: ServerConnector is not initialized.");
                return false;
            }

            if (WebSocketChannel.IsConnected)
            {
                Log.Information("Already connected via WebSocket.");
                return true;
            }

            if ((DateTime.UtcNow - _lastConnectAttempt).TotalSeconds < _configProvider.Config.websocket_settings.reconnect_delay_initial_sec)
            {
                Log.Debug("Throttling connection attempt.");
                return false;
            }
            _lastConnectAttempt = DateTime.UtcNow;

            try
            {
                if (string.IsNullOrEmpty(_agentToken))
                {
                    Log.Information("No agent token. Attempting to identify with server...");
                    bool identified = await InternalTryIdentifyWithServerAsync(forceRenewToken: false);
                    if (!identified)
                    {
                        Log.Error("Failed to identify with server. Cannot connect WebSocket.");
                        return false;
                    }
                }
                else
                {
                    HttpChannel.SetAuthHeader("Bearer", _agentToken);
                    if (HttpChannel is HttpChannel concreteHttpChannel)
                    {
                        concreteHttpChannel.AddRequestHeader("X-Agent-Id", _runtimeStateManager.DeviceId);
                    }
                }

                Log.Information("Attempting to connect WebSocket...");
                bool wsConnected = await WebSocketChannel.ConnectAsync(_runtimeStateManager.DeviceId, _agentToken);

                if (wsConnected)
                {
                    Log.Information("Successfully connected via WebSocket.");
                    _reconnectAttempts = 0;
                    return true;
                }
                else
                {
                    Log.Warning("Failed to connect WebSocket after identification attempt.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during ConnectAsync: {Message}", ex.Message);
                return false;
            }
        }

        private async Task<bool> InternalTryIdentifyWithServerAsync(bool forceRenewToken)
        {
            var positionInfoPayload = new PositionInfoPayload();
            var roomConfig = _runtimeStateManager.GetRoomConfig();

            if (roomConfig != null)
            {
                positionInfoPayload.RoomName = roomConfig.RoomName;
                if (int.TryParse(roomConfig.PosX, out int posX) && int.TryParse(roomConfig.PosY, out int posY))
                {
                    positionInfoPayload.PosX = posX;
                    positionInfoPayload.PosY = posY;
                }
                else
                {
                    Log.Error("Failed to parse PosX or PosY from runtime config. RoomName: {RoomName}, PosX: '{PosX}', PosY: '{PosY}'", roomConfig.RoomName, roomConfig.PosX, roomConfig.PosY);
                }
            }
            else
            {
                Log.Warning("Room configuration is not available for identification.");
            }

            var identifyPayload = new IdentifyRequestPayload
            {
                UniqueAgentId = _runtimeStateManager.DeviceId,
                PositionInfo = positionInfoPayload,
                ForceRenewToken = forceRenewToken
            };

            Log.Information("Attempting to identify agent with server. Device ID: {DeviceId}, ForceRenew: {ForceRenew}", identifyPayload.UniqueAgentId, forceRenewToken);

            IdentifyResponse? identifyResponse = null;
            if (HttpChannel is HttpChannel concreteHttpChannel)
            {
                var responseMessage = await concreteHttpChannel.SendJsonAsync(
                    HttpMethod.Post,
                    ApiEndpoints.Identify,
                    identifyPayload,
                    null,
                    _runtimeStateManager.DeviceId);

                if (responseMessage.IsSuccessStatusCode)
                {
                    try
                    {
                        string responseContent = await responseMessage.Content.ReadAsStringAsync();
                        identifyResponse = JsonSerializer.Deserialize<IdentifyResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        Log.Information("Identify response received: Status - {Status}, Message - {Message}", identifyResponse?.status, identifyResponse?.message);
                    }
                    catch (JsonException jsonEx)
                    {
                        Log.Error(jsonEx, "Failed to deserialize identify response.");
                        return false;
                    }
                }
                else
                {
                    string errorContent = await responseMessage.Content.ReadAsStringAsync();
                    Log.Error("Identify request failed. Status: {StatusCode}, Response: {ErrorContent}", responseMessage.StatusCode, errorContent);
                    return false;
                }
            }
            else
            {
                Log.Error("HttpChannel is not of type HttpChannel, cannot send identify request.");
                return false;
            }

            if (identifyResponse?.status == "success")
            {
                if (!string.IsNullOrEmpty(identifyResponse.agentToken))
                {
                    Log.Information("New agent token received from server.");
                    _agentToken = identifyResponse.agentToken;
                    _runtimeStateManager.SetEncryptedAgentToken(CryptoHelper.EncryptAgentToken(_agentToken));
                    _runtimeStateManager.SaveConfig(_runtimeStateManager.LoadConfig());
                }
                else
                {
                    Log.Information("Agent already identified, no new token provided. Using existing token.");
                    if (string.IsNullOrEmpty(_agentToken))
                    {
                        _agentToken = CryptoHelper.DecryptAgentToken(_runtimeStateManager.GetEncryptedAgentToken());
                    }
                }
                if (HttpChannel is HttpChannel httpChannelConcrete)
                {
                    httpChannelConcrete.UpdateAgentToken(_agentToken);
                }
                return true;
            }
            else if (identifyResponse?.status == "mfa_required")
            {
                Log.Warning("MFA is required for agent identification. This agent cannot complete MFA.");
                return false;
            }
            else
            {
                Log.Error("Failed to identify with server. Status: {Status}, Message: {Message}",
                    identifyResponse?.status ?? "N/A", identifyResponse?.message ?? "No response/error message.");
                _agentToken = null;
                HttpChannel.ClearAuthHeader();
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            Log.Information("Disconnecting ServerConnector...");
            await WebSocketChannel.DisconnectAsync();
            Log.Information("ServerConnector disconnected.");
        }

        public async Task<bool> SendHardwareInfoAsync(HardwareInfoPayload hardwareInfo)
        {
            if (!_isInitialized || string.IsNullOrEmpty(_agentToken))
            {
                Log.Warning("Cannot send hardware info: Not initialized or no agent token.");
                return false;
            }
            if (HttpChannel is HttpChannel concreteHttpChannel)
            {
                concreteHttpChannel.AddRequestHeader("X-Agent-Id", _runtimeStateManager.DeviceId);
            }
            bool success = await HttpChannel.SendHardwareInfoAsync(hardwareInfo);
            return success;
        }

        public async Task<bool> SendErrorReportAsync(ErrorReportPayload errorReport)
        {
            if (!_isInitialized || string.IsNullOrEmpty(_agentToken))
            {
                Log.Warning("ServerConnector not initialized or agent token is missing. Cannot send error report.");
                await SaveErrorReportLocallyAsync(errorReport);
                return false;
            }

            if (HttpChannel is HttpChannel concreteHttpChannel)
            {
                var response = await concreteHttpChannel.SendJsonAsync(
                    HttpMethod.Post,
                    ApiEndpoints.ReportError,
                    errorReport,
                    _agentToken,
                    _runtimeStateManager.DeviceId);

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Error report sent successfully: {ErrorType}", errorReport.ErrorType);
                    return true;
                }
                else
                {
                    Log.Error("Failed to send error report. Status: {StatusCode}, Response: {ResponseContent}. Saving locally.", response.StatusCode, await response.Content.ReadAsStringAsync());
                    await SaveErrorReportLocallyAsync(errorReport);
                    return false;
                }
            }
            else
            {
                Log.Error("HttpChannel is not of type HttpChannel, cannot send error report. Saving locally.");
                await SaveErrorReportLocallyAsync(errorReport);
                return false;
            }
        }

        private async Task SaveErrorReportLocallyAsync(ErrorReportPayload errorReport)
        {
            try
            {
                Directory.CreateDirectory(_errorReportsDir);
                string fileName = $"error_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid()}.json";
                string filePath = Path.Combine(_errorReportsDir, fileName);
                string jsonContent = JsonSerializer.Serialize(errorReport, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, jsonContent);
                Log.Information("Error report saved locally to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save error report locally.");
            }
        }

        #region WebSocket Event Handlers

        private void HandleWebSocketConnected()
        {
            Log.Information("ServerConnector: WebSocket connected event received.");
            _reconnectAttempts = 0;
        }

        private void HandleWebSocketDisconnected(string reason)
        {
            Log.Warning("ServerConnector: WebSocket disconnected event received. Reason: {Reason}", reason);
        }

        private void HandleWebSocketReconnecting(int attemptCount)
        {
            Log.Information("ServerConnector: WebSocket reconnecting event (attempt {Attempt})...", attemptCount);
            _reconnectAttempts = attemptCount;
        }

        private void HandleWebSocketReconnectFailed()
        {
            Log.Error("ServerConnector: WebSocket reconnection failed event received.");
        }

        private async void HandleWebSocketMessage(string eventName, string data)
        {
            Log.Debug("ServerConnector: WebSocket message received. Event: {EventName}", eventName);
            switch (eventName)
            {
                case "agent:ws_auth_success":
                    Log.Information("ServerConnector: WebSocket authentication successful.");
                    break;
                case "agent:ws_auth_failed":
                    Log.Error("ServerConnector: WebSocket authentication failed. Data: {Data}", data);
                    _agentToken = null;
                    HttpChannel.ClearAuthHeader();
                    Log.Information("Cleared agent token due to WebSocket auth failure. Attempting immediate re-identification and WebSocket reconnect.");
                    _ = Task.Run(async () => await ProcessWebSocketAuthFailureAsync());
                    break;
            }
        }

        private async Task ProcessWebSocketAuthFailureAsync()
        {
            Log.Information("Attempting to re-identify with server after WebSocket auth failure...");
            bool identified = await InternalTryIdentifyWithServerAsync(forceRenewToken: false);

            if (identified)
            {
                Log.Information("Re-identification successful. Attempting to reconnect WebSocket with new/confirmed token.");
                if (string.IsNullOrEmpty(_agentToken) || string.IsNullOrEmpty(_runtimeStateManager.DeviceId))
                {
                    Log.Error("Cannot reconnect WebSocket: Agent token or Device ID is missing after re-identification.");
                    return;
                }
                bool wsReconnected = await WebSocketChannel.ConnectAsync(_runtimeStateManager.DeviceId, _agentToken);
                if (wsReconnected)
                {
                    Log.Information("Successfully reconnected WebSocket after re-identification.");
                }
                else
                {
                    Log.Error("Failed to reconnect WebSocket after re-identification.");
                }
            }
            else
            {
                Log.Error("Re-identification failed after WebSocket auth failure. Agent will attempt to connect on the next scheduled cycle.");
            }
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                Log.Information("Disposing ServerConnector...");
                if (WebSocketChannel is WebSocketChannel concreteWebSocketChannel)
                {
                    concreteWebSocketChannel.OnInternalConnected -= HandleWebSocketConnected;
                    concreteWebSocketChannel.OnInternalDisconnected -= HandleWebSocketDisconnected;
                    concreteWebSocketChannel.OnInternalReconnecting -= HandleWebSocketReconnecting;
                    concreteWebSocketChannel.OnInternalReconnectFailed -= HandleWebSocketReconnectFailed;
                    concreteWebSocketChannel.OnInternalMessage -= HandleWebSocketMessage;
                }

                HttpChannel?.Dispose();
                WebSocketChannel?.Dispose();
                Log.Information("ServerConnector disposed.");
            }
            _isDisposed = true;
        }
    }
}