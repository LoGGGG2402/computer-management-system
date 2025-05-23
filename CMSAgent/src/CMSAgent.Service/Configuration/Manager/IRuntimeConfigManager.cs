using CMSAgent.Service.Configuration.Models;

namespace CMSAgent.Service.Configuration.Manager
{
    /// <summary>
    /// Interface defining methods to manage Agent's runtime configuration (runtime_config.json).
    /// </summary>
    public interface IRuntimeConfigManager
    {
        /// <summary>
        /// Load runtime configuration from file.
        /// If file doesn't exist or has errors, a default (or empty) configuration may be returned.
        /// </summary>
        /// <returns>Loaded RuntimeConfig object, or a new instance if loading fails.</returns>
        Task<RuntimeConfig> LoadConfigAsync();

        /// <summary>
        /// Save the current RuntimeConfig object to file.
        /// </summary>
        /// <param name="config">RuntimeConfig object to save.</param>
        /// <returns>True if save successful, False otherwise.</returns>
        Task<bool> SaveConfigAsync(RuntimeConfig config);

        /// <summary>
        /// Get current Agent ID from configuration.
        /// </summary>
        /// <returns>Agent ID or null if not configured.</returns>
        Task<string?> GetAgentIdAsync();

        /// <summary>
        /// Get Agent's encrypted token from configuration.
        /// </summary>
        /// <returns>Encrypted token or null if not available.</returns>
        Task<string?> GetEncryptedAgentTokenAsync();

        /// <summary>
        /// Get Agent's position information (RoomConfig).
        /// </summary>
        /// <returns>PositionInfo object or null if not configured.</returns>
        Task<PositionInfo?> GetPositionInfoAsync();

        /// <summary>
        /// Update Agent ID in configuration and save.
        /// </summary>
        /// <param name="agentId">New Agent ID.</param>
        Task UpdateAgentIdAsync(string agentId);

        /// <summary>
        /// Update encrypted token and save.
        /// </summary>
        /// <param name="encryptedToken">New encrypted token.</param>
        Task UpdateEncryptedAgentTokenAsync(string encryptedToken);

        /// <summary>
        /// Update position information and save.
        /// </summary>
        /// <param name="positionInfo">New position information.</param>
        Task UpdatePositionInfoAsync(PositionInfo positionInfo);

        /// <summary>
        /// Get full path to Agent's root data storage directory in ProgramData
        /// (e.g., C:\ProgramData\CMSAgent).
        /// </summary>
        /// <returns>Path to Agent's ProgramData directory.</returns>
        string GetAgentProgramDataPath();

        /// <summary>
        /// Get full path to runtime_config.json file.
        /// </summary>
        /// <returns>Path to runtime_config.json file.</returns>
        string GetRuntimeConfigFilePath();
    }
}
