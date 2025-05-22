// Shared/Utils/ProcessUtils.cs
using System.Diagnostics;
using System.Text;
using CMSAgent.Shared.Constants;
using Serilog;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides utility functions related to process management and execution.
    /// </summary>
    public static class ProcessUtils
    {
        /// <summary>
        /// Executes a command line and returns output, error, and exit code.
        /// </summary>
        /// <param name="fileName">Executable file name (e.g., "cmd.exe", "powershell.exe" or full path).</param>
        /// <param name="arguments">Arguments for the command.</param>
        /// <param name="workingDirectory">Working directory for the process (default is current directory).</param>
        /// <param name="timeoutMilliseconds">Maximum allowed time (ms) for the command to run. Default is 60 seconds.</param>
        /// <param name="cancellationToken">Token to cancel the process early.</param>
        /// <returns>A tuple containing (string standardOutput, string standardError, int exitCode).</returns>
        public static async Task<(string StandardOutput, string StandardError, int ExitCode)> ExecuteCommandAsync(
            string fileName,
            string arguments,
            string? workingDirectory = null,
            int timeoutMilliseconds = AgentConstants.DefaultProcessWaitForExitTimeoutSeconds * 1000,
            CancellationToken cancellationToken = default)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            };

            Log.Information("Executing command: {FileName} {Arguments} in {WorkingDirectory}", fileName, arguments, processStartInfo.WorkingDirectory);

            using var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var outputTask = new TaskCompletionSource<bool>();
            var errorTask = new TaskCompletionSource<bool>();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null) outputTask.TrySetResult(true);
                else outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null) errorTask.TrySetResult(true);
                else errorBuilder.AppendLine(e.Data);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    if (timeoutMilliseconds > 0)
                    {
                        combinedCts.CancelAfter(timeoutMilliseconds);
                    }

                    await process.WaitForExitAsync(combinedCts.Token);
                }

                // Đợi cả output và error stream đóng với timeout
                var streamTasks = new[] { outputTask.Task, errorTask.Task };
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(AgentConstants.DefaultProcessStreamCloseTimeoutSeconds));
                
                if (await Task.WhenAny(Task.WhenAll(streamTasks), timeoutTask) != timeoutTask)
                {
                    Log.Information("All process streams closed successfully");
                }
                else
                {
                    Log.Warning("Timeout waiting for process streams to close");
                }

                if (process.HasExited)
                {
                    Log.Information("Command executed. ExitCode: {ExitCode}", process.ExitCode);
                    return (outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode);
                }
                else
                {
                    Log.Warning("Command did not exit normally (timeout or cancellation). Attempting graceful shutdown.");
                    await CloseProcessGracefully(process);
                    return (outputBuilder.ToString(), errorBuilder.ToString(), -1);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Command execution was cancelled for {FileName} {Arguments}.", fileName, arguments);
                await CloseProcessGracefully(process);
                return (outputBuilder.ToString(), errorBuilder.ToString(), -1);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing command: {FileName} {Arguments}", fileName, arguments);
                await CloseProcessGracefully(process);
                return (string.Empty, ex.Message, ex.HResult);
            }
        }

        /// <summary>
        /// Wait for a process (by PID) to exit with a timeout.
        /// </summary>
        /// <param name="processId">Process ID to wait for.</param>
        /// <param name="timeoutMilliseconds">Maximum time (ms) to wait. 0 or negative means wait indefinitely.</param>
        /// <returns>True if the process exited within the wait time, False if timeout.</returns>
        public static bool WaitForProcessExit(int processId, int timeoutMilliseconds = AgentConstants.DefaultProcessWaitForExitTimeoutSeconds * 1000)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                if (process == null)
                {
                    Log.Warning("WaitForProcessExit: Process with ID {ProcessId} not found.", processId);
                    return true;
                }

                Log.Information("Waiting for process {ProcessId} ({ProcessName}) to exit with timeout {TimeoutMs}ms.", processId, process.ProcessName, timeoutMilliseconds);
                if (timeoutMilliseconds <= 0)
                {
                    process.WaitForExit();
                    return true;
                }
                else
                {
                    return process.WaitForExit(timeoutMilliseconds);
                }
            }
            catch (ArgumentException)
            {
                Log.Warning("WaitForProcessExit: Process with ID {ProcessId} does not exist (ArgumentException).", processId);
                return true;
            }
            catch (InvalidOperationException)
            {
                Log.Information("WaitForProcessExit: Process with ID {ProcessId} has already exited (InvalidOperationException).", processId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WaitForProcessExit: Error waiting for process ID {ProcessId} to exit.", processId);
                return false;
            }
        }

        /// <summary>
        /// Start a new process.
        /// </summary>
        /// <param name="filePath">Path to the executable file.</param>
        /// <param name="arguments">Arguments for the process.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <param name="createNoWindow">True to not create a window for the process.</param>
        /// <param name="useShellExecute">True to use the OS shell to launch.</param>
        /// <returns>Started Process object or null if error.</returns>
        public static Process? StartProcess(string filePath, string? arguments = null, string? workingDirectory = null, bool createNoWindow = true, bool useShellExecute = false)
        {
            try
            {
                var startInfo = new ProcessStartInfo(filePath)
                {
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(filePath),
                    CreateNoWindow = createNoWindow,
                    UseShellExecute = useShellExecute
                };

                Log.Information("Starting process: {FilePath} {Arguments} in {WorkingDirectory}", filePath, arguments, startInfo.WorkingDirectory);
                return Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start process: {FilePath} {Arguments}", filePath, arguments);
                return null;
            }
        }

        private static async Task<bool> CloseProcessGracefully(Process process, int timeoutMilliseconds = 5000)
        {
            try
            {
                if (process.HasExited) return true;

                // Thử đóng tiến trình một cách nhẹ nhàng trước
                process.CloseMainWindow();
                
                // Đợi tiến trình đóng trong một khoảng thời gian
                if (await Task.Run(() => process.WaitForExit(timeoutMilliseconds)))
                {
                    return true;
                }

                // Nếu không đóng được, thử gửi tín hiệu kết thúc
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        process.EnableRaisingEvents = true;
                        var tcs = new TaskCompletionSource<bool>();
                        process.Exited += (s, e) => tcs.TrySetResult(true);
                        
                        if (await Task.WhenAny(tcs.Task, Task.Delay(timeoutMilliseconds)) == tcs.Task)
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to enable process events for graceful shutdown");
                    }
                }

                // Nếu vẫn không đóng được, mới sử dụng Kill
                Log.Warning("Process did not exit gracefully, forcing termination");
                process.Kill(true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during graceful process shutdown");
                return false;
            }
        }
    }
}
