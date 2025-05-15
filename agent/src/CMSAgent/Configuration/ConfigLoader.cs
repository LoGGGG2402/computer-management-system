using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Configuration
{
    /// <summary>
    /// Lớp thực hiện tải và lưu cấu hình của agent
    /// </summary>
    public class ConfigLoader : IConfigLoader
    {
        private readonly ILogger<ConfigLoader> _logger;
        private readonly IOptionsMonitor<CmsAgentSettingsOptions> _settingsMonitor;
        private RuntimeConfig? _runtimeConfigCache = null;
        private readonly string _runtimeConfigPath;
        private readonly string _installPath = string.Empty;
        private readonly string _dataPath;
        private readonly string _runtimeConfigFileName = "runtime_config.json";
        
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Cấu hình hiện tại của agent từ appsettings.json
        /// </summary>
        public CmsAgentSettingsOptions Settings => _settingsMonitor.CurrentValue;

        /// <summary>
        /// Cấu hình riêng của agent (phần AgentSettings trong CmsAgentSettingsOptions)
        /// </summary>
        public AgentSpecificSettingsOptions AgentSettings => _settingsMonitor.CurrentValue.AgentSettings;

        /// <summary>
        /// Khởi tạo một instance mới của ConfigLoader
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <param name="settingsMonitor">Monitor cấu hình từ appsettings.json</param>
        public ConfigLoader(ILogger<ConfigLoader> logger, IOptionsMonitor<CmsAgentSettingsOptions> settingsMonitor)
        {
            _logger = logger;
            _settingsMonitor = settingsMonitor;
            
            _installPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Settings.AppName ?? "CMSAgent");
            _runtimeConfigPath = Path.Combine(_dataPath, "runtime_config", _runtimeConfigFileName);
        }

        /// <summary>
        /// Tải cấu hình runtime từ file
        /// </summary>
        /// <param name="forceReload">Có bắt buộc tải lại từ đĩa kể cả đã có trong bộ nhớ cache</param>
        /// <returns>Đối tượng RuntimeConfig hoặc cấu hình mặc định nếu không tìm thấy/lỗi</returns>
        public async Task<RuntimeConfig> LoadRuntimeConfigAsync(bool forceReload = false)
        {
            if (_runtimeConfigCache != null && !forceReload)
                return _runtimeConfigCache;

            try
            {
                if (!File.Exists(_runtimeConfigPath))
                {
                    _logger.LogWarning("Không tìm thấy file cấu hình runtime tại {Path}", _runtimeConfigPath);
                    return CreateDefaultRuntimeConfig();
                }

                var json = await File.ReadAllTextAsync(_runtimeConfigPath);
                _runtimeConfigCache = JsonSerializer.Deserialize<RuntimeConfig>(json);
                
                if (_runtimeConfigCache == null)
                {
                    _logger.LogError("Không thể deserialize cấu hình runtime, tạo cấu hình mặc định");
                    return CreateDefaultRuntimeConfig();
                }

                return _runtimeConfigCache;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể tải cấu hình runtime từ {Path}", _runtimeConfigPath);
                return CreateDefaultRuntimeConfig();
            }
        }

        /// <summary>
        /// Tạo cấu hình runtime mặc định
        /// </summary>
        private RuntimeConfig CreateDefaultRuntimeConfig()
        {
            var config = new RuntimeConfig
            {
                AgentId = "UNCONFIGURED-" + Guid.NewGuid().ToString("N")[..8],
                RoomConfig = new RoomConfig
                {
                    RoomName = "Default",
                    PosX = 0,
                    PosY = 0
                },
                AgentTokenEncrypted = string.Empty
            };
            
            _runtimeConfigCache = config;
            return config;
        }

        /// <summary>
        /// Lưu cấu hình runtime ra file
        /// </summary>
        /// <param name="config">Đối tượng cấu hình cần lưu</param>
        public async Task SaveRuntimeConfigAsync(RuntimeConfig config)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(_runtimeConfigPath);
                if (directoryPath != null)
                {
                    Directory.CreateDirectory(directoryPath);
                    var json = JsonSerializer.Serialize(config, _jsonOptions);
                    await File.WriteAllTextAsync(_runtimeConfigPath, json);
                    _runtimeConfigCache = config;
                    _logger.LogInformation("Đã lưu cấu hình runtime vào {Path}", _runtimeConfigPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể lưu cấu hình runtime vào {Path}", _runtimeConfigPath);
            }
        }

        /// <summary>
        /// Lấy ID agent từ cấu hình runtime
        /// </summary>
        /// <returns>AgentId hoặc chuỗi rỗng nếu chưa được đặt</returns>
        public string GetAgentId() => _runtimeConfigCache?.AgentId ?? string.Empty;

        /// <summary>
        /// Lấy token đã mã hóa của agent từ cấu hình runtime
        /// </summary>
        /// <returns>Token đã mã hóa hoặc chuỗi rỗng nếu chưa được đặt</returns>
        public string GetEncryptedAgentToken() => _runtimeConfigCache?.AgentTokenEncrypted ?? string.Empty;

        /// <summary>
        /// Lấy đường dẫn cài đặt của agent
        /// </summary>
        /// <returns>Đường dẫn cài đặt</returns>
        public string GetInstallPath() => _installPath;

        /// <summary>
        /// Lấy đường dẫn thư mục dữ liệu của agent
        /// </summary>
        /// <returns>Đường dẫn thư mục dữ liệu</returns>
        public string GetDataPath() => _dataPath;

        /// <summary>
        /// Lấy phiên bản hiện tại của agent.
        /// </summary>
        /// <returns>Phiên bản agent.</returns>
        public string GetAgentVersion() => Settings.Version;
    }
}
