using CMSAgent.Communication; // Added for IServerConnector
using CMSAgent.Configuration; // For RoomConfig
using CMSAgent.Models.Payloads; // Added for IdentifyRequest, IdentifyResponse, VerifyMfaRequest, VerifyMfaResponse
using Serilog;
using System; // Added for Console, OperationCanceledException, etc.
using System.Text.RegularExpressions;
using System.Threading; // Added for CancellationToken
using System.Threading.Tasks; // Added for Task

namespace CMSAgent.UserInterface
{
    /// <summary>
    /// Console user interface for agent configuration
    /// </summary>
    public class ConsoleUI
    {
        private readonly StaticConfigProvider _staticConfigProvider; // Renamed from _configProvider
        private readonly RuntimeStateManager _runtimeStateManager;
        private readonly IServerConnector _serverConnector; // Added

        /// <summary>
        /// Creates a new instance of the ConsoleUI class
        /// </summary>
        public ConsoleUI(
            StaticConfigProvider staticConfigProvider, // Renamed
            RuntimeStateManager runtimeStateManager,
            IServerConnector serverConnector) // Added
        {
            _staticConfigProvider = staticConfigProvider; // Renamed
            _runtimeStateManager = runtimeStateManager;
            _serverConnector = serverConnector; // Added
        }

