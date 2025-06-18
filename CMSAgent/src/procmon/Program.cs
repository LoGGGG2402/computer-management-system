namespace procmon
{
    class Program
    {
        private static ProcessManager? _processManager;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static Timer? _displayTimer;

        static async Task Main(string[] args)
        {
            Console.Title = "Process Monitor (procmon) - Realtime View";
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("                       PROCESS MONITOR - REALTIME TABLE VIEW");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("Initializing process monitoring...");

            _cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _cancellationTokenSource?.Cancel();
                Console.WriteLine("\nShutting down...");
            };

            try
            {
                // Initialize ProcessManager
                _processManager = new ProcessManager();

                // Load config
                _processManager.LoadConfigurationFromFile("processlist.json");

                Console.WriteLine("Configuration loaded successfully");
                Console.WriteLine("Starting monitoring in 3 seconds...");
                await Task.Delay(3000);

                // Subscribe to events
                _processManager.ProcessBlocked += (processInfo) =>
                {
                    // Blocking is handled automatically by ProcessManager
                };
                _processManager.BlockingError += error =>
                    Console.WriteLine($"[ERROR] {error}");

                // Start monitoring
                await _processManager.StartAsync();

                // Start display timer
                _displayTimer = new Timer(DisplayRealtimeTable, null, 2000, 2000);

                Console.WriteLine("Process monitoring started. Realtime table view active.");
                Console.WriteLine("Press Ctrl+C to exit.");

                await Task.Delay(-1, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Process Monitor stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                _processManager?.Dispose();
                _displayTimer?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }

        private static void DisplayRealtimeTable(object? state = null)
        {
            try
            {
                if (_processManager == null) return;

                Console.Clear();

                // Header
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
                Console.WriteLine("                           PROCESS MONITOR - REALTIME VIEW");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Last Update: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();                // Get grouped processes data from ProcessManager
                var groupedJson = _processManager.GetGroupedProcessesAsJson();

                try
                {
                    using var jsonDoc = System.Text.Json.JsonDocument.Parse(groupedJson);
                    var jsonElement = jsonDoc.RootElement;

                    // Display running processes
                    if (jsonElement.TryGetProperty("Running", out var runningElement))
                    {
                        var runningArray = runningElement.EnumerateArray().ToList();
                        var summaryElement = jsonElement.GetProperty("Summary");
                        var totalRunning = summaryElement.GetProperty("TotalRunning").GetInt32();
                        var uniqueRunning = summaryElement.GetProperty("UniqueRunning").GetInt32();

                        Console.WriteLine($"RUNNING PROCESSES ({totalRunning} total, {uniqueRunning} unique):");
                        Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");
                        Console.WriteLine($"{"Process Name",-35} {"Count",-6} {"PIDs",-25} {"Latest Start",-15}");
                        Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");

                        foreach (var group in runningArray.Take(25))
                        {
                            var processName = group.GetProperty("ProcessName").GetString() ?? "";
                            var count = group.GetProperty("Count").GetInt32();
                            var pids = group.GetProperty("PIDs").EnumerateArray().Select(p => p.GetInt32()).ToList();
                            var latestStart = DateTime.Parse(group.GetProperty("LatestStartTime").GetString() ?? DateTime.Now.ToString());

                            Console.ForegroundColor = ConsoleColor.Green;
                            var pidsDisplay = count > 5
                                ? $"{string.Join(",", pids.Take(5))}... (+{count - 5})"
                                : string.Join(",", pids);

                            Console.WriteLine($"{processName,-35} {count,-6} {pidsDisplay,-25} {latestStart.ToString("HH:mm:ss"),-15}");
                            Console.ResetColor();
                        }

                        if (uniqueRunning > 25)
                        {
                            Console.WriteLine($"... and {uniqueRunning - 25} more process types");
                        }
                    }

                    // Display blocked processes
                    if (jsonElement.TryGetProperty("Blocked", out var blockedElement))
                    {
                        var blockedArray = blockedElement.EnumerateArray().ToList();
                        if (blockedArray.Any())
                        {
                            var summaryElement2 = jsonElement.GetProperty("Summary");
                            var blockedTotalBlocked = summaryElement2.GetProperty("TotalBlocked").GetInt32();
                            var blockedUniqueBlocked = summaryElement2.GetProperty("UniqueBlocked").GetInt32();

                            Console.WriteLine();
                            Console.WriteLine($"BLOCKED PROCESSES ({blockedTotalBlocked} total, {blockedUniqueBlocked} unique):");
                            Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");
                            Console.WriteLine($"{"Process Name",-35} {"Count",-6} {"PIDs",-25} {"Latest Start",-15}");
                            Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");

                            foreach (var group in blockedArray)
                            {
                                var processName = group.GetProperty("ProcessName").GetString() ?? "";
                                var count = group.GetProperty("Count").GetInt32();
                                var pids = group.GetProperty("PIDs").EnumerateArray().Select(p => p.GetInt32()).ToList();
                                var latestStart = DateTime.Parse(group.GetProperty("LatestStartTime").GetString() ?? DateTime.Now.ToString());

                                Console.ForegroundColor = ConsoleColor.Red;
                                var pidsDisplay = count > 5
                                    ? $"{string.Join(",", pids.Take(5))}... (+{count - 5})"
                                    : string.Join(",", pids);

                                Console.WriteLine($"{processName,-35} {count,-6} {pidsDisplay,-25} {latestStart.ToString("HH:mm:ss"),-15}");
                                Console.ResetColor();
                            }
                        }
                    }                    // Summary
                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
                    Console.Write("SUMMARY: ");

                    var summary = jsonElement.GetProperty("Summary");
                    var summaryTotalRunning = summary.GetProperty("TotalRunning").GetInt32();
                    var summaryTotalBlocked = summary.GetProperty("TotalBlocked").GetInt32();
                    var summaryUniqueRunning = summary.GetProperty("UniqueRunning").GetInt32();
                    var total = summary.GetProperty("Total").GetInt32(); Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{summaryTotalRunning} Running ({summaryUniqueRunning} types)");
                    Console.ResetColor();

                    if (summaryTotalBlocked > 0)
                    {
                        Console.Write(" | ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write($"{summaryTotalBlocked} Blocked");
                        Console.ResetColor();
                    }
                    Console.WriteLine($" | Total: {total} processes");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
                    Console.WriteLine("Press Ctrl+C to exit");
                }
                catch (Exception parseEx)
                {
                    Console.WriteLine($"[DISPLAY] JSON Parse Error: {parseEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DISPLAY] Error: {ex.Message}");
            }
        }
    }
}
