using System.Diagnostics; 
using System.Runtime.InteropServices;

namespace CMSAgent.Service.Monitoring
{
    /// <summary>
    /// Monitor system resources (CPU, RAM, Disk) and report periodically.
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
        // Disk usage will be calculated manually because PerformanceCounter for % Free Space can be complex
        private string _mainDriveLetter = "C"; // Default is C drive

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
                    // Get an initial value to "warm up" the counter
                    _cpuCounter.NextValue();

                    _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", null, true);
                    // _ramCounter = new PerformanceCounter("Memory", "Available MBytes", null, true); // Alternative way to calculate % RAM
                }
                else
                {
                    _logger.LogWarning("PerformanceCounters are not fully supported on this operating system. Resource monitoring may be limited.");
                    // Need to find alternative solutions for Linux/macOS if support is needed
                }

                // Determine main drive
                DriveInfo? cDrive = DriveInfo.GetDrives().FirstOrDefault(d =>
                    d.DriveType == DriveType.Fixed &&
                    (d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ||
                     (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && d.Name.Equals(Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase)))
                );
                if (cDrive != null)
                {
                    _mainDriveLetter = cDrive.Name.Substring(0, 1);
                }
                _logger.LogInformation("Will monitor drive: {DriveLetter}:\\", _mainDriveLetter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing PerformanceCounters. Resource monitoring may not work correctly.");
                // Disable counters if error
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
                    _logger.LogWarning("Resource monitor is already running. Ignoring StartMonitoringAsync request.");
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Starting resource monitoring with report interval of {Interval} seconds.", reportIntervalSeconds);
                _statusUpdateAction = statusUpdateAction ?? throw new ArgumentNullException(nameof(statusUpdateAction));
                _cancellationToken = cancellationToken;
                _cancellationToken.Register(() => StopMonitoringAsync().ConfigureAwait(false).GetAwaiter().GetResult());

                _timer = new Timer(
                    async state => await ReportStatusAsync(),
                    null,
                    TimeSpan.Zero, // Start immediately
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
                    _logger.LogInformation("Resource monitor is not running or has already stopped.");
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Stopping resource monitoring.");
                _timer?.Change(Timeout.Infinite, 0); // Stop timer
                _timer?.Dispose();
                _timer = null;
                _isMonitoring = false;
                _statusUpdateAction = null; // Clear action to avoid accidental calls
            }
            return Task.CompletedTask;
        }

        private async Task ReportStatusAsync()
        {
            if (!_isMonitoring || _cancellationToken.IsCancellationRequested)
            {
                if (_isMonitoring) // If still isMonitoring but token is cancelled
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

                _logger.LogInformation("Resource status: CPU={Cpu}%, RAM={Ram}%, Disk={Disk}%", cpuUsage, ramUsage, diskUsage);

                if (_statusUpdateAction != null)
                {
                    await _statusUpdateAction(cpuUsage, ramUsage, diskUsage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReportStatusAsync.");
                // Consider stopping monitor if errors occur repeatedly
            }
        }

        public float GetCurrentCpuUsage()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logger.LogWarning("CPU usage monitoring is not supported on this platform.");
                    return 0f;
                }

                // Need to call NextValue() twice with a small delay to get accurate CPU value
                // First call is in InitializePerformanceCounters() or here if _cpuCounter was just created.
                // However, with timer running periodically, values will stabilize after a few calls.
                return _cpuCounter?.NextValue() ?? 0f;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot read CPU usage value.");
                return 0f;
            }
        }

        public float GetCurrentRamUsage()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logger.LogWarning("RAM usage monitoring is not supported on this platform.");
                    return 0f;
                }

                // "% Committed Bytes In Use" is the ratio of committed memory in use.
                // It includes both physical RAM and page file.
                // This is a good indicator of memory pressure.
                return _ramCounter?.NextValue() ?? 0f;

                // If want to calculate physical RAM usage %:
                // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                // {
                //     using (var pcTotalRam = new PerformanceCounter("Memory", "Total Commit Limit", null, true)) // KB
                //     using (var pcCommitted = new PerformanceCounter("Memory", "Committed Bytes", null, true)) // Bytes
                //     {
                //         float totalRamMb = pcTotalRam.NextValue() / 1024;
                //         float committedMb = pcCommitted.NextValue() / (1024 * 1024);
                //         // Need additional logic to get actual total physical RAM
                //     }
                // }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot read RAM usage value.");
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
                _logger.LogWarning(ex, "Cannot read Disk usage value for drive {Drive}:\\.", _mainDriveLetter);
            }
            return 0f;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing ResourceMonitor...");
            StopMonitoringAsync().ConfigureAwait(false).GetAwaiter().GetResult(); // Stop timer first
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
            GC.SuppressFinalize(this);
            _logger.LogInformation("ResourceMonitor disposed.");
        }
    }
}
