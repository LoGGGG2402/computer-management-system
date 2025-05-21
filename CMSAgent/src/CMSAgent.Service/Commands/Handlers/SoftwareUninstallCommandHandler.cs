 // CMSAgent.Service/Commands/Handlers/SoftwareUninstallCommandHandler.cs
using CMSAgent.Service.Commands.Models;
using CMSAgent.Shared.Utils; // For ProcessUtils
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Service.Configuration.Models; // For AppSettings
using System.Management; // For WMI to find uninstall string
using System.Runtime.InteropServices;

namespace CMSAgent.Service.Commands.Handlers
{
    public class SoftwareUninstallCommandHandler : CommandHandlerBase
    {
        private readonly AppSettings _appSettings;

        public SoftwareUninstallCommandHandler(
            ILogger<SoftwareUninstallCommandHandler> logger,
            IOptions<AppSettings> appSettingsOptions) : base(logger)
        {
            _appSettings = appSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(appSettingsOptions));
        }

        protected override async Task<CommandOutputResult> ExecuteInternalAsync(CommandRequest commandRequest, CancellationToken cancellationToken)
        {
            if (commandRequest.Parameters == null)
                return new CommandOutputResult { ErrorMessage = "Missing required parameters for software uninstallation.", ExitCode = -1 };

            commandRequest.Parameters.TryGetValue("package_name", out var packageNameObj);
            commandRequest.Parameters.TryGetValue("product_code", out var productCodeObj);
            commandRequest.Parameters.TryGetValue("uninstall_arguments", out var uninstallArgsObj);

            string? packageName = (packageNameObj is JsonElement pkgNameJson) ? pkgNameJson.GetString() : null;
            string? productCode = (productCodeObj is JsonElement prodCodeJson) ? prodCodeJson.GetString() : null;
            string? uninstallArguments = (uninstallArgsObj is JsonElement argsJson) ? argsJson.GetString() : null; // Thường là các cờ silent

            if (string.IsNullOrWhiteSpace(packageName) && string.IsNullOrWhiteSpace(productCode))
            {
                return new CommandOutputResult { ErrorMessage = "Either package_name or product_code is required for uninstallation.", ExitCode = -1 };
            }

            string? uninstallString = null;
            string? quietUninstallString = null;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                 return new CommandOutputResult { ErrorMessage = "Software uninstallation is only supported on Windows.", ExitCode = -10 };
            }

            // Ưu tiên ProductCode nếu có
            if (!string.IsNullOrWhiteSpace(productCode))
            {
                Logger.LogInformation("Attempting to uninstall using ProductCode: {ProductCode}", productCode);
                // MsiExec.exe /X {ProductCode} /qn
                uninstallString = $"MsiExec.exe";
                quietUninstallString = $"/X {productCode} {(string.IsNullOrWhiteSpace(uninstallArguments) ? "/qn" : uninstallArguments)}";
            }
            else if (!string.IsNullOrWhiteSpace(packageName))
            {
                Logger.LogInformation("Attempting to find uninstall string for package: {PackageName}", packageName);
                (uninstallString, quietUninstallString) = FindUninstallStringsForPackage(packageName, uninstallArguments);
            }

            if (string.IsNullOrWhiteSpace(uninstallString))
            {
                return new CommandOutputResult { ErrorMessage = $"Could not find uninstallation method for '{packageName ?? productCode}'.", ExitCode = -2 };
            }

            // Tách file thực thi và tham số nếu cần
            string executable = uninstallString;
            string argumentsToUse = quietUninstallString ?? string.Empty;

            // Một số uninstall string có thể chứa cả file và arguments, cần parse
            if (!uninstallString.StartsWith("MsiExec.exe", StringComparison.OrdinalIgnoreCase) && uninstallString.Contains(".exe\"", StringComparison.OrdinalIgnoreCase))
            {
                 // Ví dụ: "C:\Program Files\App\unins000.exe" /SILENT
                int exeEndQuote = uninstallString.IndexOf(".exe\"", StringComparison.OrdinalIgnoreCase);
                if(exeEndQuote > 0) {
                    executable = uninstallString.Substring(0, exeEndQuote + 5).Trim('"'); // Lấy cả .exe" và bỏ dấu "
                    if (uninstallString.Length > exeEndQuote + 5)
                    {
                        argumentsToUse = uninstallString.Substring(exeEndQuote + 5).Trim();
                        if (!string.IsNullOrWhiteSpace(uninstallArguments)) // Ghép thêm custom arguments nếu có
                        {
                             argumentsToUse = $"{argumentsToUse} {uninstallArguments}";
                        }
                    } else if (!string.IsNullOrWhiteSpace(uninstallArguments)) {
                        argumentsToUse = uninstallArguments;
                    }
                }
            } else if (uninstallString.StartsWith("MsiExec.exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(quietUninstallString)) {
                argumentsToUse = quietUninstallString; // Đã xử lý ở trên
            } else if (!string.IsNullOrWhiteSpace(uninstallArguments)) { // Trường hợp uninstallString chỉ là exe, và có custom args
                 argumentsToUse = uninstallArguments;
            }


            Logger.LogInformation("Executing uninstallation: \"{Executable}\" with arguments: \"{Arguments}\"", executable, argumentsToUse);
            int uninstallTimeoutSeconds = GetUninstallTimeoutSeconds(commandRequest);

            var (stdout, stderr, exitCode) = await ProcessUtils.ExecuteCommandAsync(
                executable,
                argumentsToUse,
                timeoutMilliseconds: uninstallTimeoutSeconds * 1000,
                cancellationToken: cancellationToken
            );
            
            return new CommandOutputResult
            {
                Stdout = stdout,
                Stderr = stderr,
                ExitCode = exitCode,
                ErrorMessage = string.IsNullOrEmpty(stderr) ? null : stderr
            };
        }

