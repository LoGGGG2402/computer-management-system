using System;
using System.IO;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Class for handling the uninstall command to remove the CMSAgent service.
    /// </summary>
    public class UninstallCommand
    {
        private readonly ILogger<UninstallCommand> _logger;
        private readonly ServiceUtils _serviceUtils;
        private readonly IConfigLoader _configLoader;
        private readonly string _serviceName;
        private readonly string _dataDirectory;

        /// <summary>
        /// Initializes a new instance of UninstallCommand.
        /// </summary>
        /// <param name="logger">Logger for logging events.</param>
        /// <param name="serviceUtils">Utility for managing Windows Services.</param>
        /// <param name="configLoader">ConfigLoader to access configuration paths.</param>
        /// <param name="options">Agent configuration.</param>
        public UninstallCommand(
            ILogger<UninstallCommand> logger,
            ServiceUtils serviceUtils,
            IConfigLoader configLoader,
            IOptions<CmsAgentSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";
            _dataDirectory = _configLoader.GetDataPath();
        }

        /// <summary>
        /// Executes the uninstall command.
        /// </summary>
        /// <param name="removeData">Flag determining whether to remove data.</param>
        /// <returns>Exit code of the command.</returns>
        public async Task<int> ExecuteAsync(bool removeData)
        {
            Console.WriteLine($"Uninstalling service {_serviceName}...");

            try
            {
                // Check if service exists
                if (!_serviceUtils.IsServiceInstalled(_serviceName))
                {
                    Console.WriteLine($"Service {_serviceName} does not exist or has been previously removed.");
                    
                    // Remove data if requested
                    if (removeData)
                    {
                        await RemoveDataDirectoriesAsync();
                    }
                    
                    return (int)CliExitCodes.Success;
                }

                // Uninstall service
                _serviceUtils.UninstallService(_serviceName);
                
                Console.WriteLine($"Service {_serviceName} has been successfully removed.");

                // Remove data if requested
                if (removeData)
                {
                    await RemoveDataDirectoriesAsync();
                }
                
                return (int)CliExitCodes.Success;
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Error: Administrator privileges are required to uninstall the service. Please run the command as Administrator.");
                return (int)CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error uninstalling service: {ex.Message}");
                _logger.LogError(ex, "Error uninstalling service {ServiceName}", _serviceName);
                return (int)CliExitCodes.ServiceOperationFailed;
            }
        }

        /// <summary>
        /// Removes the agent data directories.
        /// </summary>
        /// <returns>Task representing the data removal operation.</returns>
        private async Task RemoveDataDirectoriesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_dataDirectory) || !Directory.Exists(_dataDirectory))
                {
                    Console.WriteLine("Agent data directory not found.");
                    return;
                }

                Console.WriteLine($"Removing agent data from: {_dataDirectory}");
                
                // Delete configuration files
                string configFile = Path.Combine(_dataDirectory, "runtime_config.json");
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                // Delete queue directories
                string queuesDirectory = Path.Combine(_dataDirectory, "queues");
                if (Directory.Exists(queuesDirectory))
                {
                    Directory.Delete(queuesDirectory, true);
                }

                // Delete logs directories
                string logsDirectory = Path.Combine(_dataDirectory, "logs");
                if (Directory.Exists(logsDirectory))
                {
                    Directory.Delete(logsDirectory, true);
                }

                // Try to delete the root directory (may fail if processes are holding locks)
                try
                {
                    Directory.Delete(_dataDirectory, true);
                    Console.WriteLine("All agent data has been deleted.");
                }
                catch (IOException)
                {
                    Console.WriteLine("Some agent data has been deleted. Some files or directories may be in use.");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Some agent data has been deleted. Insufficient permissions to delete some files or directories.");
                }
                
                // Add an await to remove warning
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error when deleting data: {ex.Message}");
                _logger.LogError(ex, "Error deleting agent data at {DataDirectory}", _dataDirectory);
            }
        }
    }
}
