// Filepath: c:\Users\longpph\Desktop\computer-management-system\dotnet_agent\CMSAgent\SystemOperations\WindowsUtils.cs
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Collections.Generic;
using System.IO;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Provides utility methods for Windows-specific operations,
    /// such as checking administrative privileges, process information, and user session details.
    /// </summary>
    public class WindowsUtils
    {
        private readonly ILogger<WindowsUtils> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsUtils"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging messages.</param>
        public WindowsUtils(ILogger<WindowsUtils> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if the current process is running with administrator privileges.
        /// </summary>
        /// <returns>True if the process is identified as running in the Administrator role; otherwise, false.
        /// Returns false if an exception occurs during the check.</returns>
        public bool IsRunningAsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    _logger.LogDebug("Current process running as admin: {IsAdmin}", isAdmin);
                    return isAdmin;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for administrator privileges.");
                return false;
            }
        }

        /// <summary>
        /// Checks if the current process is running as the SYSTEM account.
        /// </summary>
        /// <returns>True if the process identity is the SYSTEM account; otherwise, false.
        /// Returns false if an exception occurs during the check.</returns>
        public bool IsRunningAsSystem()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    bool isSystem = identity.IsSystem;
                    _logger.LogDebug("Current process running as SYSTEM: {IsSystem}", isSystem);
                    return isSystem;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if running as SYSTEM account.");
                return false;
            }
        }

        /// <summary>
        /// Gets the full path of the currently executing application.
        /// It first tries to get the path from the MainModule, then falls back to combining
        /// AppContext.BaseDirectory with the friendly name or process name.
        /// </summary>
        /// <returns>The full path to the executable. Returns an empty string if the path cannot be determined or an error occurs.</returns>
        public string GetExecutablePath()
        {
            try
            {
                string? processName = Process.GetCurrentProcess().ProcessName;
                string? mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;

                if (!string.IsNullOrEmpty(mainModulePath))
                {
                    _logger.LogDebug("Executable path from MainModule: {Path}", mainModulePath);
                    return mainModulePath;
                }
                
                string exeName = AppDomain.CurrentDomain.FriendlyName;
                if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && processName != null && processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    exeName = processName;
                }
                else if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                     exeName += ".exe";
                }

                string path = Path.Combine(AppContext.BaseDirectory, exeName);
                _logger.LogDebug("Executable path (fallback using AppContext.BaseDirectory and AppDomain.FriendlyName/ProcessName): {Path}", path);
                return path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting executable path.");
                return string.Empty; 
            }
        }

        #region P/Invoke for WTSEnumerateSessions

        /// <summary>
        /// Contains information about a client session on a Remote Desktop Session Host (RD Session Host) server.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            /// <summary>
            /// Session identifier.
            /// </summary>
            public int SessionID;
            /// <summary>
            /// Pointer to a null-terminated string that contains the name of the WinStation for this session.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pWinStationName;
            /// <summary>
            /// A value from the WTS_CONNECTSTATE_CLASS enumeration that indicates the session's current connection state.
            /// </summary>
            public WTS_CONNECTSTATE_CLASS State;
        }

        /// <summary>
        /// Specifies the connection state of a Remote Desktop Services session.
        /// </summary>
        private enum WTS_CONNECTSTATE_CLASS
        {
            /// <summary>A user is logged on to the WinStation. This state occurs when a user is signed in and actively using the session.</summary>
            WTSActive,
            /// <summary>The WinStation is connected to the client.</summary>
            WTSConnected,
            /// <summary>The WinStation is in the process of connecting to the client.</summary>
            WTSConnectQuery,
            /// <summary>The WinStation is shadowing another WinStation.</summary>
            WTSShadow,
            /// <summary>The WinStation is active but the client is disconnected.</summary>
            WTSDisconnected,
            /// <summary>The WinStation is waiting for a client to connect.</summary>
            WTSIdle,
            /// <summary>The WinStation is listening for a connection.</summary>
            WTSListen,
            /// <summary>The WinStation is being reset.</summary>
            WTSReset,
            /// <summary>The WinStation is down due to an error.</summary>
            WTSDown,
            /// <summary>The WinStation is initializing.</summary>
            WTSInit
        }

        /// <summary>
        /// Opens a handle to the specified Remote Desktop Session Host (RD Session Host) server.
        /// </summary>
        /// <param name="pServerName">Pointer to a null-terminated string specifying the NetBIOS name of the RD Session Host server.</param>
        /// <returns>If the function succeeds, the return value is a handle to the specified server. If the function fails, it returns IntPtr.Zero.</returns>
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] string pServerName);

        /// <summary>
        /// Closes an open handle to a Remote Desktop Session Host (RD Session Host) server.
        /// </summary>
        /// <param name="hServer">A handle to an RD Session Host server opened by a call to the WTSOpenServer function.</param>
        [DllImport("wtsapi32.dll")]
        private static extern void WTSCloseServer(IntPtr hServer);

        /// <summary>
        /// Retrieves a list of sessions on a Remote Desktop Session Host (RD Session Host) server.
        /// </summary>
        /// <param name="hServer">A handle to the RD Session Host server. </param>
        /// <param name="Reserved">This parameter is reserved. It must be zero.</param>
        /// <param name="Version">The version of the enumeration request. This parameter must be 1.</param>
        /// <param name="ppSessionInfo">A pointer to a variable that receives a pointer to an array of WTS_SESSION_INFO structures. </param>
        /// <param name="pCount">A pointer to a variable that receives the number of WTS_SESSION_INFO structures returned in the ppSessionInfo buffer.</param>
        /// <returns>If the function succeeds, the return value is a nonzero value. If the function fails, the return value is zero.</returns>
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] int Reserved,
            [MarshalAs(UnmanagedType.U4)] int Version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref int pCount);

        /// <summary>
        /// Frees memory allocated by a Remote Desktop Services function.
        /// </summary>
        /// <param name="pMemory">Pointer to the memory to free.</param>
        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        /// <summary>
        /// Retrieves session information for the specified session on the specified Remote Desktop Session Host (RD Session Host) server.
        /// </summary>
        /// <param name="hServer">A handle to an RD Session Host server.</param>
        /// <param name="sessionId">A Remote Desktop Services session identifier.</param>
        /// <param name="wtsInfoClass">A value of the WTS_INFO_CLASS enumeration that specifies the type of session information to retrieve.</param>
        /// <param name="ppBuffer">A pointer to a variable that receives a pointer to the requested information.</param>
        /// <param name="pBytesReturned">A pointer to a variable that receives the size, in bytes, of the data returned in ppBuffer.</param>
        /// <returns>If the function succeeds, the return value is a nonzero value. If the function fails, the return value is zero.</returns>
        [DllImport("Wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(
            IntPtr hServer,
            int sessionId,
            WTS_INFO_CLASS wtsInfoClass,
            out IntPtr ppBuffer,
            out uint pBytesReturned);

        /// <summary>
        /// Specifies the type of session information to retrieve in a call to the WTSQuerySessionInformation function.
        /// </summary>
        private enum WTS_INFO_CLASS
        {
            /// <summary>A null-terminated string that contains the name of the initial program that Remote Desktop Services runs when the user logs on. </summary>
            WTSInitialProgram,
            /// <summary>A null-terminated string that contains the published name of the application that the session is running.</summary>
            WTSApplicationName,
            /// <summary>A null-terminated string that contains the default directory for the initial program.</summary>
            WTSWorkingDirectory,
            /// <summary>A null-terminated string that contains the OEM identifier for the session.</summary>
            WTSOEMId,
            /// <summary>The session identifier.</summary>
            WTSSessionId,
            /// <summary>A null-terminated string that contains the user name of the user logged on to the session.</summary>
            WTSUserName,
            /// <summary>A null-terminated string that contains the name of the WinStation for the session.</summary>
            WTSWinStationName,
            /// <summary>A null-terminated string that contains the name of the domain to which the logged-on user belongs.</summary>
            WTSDomainName,
            /// <summary>The session's current connection state. For more information, see WTS_CONNECTSTATE_CLASS.</summary>
            WTSConnectState,
            /// <summary>The build number of the client.</summary>
            WTSClientBuildNumber,
            /// <summary>A null-terminated string that contains the name of the client computer.</summary>
            WTSClientName,
            /// <summary>A null-terminated string that contains the directory in which the client is installed.</summary>
            WTSClientDirectory,
            /// <summary>The product identifier of the client.</summary>
            WTSClientProductId,
            /// <summary>The hardware identifier of the client.</summary>
            WTSClientHardwareId,
            /// <summary>The network address of the client. </summary>
            WTSClientAddress,
            /// <summary>Information about the display resolution of the client. </summary>
            WTSClientDisplay,
            /// <summary>The protocol type of the session. </summary>
            WTSClientProtocolType,
            /// <summary>The amount of time, in milliseconds, that the session has been idle.</summary>
            WTSIdleTime,
            /// <summary>The time that the user logged on to the session.</summary>
            WTSLogonTime,
            /// <summary>The number of bytes of data sent from the client to the server since the client connected.</summary>
            WTSIncomingBytes,
            /// <summary>The number of bytes of data sent from the server to the client since the client connected.</summary>
            WTSOutgoingBytes,
            /// <summary>The number of frames of data sent from the client to the server since the client connected.</summary>
            WTSIncomingFrames,
            /// <summary>The number of frames of data sent from the server to the client since the client connected.</summary>
            WTSOutgoingFrames,
            /// <summary>Information about the client session. </summary>
            WTSClientInfo,
            /// <summary>Information about the session. </summary>
            WTSSessionInfo
        }
        #endregion

        /// <summary>
        /// Retrieves a list of usernames for currently logged-in users using the Windows Terminal Services (WTS) API.
        /// This method queries active and connected sessions to identify users.
        /// </summary>
        /// <returns>
        /// A list of unique string usernames of users currently logged into an active or connected session.
        /// Returns an empty list if an error occurs during the process, if no server handle can be obtained,
        /// or if no users are found in qualifying sessions.
        /// </returns>
        public List<string> GetLoggedInUsersWts()
        {
            List<string> loggedInUsers = new List<string>();
            IntPtr serverHandle = IntPtr.Zero;
            IntPtr sessionInfoPtr = IntPtr.Zero;
            int sessionCount = 0;

            try
            {
                serverHandle = WTSOpenServer(Environment.MachineName);
                if (serverHandle == IntPtr.Zero)
                {
                    _logger.LogError("Failed to open handle to local WTS server. Error code: {ErrorCode}", Marshal.GetLastWin32Error());
                    return loggedInUsers;
                }

                if (WTSEnumerateSessions(serverHandle, 0, 1, ref sessionInfoPtr, ref sessionCount))
                {
                    _logger.LogDebug("Found {SessionCount} WTS sessions.", sessionCount);
                    IntPtr currentSessionPtr = sessionInfoPtr;
                    for (int i = 0; i < sessionCount; i++)
                    {
                        WTS_SESSION_INFO si = (WTS_SESSION_INFO)Marshal.PtrToStructure(currentSessionPtr, typeof(WTS_SESSION_INFO))!;
                        currentSessionPtr = IntPtr.Add(currentSessionPtr, Marshal.SizeOf(typeof(WTS_SESSION_INFO)));

                        if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive || si.State == WTS_CONNECTSTATE_CLASS.WTSConnected)
                        {
                            IntPtr userNamePtr = IntPtr.Zero;
                            uint bytesReturned = 0;
                            if (WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSUserName, out userNamePtr, out bytesReturned) && bytesReturned > 1)
                            {
                                string? userName = Marshal.PtrToStringAnsi(userNamePtr);
                                WTSFreeMemory(userNamePtr);

                                if (!string.IsNullOrEmpty(userName) && !loggedInUsers.Contains(userName))
                                {
                                    loggedInUsers.Add(userName);
                                    _logger.LogDebug("Found logged-in user: {UserName} in Session ID: {SessionId}", userName, si.SessionID);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Could not query username for Session ID: {SessionId}. Error code: {ErrorCode}", si.SessionID, Marshal.GetLastWin32Error());
                            }
                        }
                    }
                }
                else
                {
                     _logger.LogError("Failed to enumerate WTS sessions. Error code: {ErrorCode}", Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in GetLoggedInUsersWts.");
            }
            finally
            {
                if (sessionInfoPtr != IntPtr.Zero) WTSFreeMemory(sessionInfoPtr);
                if (serverHandle != IntPtr.Zero) WTSCloseServer(serverHandle);
            }

            return loggedInUsers.Distinct().ToList();
        }
    }
}
