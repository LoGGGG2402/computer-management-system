using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CMSUpdater.Utilities;

namespace CMSUpdater
{
    /// <summary>
    /// Entry point for the CMSUpdater application
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>0 for success, non-zero for failure</returns>
        public static async Task<int> Main(string[] args)
        {
            // Define command line arguments according to Standard.md X.B
            var pidOption = new Option<int>(
                name: "--pid",
                description: "(Required) Process ID of the CMSAgent.exe (service) old instance.");
            pidOption.IsRequired = true;

            var newAgentPathOption = new Option<string>(
                name: "--new-agent-path",
                description: "(Required) Absolute path to the directory containing the new agent files.");
            newAgentPathOption.IsRequired = true;

            var currentAgentInstallDirOption = new Option<string>(
                name: "--current-agent-install-dir",
                description: "(Required) Absolute path to the current installation directory of the old agent.");
            currentAgentInstallDirOption.IsRequired = true;

            var updaterLogDirOption = new Option<string>(
                name: "--updater-log-dir",
                description: "(Required) Absolute path to the directory where updater logs should be stored.");
            updaterLogDirOption.IsRequired = true;

            var rootCommand = new RootCommand("CMSUpdater: Updates the CMSAgent application.")
            {
                pidOption,
                newAgentPathOption,
                currentAgentInstallDirOption,
                updaterLogDirOption
            };

            rootCommand.SetHandler(async (InvocationContext context) =>
            {
                int pid = context.ParseResult.GetValueForOption(pidOption)!;
                string newAgentPath = context.ParseResult.GetValueForOption(newAgentPathOption)!;
                string currentAgentInstallDir = context.ParseResult.GetValueForOption(currentAgentInstallDirOption)!;
                string updaterLogDir = context.ParseResult.GetValueForOption(updaterLogDirOption)!;

                // Setup logging according to Standard.md VIII.1 (updater_YYYYMMDD_HHMMSS.log)
                // The exact filename format will be handled by LoggingSetup
                var logger = LoggingSetup.ConfigureUpdaterLogger<UpdaterLogic>(updaterLogDir, "updater");
                logger.LogInformation("CMSUpdater started with arguments: PID={PID}, NewPath={NewPath}, CurrentDir={CurrentDir}, LogDir={LogDir}", 
                                    pid, newAgentPath, currentAgentInstallDir, updaterLogDir);

                var updateParameters = new UpdateParameters
                {
                    PidToWatch = pid,
                    NewAgentPath = newAgentPath,
                    CurrentAgentInstallDir = currentAgentInstallDir,
                    UpdaterLogDir = updaterLogDir,
                    // Assuming BackupDirRoot and CurrentAgentVersion might be needed.
                    // These will need to be sourced, possibly from config or other means if required by UpdaterLogic.
                    // For now, providing placeholder or default values if not passed as command-line args.
                    // If these are critical and not available, the UpdaterLogic might need adjustment
                    // or these parameters need to be added to the command line options.
                    BackupDirRoot = Path.Combine(Path.GetTempPath(), "CMSAgent_Backups"), // Example placeholder
                    CurrentAgentVersion = "0.0.0" // Example placeholder, versioning might come from elsewhere
                };

                var updaterLogic = new UpdaterLogic(logger, updateParameters);
                await updaterLogic.ExecuteUpdateAsync(); // Corrected method name
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}