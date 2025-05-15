using System.Threading.Tasks;
using CMSAgent.Common.Models;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface for loading and saving agent configuration.
    /// </summary>
    public interface IConfigLoader
    {
        /// <summary>
        /// Main agent configuration from appsettings.json.
        /// </summary>
        CmsAgentSettingsOptions Settings { get; }

        /// <summary>
        /// Agent-specific configuration from appsettings.json.
        /// </summary>
        AgentSpecificSettingsOptions AgentSettings { get; }

        /// <summary>
        /// Loads runtime configuration from file.
        /// </summary>
        /// <param name="forceReload">Force reload from disk instead of using cache.</param>
        /// <returns>Runtime configuration or null if loading fails.</returns>
        Task<RuntimeConfig> LoadRuntimeConfigAsync(bool forceReload = false);

        /// <summary>
        /// Saves runtime configuration to file.
        /// </summary>
        /// <param name="config">Runtime configuration to save.</param>
        /// <returns>Task representing the save operation.</returns>
        Task SaveRuntimeConfigAsync(RuntimeConfig config);

        /// <summary>
        /// Gets agent ID from loaded runtime configuration.
        /// </summary>
        /// <returns>Agent ID or null if configuration not loaded.</returns>
        string GetAgentId();

        /// <summary>
        /// Gets encrypted agent token from loaded runtime configuration.
        /// </summary>
        /// <returns>Encrypted token or null if configuration not loaded.</returns>
        string GetEncryptedAgentToken();

        /// <summary>
        /// Gets agent installation path.
        /// </summary>
        /// <returns>Installation directory path.</returns>
        string GetInstallPath();

        /// <summary>
        /// Gets agent data directory path.
        /// </summary>
        /// <returns>Data directory path.</returns>
        string GetDataPath();

        /// <summary>
        /// Gets current agent version.
        /// </summary>
        /// <returns>Agent version.</returns>
        string GetAgentVersion();
    }
}
