using CMSAgent.Configuration;
using CMSAgent.Core;
using CMSAgent.Models.Payloads;
using CMSAgent.Monitoring;
using Serilog;
using System.Diagnostics;
using System.Text.Json; // Required for System.Text.Json.JsonSerializer
using System.IO; // Required for Path, Directory

namespace CMSAgent.CommandHandlers
{
    public class SystemCommandHandler : ISystemCommandHandler
    {
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly StaticConfigProvider _configProvider;
        private readonly ISystemMonitor _systemMonitor;
        private readonly IUpdateHandler _updateHandler; // Changed from UpdateHandler to IUpdateHandler
        private readonly ICoreAgent _coreAgent;

        public SystemCommandHandler(
            RuntimeStateManager runtimeStateManager,
            StaticConfigProvider configProvider,
            ISystemMonitor systemMonitor,
            IUpdateHandler updateHandler, // Changed from UpdateHandler to IUpdateHandler
            ICoreAgent coreAgent)
        {
            _runtimeStateManager = runtimeStateManager;
            _configProvider = configProvider;
            _systemMonitor = systemMonitor;
            _updateHandler = updateHandler;
            _coreAgent = coreAgent;
        }

        public Task InitializeAsync()
        {
            Log.Information("System command handler initialized");
            return Task.CompletedTask;
        }

        public async Task<CommandResult> HandleCommandAsync(CommandRequest commandRequest)
        {
            string agentId = _runtimeStateManager.DeviceId ?? "unknown_agent";
            Log.Information("Handling system command ID: {CommandId}, Type: {CommandType}, AgentID: {AgentId}", 
                commandRequest.commandId, commandRequest.commandType, agentId);

            switch (commandRequest.commandType.ToLowerInvariant())
            {
                case "system_restart":
                    Log.Information("Handling system_restart command ID: {CommandId}", commandRequest.commandId);
                    return await RestartAgentAsync(commandRequest, agentId);

                case "system_shutdown":
                    Log.Information("Handling system_shutdown command ID: {CommandId}", commandRequest.commandId);
                    return await ShutdownAgentAsync(commandRequest, agentId);

                case "system_update":
                    Log.Information("Handling system_update command ID: {CommandId}", commandRequest.commandId);
                    return await UpdateAgentAsync(commandRequest, agentId);

                case "system_info":
                    Log.Information("Handling system_info command ID: {CommandId}", commandRequest.commandId);
                    return await GetSystemInfoAsync(commandRequest, agentId);

                default:
                    Log.Warning("Received unknown system command type: {CommandType} for ID: {CommandId}", commandRequest.commandType, commandRequest.commandId);
                    return CommandResult.CreateFailureResult(
                        agentId, 
                        commandRequest.commandId, 
                        commandRequest.commandType, 
                        $"Unknown system command type: '{commandRequest.commandType}'", 
                        -1);
            }
        }

        private async Task<CommandResult> RestartAgentAsync(CommandRequest commandRequest, string agentId)
        {
            Log.Information("Initiating agent restart for command ID: {CommandId}", commandRequest.commandId);
            try
            {
                await _coreAgent.StopAsync(true); // true for restart hint, if StopAsync supports it
                return CommandResult.CreateSuccessResult(agentId, commandRequest.commandId, commandRequest.commandType, "Agent shutdown for restart initiated. External mechanism should restart the agent.", 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initiate agent restart for command ID: {CommandId}", commandRequest.commandId);
                return CommandResult.CreateSystemFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, $"Failed to initiate restart: {ex.Message}");
            }
        }

        private async Task<CommandResult> ShutdownAgentAsync(CommandRequest commandRequest, string agentId)
        {
            Log.Information("Initiating agent shutdown for command ID: {CommandId}", commandRequest.commandId);
            try
            {
                await _coreAgent.StopAsync(false); // false for permanent stop
                return CommandResult.CreateSuccessResult(agentId, commandRequest.commandId, commandRequest.commandType, "Agent shutdown initiated.", 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initiate agent shutdown for command ID: {CommandId}", commandRequest.commandId);
                return CommandResult.CreateSystemFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, $"Failed to initiate shutdown: {ex.Message}");
            }
        }

