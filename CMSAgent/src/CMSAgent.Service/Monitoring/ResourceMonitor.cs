 // CMSAgent.Service/Monitoring/ResourceMonitor.cs
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics; // For PerformanceCounter
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Giám sát tài nguyên hệ thống (CPU, RAM, Disk) và báo cáo định kỳ.
    /// </summary>
    public class ResourceMonitor : IResourceMonitor
    {
        private readonly ILogger<ResourceMonitor> _logger;
        private Timer? _timer;
        private Func<double, double, double, Task>? _statusUpdateAction;
        private CancellationToken _cancellationToken;

        // Performance Counters
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        // Disk usage sẽ được tính toán thủ công vì PerformanceCounter cho % Free Space có thể phức tạp
        private string _mainDriveLetter = "C"; // Mặc định là ổ C

        private bool _isMonitoring = false;
        private readonly object _lock = new object();


        public ResourceMonitor(ILogger<ResourceMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializePerformanceCounters();
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    // Lấy một giá trị ban đầu để "khởi động" counter
                    _cpuCounter.NextValue();

                    _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", null, true);
                    // _ramCounter = new PerformanceCounter("Memory", "Available MBytes", null, true); // Cách khác để tính % RAM
                }
                else
                {
                    _logger.LogWarning("PerformanceCounters không được hỗ trợ đầy đủ trên hệ điều hành này. Giám sát tài nguyên có thể bị hạn chế.");
                    // Cần tìm giải pháp thay thế cho Linux/macOS nếu hỗ trợ
                }

                // Xác định ổ đĩa chính
                DriveInfo? cDrive = DriveInfo.GetDrives().FirstOrDefault(d =>
                    d.DriveType == DriveType.Fixed &&
                    (d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ||
                     (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && d.Name.Equals(Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase)))
                );
                if (cDrive != null)
                {
                    _mainDriveLetter = cDrive.Name.Substring(0, 1);
                }
                 _logger.LogInformation("Sẽ giám sát ổ đĩa: {DriveLetter}:\\", _mainDriveLetter);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi khởi tạo PerformanceCounters. Giám sát tài nguyên có thể không hoạt động chính xác.");
                // Vô hiệu hóa counter nếu lỗi
                _cpuCounter?.Dispose();
                _cpuCounter = null;
                _ramCounter?.Dispose();
                _ramCounter = null;
            }
        }

        public Task StartMonitoringAsync(int reportIntervalSeconds, Func<double, double, double, Task> statusUpdateAction, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("Resource monitor đã chạy. Bỏ qua yêu cầu StartMonitoringAsync.");
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Bắt đầu giám sát tài nguyên với khoảng thời gian báo cáo là {Interval} giây.", reportIntervalSeconds);
                _statusUpdateAction = statusUpdateAction ?? throw new ArgumentNullException(nameof(statusUpdateAction));
                _cancellationToken = cancellationToken;
                _cancellationToken.Register(() => StopMonitoringAsync().ConfigureAwait(false).GetAwaiter().GetResult());


                _timer = new Timer(
                    async state => await ReportStatusAsync(),
                    null,
                    TimeSpan.Zero, // Bắt đầu ngay lập tức
                    TimeSpan.FromSeconds(reportIntervalSeconds)
                );
                _isMonitoring = true;
            }
            return Task.CompletedTask;
        }

        public Task StopMonitoringAsync()
        {
            lock (_lock)
            {
                if (!_isMonitoring)
                {
                    _logger.LogInformation("Resource monitor chưa chạy hoặc đã dừng.");
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Đang dừng giám sát tài nguyên.");
                _timer?.Change(Timeout.Infinite, 0); // Dừng timer
                _timer?.Dispose();
                _timer = null;
                _isMonitoring = false;
                _statusUpdateAction = null; // Xóa action để tránh gọi nhầm
            }
            return Task.CompletedTask;
        }

        private async Task ReportStatusAsync()
        {
            if (!_isMonitoring || _cancellationToken.IsCancellationRequested)
            {
                if (_isMonitoring) // Nếu vẫn isMonitoring nhưng token đã bị hủy
                {
                   await StopMonitoringAsync();
                }
                return;
            }

            try
            {
                float cpuUsage = GetCurrentCpuUsage();
                float ramUsage = GetCurrentRamUsage();
                float diskUsage = GetCurrentDiskUsage();

                _logger.LogDebug("Trạng thái tài nguyên: CPU={Cpu}%, RAM={Ram}%, Disk={Disk}%", cpuUsage, ramUsage, diskUsage);

                if (_statusUpdateAction != null)
                {
                    await _statusUpdateAction(cpuUsage, ramUsage, diskUsage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình ReportStatusAsync.");
                // Cân nhắc dừng monitor nếu lỗi lặp lại nhiều lần
            }
        }

        public float GetCurrentCpuUsage()
        {
            try
            {
                // Cần gọi NextValue() hai lần với một khoảng trễ nhỏ để có giá trị chính xác cho CPU
                // Lần gọi đầu tiên trong InitializePerformanceCounters() hoặc ở đây nếu _cpuCounter mới được tạo.
                // Tuy nhiên, với timer chạy định kỳ, giá trị sẽ ổn định hơn sau vài lần gọi.
                return _cpuCounter?.NextValue() ?? 0f;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc giá trị CPU usage.");
                return 0f;
            }
        }

        public float GetCurrentRamUsage()
        {
            try
            {
                // "% Committed Bytes In Use" là tỷ lệ bộ nhớ đã cam kết đang được sử dụng.
                // Nó bao gồm cả RAM vật lý và page file.
                // Đây là một chỉ số tốt về áp lực bộ nhớ.
                return _ramCounter?.NextValue() ?? 0f;

                // Nếu muốn tính % RAM vật lý sử dụng:
                // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                // {
                //     using (var pcTotalRam = new PerformanceCounter("Memory", "Total Commit Limit", null, true)) // KB
                //     using (var pcCommitted = new PerformanceCounter("Memory", "Committed Bytes", null, true)) // Bytes
                //     {
                //         float totalRamMb = pcTotalRam.NextValue() / 1024;
                //         float committedMb = pcCommitted.NextValue() / (1024 * 1024);
                //         // Cần thêm logic để lấy tổng RAM vật lý thực sự
                //     }
                // }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc giá trị RAM usage.");
                return 0f;
            }
        }

        public float GetCurrentDiskUsage()
        {
            try
            {
                DriveInfo drive = new DriveInfo(_mainDriveLetter);
                if (drive.IsReady)
                {
                    long totalSize = drive.TotalSize;
                    long freeSpace = drive.TotalFreeSpace;
                    if (totalSize > 0)
                    {
                        double usedSpace = totalSize - freeSpace;
                        return (float)(usedSpace * 100.0 / totalSize);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc giá trị Disk usage cho ổ {Drive}:\\.", _mainDriveLetter);
            }
            return 0f;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing ResourceMonitor...");
            StopMonitoringAsync().ConfigureAwait(false).GetAwaiter().GetResult(); // Dừng timer trước
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
            GC.SuppressFinalize(this);
            _logger.LogInformation("ResourceMonitor disposed.");
        }
    }
}
