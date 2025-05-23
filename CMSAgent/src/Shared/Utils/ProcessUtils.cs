using System.Diagnostics;
using System.Text;
using CMSAgent.Shared.Constants;
using Serilog;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides comprehensive utility functions for process management and execution.
    /// Supports command execution, process lifecycle management, and graceful shutdown operations.
    /// </summary>
    public static class ProcessUtils
    {
        /// <summary>
        /// Executes a command asynchronously with comprehensive output capture and timeout support.
        /// </summary>
        /// <param name="fileName">The executable file name or full path to execute (e.g., "cmd.exe", "powershell.exe").</param>
        /// <param name="arguments">Command line arguments to pass to the executable.</param>
        /// <param name="workingDirectory">Working directory for process execution. Uses current directory if null.</param>
        /// <param name="timeoutMilliseconds">Maximum execution time in milliseconds. Default is 60 seconds.</param>
        /// <param name="cancellationToken">Token to enable early cancellation of the operation.</param>
        /// <returns>
        /// A tuple containing:
        /// - StandardOutput: All text written to standard output stream
        /// - StandardError: All text written to standard error stream  
        /// - ExitCode: Process exit code (-1 if timeout/cancellation occurred)
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
        /// <remarks>
        /// This method captures both stdout and stderr asynchronously to prevent deadlocks.
        /// If the process doesn't exit within the timeout, it attempts graceful shutdown before returning.
        /// </remarks>
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
        /// Waits for a process with the specified PID to exit within a given timeout period.
        /// </summary>
        /// <param name="processId">The process identifier to wait for.</param>
        /// <param name="timeoutMilliseconds">Maximum wait time in milliseconds. Values ≤ 0 wait indefinitely.</param>
        /// <returns>
        /// True if the process exited within the timeout period or doesn't exist.
        /// False if the timeout expired or an error occurred during the wait.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when the process ID is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the process has already exited.</exception>
        /// <remarks>
        /// This method safely handles cases where the process doesn't exist or has already terminated.
        /// Non-existent processes are considered as having "exited" and return true.
        /// </remarks>
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
        /// Starts a new process with the specified parameters and configuration options.
        /// </summary>
        /// <param name="filePath">Full path to the executable file to start.</param>
        /// <param name="arguments">Command line arguments to pass to the process. Can be null.</param>
        /// <param name="workingDirectory">Working directory for the process. Uses executable's directory if null.</param>
        /// <param name="createNoWindow">When true, prevents creating a visible window for the process.</param>
        /// <param name="useShellExecute">When true, uses the operating system shell to start the process.</param>
        /// <returns>
        /// The started Process object if successful, or null if the process failed to start.
        /// </returns>
        /// <exception cref="System.ComponentModel.Win32Exception">Thrown when the executable file is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown when there's an error starting the process.</exception>
        /// <remarks>
        /// This method provides a simplified interface for starting processes with common configuration options.
        /// The caller is responsible for disposing of the returned Process object.
        /// </remarks>
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

        /// <summary>
        /// Attempts to close a process gracefully using multiple shutdown strategies before forcing termination.
        /// </summary>
        /// <param name="process">The process instance to close gracefully.</param>
        /// <param name="timeoutMilliseconds">Maximum time to wait for each shutdown attempt in milliseconds.</param>
        /// <returns>
        /// True if the process was successfully closed through any method.
        /// False if all shutdown attempts failed.
        /// </returns>
        /// <remarks>
        /// This method employs a multi-stage shutdown approach:
        /// 1. Attempts to close the main window gracefully
        /// 2. On Windows, tries enabling process events for clean shutdown  
        /// 3. As a last resort, forcefully terminates the process
        /// Each stage respects the specified timeout before moving to the next approach.
        /// </remarks>
        private static async Task<bool> CloseProcessGracefully(Process process, int timeoutMilliseconds = 5000)
        {
            try
            {
                if (process.HasExited) return true;

                process.CloseMainWindow();
                
                if (await Task.Run(() => process.WaitForExit(timeoutMilliseconds)))
                {
                    return true;
                }

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
