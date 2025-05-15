using System;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Handles the debug command to run CMSAgent in console application mode.
    /// </summary>
    public class DebugCommand
    {
        private readonly ILogger<DebugCommand> _logger;

        /// <summary>
        /// Initializes a new instance of DebugCommand.
        /// </summary>
        /// <param name="logger">Logger for logging events.</param>
        public DebugCommand(ILogger<DebugCommand> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the debug command.
        /// </summary>
        /// <returns>Exit code of the command.</returns>
        public int Execute()
        {
            try
            {
                // Print information about debug mode
                Console.WriteLine("--------------------------------");
                Console.WriteLine("| CMSAgent running in DEBUG mode |");
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Press CTRL+C to stop.");
                Console.WriteLine();

                // Log the event
                _logger.LogInformation("CMSAgent has started in debug mode");

                // Debug command only displays a message, doesn't actually do anything
                // Host is managed in Program.cs
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in debug mode: {ex.Message}");
                _logger.LogError(ex, "Error when starting debug mode");
                return -1;
            }
        }
    }
}
