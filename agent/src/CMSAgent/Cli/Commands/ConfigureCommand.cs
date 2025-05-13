using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.Constants;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;
using CMSAgent.Common.Interfaces;
using Microsoft.Extensions.Logging;

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
        /// <param name="console">Console để tương tác với người dùng.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Mã lỗi của lệnh.</returns>
        public async Task<int> ExecuteAsync(IConsole console, CancellationToken cancellationToken)
        {
            console.Out.WriteLine("\n==== CMSAgent - Cấu hình Ban Đầu ====\n");
            console.Out.WriteLine("Lệnh này sẽ thiết lập cấu hình cơ bản cho agent, bao gồm thông tin phòng và vị trí.");
            console.Out.WriteLine("Bạn có thể hủy quá trình bất kỳ lúc nào bằng cách nhấn Ctrl+C.");
            console.Out.WriteLine();

            try
            {
                // Tải cấu hình runtime hiện tại (nếu có)
                var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
                
                // Nếu chưa có cấu hình, tạo mới với agentId
                if (runtimeConfig == null)
                {
                    runtimeConfig = new Common.Models.RuntimeConfig
                    {
                        agentId = GenerateAgentId(),
                        room_config = new Common.Models.RoomConfig()
                    };
                }

                console.Out.WriteLine($"Agent ID: {runtimeConfig.agentId}");
                console.Out.WriteLine();

                // Lấy thông tin vị trí
                var roomName = PromptForInput(console, 
                    "Nhập tên phòng:", 
                    runtimeConfig.room_config?.roomName);

                if (cancellationToken.IsCancellationRequested)
                {
                    console.Out.WriteLine("\nQuá trình cấu hình đã bị hủy bởi người dùng.");
                    return CliExitCodes.UserCancelled;
                }

                if (!TryParseInt(console, "Nhập tọa độ X:", out int posX, 
                    runtimeConfig.room_config?.posX.ToString()))
                {
                    return CliExitCodes.InvalidArguments;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    console.Out.WriteLine("\nQuá trình cấu hình đã bị hủy bởi người dùng.");
                    return CliExitCodes.UserCancelled;
                }

                if (!TryParseInt(console, "Nhập tọa độ Y:", out int posY, 
                    runtimeConfig.room_config?.posY.ToString()))
                {
                    return CliExitCodes.InvalidArguments;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    console.Out.WriteLine("\nQuá trình cấu hình đã bị hủy bởi người dùng.");
                    return CliExitCodes.UserCancelled;
                }

                // Cập nhật thông tin vị trí
                runtimeConfig.room_config = new Common.Models.RoomConfig
                {
                    roomName = roomName,
                    posX = posX,
                    posY = posY
                };

                // Tạo yêu cầu đăng ký
                var identifyRequest = new AgentIdentifyRequest
                {
                    agentId = runtimeConfig.agentId,
                    positionInfo = new PositionInfo
                    {
                        roomName = roomName,
                        posX = posX,
                        posY = posY
                    },
                    forceRenewToken = true
                };

                console.Out.WriteLine("\nĐang gửi thông tin đến server...");

                try
                {
                    // Gửi yêu cầu đăng ký đến server
                    var response = await _httpClient.PostAsync<AgentIdentifyRequest, AgentIdentifyResponse>(
                        ApiRoutes.Identify, 
                        identifyRequest,
                        null,  // agentId không cần cho API identify
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
                                runtimeConfig.agent_token_encrypted = _tokenProtector.EncryptToken(response.agentToken);
                                console.Out.WriteLine("\nXác thực thành công!");
                            }
                            else
                            {
                                // Token hiện tại vẫn hợp lệ
                                console.Out.WriteLine("\nAgent đã được đăng ký trước đó.");
                            }
                        }
                        else if (response.status == "mfa_required")
                        {
                            console.Out.WriteLine("\nYêu cầu xác thực hai yếu tố (MFA).");
                            
                            // Xử lý MFA
                            var mfaResult = await HandleMfaVerificationAsync(console, runtimeConfig.agentId, cancellationToken);
                            if (!mfaResult)
                            {
                                return CliExitCodes.ServerConnectionFailed;
                            }
                        }
                        else if (response.status == "position_error")
                        {
                            console.Error.WriteLine($"\nLỗi: {response.message}");
                            console.Out.WriteLine("Vui lòng chạy lại lệnh configure và chọn vị trí khác.");
                            return CliExitCodes.ServerConnectionFailed;
                        }
                        else
                        {
                            // Lỗi khác
                            console.Error.WriteLine($"\nLỗi từ server: {response.message}");
                            return CliExitCodes.ServerConnectionFailed;
                        }
                    }
                    else
                    {
                        console.Error.WriteLine("\nKhông nhận được phản hồi từ server.");
                        return CliExitCodes.ServerConnectionFailed;
                    }
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine($"\nLỗi khi gửi thông tin đến server: {ex.Message}");
                    return CliExitCodes.ServerConnectionFailed;
                }

                // Lưu cấu hình
                try
                {
                    await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                    console.Out.WriteLine("\nĐã lưu cấu hình thành công.");
                    console.Out.WriteLine("\nAgent đã sẵn sàng hoạt động. Bạn có thể khởi động service bằng lệnh: CMSAgent.exe start");
                    return CliExitCodes.Success;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine($"\nLỗi khi lưu cấu hình: {ex.Message}");
                    return CliExitCodes.ConfigSaveFailed;
                }
            }
            catch (OperationCanceledException)
            {
                console.Out.WriteLine("\nQuá trình cấu hình đã bị hủy bởi người dùng.");
                return CliExitCodes.UserCancelled;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine($"\nLỗi không xác định: {ex.Message}");
                return CliExitCodes.GeneralError;
            }
        }

        /// <summary>
        /// Xử lý xác thực MFA.
        /// </summary>
        /// <param name="console">Console để tương tác với người dùng.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>True nếu xác thực thành công, ngược lại là False.</returns>
        private async Task<bool> HandleMfaVerificationAsync(IConsole console, string agentId, CancellationToken cancellationToken)
        {
            // Nhắc người dùng nhập mã MFA
            var mfaCode = PromptForInput(console, "Nhập mã xác thực (MFA):");

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(mfaCode))
            {
                console.Error.WriteLine("Mã xác thực không hợp lệ.");
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

                var response = await _httpClient.PostAsync<VerifyMfaRequest, VerifyMfaResponse>(
                    ApiRoutes.VerifyMfa,
                    mfaRequest,
                    null,
                    null
                );

                if (response != null && response.status == "success" && !string.IsNullOrEmpty(response.agentToken))
                {
                    // Mã hóa và lưu token
                    var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
                    if (runtimeConfig != null)
                    {
                        runtimeConfig.agent_token_encrypted = _tokenProtector.EncryptToken(response.agentToken);
                        await _configLoader.SaveRuntimeConfigAsync(runtimeConfig);
                        console.Out.WriteLine("Xác thực MFA thành công!");
                        return true;
                    }
                }
                else
                {
                    var errorMessage = response?.message ?? "Không nhận được phản hồi hợp lệ từ server.";
                    console.Error.WriteLine($"Xác thực MFA thất bại: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                console.Error.WriteLine($"Lỗi khi xác thực MFA: {ex.Message}");
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
        /// Hiển thị prompt và lấy input từ người dùng.
        /// </summary>
        /// <param name="console">Console để tương tác với người dùng.</param>
        /// <param name="promptMessage">Thông điệp hiển thị.</param>
        /// <param name="defaultValue">Giá trị mặc định.</param>
        /// <returns>Input của người dùng.</returns>
        private string PromptForInput(IConsole console, string promptMessage, string defaultValue = null)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                console.Out.Write($"{promptMessage} [{defaultValue}]: ");
            }
            else
            {
                console.Out.Write($"{promptMessage} ");
            }

            string input = console.In.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            return input;
        }

        /// <summary>
        /// Hiển thị prompt và lấy input số nguyên từ người dùng.
        /// </summary>
        /// <param name="console">Console để tương tác với người dùng.</param>
        /// <param name="promptMessage">Thông điệp hiển thị.</param>
        /// <param name="value">Giá trị số nguyên đầu ra.</param>
        /// <param name="defaultValue">Giá trị mặc định.</param>
        /// <returns>True nếu parse thành công, ngược lại là False.</returns>
        private bool TryParseInt(IConsole console, string promptMessage, out int value, string defaultValue = null)
        {
            string input = PromptForInput(console, promptMessage, defaultValue);
            
            if (int.TryParse(input, out value))
            {
                return true;
            }
            
            console.Error.WriteLine("Giá trị không hợp lệ. Vui lòng nhập số nguyên.");
            value = 0;
            return false;
        }
    }
}
