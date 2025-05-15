using System;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Security;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Class for handling the configure command to set up initial configuration for the agent.
    /// </summary>
    /// <param name="logger">Logger for logging events.</param>
    /// <param name="configLoader">ConfigLoader to load and save configuration.</param>
    /// <param name="httpClient">HttpClient to communicate with the server.</param>
    /// <param name="tokenProtector">TokenProtector to encrypt tokens.</param>
    public class ConfigureCommand(
        ILogger<ConfigureCommand> logger,
        IConfigLoader configLoader,
        IHttpClientWrapper httpClient,
        TokenProtector tokenProtector)
    {
        private readonly ILogger<ConfigureCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IConfigLoader _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
        private readonly IHttpClientWrapper _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly TokenProtector _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));

        /// <summary>
        /// Executes the configure command.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Exit code of the command.</returns>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("\n==== CMSAgent - Initial Configuration ====\n");
            Console.WriteLine("This command will set up basic configuration for the agent, including room information and position.");
            Console.WriteLine("You can cancel the process at any time by pressing Ctrl+C.");
            Console.WriteLine();

            try
            {
                // Load current runtime configuration (if exists)
                var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
                
                // Create new configuration if none exists
                if (runtimeConfig == null)
                {
                    // If no configuration exists, create new with agentId
                    runtimeConfig = new RuntimeConfig
                    {
                        AgentId = GenerateAgentId(),
                        RoomConfig = new Common.Models.RoomConfig
                        {
                            RoomName = "Default" // Will be updated later
                        },
                        AgentTokenEncrypted = "" // Will be updated after receiving token from server
                    };
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                }

                Console.WriteLine($"Agent ID: {runtimeConfig.AgentId}");
                Console.WriteLine();

                // Enter room name
                string roomName = PromptForInput(
                    "Enter room name (e.g., R101, R102): ",
                    runtimeConfig.RoomConfig?.RoomName);

                if (string.IsNullOrWhiteSpace(roomName))
                {
                    Console.Error.WriteLine("Room name cannot be empty.");
                    return (int)CliExitCodes.InvalidInput;
                }

                // Enter X coordinate
                if (!TryParseInt("Enter X coordinate in the room (integer): ", out int posX, runtimeConfig.RoomConfig?.PosX.ToString()))
                {
                    Console.Error.WriteLine("X coordinate must be an integer.");
                    return (int)CliExitCodes.InvalidInput;
                }

                // Enter Y coordinate
                if (!TryParseInt("Enter Y coordinate in the room (integer): ", out int posY, runtimeConfig.RoomConfig?.PosY.ToString()))
                {
                    Console.Error.WriteLine("Y coordinate must be an integer.");
                    return (int)CliExitCodes.InvalidInput;
                }

                // Update room configuration
                runtimeConfig.RoomConfig = new Common.Models.RoomConfig
                {
                    RoomName = roomName,
                    PosX = posX,
                    PosY = posY
                };

                // Create registration request
                var identifyRequest = new AgentIdentifyRequest
                {
                    agentId = runtimeConfig.AgentId,
                    positionInfo = new PositionInfo
                    {
                        roomName = roomName,
                        posX = posX,
                        posY = posY
                    },
                    forceRenewToken = true
                };

                Console.WriteLine("\nSending information to server...");

                try
                {
                    // Send registration request to server
                    var response = await _httpClient.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse?>(
                        ApiRoutes.Identify, 
                        identifyRequest,
                        string.Empty,  // agentId not needed for identify API
                        null   // token not available at this step
                    );

                    // Process server response
                    if (response != null)
                    {
                        if (response.status == "success")
                        {
                            if (!string.IsNullOrEmpty(response.agentToken))
                            {
                                // Encrypt and save token
                                runtimeConfig.AgentTokenEncrypted = _tokenProtector.EncryptToken(response.agentToken);
                                Console.WriteLine("\nAuthentication successful!");
                            }
                            else
                            {
                                // Current token is still valid
                                Console.WriteLine("\nAgent has been previously registered.");
                            }
                        }
                        else if (response.status == "mfa_required")
                        {
                            Console.WriteLine("\nMulti-factor authentication (MFA) required.");
                            
                            // Handle MFA
                            var mfaResult = await HandleMfaVerificationAsync(runtimeConfig.AgentId, cancellationToken);
                            if (!mfaResult)
                            {
                                return (int)CliExitCodes.ServerConnectionFailed;
                            }
                        }
                        else if (response.status == "position_error")
                        {
                            Console.Error.WriteLine($"\nError: {response.message}");
                            Console.WriteLine("Please run the configure command again and choose a different position.");
                            return (int)CliExitCodes.ServerConnectionFailed;
                        }
                        else
                        {
                            // Other errors
                            Console.Error.WriteLine($"\nServer error: {response.message}");
                            return (int)CliExitCodes.ServerConnectionFailed;
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("\nNo response received from server.");
                        return (int)CliExitCodes.ServerConnectionFailed;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\nError sending information to server: {ex.Message}");
                    return (int)CliExitCodes.ServerConnectionFailed;
                }

                // Save configuration
                try
                {
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                    Console.WriteLine("\nConfiguration saved successfully.");
                    Console.WriteLine("\nAgent is ready to operate. You can start the service with: CMSAgent.exe start");
                    return (int)CliExitCodes.Success;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\nError saving configuration: {ex.Message}");
                    return (int)CliExitCodes.ConfigSaveFailed;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nConfiguration process canceled by user.");
                return (int)CliExitCodes.UserCancelled;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nUnidentified error: {ex.Message}");
                return (int)CliExitCodes.GeneralError;
            }
        }

        /// <summary>
        /// Handles MFA verification.
        /// </summary>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>True if verification is successful, otherwise False.</returns>
        private async Task<bool> HandleMfaVerificationAsync(string agentId, CancellationToken cancellationToken)
        {
            // Prompt user to enter MFA code
            var mfaCode = PromptForInput("Enter authentication code (MFA):");

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(mfaCode))
            {
                Console.Error.WriteLine("Invalid authentication code.");
                return false;
            }

            try
            {
                // Send MFA code to server
                var mfaRequest = new VerifyMfaRequest
                {
                    agentId = agentId,
                    mfaCode = mfaCode
                };

                var response = await _httpClient.PostAsync<VerifyMfaRequest, VerifyMfaResponse?>(
                    ApiRoutes.VerifyMfa,
                    mfaRequest,
                    agentId,
                    string.Empty // No token at this step, use empty string instead of null
                );

                if (response != null && response.status == "success" && !string.IsNullOrEmpty(response.agentToken))
                {
                    // Encrypt and save token
                    var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
                    runtimeConfig.AgentTokenEncrypted = _tokenProtector.EncryptToken(response.agentToken);
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                    Console.WriteLine("MFA verification successful!");
                    return true;
                }
                else
                {
                    var errorMessage = response?.message ?? "No valid response received from server.";
                    Console.Error.WriteLine($"MFA verification failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during MFA verification: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Generates a unique Agent ID.
        /// </summary>
        /// <returns>Unique Agent ID.</returns>
        private string GenerateAgentId()
        {
            string hostName = Environment.MachineName;
            string macAddress = GetMacAddress();
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return $"AGENT-{hostName}-{macAddress}-{timestamp}";
        }

        /// <summary>
        /// Gets the MAC address of the first network adapter.
        /// </summary>
        /// <returns>MAC address as a string.</returns>
        private string GetMacAddress()
        {
            try
            {
                var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in networkInterfaces)
                {
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                        !nic.Description.ToLower().Contains("virtual", StringComparison.OrdinalIgnoreCase) && 
                        !nic.Description.ToLower().Contains("pseudo", StringComparison.OrdinalIgnoreCase))
                    {
                        var mac = nic.GetPhysicalAddress();
                        if (mac != null)
                        {
                            return Convert.ToHexString(mac.GetAddressBytes());
                        }
                    }
                }
                
                // Fallback if no suitable MAC is found
                return Guid.NewGuid().ToString("N")[..12].ToUpper();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to retrieve MAC address, using GUID instead");
                return Guid.NewGuid().ToString("N")[..12].ToUpper();
            }
        }

        /// <summary>
        /// Prompts the user for input.
        /// </summary>
        /// <param name="promptMessage">Prompt message.</param>
        /// <param name="defaultValue">Default value (can be null).</param>
        /// <returns>User input string.</returns>
        private static string PromptForInput(string promptMessage, string? defaultValue = null)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write($"{promptMessage} [{defaultValue}]: ");
            }
            else
            {
                Console.Write($"{promptMessage} ");
            }

            string? input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            return input ?? string.Empty;
        }

        /// <summary>
        /// Displays a prompt and gets an integer input from the user.
        /// </summary>
        /// <param name="promptMessage">Message to display.</param>
        /// <param name="value">Output integer value.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <returns>True if parsing is successful, otherwise False.</returns>
        private static bool TryParseInt(string promptMessage, out int value, string? defaultValue = null)
        {
            string input = PromptForInput(promptMessage, defaultValue);
            
            if (int.TryParse(input, out value))
            {
                return true;
            }
            
            Console.Error.WriteLine("Invalid value. Please enter an integer.");
            value = 0;
            return false;
        }
    }
}
