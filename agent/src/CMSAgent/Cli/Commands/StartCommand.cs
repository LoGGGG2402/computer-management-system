using System;
using System.Threading.Tasks;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Class for handling the start command to start the CMSAgent Windows service.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of StartCommand.
    /// </remarks>
    /// <param name="logger">Logger for logging events.</param>
    /// <param name="serviceUtils">Utility for managing Windows Services.</param>
    /// <param name="options">Agent configuration.</param>
    public class StartCommand(
        ILogger<StartCommand> logger,
        ServiceUtils serviceUtils,
        IOptions<CmsAgentSettingsOptions> options)
    {
        private readonly ILogger<StartCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ServiceUtils _serviceUtils = serviceUtils ?? throw new ArgumentNullException(nameof(serviceUtils));
        private readonly string _serviceName = options?.Value?.AppName + "Service" ?? "CMSAgentService";

        /// <summary>
        /// Executes the start command.
        /// </summary>
        /// <returns>Exit code of the command.</returns>
        public async Task<int> ExecuteAsync()
        {
            Console.WriteLine($"Starting service {_serviceName}...");

            try
            {
                // Check if service is installed
                if (!_serviceUtils.IsServiceInstalled(_serviceName))
                {
                    Console.WriteLine($"Service {_serviceName} is not installed.");
                    return (int)CliExitCodes.ServiceNotInstalled;
                }

                // Check if service is already running
                if (_serviceUtils.IsServiceRunning(_serviceName))
                {
                    Console.WriteLine($"Service {_serviceName} is already running.");
                    return (int)CliExitCodes.Success;
                }

                // Start service
                bool startSuccess = await _serviceUtils.StartServiceAsync(_serviceName);
                if (startSuccess)
                {
                    Console.WriteLine($"Service {_serviceName} has been started successfully.");
                    return (int)CliExitCodes.Success;
                }
                else
                {
                    Console.Error.WriteLine($"Unable to start service {_serviceName}.");
                    return (int)CliExitCodes.ServiceOperationFailed;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Error: Administrator privileges are required to start the service. Please run the command as Administrator.");
                return (int)CliExitCodes.MissingPermissions;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error starting service: {ex.Message}");
                _logger.LogError(ex, "Error starting service {ServiceName}", _serviceName);
                return (int)CliExitCodes.GeneralError;
            }
        }
    }
}
