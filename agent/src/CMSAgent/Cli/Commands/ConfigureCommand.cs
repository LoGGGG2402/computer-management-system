using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Cli.Commands
{
    /// <summary>
    /// Lớp xử lý lệnh configure để thiết lập cấu hình ban đầu cho agent.
    /// </summary>
    public class ConfigureCommand
    {
        private readonly ILogger<ConfigureCommand> _logger;
        private readonly IConfigLoader _configLoader;
        private readonly IHttpClientWrapper _httpClient;
        private readonly TokenProtector _tokenProtector;

        /// <summary>
        /// Khởi tạo một instance mới của ConfigureCommand.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="configLoader">ConfigLoader để tải và lưu cấu hình.</param>
        /// <param name="httpClient">HttpClient để giao tiếp với server.</param>
        /// <param name="tokenProtector">TokenProtector để mã hóa token.</param>
        public ConfigureCommand(
            ILogger<ConfigureCommand> logger,
            IConfigLoader configLoader,
            IHttpClientWrapper httpClient,
            TokenProtector tokenProtector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));
        }

        /// <summary>
        /// Thực thi lệnh configure.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Mã lỗi của lệnh.</returns>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("\n==== CMSAgent - Cấu hình Ban Đầu ====\n");
            Console.WriteLine("Lệnh này sẽ thiết lập cấu hình cơ bản cho agent, bao gồm thông tin phòng và vị trí.");
            Console.WriteLine("Bạn có thể hủy quá trình bất kỳ lúc nào bằng cách nhấn Ctrl+C.");
            Console.WriteLine();

            try
            {
                // Tải cấu hình runtime hiện tại (nếu có)
                var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
                
                // Tạo cấu hình mới nếu chưa có
                if (runtimeConfig == null)
                {
                    // Nếu chưa có cấu hình, tạo mới với agentId
                    runtimeConfig = new RuntimeConfig
                    {
                        AgentId = GenerateAgentId(),
                        RoomConfig = new Common.Models.RoomConfig
                        {
                            RoomName = "Default" // Sẽ được cập nhật sau
                        },
                        AgentTokenEncrypted = "" // Sẽ được cập nhật sau khi nhận token từ server
                    };
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                }

                Console.WriteLine($"Agent ID: {runtimeConfig.AgentId}");
                Console.WriteLine();

                // Nhập tên phòng
                string roomName = PromptForInput(
                    "Nhập tên phòng (ví dụ: P101, P102): ",
                    runtimeConfig.RoomConfig?.RoomName);

                if (string.IsNullOrWhiteSpace(roomName))
                {
                    Console.Error.WriteLine("Tên phòng không được để trống.");
                    return (int)CliExitCodes.InvalidInput;
                }

                // Nhập tọa độ X
                if (!TryParseInt("Nhập tọa độ X trong phòng (số nguyên): ", out int posX, runtimeConfig.RoomConfig?.PosX.ToString()))
                {
                    Console.Error.WriteLine("Tọa độ X phải là số nguyên.");
                    return (int)CliExitCodes.InvalidInput;
                }

                // Nhập tọa độ Y
                if (!TryParseInt("Nhập tọa độ Y trong phòng (số nguyên): ", out int posY, runtimeConfig.RoomConfig?.PosY.ToString()))
                {
                    Console.Error.WriteLine("Tọa độ Y phải là số nguyên.");
                    return (int)CliExitCodes.InvalidInput;
                }

                // Cập nhật cấu hình phòng
                runtimeConfig.RoomConfig = new Common.Models.RoomConfig
                {
                    RoomName = roomName,
                    PosX = posX,
                    PosY = posY
                };

                // Tạo yêu cầu đăng ký
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

                Console.WriteLine("\nĐang gửi thông tin đến server...");

                try
                {
                    // Gửi yêu cầu đăng ký đến server
                    var response = await _httpClient.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse?>(
                        ApiRoutes.Identify, 
                        identifyRequest,
                        string.Empty,  // agentId không cần cho API identify
                        null   // token chưa có ở bước này
                    );

                    // Xử lý phản hồi từ server
                    if (response != null)
                    {
                        if (response.status == "success")
                        {
                            if (!string.IsNullOrEmpty(response.agentToken))
                            {
                                // Mã hóa và lưu token
                                runtimeConfig.AgentTokenEncrypted = _tokenProtector.EncryptToken(response.agentToken);
                                Console.WriteLine("\nXác thực thành công!");
                            }
                            else
                            {
                                // Token hiện tại vẫn hợp lệ
                                Console.WriteLine("\nAgent đã được đăng ký trước đó.");
                            }
                        }
                        else if (response.status == "mfa_required")
                        {
                            Console.WriteLine("\nYêu cầu xác thực hai yếu tố (MFA).");
                            
                            // Xử lý MFA
                            var mfaResult = await HandleMfaVerificationAsync(runtimeConfig.AgentId, cancellationToken);
                            if (!mfaResult)
                            {
                                return (int)CliExitCodes.ServerConnectionFailed;
                            }
                        }
                        else if (response.status == "position_error")
                        {
                            Console.Error.WriteLine($"\nLỗi: {response.message}");
                            Console.WriteLine("Vui lòng chạy lại lệnh configure và chọn vị trí khác.");
                            return (int)CliExitCodes.ServerConnectionFailed;
                        }
                        else
                        {
                            // Lỗi khác
                            Console.Error.WriteLine($"\nLỗi từ server: {response.message}");
                            return (int)CliExitCodes.ServerConnectionFailed;
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("\nKhông nhận được phản hồi từ server.");
                        return (int)CliExitCodes.ServerConnectionFailed;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\nLỗi khi gửi thông tin đến server: {ex.Message}");
                    return (int)CliExitCodes.ServerConnectionFailed;
                }

                // Lưu cấu hình
                try
                {
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                    Console.WriteLine("\nĐã lưu cấu hình thành công.");
                    Console.WriteLine("\nAgent đã sẵn sàng hoạt động. Bạn có thể khởi động service bằng lệnh: CMSAgent.exe start");
                    return (int)CliExitCodes.Success;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\nLỗi khi lưu cấu hình: {ex.Message}");
                    return (int)CliExitCodes.ConfigSaveFailed;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nQuá trình cấu hình đã bị hủy bởi người dùng.");
                return (int)CliExitCodes.UserCancelled;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nLỗi không xác định: {ex.Message}");
                return (int)CliExitCodes.GeneralError;
            }
        }

        /// <summary>
        /// Xử lý xác thực MFA.
        /// </summary>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>True nếu xác thực thành công, ngược lại là False.</returns>
        private async Task<bool> HandleMfaVerificationAsync(string agentId, CancellationToken cancellationToken)
        {
            // Nhắc người dùng nhập mã MFA
            var mfaCode = PromptForInput("Nhập mã xác thực (MFA):");

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(mfaCode))
            {
                Console.Error.WriteLine("Mã xác thực không hợp lệ.");
                return false;
            }

            try
            {
                // Gửi mã MFA đến server
                var mfaRequest = new VerifyMfaRequest
                {
                    agentId = agentId,
                    mfaCode = mfaCode
                };

                var response = await _httpClient.PostAsync<VerifyMfaRequest, VerifyMfaResponse?>(
                    ApiRoutes.VerifyMfa,
                    mfaRequest,
                    agentId,
                    string.Empty // Không có token ở bước này, sử dụng chuỗi rỗng thay vì null
                );

                if (response != null && response.status == "success" && !string.IsNullOrEmpty(response.agentToken))
                {
                    // Mã hóa và lưu token
                    var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
                    runtimeConfig.AgentTokenEncrypted = _tokenProtector.EncryptToken(response.agentToken);
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                    Console.WriteLine("Xác thực MFA thành công!");
                    return true;
                }
                else
                {
                    var errorMessage = response?.message ?? "Không nhận được phản hồi hợp lệ từ server.";
                    Console.Error.WriteLine($"Xác thực MFA thất bại: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Lỗi khi xác thực MFA: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Tạo Agent ID duy nhất.
        /// </summary>
        /// <returns>Agent ID duy nhất.</returns>
        private string GenerateAgentId()
        {
            string hostName = Environment.MachineName;
            string macAddress = GetMacAddress();
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return $"AGENT-{hostName}-{macAddress}-{timestamp}";
        }

        /// <summary>
        /// Lấy địa chỉ MAC của adapter mạng đầu tiên.
        /// </summary>
        /// <returns>Địa chỉ MAC dạng chuỗi.</returns>
        private string GetMacAddress()
        {
            try
            {
                var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in networkInterfaces)
                {
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                        !nic.Description.ToLower().Contains("virtual") && 
                        !nic.Description.ToLower().Contains("pseudo"))
                    {
                        var mac = nic.GetPhysicalAddress();
                        if (mac != null)
                        {
                            return BitConverter.ToString(mac.GetAddressBytes()).Replace("-", "");
                        }
                    }
                }
                
                // Fallback nếu không tìm thấy MAC phù hợp
                return Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy địa chỉ MAC, sử dụng GUID thay thế");
                return Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            }
        }

        /// <summary>
        /// Nhắc người dùng nhập thông tin.
        /// </summary>
        /// <param name="promptMessage">Thông báo nhắc nhở.</param>
        /// <param name="defaultValue">Giá trị mặc định (có thể null).</param>
        /// <returns>Chuỗi người dùng nhập.</returns>
        private string PromptForInput(string promptMessage, string? defaultValue = null)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write($"{promptMessage} [{defaultValue}]: ");
            }
            else
            {
                Console.Write($"{promptMessage} ");
            }

            string input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            return input;
        }

        /// <summary>
        /// Hiển thị prompt và lấy input số nguyên từ người dùng.
        /// </summary>
        /// <param name="promptMessage">Thông điệp hiển thị.</param>
        /// <param name="value">Giá trị số nguyên đầu ra.</param>
        /// <param name="defaultValue">Giá trị mặc định.</param>
        /// <returns>True nếu parse thành công, ngược lại là False.</returns>
        private bool TryParseInt(string promptMessage, out int value, string? defaultValue = null)
        {
            string input = PromptForInput(promptMessage, defaultValue);
            
            if (int.TryParse(input, out value))
            {
                return true;
            }
            
            Console.Error.WriteLine("Giá trị không hợp lệ. Vui lòng nhập số nguyên.");
            value = 0;
            return false;
        }
    }
}
