using CMSAgent.Service.Communication.Http;
using CMSAgent.Service.Configuration.Manager;
using CMSAgent.Service.Configuration.Models;
using CMSAgent.Service.Security;
using CMSAgent.Shared.Enums;
using Microsoft.Extensions.Options;

namespace CMSAgent.Service.Configuration
{
    public class AgentConfigurator
    {
        private readonly ILogger<AgentConfigurator> _logger;
        private readonly IRuntimeConfigManager _runtimeConfigManager;
        private readonly IDpapiProtector _dpapiProtector;
        private readonly IAgentApiClient _apiClient;
        private readonly AppSettings _appSettings;

        public AgentConfigurator(
            ILogger<AgentConfigurator> logger,
            IRuntimeConfigManager runtimeConfigManager,
            IDpapiProtector dpapiProtector,
            IAgentApiClient apiClient,
            IOptions<AppSettings> appSettingsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeConfigManager = runtimeConfigManager ?? throw new ArgumentNullException(nameof(runtimeConfigManager));
            _dpapiProtector = dpapiProtector ?? throw new ArgumentNullException(nameof(dpapiProtector));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));
        }

        public async Task<bool> RunInitialConfigurationAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Agent's initial configuration process...");

            // 1. Get or create AgentId
            string? agentId = await _runtimeConfigManager.GetAgentIdAsync();
            if (string.IsNullOrWhiteSpace(agentId))
            {
                agentId = Guid.NewGuid().ToString();
                await _runtimeConfigManager.UpdateAgentIdAsync(agentId);
                _logger.LogInformation("New AgentId has been created: {AgentId}", agentId);
            }
            else
            {
                _logger.LogInformation("Using existing AgentId: {AgentId}", agentId);
            }

            // 2. Request user to input position information
            Console.WriteLine($"--- CMS Agent Configuration ---");
            Console.WriteLine($"Agent ID: {agentId}");
            Console.Write("Enter room name (Room Name): ");
            string? roomName = Console.ReadLine()?.Trim();
            Console.Write("Enter X coordinate (PosX - integer): ");
            string? posXStr = Console.ReadLine()?.Trim();
            Console.Write("Enter Y coordinate (PosY - integer): ");
            string? posYStr = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(posXStr) || string.IsNullOrWhiteSpace(posYStr) || 
                !int.TryParse(posXStr, out int posX) || !int.TryParse(posYStr, out int posY) || posX < 0 || posY < 0)
            {
                _logger.LogError("Invalid position information.");
                Console.WriteLine("Error: Invalid position information. Please enter correct format.");
                return false;
            }
            var positionInfo = new PositionInfo { RoomName = roomName, PosX = posX, PosY = posY };

            // 3. Authenticate with Server (Identify Flow)
            _logger.LogInformation("Sending Identify request to server...");
            var (status, receivedToken, errorMessage) = await _apiClient.IdentifyAgentAsync(agentId, positionInfo, cancellationToken: cancellationToken);

            if (status == "mfa_required")
            {
                _logger.LogInformation("Server requires MFA.");
                Console.Write("Enter MFA code (OTP): ");
                string? mfaCode = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(mfaCode))
                {
                    _logger.LogError("MFA code not entered.");
                    Console.WriteLine("Error: MFA code cannot be empty.");
                    return false;
                }

                // Retry Identify with MFA code
                (status, receivedToken, errorMessage) = await _apiClient.VerifyMfaAsync(agentId, mfaCode, cancellationToken);
            }

            if (status != "success" || string.IsNullOrWhiteSpace(receivedToken))
            {
                _logger.LogError("Agent identification failed. Status: {Status}, Error: {ErrorMessage}", status, errorMessage);
                Console.WriteLine($"Error: Agent identification failed. {errorMessage}");
                
                // Check if error is due to server connection failure
                if (errorMessage.Contains("Network error") || errorMessage.Contains("connection attempt failed"))
                {
                    Console.WriteLine("\nPlease check the CMSAgent.iss installation file:");
                    Console.WriteLine("1. Ensure the server is running and accessible");
                    Console.WriteLine("2. Verify the server address in appsettings.json");
                    Console.WriteLine("3. Check network connectivity");
                    Console.WriteLine("\nThen follow these steps:");
                    Console.WriteLine("1. Uninstall current CMSAgent");
                    Console.WriteLine("2. Delete C:\\ProgramData\\CMSAgent directory");
                    Console.WriteLine("3. Reinstall CMSAgent");
                }
                
                return false;
            }

            // 4. Save configuration
            try
            {
                // Save position info
                await _runtimeConfigManager.UpdatePositionInfoAsync(positionInfo);

                // Encrypt and save token
                string? encryptedToken = _dpapiProtector.Protect(receivedToken);
                if (string.IsNullOrWhiteSpace(encryptedToken))
                {
                    _logger.LogError("Failed to encrypt token.");
                    Console.WriteLine("Error: Failed to encrypt token.");
                    return false;
                }
                await _runtimeConfigManager.UpdateEncryptedAgentTokenAsync(encryptedToken);

                _logger.LogInformation("Initial configuration completed successfully.");
                Console.WriteLine("Configuration completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration.");
                Console.WriteLine($"Error: Failed to save configuration. {ex.Message}");
                return false;
            }
        }
    }
} 