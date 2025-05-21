using System.Text.Json;
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Shared
{
    /// <summary>
    /// Manages the list of ignored update versions and handles reading/writing the ignored_versions.json file.
    /// </summary>
    public class VersionIgnoreManager : IVersionIgnoreManager
    {
        private readonly string _ignoredVersionsFilePath;
        private HashSet<string> _ignoredVersions;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private readonly object _fileLock = new();
        private readonly ILogger<VersionIgnoreManager> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionIgnoreManager"/> class.
        /// </summary>
        /// <param name="agentProgramDataPath">The path to the agent's ProgramData folder.</param>
        /// <param name="logger">The logger instance for logging operations.</param>
        public VersionIgnoreManager(string agentProgramDataPath, ILogger<VersionIgnoreManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(agentProgramDataPath))
            {
                _logger.LogError("agentProgramDataPath must not be empty when initializing VersionIgnoreManager.");
                throw new ArgumentNullException(nameof(agentProgramDataPath));
            }

            string runtimeConfigDir = Path.Combine(agentProgramDataPath, AgentConstants.RuntimeConfigSubFolderName);
            _ignoredVersionsFilePath = Path.Combine(runtimeConfigDir, AgentConstants.IgnoredVersionsFileName);
            _ignoredVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            LoadIgnoredVersions();
        }

        private void LoadIgnoredVersions()
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_ignoredVersionsFilePath))
                    {
                        string json = File.ReadAllText(_ignoredVersionsFilePath);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            _logger.LogInformation("Ignored versions file {FilePath} is empty. Initializing empty list.", _ignoredVersionsFilePath);
                            _ignoredVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            return;
                        }
                        var versions = JsonSerializer.Deserialize<List<string>>(json);
                        if (versions != null)
                        {
                            _ignoredVersions = new HashSet<string>(versions, StringComparer.OrdinalIgnoreCase);
                            _logger.LogInformation("Loaded {Count} ignored versions from {FilePath}.", _ignoredVersions.Count, _ignoredVersionsFilePath);
                        }
                        else
                        {
                            _logger.LogWarning("Could not deserialize content from ignored versions file {FilePath}. Initializing empty list.", _ignoredVersionsFilePath);
                            _ignoredVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Ignored versions file not found at {FilePath}. Initializing empty list.", _ignoredVersionsFilePath);
                        _ignoredVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading ignored versions from {FilePath}. Initializing empty list.", _ignoredVersionsFilePath);
                    _ignoredVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        
        private async Task SaveIgnoredVersionsAsync()
        {
            List<string> versionsToSave;
            lock (_fileLock) 
            {
                 versionsToSave = [.. _ignoredVersions];
            }

            try
            {
                string json = JsonSerializer.Serialize(versionsToSave, _jsonOptions);
                bool success = await FileUtils.WriteStringToFileAsync(_ignoredVersionsFilePath, json);
                if(success)
                {
                    _logger.LogInformation("Successfully saved {Count} ignored versions to {FilePath}.", versionsToSave.Count, _ignoredVersionsFilePath);
                }
                else
                {
                    _logger.LogError("Failed to save ignored versions to {FilePath} (using FileUtils).", _ignoredVersionsFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ignored versions to {FilePath}.", _ignoredVersionsFilePath);
            }
        }

        /// <summary>
        /// Adds a version to the ignore list asynchronously.
        /// </summary>
        /// <param name="version">The version string to ignore.</param>
        public async Task IgnoreVersionAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return;

            bool added;
            lock (_fileLock)
            {
                added = _ignoredVersions.Add(version);
            }

            if (added)
            {
                _logger.LogInformation("Version {Version} has been added to the ignore list.", version);
                await SaveIgnoredVersionsAsync();
            }
        }

        /// <summary>
        /// Removes a version from the ignore list asynchronously.
        /// </summary>
        /// <param name="version">The version string to remove from the ignore list.</param>
        public async Task UnignoreVersionAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return;

            bool removed;
            lock (_fileLock)
            {
                removed = _ignoredVersions.Remove(version);
            }
            
            if (removed)
            {
                _logger.LogInformation("Version {Version} has been removed from the ignore list.", version);
                await SaveIgnoredVersionsAsync();
            }
        }

        /// <summary>
        /// Checks if a version is in the ignore list.
        /// </summary>
        /// <param name="version">The version string to check.</param>
        /// <returns>True if the version is ignored; otherwise, false.</returns>
        public bool IsVersionIgnored(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            lock (_fileLock)
            {
                return _ignoredVersions.Contains(version);
            }
        }

        /// <summary>
        /// Gets all ignored versions.
        /// </summary>
        /// <returns>An enumerable of ignored version strings.</returns>
        public IEnumerable<string> GetIgnoredVersions()
        {
            lock (_fileLock)
            {
                return [.. _ignoredVersions];
            }
        }

        /// <summary>
        /// Clears all ignored versions asynchronously.
        /// </summary>
        public async Task ClearIgnoredVersionsAsync()
        {
            bool changed = false;
            lock (_fileLock)
            {
                if (_ignoredVersions.Count != 0)
                {
                    _ignoredVersions.Clear();
                    changed = true;
                }
            }
            if (changed)
            {
                _logger.LogInformation("All versions have been removed from the ignore list.");
                await SaveIgnoredVersionsAsync();
            }
        }
    }

    /// <summary>
    /// Interface for managing ignored versions.
    /// </summary>
    public interface IVersionIgnoreManager
    {
        /// <summary>
        /// Adds a version to the ignore list asynchronously.
        /// </summary>
        /// <param name="version">The version string to ignore.</param>
        Task IgnoreVersionAsync(string version);
        /// <summary>
        /// Removes a version from the ignore list asynchronously.
        /// </summary>
        /// <param name="version">The version string to remove from the ignore list.</param>
        Task UnignoreVersionAsync(string version);
        /// <summary>
        /// Checks if a version is in the ignore list.
        /// </summary>
        /// <param name="version">The version string to check.</param>
        /// <returns>True if the version is ignored; otherwise, false.</returns>
        bool IsVersionIgnored(string version);
        /// <summary>
        /// Gets all ignored versions.
        /// </summary>
        /// <returns>An enumerable of ignored version strings.</returns>
        IEnumerable<string> GetIgnoredVersions();
        /// <summary>
        /// Clears all ignored versions asynchronously.
        /// </summary>
        Task ClearIgnoredVersionsAsync();
    }
}
