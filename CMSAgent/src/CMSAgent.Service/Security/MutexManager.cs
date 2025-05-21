 // CMSAgent.Service/Security/MutexManager.cs
using CMSAgent.Shared.Constants; // For AgentConstants
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // For IOptions<AppSettings>
using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using CMSAgent.Service.Configuration.Models; // For AppSettings

namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Quản lý Mutex để đảm bảo chỉ một instance của Agent Service được chạy trên hệ thống.
    /// </summary>
    public class MutexManager : IDisposable
    {
        private readonly ILogger<MutexManager> _logger;
        private Mutex? _mutex;
        private bool _hasHandle = false;
        private readonly string _mutexName;

        /// <summary>
        /// Khởi tạo MutexManager.
        /// </summary>
        /// <param name="appSettingsOptions">Cấu hình ứng dụng để lấy AgentInstanceGuid.</param>
        /// <param name="logger">Logger.</param>
        public MutexManager(IOptions<AppSettings> appSettingsOptions, ILogger<MutexManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));

            if (string.IsNullOrWhiteSpace(appSettings.AgentInstanceGuid))
            {
                var errorMessage = "AgentInstanceGuid không được cấu hình trong appsettings. Không thể tạo Mutex duy nhất.";
                _logger.LogCritical(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _mutexName = $"{AgentConstants.AgentServiceMutexNamePrefix}{appSettings.AgentInstanceGuid}";
            _logger.LogInformation("Sử dụng tên Mutex: {MutexName}", _mutexName);
        }

        /// <summary>
        /// Cố gắng yêu cầu quyền sở hữu Mutex.
        /// </summary>
        /// <returns>True nếu giành được quyền sở hữu Mutex (instance hiện tại là duy nhất), False nếu Mutex đã được giữ bởi instance khác.</returns>
        public bool RequestOwnership()
        {
            if (_hasHandle) // Đã giữ Mutex rồi
            {
                return true;
            }

            try
            {
                // Cấu hình bảo mật cho Mutex để cho phép tất cả user trên máy có thể "nhìn thấy" Mutex toàn cục.
                // Điều này quan trọng vì service chạy dưới LocalSystem, nhưng có thể có instance khác
                // (ví dụ: chạy debug từ user khác) cố gắng tạo Mutex cùng tên.
                var mutexSecurity = new MutexSecurity();
                mutexSecurity.AddAccessRule(new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null), // Everyone
                    MutexRights.FullControl, // Hoặc ít nhất là Synchronize | ReadPermissions
                    AccessControlType.Allow
                ));

                _mutex = new Mutex(initiallyOwned: false, name: _mutexName, out bool createdNew, mutexSecurity);

                if (createdNew)
                {
                    // Nếu Mutex được tạo mới, instance này có thể giành quyền sở hữu.
                    _logger.LogInformation("Mutex {MutexName} được tạo mới.", _mutexName);
                }
                else
                {
                    // Mutex đã tồn tại, cố gắng giành quyền sở hữu với timeout ngắn.
                    _logger.LogInformation("Mutex {MutexName} đã tồn tại. Đang thử giành quyền sở hữu...", _mutexName);
                }

                // Cố gắng giành quyền sở hữu Mutex.
                // WaitOne(0) sẽ trả về ngay lập tức. True nếu giành được, False nếu không.
                _hasHandle = _mutex.WaitOne(TimeSpan.Zero, false);

                if (_hasHandle)
                {
                    _logger.LogInformation("Đã giành được quyền sở hữu Mutex: {MutexName}.", _mutexName);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Không thể giành quyền sở hữu Mutex: {MutexName}. Một instance khác có thể đang chạy.", _mutexName);
                    _mutex.Close(); // Đóng handle nếu không giành được quyền
                    _mutex = null;
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                // Xảy ra khi một process khác giữ Mutex và kết thúc mà không giải phóng.
                // Instance hiện tại có thể giành quyền sở hữu.
                _logger.LogWarning("Mutex {MutexName} đã bị bỏ rơi (abandoned). Giành quyền sở hữu.", _mutexName);
                _hasHandle = true; // Giành quyền sở hữu
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogCritical(ex, "Lỗi UnauthorizedAccessException khi tạo hoặc truy cập Mutex {MutexName}. Kiểm tra quyền của service/user.", _mutexName);
                // Đây là lỗi nghiêm trọng, có thể service không có quyền tạo global mutex.
                return false; // Không thể tiếp tục nếu không tạo được Mutex
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi yêu cầu Mutex {MutexName}.", _mutexName);
                return false;
            }
        }

        /// <summary>
        /// Giải phóng Mutex nếu đã giữ.
        /// </summary>
        public void ReleaseOwnership()
        {
            if (_hasHandle && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                    _logger.LogInformation("Đã giải phóng Mutex: {MutexName}.", _mutexName);
                }
                catch (ApplicationException ex) // Có thể xảy ra nếu thread không sở hữu mutex cố gắng giải phóng
                {
                    _logger.LogError(ex, "Lỗi ApplicationException khi giải phóng Mutex {MutexName}. Thread này có thể không sở hữu Mutex.", _mutexName);
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Lỗi không xác định khi giải phóng Mutex {MutexName}.", _mutexName);
                }
                _hasHandle = false;
            }
        }

        public void Dispose()
        {
            ReleaseOwnership();
            _mutex?.Dispose();
            _mutex = null;
            GC.SuppressFinalize(this);
            _logger.LogDebug("MutexManager đã được dispose.");
        }
    }
}