        private async Task<CommandResult> UpdateAgentAsync(CommandRequest commandRequest, string agentId)
        {
            try
            {
                Log.Information("Checking for agent updates for command ID: {CommandId}...", commandRequest.commandId);
                var (updateAvailable, availableVersion, downloadUrl) = await _updateHandler.CheckForUpdateAsync();

                if (!updateAvailable || string.IsNullOrEmpty(availableVersion) || string.IsNullOrEmpty(downloadUrl))
                {
                    Log.Information("No update available or update details missing. Agent is up-to-date.");
                    return CommandResult.CreateSuccessResult(agentId, commandRequest.commandId, commandRequest.commandType, "Agent is already up-to-date or update details are unavailable.", 0);
                }

                Log.Information("Update available: Version {AvailableVersion}. Downloading from {DownloadUrl}", availableVersion, downloadUrl);

                var updateSettings = _configProvider.Config.update_settings; 
                string configuredUpdateDir = updateSettings?.download_directory;

                if (string.IsNullOrEmpty(configuredUpdateDir))
                {
                    Log.Error("Update download directory is not configured in agent_config.json.");
                    return CommandResult.CreateFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, "Update download directory not configured.", -1);
                }
                Directory.CreateDirectory(configuredUpdateDir);

                string packageFileName = $"CMSAgent_Update_{availableVersion}.zip";
                string fullPackagePath = Path.Combine(configuredUpdateDir, packageFileName);

                Log.Information("Downloading update to {FullPackagePath}", fullPackagePath);
                bool downloadSuccess = await _updateHandler.DownloadUpdateAsync(downloadUrl, availableVersion); 
                if (!downloadSuccess)
                {
                    Log.Error("Failed to download update package for version {AvailableVersion} from {DownloadUrl}. Check UpdateHandler logs.", availableVersion, downloadUrl);
                    return CommandResult.CreateFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, "Failed to download update package.", -1);
                }
                
                if (!File.Exists(fullPackagePath))
                {
                    Log.Error("Update package was reported as downloaded, but not found at expected path: {FullPackagePath}", fullPackagePath);
                    return CommandResult.CreateFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, "Downloaded update package not found at expected location.", -1);
                }
                Log.Information("Update package downloaded successfully to {FullPackagePath}", fullPackagePath);
                
                Log.Information("Attempting to install update from {FullPackagePath} for version {AvailableVersion}", fullPackagePath, availableVersion);
                bool installSuccess = await _updateHandler.InstallUpdateAsync(fullPackagePath, availableVersion);

                if (installSuccess)
                {
                    Log.Information("Update process initiated for version {AvailableVersion}. Agent may restart.", availableVersion);
                    return CommandResult.CreateSuccessResult(agentId, commandRequest.commandId, commandRequest.commandType, $"Update to version {availableVersion} initiated. Agent will restart if update is successful.", 0);
                }
                else
                {
                    Log.Error("Failed to install update for version {AvailableVersion} from {FullPackagePath}. Check UpdateHandler and CMSUpdater logs.", availableVersion, fullPackagePath);
                    return CommandResult.CreateFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, "Failed to install update package.", -1);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during agent update process for command ID: {CommandId}", commandRequest.commandId);
                return CommandResult.CreateSystemFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, $"Update process failed: {ex.Message}");
            }
        }

        private async Task<CommandResult> GetSystemInfoAsync(CommandRequest commandRequest, string agentId)
        {
            try
            {
                var systemInfoPayload = await _systemMonitor.CollectSystemInformationAsync();

                if (systemInfoPayload == null)
                {
                    Log.Warning("System monitor returned null for system information for command ID: {CommandId}.", commandRequest.commandId);
                    return CommandResult.CreateFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, "Failed to retrieve system information (monitor returned null).", -1);
                }

                string jsonOutput = JsonSerializer.Serialize(systemInfoPayload, new JsonSerializerOptions { WriteIndented = true });

                return CommandResult.CreateSuccessResult(agentId, commandRequest.commandId, commandRequest.commandType, jsonOutput, 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception gathering system info for command ID: {CommandId}", commandRequest.commandId);
                return CommandResult.CreateSystemFailureResult(agentId, commandRequest.commandId, commandRequest.commandType, $"Failed to get system info: {ex.Message}");
            }
        }
    }
}