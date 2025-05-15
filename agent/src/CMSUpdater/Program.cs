using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using CMSAgent.Common.Enums;
using CMSUpdater.Core;
using CMSUpdater.Services;
using System.Runtime.Versioning;
using System.Reflection;
using Serilog;
using CMSAgent.Common.Logging;
using System.Text.Json;
using CMSAgent.Common.Models;

/// <summary>
/// Main Program class for CMSUpdater
/// </summary>
[SupportedOSPlatform("windows")]
public class Program
{
    /// <summary>
    /// Static logger for Updater
    /// </summary>
    private static Microsoft.Extensions.Logging.ILogger _logger = NullLogger.Instance;
    
    /// <summary>
    /// Application configuration
    /// </summary>
    private static IConfiguration _configuration = null!;

    /// <summary>
    /// Path to update_info.json file
    /// </summary>
    private static readonly string UpdateInfoPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "update_info.json");

    /// <summary>
    /// Application entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Read configuration from appsettings.json
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
            
            // Update version information from assembly
            var assembly = Assembly.GetExecutingAssembly();
            
            // Add assembly information to configuration
            var configDictionary = new Dictionary<string, string?>
            {
                { "Application:Name", assembly.GetName().Name },
                { "Application:Version", assembly.GetName().Version?.ToString() }
            };

            // Combine current configuration with assembly information
            _configuration = new ConfigurationBuilder()
                .AddConfiguration(_configuration)
                .AddInMemoryCollection(configDictionary)
                .Build();
            
            // Configure logging using LoggingSetup
            _logger = LoggingSetup.CreateLogger(_configuration);
            
            // Parse command line arguments
            var (isValid, agentProcessIdToWait, newAgentPath, currentAgentInstallDir, updaterLogDir, currentAgentVersion, newAgentVersion) = ParseArguments(args);
            
            // If command line arguments are insufficient, try reading from update_info.json
            if (!isValid && File.Exists(UpdateInfoPath))
            {
                _logger.LogInformation("Reading update information from file {UpdateInfoPath}", UpdateInfoPath);
                
                try 
                {
                    var updateInfoJson = File.ReadAllText(UpdateInfoPath);
                    var updateInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(updateInfoJson);
                    
                    if (updateInfo != null)
                    {
                        // Retrieve information from update_info.json
                        if (updateInfo.TryGetValue("package_path", out var packagePath) &&
                            updateInfo.TryGetValue("install_directory", out var installDir) &&
                            updateInfo.TryGetValue("new_version", out var newVersion))
                        {
                            // Current PID may not be stored in the file, retrieve PID from command line arguments
                            int pid = 0;
                            if (args.Length > 1 && args[0].Contains("pid") && int.TryParse(args[1], out pid))
                            {
                                agentProcessIdToWait = pid;
                            }
                            
                            // Use information from update_info.json
                            newAgentPath = packagePath;
                            currentAgentInstallDir = installDir;
                            currentAgentVersion = newVersion;
                            updaterLogDir = Path.Combine(currentAgentInstallDir, "logs");
                            
                            isValid = !string.IsNullOrEmpty(newAgentPath) && 
                                     !string.IsNullOrEmpty(currentAgentInstallDir) && 
                                     !string.IsNullOrEmpty(currentAgentVersion);
                            
                            _logger.LogInformation("Read information from update_info.json: " +
                                "PID={PID}, NewPath={NewPath}, InstallDir={InstallDir}, Version={Version}",
                                agentProcessIdToWait, newAgentPath, currentAgentInstallDir, currentAgentVersion);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading update_info.json file");
                }
            }
            
            if (!isValid)
            {
                _logger.LogError("Invalid arguments for CMSUpdater.");
                PrintUsage();
                return (int)UpdaterExitCodes.InvalidArguments;
            }
            
            _logger.LogInformation("CMSUpdater started with PID: {PID}, NewPath: {NewPath}, CurrentDir: {CurrentDir}, LogDir: {LogDir}, CurrentVersion: {Version}", 
                agentProcessIdToWait, newAgentPath, currentAgentInstallDir, updaterLogDir, currentAgentVersion);
            
            // Read settings from appsettings.json 
            int retryAttempts = _configuration.GetValue<int>("Updater:RetryAttempts", 3);
            int retryDelayMs = _configuration.GetValue<int>("Updater:RetryDelayMilliseconds", 1000);
            int processTimeoutSec = _configuration.GetValue<int>("Updater:WaitForProcessTimeoutSeconds", 30);
            var filesToExclude = _configuration.GetSection("Updater:FilesToExcludeFromUpdate").Get<string[]>() ?? Array.Empty<string>();
            
            _logger.LogInformation("Configuration from appsettings.json: RetryAttempts={Attempts}, RetryDelay={Delay}ms, ProcessTimeout={Timeout}s, FilesToExclude={ExcludeCount} items", 
                retryAttempts, retryDelayMs, processTimeoutSec, filesToExclude.Length);
            
            var serviceHelper = new ServiceHelper(_logger);
            var rollbackManager = new RollbackManager(_logger, currentAgentInstallDir, currentAgentVersion, serviceHelper);
            var updaterLogic = new UpdaterLogic(
                _logger, 
                rollbackManager, 
                serviceHelper, 
                agentProcessIdToWait, 
                newAgentPath, 
                currentAgentInstallDir, 
                currentAgentVersion,
                _configuration,
                newAgentVersion);
            
            return await updaterLogic.ExecuteUpdateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in CMSUpdater");
            return (int)UpdaterExitCodes.GeneralError;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    /// <summary>
    /// Parse command line arguments
    /// </summary>
    /// <param name="args">Command line arguments array</param>
    /// <returns>Tuple containing validity and argument values</returns>
    private static (bool isValid, int agentProcessIdToWait, string newAgentPath, string currentAgentInstallDir, string updaterLogDir, string currentAgentVersion, string newAgentVersion) ParseArguments(string[] args)
    {
        if (args.Length < 6)
        {
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        int agentProcessIdToWait = 0;
        string newAgentPath = string.Empty;
        string currentAgentInstallDir = string.Empty;
        string updaterLogDir = string.Empty;
        string currentAgentVersion = string.Empty;
        string newAgentVersion = string.Empty;
        
        bool hasPid = false;
        bool hasNewAgentPath = false;
        bool hasCurrentInstallDir = false;
        bool hasUpdaterLogDir = false;
        bool hasCurrentVersion = false;
        bool hasNewVersion = false;
        
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLower())
            {
                case "pid":
                case "--pid":
                    if (int.TryParse(args[i + 1], out int pid))
                    {
                        agentProcessIdToWait = pid;
                        hasPid = true;
                    }
                    break;
                
                case "new-agent-path":
                case "--new-agent-path":
                    newAgentPath = args[i + 1].Trim('"');
                    hasNewAgentPath = true;
                    break;
                
                case "current-agent-install-dir":
                case "--current-agent-install-dir":
                    currentAgentInstallDir = args[i + 1].Trim('"');
                    hasCurrentInstallDir = true;
                    break;
                
                case "updater-log-dir":
                case "--updater-log-dir":
                    updaterLogDir = args[i + 1].Trim('"');
                    hasUpdaterLogDir = true;
                    break;
                
                case "current-agent-version":
                case "--current-agent-version":
                    currentAgentVersion = args[i + 1].Trim('"');
                    hasCurrentVersion = true;
                    break;

                case "new-agent-version":
                case "--new-agent-version":
                    newAgentVersion = args[i + 1].Trim('"');
                    hasNewVersion = true;
                    break;
            }
        }
        
        // Check if all required arguments are present
        if (!hasPid || !hasNewAgentPath || !hasCurrentInstallDir || !hasUpdaterLogDir || !hasCurrentVersion || !hasNewVersion)
        {
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        // Validate paths
        if (!Directory.Exists(newAgentPath))
        {
            _logger.LogError($"Error: New agent directory does not exist: {newAgentPath}");
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        if (!Directory.Exists(currentAgentInstallDir))
        {
            _logger.LogError($"Error: Current installation directory does not exist: {currentAgentInstallDir}");
            return (false, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        
        return (true, agentProcessIdToWait, newAgentPath, currentAgentInstallDir, updaterLogDir, currentAgentVersion, newAgentVersion);
    }
    
    /// <summary>
    /// Print usage information to console
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Usage: CMSUpdater.exe [parameters]");
        Console.WriteLine("Required parameters:");
        Console.WriteLine("  --pid <process_id>                    PID of old CMSAgent.exe process to stop");
        Console.WriteLine("  --new-agent-path \"<path>\"            Path to extracted new agent files");
        Console.WriteLine("  --current-agent-install-dir \"<path>\" Current installation directory path");
        Console.WriteLine("  --updater-log-dir \"<path>\"           Updater log directory");
        Console.WriteLine("  --current-agent-version \"<version>\"  Current agent version (used for backup name)");
        Console.WriteLine("  --new-agent-version \"<version>\"      New agent version being installed");
        Console.WriteLine();
        Console.WriteLine("Note: You can use update_info.json in the CMSUpdater directory instead of command line parameters");
        Console.WriteLine("Example:");
        Console.WriteLine("  CMSUpdater.exe --pid 1234 --new-agent-path \"C:\\ProgramData\\CMSAgent\\updates\\extracted\\v1.1.0\" --current-agent-install-dir \"C:\\Program Files\\CMSAgent\" --updater-log-dir \"C:\\ProgramData\\CMSAgent\\logs\" --current-agent-version \"1.0.2\" --new-agent-version \"1.1.0\"");
    }
}