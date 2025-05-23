using System.Text.Json;
using CMSAgent.Shared.Constants;
using CMSAgent.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Shared
{
    /// <summary>
    /// Thread-safe manager for handling version ignore lists in the agent update system.
    /// Provides persistent storage and retrieval of version strings that should be skipped during update checks.
    /// Implements asynchronous file operations with proper locking mechanisms for concurrent access.
    /// </summary>
    /// <remarks>
    /// This class manages a JSON file containing ignored version strings and provides:
    /// - Thread-safe concurrent read/write operations using ReaderWriterLockSlim
    /// - Asynchronous file I/O operations with semaphore-based serialization
    /// - Case-insensitive version string comparison
    /// - Automatic directory creation and error handling
    /// - Persistent storage in the agent's runtime configuration directory
    /// </remarks>
    public class VersionIgnoreManager : IVersionIgnoreManager
    {
        /// <summary>
        /// Full file path to the JSON file storing ignored version strings.
        /// </summary>
        private readonly string _ignoredVersionsFilePath;
        
        /// <summary>
        /// Thread-safe collection of ignored version strings with case-insensitive comparison.
        /// </summary>
        private HashSet<string> _ignoredVersions;
        
        /// <summary>
        /// JSON serialization options configured for readable output formatting.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        
        /// <summary>
        /// Reader-writer lock for managing concurrent access to the ignored versions collection.
        /// Allows multiple concurrent readers while ensuring exclusive write access.
        /// </summary>
        private readonly ReaderWriterLockSlim _rwLock = new();
        
        /// <summary>
        /// Semaphore for serializing file I/O operations to prevent concurrent disk access conflicts.
        /// </summary>
        private readonly SemaphoreSlim _ioLock = new(1, 1);
        
        /// <summary>
        /// Logger instance for recording operations, errors, and debugging information.
        /// </summary>
        private readonly ILogger<VersionIgnoreManager> _logger;

        /// <summary>
        /// Initializes a new instance of the VersionIgnoreManager with the specified configuration.
        /// Sets up file paths and initializes internal data structures without loading data.
        /// </summary>
        /// <param name="agentProgramDataPath">Base directory path for agent data storage where the runtime config folder will be located</param>
        /// <param name="logger">Logger instance for recording operations and error information</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null or agentProgramDataPath is null or whitespace</exception>
        /// <remarks>
        /// The constructor only sets up the file path and initializes collections.
        /// Call InitializeAsync() after construction to load existing ignored versions from disk.
        /// </remarks>
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
        }

        /// <summary>
        /// Asynchronously initializes the VersionIgnoreManager by loading existing ignored versions from disk.
        /// Must be called after construction and before using other methods.
        /// </summary>
        /// <returns>A task representing the asynchronous initialization operation</returns>
        /// <exception cref="IOException">Thrown when file system errors occur during loading</exception>
        /// <exception cref="JsonException">Thrown when the ignored versions file contains invalid JSON</exception>
        /// <remarks>
        /// This method handles missing files gracefully by initializing an empty collection.
        /// If the file exists but contains invalid data, it logs warnings and continues with an empty collection.
        /// </remarks>
        public async Task InitializeAsync()
        {
            await LoadIgnoredVersionsAsync();
        }

        /// <summary>
        /// Asynchronously loads ignored version strings from the JSON file into memory.
        /// Handles missing files, empty files, and JSON parsing errors gracefully.
        /// </summary>
        /// <returns>A task representing the asynchronous load operation</returns>
        /// <exception cref="JsonException">Thrown when JSON deserialization fails due to malformed data</exception>
        /// <exception cref="IOException">Thrown when file read operations fail</exception>
        /// <remarks>
        /// Error handling strategy:
        /// - Missing file: Initialize empty collection and log information
        /// - Empty file: Initialize empty collection and log information  
        /// - Invalid JSON: Log error, initialize empty collection, and continue
        /// - File access errors: Log error, initialize empty collection, and continue
        /// </remarks>
        private async Task LoadIgnoredVersionsAsync()
        {
            try
            {
                if (File.Exists(_ignoredVersionsFilePath))
                {
                    string json = await File.ReadAllTextAsync(_ignoredVersionsFilePath);
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
        
        /// <summary>
        /// Asynchronously saves the current ignored versions collection to the JSON file.
        /// Uses file locking to prevent concurrent write operations and ensure data integrity.
        /// </summary>
        /// <returns>A task representing the asynchronous save operation</returns>
        /// <exception cref="IOException">Thrown when file write operations fail</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when insufficient permissions to write to the file</exception>
        /// <remarks>
        /// The method:
        /// - Creates a snapshot of the current ignored versions under read lock
        /// - Serializes the data to JSON with indented formatting
        /// - Uses FileUtils.WriteStringToFileAsync for reliable file writing
        /// - Handles directory creation automatically if needed
        /// - Logs success and failure outcomes
        /// </remarks>
        private async Task SaveIgnoredVersionsAsync()
        {
            List<string> versionsToSave;
            _rwLock.EnterReadLock();
            try
            {
                versionsToSave = [.. _ignoredVersions];
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            try
            {
                await _ioLock.WaitAsync();
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
                finally
                {
                    _ioLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ignored versions to {FilePath}.", _ignoredVersionsFilePath);
            }
        }

        /// <summary>
        /// Asynchronously adds a version string to the ignore list if it's not already present.
        /// Automatically saves the updated list to disk if a new version was added.
        /// </summary>
        /// <param name="version">The version string to add to the ignore list</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// The method:
        /// - Ignores null, empty, or whitespace-only version strings
        /// - Uses case-insensitive comparison for duplicate detection
        /// - Only saves to disk if the version was actually added (not already present)
        /// - Logs information when a new version is successfully added
        /// - Thread-safe operation using write locks
        /// </remarks>
        public async Task IgnoreVersionAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return;

            bool added;
            _rwLock.EnterWriteLock();
            try
            {
                added = _ignoredVersions.Add(version);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            if (added)
            {
                _logger.LogInformation("Version {Version} has been added to the ignore list.", version);
                await SaveIgnoredVersionsAsync();
            }
        }

        /// <summary>
        /// Asynchronously removes a version string from the ignore list if it exists.
        /// Automatically saves the updated list to disk if a version was actually removed.
        /// </summary>
        /// <param name="version">The version string to remove from the ignore list</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// The method:
        /// - Ignores null, empty, or whitespace-only version strings
        /// - Uses case-insensitive comparison for version matching
        /// - Only saves to disk if the version was actually removed (was present)
        /// - Logs information when a version is successfully removed
        /// - Thread-safe operation using write locks
        /// </remarks>
        public async Task UnignoreVersionAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return;

            bool removed;
            _rwLock.EnterWriteLock();
            try
            {
                removed = _ignoredVersions.Remove(version);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
            
            if (removed)
            {
                _logger.LogInformation("Version {Version} has been removed from the ignore list.", version);
                await SaveIgnoredVersionsAsync();
            }
        }

        /// <summary>
        /// Synchronously checks if a specific version string is currently in the ignore list.
        /// Provides fast, thread-safe read access without blocking other readers.
        /// </summary>
        /// <param name="version">The version string to check for in the ignore list</param>
        /// <returns>True if the version is in the ignore list; false if not found or version is null/empty</returns>
        /// <remarks>
        /// This method:
        /// - Returns false immediately for null, empty, or whitespace-only version strings
        /// - Uses case-insensitive comparison for version matching
        /// - Allows concurrent reads from multiple threads
        /// - Does not block write operations unnecessarily
        /// </remarks>
        public bool IsVersionIgnored(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            
            _rwLock.EnterReadLock();
            try
            {
                return _ignoredVersions.Contains(version);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Retrieves a snapshot of all currently ignored version strings.
        /// Returns a new collection to prevent external modification of internal state.
        /// </summary>
        /// <returns>An enumerable collection containing all ignored version strings</returns>
        /// <remarks>
        /// The returned collection:
        /// - Is a snapshot taken at the time of the call
        /// - Will not reflect subsequent changes to the ignore list
        /// - Is safe to iterate over without holding locks
        /// - Contains case-insensitive version strings as originally added
        /// - May be empty if no versions are currently ignored
        /// </remarks>
        public IEnumerable<string> GetIgnoredVersions()
        {
            _rwLock.EnterReadLock();
            try
            {
                return [.. _ignoredVersions];
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Asynchronously removes all version strings from the ignore list.
        /// Automatically saves the empty list to disk if any versions were actually removed.
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// The method:
        /// - Only performs disk I/O if there were actually versions to remove
        /// - Logs information when versions are successfully cleared
        /// - Thread-safe operation using write locks
        /// - Leaves the ignore list in a valid empty state
        /// - Does not delete the underlying JSON file, just empties it
        /// </remarks>
        public async Task ClearIgnoredVersionsAsync()
        {
            bool changed = false;
            _rwLock.EnterWriteLock();
            try
            {
                if (_ignoredVersions.Count != 0)
                {
                    _ignoredVersions.Clear();
                    changed = true;
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
            
            if (changed)
            {
                _logger.LogInformation("All versions have been removed from the ignore list.");
                await SaveIgnoredVersionsAsync();
            }
        }

        /// <summary>
        /// Releases all resources used by the VersionIgnoreManager including locks and semaphores.
        /// Should be called when the instance is no longer needed to prevent resource leaks.
        /// </summary>
        /// <remarks>
        /// This method disposes:
        /// - The ReaderWriterLockSlim used for thread synchronization
        /// - The SemaphoreSlim used for file I/O serialization
        /// After disposal, the instance should not be used for any operations.
        /// </remarks>
        public void Dispose()
        {
            _rwLock.Dispose();
            _ioLock.Dispose();
        }
    }

    /// <summary>
    /// Contract interface for managing version ignore functionality in the agent update system.
    /// Defines operations for adding, removing, checking, and retrieving ignored version strings.
    /// Extends IDisposable to ensure proper resource cleanup of implementing classes.
    /// </summary>
    /// <remarks>
    /// This interface provides:
    /// - Asynchronous operations for modifying the ignore list
    /// - Synchronous read operations for performance
    /// - Thread-safe access patterns
    /// - Proper resource management through IDisposable
    /// Implementations should handle persistence, thread safety, and error conditions appropriately.
    /// </remarks>
    public interface IVersionIgnoreManager : IDisposable
    {
        /// <summary>
        /// Asynchronously adds a version string to the ignore list.
        /// Prevents the specified version from being considered during update checks.
        /// </summary>
        /// <param name="version">The version string to add to the ignore list</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// Implementations should:
        /// - Handle null or empty version strings gracefully
        /// - Use case-insensitive comparison to prevent duplicates
        /// - Persist changes to storage
        /// - Be thread-safe for concurrent access
        /// </remarks>
        Task IgnoreVersionAsync(string version);

        /// <summary>
        /// Asynchronously removes a version string from the ignore list.
        /// Allows the specified version to be considered again during update checks.
        /// </summary>
        /// <param name="version">The version string to remove from the ignore list</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// Implementations should:
        /// - Handle null or empty version strings gracefully
        /// - Use case-insensitive comparison for version matching
        /// - Persist changes to storage
        /// - Be thread-safe for concurrent access
        /// </remarks>
        Task UnignoreVersionAsync(string version);

        /// <summary>
        /// Synchronously checks whether a specific version string is currently ignored.
        /// Used by update logic to determine if a version should be skipped.
        /// </summary>
        /// <param name="version">The version string to check</param>
        /// <returns>True if the version is ignored and should be skipped; false otherwise</returns>
        /// <remarks>
        /// Implementations should:
        /// - Return false for null or empty version strings
        /// - Use case-insensitive comparison for version matching
        /// - Provide fast read access without blocking
        /// - Be thread-safe for concurrent read access
        /// </remarks>
        bool IsVersionIgnored(string version);

        /// <summary>
        /// Retrieves all currently ignored version strings as an enumerable collection.
        /// Useful for displaying ignored versions to users or for diagnostic purposes.
        /// </summary>
        /// <returns>An enumerable containing all ignored version strings</returns>
        /// <remarks>
        /// Implementations should:
        /// - Return a snapshot that won't change during iteration
        /// - Handle empty ignore lists appropriately
        /// - Provide thread-safe access to the collection
        /// - Return version strings in their original casing
        /// </remarks>
        IEnumerable<string> GetIgnoredVersions();

        /// <summary>
        /// Asynchronously removes all version strings from the ignore list.
        /// Resets the ignore list to an empty state, allowing all versions to be considered.
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// Implementations should:
        /// - Persist the cleared state to storage
        /// - Be thread-safe for concurrent access
        /// - Handle already-empty lists gracefully
        /// - Provide appropriate logging or feedback
        /// </remarks>
        Task ClearIgnoredVersionsAsync();
    }
}