        private (string? UninstallString, string? QuietUninstallString) FindUninstallStringsForPackage(string packageName, string? customUninstallArgs)
        {
            string? uninstallString = null;
            string? quietUninstallString = null;

            // Thử tìm trong Registry (cả 32-bit và 64-bit view)
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" // For 32-bit apps on 64-bit OS
            };

            foreach (string keyPath in registryKeys)
            {
                try
                {
                    using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64); // Hoặc Registry32
                    using var uninstallKey = baseKey.OpenSubKey(keyPath);
                    if (uninstallKey == null) continue;

                    foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey != null)
                        {
                            var displayName = appKey.GetValue("DisplayName") as string;
                            if (displayName != null && displayName.Contains(packageName, StringComparison.OrdinalIgnoreCase))
                            {
                                uninstallString = appKey.GetValue("UninstallString") as string;
                                quietUninstallString = appKey.GetValue("QuietUninstallString") as string; // Thường chứa cờ silent

                                if (!string.IsNullOrWhiteSpace(quietUninstallString))
                                {
                                    // Nếu có QuietUninstallString, sử dụng nó
                                    // Cần parse để tách executable và arguments
                                    // Ví dụ: MsiExec.exe /I{PRODUCT_CODE} /qn
                                    // Hoặc "C:\path\to\uninstaller.exe" /S
                                    if (!string.IsNullOrWhiteSpace(customUninstallArgs)) {
                                        // Nếu user cung cấp custom args, nó có thể override hoặc bổ sung
                                        // Tạm thời ưu tiên custom args nếu nó khác với phần args của quiet string
                                        // Logic này có thể cần phức tạp hơn
                                        string[] quietParts = SplitCommandAndArgs(quietUninstallString);
                                        return (quietParts[0], customUninstallArgs);
                                    }
                                    string[] parts = SplitCommandAndArgs(quietUninstallString);
                                    return (parts[0], parts[1]);
                                }
                                else if (!string.IsNullOrWhiteSpace(uninstallString))
                                {
                                    // Nếu không có QuietUninstallString, dùng UninstallString và ghép custom args
                                    string[] parts = SplitCommandAndArgs(uninstallString);
                                    return (parts[0], $"{parts[1]} {customUninstallArgs}".Trim());
                                }
                                // Tìm thấy, không cần tìm nữa
                                return (uninstallString, quietUninstallString);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Lỗi khi truy cập Registry key: {KeyPath}", keyPath);
                }
            }
            return (null, null);
        }

        private string[] SplitCommandAndArgs(string commandLine)
        {
            string executable = commandLine;
            string arguments = string.Empty;

            commandLine = commandLine.Trim();
            if (commandLine.StartsWith("\""))
            {
                int endQuote = commandLine.IndexOf('\"', 1);
                if (endQuote > 0)
                {
                    executable = commandLine.Substring(0, endQuote + 1).Trim('"');
                    if (commandLine.Length > endQuote + 1)
                    {
                        arguments = commandLine.Substring(endQuote + 1).Trim();
                    }
                }
            }
            else
            {
                int firstSpace = commandLine.IndexOf(' ');
                if (firstSpace > 0)
                {
                    executable = commandLine.Substring(0, firstSpace);
                    arguments = commandLine.Substring(firstSpace + 1).Trim();
                }
            }
            return new[] { executable, arguments };
        }


        protected override int GetDefaultCommandTimeoutSeconds(CommandRequest commandRequest)
        {
            if (commandRequest.Parameters != null &&
                commandRequest.Parameters.TryGetValue("timeout_sec", out var timeoutObj) &&
                timeoutObj is JsonElement timeoutJson &&
                timeoutJson.TryGetInt32(out int timeoutSec) &&
                timeoutSec > 0)
            {
                return timeoutSec;
            }
            return _appSettings.CommandExecution.DefaultCommandTimeoutSeconds * 5; // Gỡ cài đặt có thể mất thời gian
        }
        private int GetUninstallTimeoutSeconds(CommandRequest commandRequest) => GetDefaultCommandTimeoutSeconds(commandRequest);
    }
}
