// CMSUpdater/Program.cs
using Microsoft.Extensions.Logging;
using Serilog;
using System.CommandLine;
using System.CommandLine.Invocation;
using CMSAgent.Shared; // For VersionIgnoreManager, IVersionIgnoreManager
using CMSAgent.Shared.Logging; // For SerilogConfigurator
using CMSAgent.Shared.Constants; // For AgentConstants
using Microsoft.Extensions.Configuration;

namespace CMSUpdater
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // --- System.CommandLine Configuration ---
            var newVersionOption = new Option<string>("-new-version", description: "Version string of the new Agent to be installed.") { IsRequired = true };
            var oldVersionOption = new Option<string>("-old-version", description: "Version string of the old Agent (for backup).") { IsRequired = true };
            var sourcePathOption = new Option<string>("-source-path", description: "Path to the directory containing the extracted files of the new Agent version.") { IsRequired = true };
            var serviceWaitTimeoutOption = new Option<int>("-service-wait-timeout", getDefaultValue: () => 60, description: "Timeout duration (seconds) to wait for old Agent to stop or new Agent to start.");
            var watchdogPeriodOption = new Option<int>("-watchdog-period", getDefaultValue: () => 120, description: "Duration (seconds) for CMSUpdater to monitor the new Agent after startup.");

            var rootCommand = new RootCommand("CMS Agent Updater Utility")
            {
                newVersionOption, oldVersionOption, sourcePathOption,
                serviceWaitTimeoutOption, watchdogPeriodOption
            };

            rootCommand.SetHandler(async (InvocationContext context) =>
            {
                var config = new UpdaterConfig
                {
                    NewAgentVersion = context.ParseResult.GetValueForOption(newVersionOption)!,
                    OldAgentVersion = context.ParseResult.GetValueForOption(oldVersionOption)!,
                    NewAgentExtractedPath = context.ParseResult.GetValueForOption(sourcePathOption)!,
                    AgentInstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), AgentConstants.ServiceName),
                    AgentProgramDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AgentConstants.AgentProgramDataFolderName),
                    ServiceWaitTimeoutSeconds = context.ParseResult.GetValueForOption(serviceWaitTimeoutOption),
                    NewAgentWatchdogPeriodSeconds = context.ParseResult.GetValueForOption(watchdogPeriodOption)
                };
                context.ExitCode = await RunUpdaterLogicAsync(config);
            });

            return await rootCommand.InvokeAsync(args);
        }

        static async Task<int> RunUpdaterLogicAsync(UpdaterConfig config)
        {
            Directory.SetCurrentDirectory(config.AgentInstallDirectory); // Ensure appsettings.json is found correctly
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Ensure log directory exists
            var logDirectory = Path.Combine(config.AgentProgramDataDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            SerilogConfigurator.Configure(
                configuration,
                config.AgentProgramDataDirectory,
                AgentConstants.UpdaterLogFilePrefix,
                false 
            );

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(Log.Logger);
            });

            var loggerForRunner = loggerFactory.CreateLogger<UpdateTaskRunner>();
            var loggerForVersionManager = loggerFactory.CreateLogger<VersionIgnoreManager>();

            IVersionIgnoreManager versionIgnoreManager = new VersionIgnoreManager(config.AgentProgramDataDirectory, loggerForVersionManager);

            var updaterRunner = new UpdateTaskRunner(config, loggerForRunner, versionIgnoreManager);

            try
            {
                bool success = await updaterRunner.RunUpdateAsync();
                if (success)
                {
                    loggerForRunner.LogInformation("CMSUpdater completed successfully.");
                    return 0; // Success
                }
                else
                {
                    loggerForRunner.LogError("CMSUpdater completed with errors.");
                    return 1; // Failure
                }
            }
            catch (Exception ex)
            {
                loggerForRunner.LogCritical(ex, "Unhandled critical error in CMSUpdater.");
                return 2; // Critical failure
            }
            finally
            {
                await Log.CloseAndFlushAsync(); // Ensure Serilog flushes all logs
                loggerFactory.Dispose();
            }
        }
    }
}