        /// <summary>
        /// Performs the initial configuration of the agent as per Standard.md II.3
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if configuration is successful, false otherwise.</returns>
        public async Task<bool> PerformInitialConfigurationAsync(CancellationToken cancellationToken)
        {
            Console.Clear();
            Console.WriteLine("CMS Agent Configuration Utility");
            Console.WriteLine("---------------------------------");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Device ID is already initialized by RuntimeStateManager constructor or its InitializeAsync
                // and available via _runtimeStateManager.DeviceId
                Console.WriteLine($"Đang kiểm tra/tạo Device ID... Device ID: {_runtimeStateManager.DeviceId}");
                Log.Information("Device ID for configuration: {DeviceId}", _runtimeStateManager.DeviceId);

                string? agentToken = null;
                RoomConfigPoco roomConfigForSave = new RoomConfigPoco();

                while (!cancellationToken.IsCancellationRequested)
                {
                    // 1. Vòng Lặp Nhập Thông Tin Vị Trí
                    Console.Write("Vui lòng nhập tên phòng (Room Name): ");
                    string roomName = Console.ReadLine() ?? string.Empty;
                    if (cancellationToken.IsCancellationRequested) break;
                    while (string.IsNullOrWhiteSpace(roomName) && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Tên phòng không được trống.");
                        Console.Write("Vui lòng nhập tên phòng (Room Name): ");
                        roomName = Console.ReadLine() ?? string.Empty;
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                    if (cancellationToken.IsCancellationRequested) break;
                    roomConfigForSave.roomName = roomName;

                    Console.Write("Vui lòng nhập tọa độ X: ");
                    string posXStr = Console.ReadLine() ?? string.Empty;
                    if (cancellationToken.IsCancellationRequested) break;
                    int posX;
                    while (!int.TryParse(posXStr, out posX) && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Tọa độ X phải là số.");
                        Console.Write("Vui lòng nhập tọa độ X: ");
                        posXStr = Console.ReadLine() ?? string.Empty;
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                    if (cancellationToken.IsCancellationRequested) break;
                    roomConfigForSave.posX = posX.ToString();

                    Console.Write("Vui lòng nhập tọa độ Y: ");
                    string posYStr = Console.ReadLine() ?? string.Empty;
                    if (cancellationToken.IsCancellationRequested) break;
                    int posY;
                    while (!int.TryParse(posYStr, out posY) && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Tọa độ Y phải là số.");
                        Console.Write("Vui lòng nhập tọa độ Y: ");
                        posYStr = Console.ReadLine() ?? string.Empty;
                        if (cancellationToken.IsCancellationRequested) break;
                    }
                    if (cancellationToken.IsCancellationRequested) break;
                    roomConfigForSave.posY = posY.ToString();

                    Console.WriteLine("Đang gửi thông tin đến server...");

                    // 2. Gửi Yêu Cầu Định Danh Server
                    var identifyRequest = new IdentifyRequest
                    {
                        unique_agent_id = _runtimeStateManager.DeviceId,
                        positionInfo = new PositionInfo { roomName = roomName, posX = posX, posY = posY },
                        forceRenewToken = false
                    };

                    IdentifyResponse? identifyResponse = null;
                    try
                    {
                        identifyResponse = await _serverConnector.HttpChannel.PostAsync<IdentifyRequest, IdentifyResponse>(
                            ApiEndpoints.Identify,
                            identifyRequest,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Lỗi khi gửi yêu cầu định danh đến server.");
                        Console.WriteLine($"Lỗi: Không thể kết nối đến server hoặc server gặp lỗi không xác định. Chi tiết: {ex.Message}");
                        if (!AskToRetry(cancellationToken)) return false;
                        continue; // Retry from room input
                    }

                    if (identifyResponse == null)
                    {
                        Console.WriteLine("Lỗi: Không nhận được phản hồi từ server.");
                        if (!AskToRetry(cancellationToken)) return false;
                        continue; // Retry from room input
                    }

                    // 3. Xử Lý Phản Hồi Từ Server
                    switch (identifyResponse.status)
                    {
                        case "success":
                            if (!string.IsNullOrEmpty(identifyResponse.agentToken))
                            {
                                agentToken = identifyResponse.agentToken;
                                Console.WriteLine("Định danh agent thành công. Token đã nhận.");
                                Log.Information("Agent identified successfully, new token received.");
                            }
                            else
                            {
                                Log.Information("Agent identified successfully (already registered, token still valid). Attempting to load local token.");
                                agentToken = await _runtimeStateManager.GetAgentTokenAsync(cancellationToken);
                                if (string.IsNullOrEmpty(agentToken))
                                {
                                    Log.Error("Agent đã đăng ký nhưng không tìm thấy token cục bộ và server không cấp token mới. Cấu hình thất bại.");
                                    Console.WriteLine("Lỗi: Agent đã đăng ký nhưng không thể tải token. Vui lòng liên hệ quản trị viên.");
                                    return false; // Critical failure
                                }
                                Console.WriteLine("Agent đã được đăng ký trước đó. Sử dụng token hiện tại.");
                            }
                            // Save runtime config and token
                            await _runtimeStateManager.UpdateRoomConfigAsync(roomConfigForSave, cancellationToken);
                            await _runtimeStateManager.SaveAgentTokenAsync(agentToken, cancellationToken);
                            Console.WriteLine("Đã lưu cấu hình và token.");
                            return true; // Configuration successful

                        case "position_error":
                            Console.WriteLine($"Lỗi: Thông tin vị trí không hợp lệ hoặc đã được sử dụng. Chi tiết từ server: {identifyResponse.message}");
                            Log.Warning("Position error from server: {Message}", identifyResponse.message);
                            if (!AskToRetry(cancellationToken, "Bạn có muốn thử nhập lại thông tin vị trí không? (Y/N): ")) return false;
                            continue; // Retry from room input

                        case "mfa_required":
                            Console.WriteLine("Xác thực thành công bước đầu. Server yêu cầu Xác thực Đa Yếu Tố (MFA).");
                            Log.Information("MFA required by server.");

                            bool mfaSuccess = false;
                            while (!mfaSuccess && !cancellationToken.IsCancellationRequested)
                            {
                                Console.Write("Vui lòng nhập mã MFA từ ứng dụng xác thực của bạn: ");
                                string mfaCode = Console.ReadLine() ?? string.Empty;
                                if (cancellationToken.IsCancellationRequested) break;
                                if (string.IsNullOrWhiteSpace(mfaCode))
                                {
                                    Console.WriteLine("Đã hủy nhập MFA.");
                                    if (!AskToRetry(cancellationToken, "Bạn có muốn thử lại quy trình định danh (nhập lại vị trí)? (Y/N): ")) return false;
                                    goto NextIteration; // Retry from room input by breaking inner and continuing outer
                                }

                                var verifyMfaRequest = new VerifyMfaRequest
                                {
                                    unique_agent_id = _runtimeStateManager.DeviceId,
                                    mfa_code = mfaCode
                                };

                                VerifyMfaResponse? mfaResponse = null;
                                try
                                {
                                    mfaResponse = await _serverConnector.HttpChannel.PostAsync<VerifyMfaRequest, VerifyMfaResponse>(
                                        ApiEndpoints.VerifyMfa,
                                        verifyMfaRequest,
                                        cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Lỗi khi gửi yêu cầu xác thực MFA đến server.");
                                    Console.WriteLine($"Lỗi: Không thể kết nối đến server hoặc server gặp lỗi không xác định khi xác thực MFA. Chi tiết: {ex.Message}");
                                    if (!AskToRetry(cancellationToken, "Bạn có muốn thử lại quy trình định danh (nhập lại vị trí)? (Y/N): ")) return false;
                                    goto NextIteration; // Retry from room input
                                }

                                if (mfaResponse == null)
                                {
                                    Console.WriteLine("Lỗi: Không nhận được phản hồi MFA từ server.");
                                    if (!AskToRetry(cancellationToken, "Bạn có muốn thử nhập lại mã MFA không? (Y/N): "))
                                    {
                                        if (!AskToRetry(cancellationToken, "Bạn có muốn thử lại quy trình định danh (nhập lại vị trí)? (Y/N): ")) return false;
                                        goto NextIteration; // Retry from room input
                                    }
                                    continue; // Retry MFA input
                                }

                                if (mfaResponse.status == "success" && !string.IsNullOrEmpty(mfaResponse.agentToken))
                                {
                                    agentToken = mfaResponse.agentToken;
                                    Console.WriteLine("Xác thực MFA thành công.");
                                    Log.Information("MFA verified successfully, new token received.");
                                    mfaSuccess = true;

                                    // Save runtime config and token
                                    await _runtimeStateManager.UpdateRoomConfigAsync(roomConfigForSave, cancellationToken);
                                    await _runtimeStateManager.SaveAgentTokenAsync(agentToken, cancellationToken);
                                    Console.WriteLine("Đã lưu cấu hình và token.");
                                    return true; // Configuration successful
                                }
                                else
                                {
                                    Console.WriteLine($"Lỗi: Mã MFA không chính xác hoặc đã hết hạn. Chi tiết từ server: {mfaResponse.message}");
                                    Log.Warning("MFA verification failed: {Message}", mfaResponse.message);
                                    if (!AskToRetry(cancellationToken, "Bạn có muốn thử nhập lại không? (Y/N): "))
                                    {
                                        if (!AskToRetry(cancellationToken, "Bạn có muốn thử lại quy trình định danh (nhập lại vị trí)? (Y/N): ")) return false;
                                        goto NextIteration; // Retry from room input
                                    }
                                }
                            }
                            if (cancellationToken.IsCancellationRequested) break;
                            if (!mfaSuccess)
                            {
                                if (!AskToRetry(cancellationToken, "Bạn có muốn thử lại quy trình định danh (nhập lại vị trí)? (Y/N): ")) return false;
                                continue; // Retry from room input
                            }
                            break;

                        default:
                            Console.WriteLine($"Lỗi: Không thể kết nối đến server hoặc server gặp lỗi không xác định. Phản hồi từ server: Status='{identifyResponse.status}', Message='{identifyResponse.message}'");
                            Log.Error("Unhandled server response during identify: Status={Status}, Message={Message}", identifyResponse.status, identifyResponse.message);
                            if (!AskToRetry(cancellationToken)) return false;
                            continue; // Retry from room input
                    }
                    NextIteration:;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    Log.Information("Initial configuration cancelled by user.");
                    Console.WriteLine("\nĐã hủy cấu hình.");
                    return false;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Initial configuration was cancelled.");
                Console.WriteLine("\nĐã hủy cấu hình.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi nghiêm trọng trong quá trình cấu hình ban đầu: {Message}", ex.Message);
                Console.WriteLine($"\nLỗi không mong muốn: {ex.Message}");
                return false;
            }
        }

        public async Task RunConfigureLogicAsync()
        {
            _logger.LogInformation("Starting agent configuration process.");
            var runtimeConfig = _runtimeStateManager.LoadConfig();

            // 1. Tạo/Kiểm Tra device_id (Standard.md III.3)
            if (string.IsNullOrEmpty(runtimeConfig.DeviceId))
            {
                _logger.LogInformation("Device ID not found. Generating new Device ID.");
                // Ví dụ đơn giản: Kết hợp Hostname và một GUID. Cần đảm bảo tính duy nhất.
                // Standard.md gợi ý: kết hợp hostname và MAC address.
                // Việc lấy MAC address có thể phức tạp và cần quyền, cân nhắc giải pháp phù hợp.
                var newDeviceId = $"AGENT-{Environment.MachineName.ToUpperInvariant()}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}";
                runtimeConfig.DeviceId = newDeviceId;
                _runtimeStateManager.SetDeviceId(newDeviceId); // Cập nhật vào state manager
                _logger.LogInformation("Generated Device ID: {DeviceId}", newDeviceId);
            }
            else
            {
                _logger.LogInformation("Existing Device ID found: {DeviceId}", runtimeConfig.DeviceId);
            }
            Console.WriteLine($"Device ID: {runtimeConfig.DeviceId}");

            // 2. Vòng Lặp Nhập Thông Tin Vị Trí (Standard.md III.3)
            RoomConfig newRoomConfig = new RoomConfig();
            bool positionValid = false;
            IdentifyResponsePayload? identifyResponse = null;

            while (!positionValid)
            {
                Console.Write("Vui lòng nhập tên phòng (Room Name): ");
                newRoomConfig.RoomName = Console.ReadLine() ?? string.Empty;

                Console.Write("Vui lòng nhập tọa độ X (posX): ");
                newRoomConfig.PosX = Console.ReadLine() ?? string.Empty;

                Console.Write("Vui lòng nhập tọa độ Y (posY): ");
                newRoomConfig.PosY = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(newRoomConfig.RoomName) ||
                    !int.TryParse(newRoomConfig.PosX, out int posXInt) || // Standard.md: posX/posY phải là số
                    !int.TryParse(newRoomConfig.PosY, out int posYInt))
                {
                    Console.WriteLine("Lỗi: Tên phòng không được trống và posX, posY phải là số. Vui lòng nhập lại.");
                    continue;
                }

                // 3. Gửi Yêu Cầu Định Danh Server (Standard.md III.3)
                var identifyPayload = new IdentifyRequestPayload
                {
                    UniqueAgentId = runtimeConfig.DeviceId,
                    PositionInfo = new PositionInfoPayload 
                    { 
                        RoomName = newRoomConfig.RoomName, 
                        PosX = posXInt, 
                        PosY = posYInt 
                    },
                    ForceRenewToken = true // Luôn yêu cầu token mới khi chạy configure
                };

                Console.WriteLine("Đang gửi thông tin định danh đến server...");
                identifyResponse = await _serverConnector.IdentifyAgentAsync(identifyPayload);

                if (identifyResponse == null)
                {
                    Console.WriteLine("Lỗi: Không nhận được phản hồi từ server. Bạn có muốn thử lại không? (Y/N): ");
                    if (Console.ReadLine()?.Trim().ToUpperInvariant() != "Y") Environment.Exit(1); // Thoát nếu không thử lại
                    continue;
                }

                switch (identifyResponse.Status)
                {
                    case "success":
                        _logger.LogInformation("Server accepted position. Agent token received/confirmed.");
                        positionValid = true;
                        break;
                    case "position_error":
                        Console.WriteLine($"Lỗi từ server: {identifyResponse.Message}. Vui lòng kiểm tra lại thông tin vị trí.");
                        // Vòng lặp sẽ tiếp tục để người dùng nhập lại
                        break;
                    case "mfa_required":
                        _logger.LogInformation("MFA is required by server.");
                        Console.WriteLine($"Server yêu cầu xác thực MFA. {identifyResponse.Message}");
                        Console.Write("Vui lòng nhập mã MFA: ");
                        string mfaCode = Console.ReadLine() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(mfaCode))
                        {
                            Console.WriteLine("Mã MFA không được trống. Thử lại từ đầu.");
                            continue; // Hoặc có thể cho phép thử lại chỉ bước MFA
                        }

                        var mfaPayload = new VerifyMfaRequestPayload
                        {
                            UniqueAgentId = runtimeConfig.DeviceId,
                            MfaCode = mfaCode
                        };
                        var mfaResponse = await _serverConnector.VerifyMfaAsync(mfaPayload);
                        if (mfaResponse?.Status == "success")
                        {
                            _logger.LogInformation("MFA verification successful. Agent token received.");
                            identifyResponse.AgentToken = mfaResponse.AgentToken; // Lấy token từ phản hồi MFA
                            identifyResponse.Status = "success"; // Đánh dấu là thành công để lưu cấu hình
                            positionValid = true;
                        }
                        else
                        {
                            Console.WriteLine($"Lỗi xác thực MFA: {mfaResponse?.Message ?? "Không rõ lỗi"}. Vui lòng thử lại từ đầu.");
                            // Vòng lặp sẽ tiếp tục để người dùng nhập lại thông tin vị trí và MFA
                        }
                        break;
                    default:
                        Console.WriteLine($"Lỗi không xác định từ server: {identifyResponse.Message}. Bạn có muốn thử lại không? (Y/N): ");
                        if (Console.ReadLine()?.Trim().ToUpperInvariant() != "Y") Environment.Exit(1);
                        break;
                }
            }

            // 4. Lưu Trữ Cấu Hình Runtime và Token (Standard.md III.4)
            if (identifyResponse != null && identifyResponse.Status == "success" && !string.IsNullOrEmpty(identifyResponse.AgentToken))
            {
                runtimeConfig.RoomConfig = newRoomConfig; // Lưu roomConfig với posX, posY là chuỗi
                // Mã hóa token trước khi lưu
                var encryptedToken = _cryptoHelper.Encrypt(identifyResponse.AgentToken);
                runtimeConfig.AgentTokenEncrypted = encryptedToken;
                _runtimeStateManager.SetRoomConfig(newRoomConfig);
                _runtimeStateManager.SetEncryptedAgentToken(encryptedToken);
                _runtimeStateManager.SaveConfig(runtimeConfig); // Lưu toàn bộ runtimeConfig vào file
                Console.WriteLine("Đã lưu cấu hình và token thành công.");
                _logger.LogInformation("Runtime configuration and agent token saved successfully.");
                Environment.ExitCode = 0; // Thành công
            }
            else
            {
                Console.WriteLine("Không thể hoàn tất cấu hình do lỗi hoặc thiếu token từ server.");
                _logger.LogError("Configuration failed or agent token was not provided by the server.");
                Environment.ExitCode = 1; // Lỗi
            }
        }

        private bool AskToRetry(CancellationToken cancellationToken, string prompt = "Bạn có muốn thử lại không? (Y/N): ")
        {
            if (cancellationToken.IsCancellationRequested) return false;
            Console.Write(prompt);
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (cancellationToken.IsCancellationRequested) return false;
            return input == "Y";
        }
    }
}