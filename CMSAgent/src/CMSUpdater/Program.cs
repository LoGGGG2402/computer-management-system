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
            var pidOption = new Option<int>("-pid", description: "Process ID of the running CMSAgent.Service.") { IsRequired = true };
            var newVersionOption = new Option<string>("-new-version", description: "New version of the Agent.") { IsRequired = true };
            var oldVersionOption = new Option<string>("-old-version", description: "Old version of the Agent (for backup).") { IsRequired = true };
            var sourcePathOption = new Option<string>("-source-path", description: "Path to the directory containing extracted new version files.") { IsRequired = true };
            var timeoutOption = new Option<int>("-timeout", getDefaultValue: () => 60, description: "Timeout duration (seconds) for service operations.");
            var watchdogOption = new Option<int>("-watchdog", getDefaultValue: () => 120, description: "Duration (seconds) to monitor new Agent.");

            var rootCommand = new RootCommand("CMS Agent Updater Utility")
            {
                pidOption, newVersionOption, oldVersionOption, sourcePathOption,
                timeoutOption, watchdogOption
            };

            rootCommand.SetHandler(async (InvocationContext context) =>
            {
                var config = new UpdaterConfig
                {
                    CurrentAgentPid = context.ParseResult.GetValueForOption(pidOption),
                    NewAgentVersion = context.ParseResult.GetValueForOption(newVersionOption)!,
                    OldAgentVersion = context.ParseResult.GetValueForOption(oldVersionOption)!,
                    NewAgentExtractedPath = context.ParseResult.GetValueForOption(sourcePathOption)!,
                    AgentInstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AgentConstants.ServiceName),
                    AgentProgramDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AgentConstants.AgentProgramDataFolderName),
                    ServiceWaitTimeoutSeconds = context.ParseResult.GetValueForOption(timeoutOption),
                    NewAgentWatchdogPeriodSeconds = context.ParseResult.GetValueForOption(watchdogOption),
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
            var loggerForVersionManager = loggerFactory.CreateLogger<VersionIgnoreManager>(); // Logger for VersionIgnoreManager

            // Initialize VersionIgnoreManager
            // VersionIgnoreManager needs AgentProgramDataPath, available in config.AgentProgramDataDirectory
            IVersionIgnoreManager versionIgnoreManager = new VersionIgnoreManager(config.AgentProgramDataDirectory, loggerForVersionManager);

            var updaterRunner = new UpdateTaskRunner(config, loggerForRunner, versionIgnoreManager); // Pass versionIgnoreManager

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
