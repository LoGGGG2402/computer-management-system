using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Core
{
    /// <summary>
    /// Lớp hỗ trợ đảm bảo chỉ có một phiên bản của ứng dụng được chạy tại một thời điểm.
    /// </summary>
    public class SingletonMutex : IDisposable
    {
        private readonly string _mutexName;
        private readonly Mutex _mutex;
        private readonly ILogger<SingletonMutex> _logger;
        private bool _ownsHandle = false;
        private bool _disposed = false;

        /// <summary>
        /// Khởi tạo một instance mới của SingletonMutex.
        /// </summary>
        /// <param name="mutexName">Tên của mutex, xác định danh tính duy nhất.</param>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public SingletonMutex(string mutexName, ILogger<SingletonMutex> logger)
        {
            _mutexName = string.IsNullOrEmpty(mutexName) 
                ? "Global\\CMSAgentSingleInstance" 
                : $"Global\\{mutexName}SingleInstance";
            _logger = logger;
            
            try
            {
                // Cố gắng tạo và sở hữu mutex toàn cục
                _mutex = new Mutex(true, _mutexName, out _ownsHandle);
                
                if (_ownsHandle)
                {
                    _logger.LogInformation("Đã có được khóa singleton với tên {MutexName}", _mutexName);
                }
                else
                {
                    _logger.LogWarning("Không thể lấy khóa singleton. Một phiên bản khác của ứng dụng có thể đang chạy.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo singleton mutex");
                throw;
            }
        }

        /// <summary>
        /// Kiểm tra xem phiên bản này có sở hữu mutex hay không.
        /// </summary>
        /// <returns>True nếu phiên bản này là duy nhất, ngược lại là False.</returns>
        public bool IsSingleInstance()
        {
            return _ownsHandle;
        }

        /// <summary>
        /// Thử lấy quyền sở hữu mutex.
        /// </summary>
        /// <param name="timeout">Thời gian chờ tối đa để lấy khóa.</param>
        /// <returns>True nếu lấy được khóa, ngược lại là False.</returns>
        public bool TryAcquire(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SingletonMutex));
            }

            if (_ownsHandle)
            {
                return true;
            }

            try
            {
                _ownsHandle = _mutex.WaitOne(timeout);
                
                if (_ownsHandle)
                {
                    _logger.LogInformation("Đã có được khóa singleton với tên {MutexName} sau khi chờ", _mutexName);
                }
                else
                {
                    _logger.LogWarning("Không thể lấy khóa singleton sau khi chờ {Timeout}ms", timeout.TotalMilliseconds);
                }
                
                return _ownsHandle;
            }
            catch (AbandonedMutexException)
            {
                // Một phiên bản khác đã kết thúc bất ngờ mà không giải phóng mutex
                _logger.LogWarning("Phát hiện mutex bị bỏ rơi, lấy quyền sở hữu");
                _ownsHandle = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cố gắng lấy khóa singleton");
                return false;
            }
        }

        /// <summary>
        /// Giải phóng tài nguyên.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Giải phóng tài nguyên có quản lý và không quản lý.
        /// </summary>
        /// <param name="disposing">True nếu được gọi từ mã người dùng, False nếu từ finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_ownsHandle)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                        _logger.LogInformation("Đã giải phóng khóa singleton {MutexName}", _mutexName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi giải phóng khóa singleton");
                    }
                }

                _mutex.Dispose();
            }

            _disposed = true;
        }
    }
}
