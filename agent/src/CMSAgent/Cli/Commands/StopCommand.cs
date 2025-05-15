using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Class for handling the stop command to stop the CMSAgent service.
    /// </summary>
    public class StopCommand
    {
        private readonly ILogger<StopCommand> _logger;
        private readonly ServiceUtils _serviceUtils;
        private readonly string _serviceName;

        /// <summary>
        /// Initializes a new instance of StopCommand.
        /// </summary>
        /// <param name="logger">Logger for logging events.</param>
        /// <param name="serviceUtils">Utility for managing Windows Services.</param>
        /// <param name="options">Agent configuration.</param>
        public StopCommand(
            ILogger<StopCommand> logger,
            ServiceUtils serviceUtils,
            IOptions<CmsAgentSettingsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
            _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";
        }

        /// <summary>
        /// Executes the stop command.
        /// </summary>
        /// <returns>Exit code of the command.</returns>
        public async Task<int> ExecuteAsync()
        {
            Console.WriteLine($"Stopping service {_serviceName}...");

            try
            {
                // Check if service is installed
                if (!_serviceUtils.IsServiceInstalled(_serviceName))
                {
                    Console.WriteLine($"Service {_serviceName} is not installed.");
                    return (int)CliExitCodes.ServiceNotInstalled;
                }

                // Check if service is running
                if (!_serviceUtils.IsServiceRunning(_serviceName))
                {
                    Console.WriteLine($"Service {_serviceName} is already stopped.");
                    return (int)CliExitCodes.Success;
                }

                // Stop service
                bool stopSuccess = await _serviceUtils.StopServiceAsync(_serviceName);
                if (stopSuccess)
                {
                    Console.WriteLine($"Service {_serviceName} has been stopped successfully.");
                    return (int)CliExitCodes.Success;
                }
                else
                {
                    Console.Error.WriteLine($"Unable to stop service {_serviceName}.");
                    return (int)CliExitCodes.ServiceOperationFailed;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Error: Administrator privileges are required to stop the service. Please run the command as Administrator.");
                return (int)CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error stopping service: {ex.Message}");
                _logger.LogError(ex, "Error stopping service {ServiceName}", _serviceName);
                return (int)CliExitCodes.GeneralError;
            }
        }
    }
}
