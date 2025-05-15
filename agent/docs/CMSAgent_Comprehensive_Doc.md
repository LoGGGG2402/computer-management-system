# Comprehensive Documentation: Operation, Communication, and Configuration of CMSAgent

**Last updated:** May 13, 2025

## I. Agent Overview

CMSAgent is an application running on client machines with the following main tasks:

- **Information Collection:** Gather detailed system hardware information and monitor resource usage status (CPU, RAM, disk) in real-time.
- **Server Communication:** Establish and maintain secure connections with the central server to send collected information and receive control instructions.
- **Command Execution:** Receive and execute remote commands sent from the server (e.g., run scripts, collect specific logs).
- **Automatic Updates:** Ability to automatically download and install new versions of itself when notified by the server.
- **Stable Operation:** Designed to run as a Windows Service, ensuring background operation, continuous functionality, and automatic startup with the system.

## II. Operating Environment and Requirements

- **Supported Operating Systems:**
    - Windows 10 (version 1903 or later recommended, 64-bit).
    - Windows 11 (64-bit).
    - Windows Server 2016, Windows Server 2019, Windows Server 2022 (64-bit).
    - *Note:* Thorough compatibility testing of specific system APIs (e.g., WMI, Performance Counters) is required if there are plans to support older Windows versions or 32-bit versions.
- **Software Dependencies:**
    - **.NET Runtime:** The .NET version the agent is compiled with (e.g., .NET 6.0 LTS or .NET 8.0 LTS). This runtime needs to be installed on the client machine if the agent is not deployed as a "self-contained application".
    - **External Libraries (Expected NuGet Packages):**
        
        
        | Package | Recommended Version | Notes |
        | --- | --- | --- |
        | SocketIOClient.Net | 3.x.x | WebSocket (Socket.IO) communication with server. |
        | Serilog | 2.x.x or 3.x.x | Logging framework. |
        | Serilog.Sinks.File | 5.x.x | File logging. |
        | Serilog.Sinks.Console | 3.x.x or 4.x.x | Console logging (useful for debugging). |
        | Serilog.Sinks.EventLog | 3.x.x | Windows Event Log logging. |
        | System.Management | 6.0.x / 8.0.x | Access to Windows Management Instrumentation (WMI) for hardware information. |
        | System.CommandLine | 2.0.0-betaX | Powerful command-line argument processing. |
        | Microsoft.Extensions.DependencyInjection | 6.0.x / 8.0.x | Dependency Injection implementation. |
        | Microsoft.Extensions.Hosting | 6.0.x / 8.0.x | Support for hosting console applications as Windows Services. |
        | Microsoft.Extensions.Hosting.WindowsServices | 6.0.x / 8.0.x | Windows Services integration. |
        | Microsoft.Extensions.Logging | 6.0.x / 8.0.x | Basic .NET logging framework. |
        | Microsoft.Extensions.Logging.EventLog | 6.0.x / 8.0.x | Event Log provider for Microsoft.Extensions.Logging. |
- **Required Permissions:**
    - **During Installation (Setup.CMSAgent.exe and CMSAgent.exe configure):**
        - Administrator rights are required to:
            - Write files to the installation directory (e.g., C:\Program Files\CMSAgent).
            - Create and write files/directories to the common data directory (e.g., C:\ProgramData\CMSAgent).
            - Register, configure, and start Windows Service.
    - **When Agent Operates As Windows Service (running under LocalSystem account):**
        - Data directory (C:\ProgramData\CMSAgent and its subdirectories): LocalSystem account needs Full Control.
        - Installation directory (C:\Program Files\CMSAgent): LocalSystem account needs Read & Execute. During updates, CMSUpdater.exe will need Modify permission.
        - Network access: For connecting to the server.
        - System information access: Permission to access WMI, performance counters, registry.
        - Console command execution: Commands will run with LocalSystem privileges.
        - *See details on directory access permissions setup in **Section VIII.3**.*
    - **When Running CLI Commands (by user/administrator after installation):**
        - CMSAgent.exe start, stop, uninstall: Requires Administrator privileges.
        - CMSAgent.exe configure (for reconfiguration): Requires Administrator privileges to write to C:\ProgramData\CMSAgent\runtime_config\runtime_config.json.
        - CMSAgent.exe debug: Can run with regular user privileges, but access to some system resources or writing to ProgramData may be limited.

## III. Installation and Initial Configuration Flow

This flow describes the process from when a user executes the installation file until the agent is installed, initially configured, and begins operating as a Windows Service.

1. **Prepare Installation Package (Developer Task):**
    - An installation package (e.g., Setup.CMSAgent.exe) is created, containing components:
        - CMSAgent.exe: Main agent executable.
        - CMSUpdater.exe: Executable for the self-update process.
        - `appsettings.json` (default): Default main configuration file.
        - Other necessary DLL libraries.
2. **Execute Installer (By User/Administrator):**
    - User runs Setup.CMSAgent.exe with Administrator privileges.
3. **Step 1: Copy Application Files:**
    - Installer copies necessary files to installation directory (e.g., C:\Program Files\CMSAgent).
4. **Step 2: Create Data Directory Structure and Set Permissions:**
    - Create main data directory (e.g., C:\ProgramData\CMSAgent) and subdirectories: `logs/`, `runtime_config/`, `updates/`, `error_reports/`.
    - **Important:** Set "Full Control" permissions for LocalSystem on `C:\\ProgramData\\CMSAgent` and its subdirectories. (See Section VIII.3).
5. **Collect and Validate Runtime Configuration (via `CMSAgent.exe configure`):**
    - **Activation:** Installer executes: `"<Installation_path>\\CMSAgent.exe" configure`.
    - **CLI Interaction:** `CMSAgent.exe configure` opens console to collect location information and perform initial validation with server.
    - **Create/Check `agentId`:** Save unique `agentId` to `runtime_config/runtime_config.json`.
    - **Enter Location Information and Server Validation:** Request `roomName`, `posX`, `posY`. Send identification request to server. Process response (position error, MFA request, success).
    - **Handle configuration cancellation:** If user cancels (Ctrl+C), exit without saving changes (except `agentId`).
6. **Store Runtime Configuration and Token:**
    - After successful validation, save `room_config` and `agent_token` (encrypted) to `runtime_config/runtime_config.json`.
7. **Register and Start Windows Service (By Installer):**
    - Register `CMSAgent.exe` as Windows Service (ServiceName: "CMSAgentService", StartType: Automatic, Account: LocalSystem).
    - Start service.
8. **Complete Installation:** Display installation success message.

## IV. Regular Agent Operation Flow

1. **Service Start:** SCM starts `CMSAgent.exe`. Agent switches to `INITIALIZING` state.
2. **Set up Logging:** Initialize Serilog (read configuration from `appsettings.json`). Log `INITIALIZING` state.
3. **Ensure Data Directory Structure:** Create if not exists.
4. **Ensure Single Instance:** Use Mutex (See Section VIII.5).
5. **Load Configuration:**
    - Read configuration from `appsettings.json`.
    - Read `runtime_config/runtime_config.json`.
    - Validate configuration (See Section VII.4).
    - Decrypt `agent_token`.
6. **Check Runtime Configuration Integrity:** If missing or invalid, switch to `ERROR` state.
7. **Initialize Modules:** HTTP client, WebSocket client, resource monitoring, command execution, update handling.
8. **Notes When Operating As Windows Service:** Required permissions, availability of configuration files, stable network connection, safe error handling in `OnStart()`, no desktop interaction, careful resource management, always use absolute paths or paths relative to executable, and `CMSUpdater.exe` requires SCM interaction privileges.
9. **Initial Authentication and Connection with Server:**
    - Agent switches to `AUTHENTICATING` state. Log state.
    - **WebSocket Connection (Socket.IO):**
        - Agent uses `agentId` (as `agentId`) and `agent_token` to establish WebSocket connection to server.
        - **During WebSocket handshake, Agent MUST send header `x-client-type: agent`.**
        - **Agent MUST send the following headers during handshake:**
            - `Authorization: Bearer <agent_token>`
            - `X-Agent-Id: <agentId>` 
        - Server middleware will automatically try to extract `authToken` and `agentId` from these headers and store in `socket.data`.
        - Complete server-side authentication logic (in `setupAgentHandlers`) will use information in `socket.data` (if available from headers) or might wait for `agent:authenticate` event if header information is insufficient or not sent.
        - **Authentication via Event (Fallback):** If agent doesn't send authentication headers, or if server-side logic (in `setupAgentHandlers`) determines header information is invalid/insufficient, server might wait for agent to send `agent:authenticate` event with payload `{ agentId, token }`.
        - Listen for `agent:ws_auth_success` event from server. Upon receipt, switch to `CONNECTED` state. Log state.
        - If receiving `agent:ws_auth_failed` (e.g., expired/invalid token):
            - Log error.
            - Try to perform POST `/api/agent/identify` process again (using stored `agentId` and `room_config`, without `forceRenewToken`).
            - If `identify` succeeds and receives new token, update local token (encrypt and save to `runtime_config.json`), return to WebSocket connection step (including sending required headers).
            - If `identify` requires MFA, agent in service context cannot process, will log error and switch to `DISCONNECTED` state, retry after a time interval.
            - If `identify` fails for other reasons, log error, switch to `DISCONNECTED` state, retry later.
    - **Send Initial Hardware Information (HTTP POST `/api/agent/hardware-info`):**
        - After WebSocket connection is authenticated (`CONNECTED`) or after having valid token from HTTP `identify`, collect detailed hardware information.
        - Send this information to server. If fails, log error and continue.
10. **Main Operation Loop (State `CONNECTED`):**
    - Send periodic status reports (WebSocket `agent:status_update`).
    - Check for updates (GET `/api/agent/check-update` or WebSocket `agent:new_version_available`). If available, switch to `UPDATING` state.
    - Process commands from server (WebSocket `command:execute`).
    - Report occurring errors (POST `/api/agent/report-error`). If fails, save to `error_reports/`.
11. **Handle Connection Loss (State `DISCONNECTED`):**
    - SocketIOClient.Net automatically attempts to reconnect.
    - Pause status reporting. Temporarily store data (See IV.12).
    - When reconnected, switch back to `AUTHENTICATING` -> `CONNECTED`.
12. **Offline Operation and Temporary Storage (Queueing):**
    - Temporary disk storage for: status reports, command results, error reports.
    - Storage limits configured in `appsettings.json`.
    - Send when connection is restored.
13. **Internal State Management:** Agent manages and logs key operational states.
    
    
    | State | Meaning |
    | --- | --- |
    | `INITIALIZING` | Agent starting, loading initial configuration and modules. |
    | `AUTHENTICATING` | In the process of connecting and authenticating WebSocket with server. |
    | `CONNECTED` | Successfully connected and authenticated with server, operating normally. |
    | `DISCONNECTED` | Lost connection with server, in process of automatic reconnection attempts. |
    | `UPDATING` | In the process of downloading and preparing for new version update. |
    | `ERROR` | Encountered critical unrecoverable error (e.g., corrupted configuration), cannot operate. |
    | `STOPPING` | In the process of safely stopping (e.g., when requested by SCM). |
14. **Safe Shutdown:** Switch to `STOPPING` state. Disconnect WebSocket, complete running commands, cancel timers, release Mutex.

## V. Agent Update Flow

1. **Update Trigger:** Receive new version information from server.
2. **Update Preparation:**
    - Switch to `UPDATING` state.
    - Notify server (`agent:update_status` with `status: "update_started"`).
    - Download update package. Notify server (`status: "update_downloaded"`).
    - Verify checksum. If error, notify server (`status: "update_failed", reason: "checksum_mismatch"`), return to `CONNECTED`.
    - Extract package. Notify server (`status: "update_extracted"`).
    - Identify `CMSUpdater.exe` file.
    - Launch `CMSUpdater.exe`. Notify server (`status: "updater_launched"`).
3. **Old Agent Self-Termination:** Switch to `STOPPING` state.
4. **Updater Process Operation (`CMSUpdater.exe`):**
    - Wait for old agent to stop.
    - Backup old agent.
    - Deploy new agent. If error, rollback.
    - Start new agent service. If error, rollback, old agent (if restored) reports error (`status: "update_failed", reason: "service_start_failed"`).
    - **Handle Post-Update Crashes (Advanced Rollback):** "Watchdog" mechanism in Updater automatically rolls back if new agent crashes repeatedly.
    - **Cleanup:** If successful, delete backup, temporary files. New agent reports success (`status: "update_success"`) upon startup.
5. **Handle errors during update process:**
    - **Unable to download update package:** Agent logs `UPDATE_DOWNLOAD_FAILED` error, retries according to retry mechanism (using `NetworkRetryMaxAttempts` and `NetworkRetryInitialDelaySec` from `CMSAgentSettings:AgentSettings` in `appsettings.json`). If completely fails after retry attempts, agent will notify error to server (e.g., `agent:update_status` with `status: "update_failed", reason: "download_failed"`) and return to `CONNECTED` state.
    - **Checksum mismatch:** Agent deletes downloaded file, logs `UPDATE_CHECKSUM_MISMATCH` error, notifies server (`agent:update_status` with `status: "update_failed", reason: "checksum_mismatch"`), and returns to `CONNECTED` state.

## VI. Detailed Agent-Server Communication Protocol

### A. HTTP Communication (API Endpoints)

- **API Base URL:** Defined in `appsettings.json` (e.g., section `CMSAgentSettings:ServerUrl`), example: `https://your-server.com:3000/api/agent/`.
- **Common Headers (For authenticated requests):**
    - `X-Agent-Id`: `<agentId>` (The agent's `agentId` value)
    - `Authorization`: `Bearer <agent_token>` (Token received after authentication)
    - `Content-Type`: `application/json` (For requests with JSON body)

**1. Agent Identification (POST `/identify`)**

- **Purpose:** Register new agent or identify an existing agent with the server.
- **Request Payload (JSON):**
    
    ```
    {
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "positionInfo": {
            "roomName": "Lab Room A",
            "posX": 10,
            "posY": 15
        },
        "forceRenewToken": false
    }
    
    ```
    
    - `agentId` (String, Required): Unique device ID of the agent.
    - `positionInfo` (Object, Required):
        - `roomName` (String, Required): Room name.
        - `posX` (Number, Required): X coordinate.
        - `posY` (Number, Required): Y coordinate.
    - `forceRenewToken` (Boolean, Optional, Default: `false`): If `true`, requests the server to issue a new token even if the agent already has a valid token.
- **Response Payload (JSON) - Success (new/renewed token):**
    
    ```
    {
        "status": "success",
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "agentToken": "new_or_renewed_plain_text_token_string"
    }
    
    ```
    
- **Response Payload (JSON) - Success (agent exists, old token valid, `forceRenewToken` is `false`):**
    
    ```
    {
        "status": "success"
    }
    
    ```
    
- **Response Payload (JSON) - MFA Required:**
    
    ```
    {
        "status": "mfa_required",
        "message": "MFA is required for this agent."
    }
    
    ```
    
- **Response Payload (JSON) - Position Error (HTTP 400):**
    
    ```
    {
        "status": "position_error",
        "message": "Position (10,15) in Room 'Lab Room A' is already occupied or invalid."
    }
    
    ```
    
- **Response Payload (JSON) - Other Error (e.g., empty `agentId` - HTTP 400):**
    
    ```
    {
        "status": "error",
        "message": "Agent ID is required"
    }
    
    ```
    

**2. MFA Authentication (POST `/verify-mfa`)**

- **Purpose:** Complete identification process by sending user-provided MFA code.
- **Request Payload (JSON):**
    
    ```
    {
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "mfaCode": "123456"
    }
    
    ```
    
- **Response Payload (JSON) - Success:**
    
    ```
    {
        "status": "success",
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "agentToken": "plain_text_token_string_after_mfa"
    }
    
    ```
    
- **Response Payload (JSON) - Failure (HTTP 401):**
    
    ```
    {
        "status": "error",
        "message": "Invalid or expired MFA code"
    }
    
    ```
    

**3. Send Hardware Information (POST `/hardware-info`)**

- **Purpose:** Provide detailed information about client machine hardware to server.
- **Request Payload (JSON):**
    
    ```
    {
        "os_info": "Microsoft Windows 10 Pro 10.0.19042 Build 19042",
        "cpu_info": "Intel(R) Core(TM) i7-8700 CPU @ 3.20GHz",
        "gpu_info": "NVIDIA GeForce GTX 1080 Ti",
        "total_ram": 17179869184,
        "total_disk_space": 511749009408
    }
    
    ```
    
    - `os_info` (String): Operating system information.
    - `cpu_info` (String): CPU information.
    - `gpu_info` (String): GPU information.
    - `total_ram` (Number): Total RAM (bytes).
    - `total_disk_space` (Number, Required): Total C: drive capacity (bytes).
- **Response - Success:** HTTP 204 No Content.
- **Response Payload (JSON) - Error (HTTP 400, e.g., `total_disk_space` missing):**
    
    ```
    {
        "status": "error",
        "message": "Total disk space is required"
    }
    
    ```
    

**4. Check For Updates (GET `/check-update`)**

- **Purpose:** Check if a new agent version is available on the server.
- **Query Parameters:**
    - `current_version` (String): Current agent version (e.g., "1.0.2", from `appsettings.json` or assembly).
- **Response Payload (JSON) - Update Available:**
    
    ```
    {
        "status": "success",
        "update_available": true,
        "version": "1.1.0",
        "download_url": "/download/agent-packages/agent_v1.1.0.zip",
        "checksum_sha256": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2",
        "notes": "New features and important bug fixes."
    }
    
    ```
    
- **Response - No Update:** HTTP 204 No Content.

**5. Error Reporting (POST `/report-error`)**

- **Purpose:** Send information about errors occurring in the agent to the server.
- **Request Payload (JSON):**
    
    ```
    {
        "error_type": "WEBSOCKET_CONNECTION_FAILED",
        "error_message": "Failed to connect to WebSocket after multiple retries.",
        "error_details": {
            "stack_trace": "...",
            "agent_version": "1.0.2",
            "server_url_attempted": "wss://server.example.com/socket.io/",
            "last_error_code": "CONNECTION_REFUSED",
            "retry_attempts": 5
        },
        "timestamp": "2025-05-11T10:30:00Z"
    }
    
    ```
    
    - **`error_type` (String, Required):** Error classification. Example values:
        - `WEBSOCKET_CONNECTION_FAILED`
        - `WEBSOCKET_AUTH_FAILED`
        - `HTTP_REQUEST_FAILED`
        - `CONFIG_LOAD_FAILED`
        - `CONFIG_VALIDATION_FAILED`
        - `TOKEN_DECRYPTION_FAILED`
        - `HARDWARE_INFO_COLLECTION_FAILED`
        - `STATUS_REPORTING_FAILED`
        - `COMMAND_EXECUTION_FAILED`
        - `COMMAND_QUEUE_FULL`
        - `UPDATE_DOWNLOAD_FAILED`
        - `UPDATE_CHECKSUM_MISMATCH`
        - `UPDATE_EXTRACTION_FAILED`
        - `UPDATE_ROLLBACK_FAILED`
        - `UPDATE_SERVICE_START_FAILED`
        - `LOGGING_FAILED`
        - `RESOURCE_LIMIT_EXCEEDED`
        - `UNHANDLED_EXCEPTION`
        - `OFFLINE_QUEUE_ERROR`
        - `LOG_UPLOAD_REQUESTED`
- **Example payload for `error_type: "LOG_UPLOAD_REQUESTED"`:**
    
    ```
    {
        "error_type": "LOG_UPLOAD_REQUESTED",
        "error_message": "Log upload requested by server for specific date range/file",
        "error_details": {
            "log_filename": "agent_logs_20250510-20250512.zip",
            "log_content_base64": "UEsDBBQAAgAI..."
        },
        "timestamp": "2025-05-12T14:00:00Z"
    }
    
    ```
    
- **Response - Success:** HTTP 204 No Content.

**6. Download Agent Update Package (GET `/download/agent-packages/:filename`)**

- **Purpose:** Download update package file. Requires agent authentication.
- **URL Parameters:**
    - `:filename` (String): Update package filename (e.g., `agent_v1.1.0.zip`).
- **Response:** File data (File stream).
- **Error:** HTTP 404 Not Found if file doesn't exist, HTTP 401 Unauthorized if token is invalid, HTTP 500 Internal Server Error if other error occurs.

### B. WebSocket Communication (Socket.IO)

- **Connection URL:** From `ServerUrl` in `appsettings.json`.
- **Authentication:**
    - Agent **MUST** send header `x-client-type: agent` during handshake.
    - Agent **MUST** send `agentId` and `token` in `socket.handshake.headers` (specifically `agent-id` and `Authorization: Bearer <token>`). Server middleware (`io.use`) will extract this information.
- **Server-to-Agent Events:**
    - `agent:ws_auth_success`: Payload: `{ "status": "success", "message": "Authentication successful" }`. Meaning: WebSocket authentication successful.
    - `agent:ws_auth_failed`: Payload: `{ "status": "error", "message": "Authentication failed (Invalid ID or token)" }`. Meaning: WebSocket authentication failed.
    - `command:execute`: Payload: `{ "commandId": "...", "command": "...", "commandType": "..." }`. Meaning: Request agent to execute command.
    - `agent:new_version_available`: Payload: `{ "new_stable_version": "...", "timestamp": "..." }`. Meaning: Notification of new agent version.
- **Agent-to-Server Events:**
    - `agent:authenticate`: Payload: `{ "agentId": "...", "token": "..." }`.
    - `agent:status_update`: Payload detailed in section C.
    - `agent:command_result`: Example payload: `{ "commandId": "...", "success": true/false, "type": "...", "result": { ... } }`.
    - `agent:update_status`: Payload: `{ "status": "...", "reason": "...", "new_version": "..." }`.

### C. Status Information (Stats) Sent to Server (via WebSocket `agent:status_update`)

- **Payload (JSON):**
    
    ```
    {
        "cpuUsage": 25.5,
        "ramUsage": 60.1,
        "diskUsage": 75.0
    }
    
    ```
    
- **Frequency:** According to `StatusReportIntervalSec` in `appsettings.json`.

## VII. Detailed Agent Configuration

**1. Application Configuration (Stored in `appsettings.json`)**

The `appsettings.json` file is the main configuration file. *Note: As of version 7.3, this file completely replaces the previous `agent_config.json`.*

```
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "CMSAgent.Communication.SocketIOClientWrapper": "Debug",
        "CMSAgent.Core.UpdateHandler": "Debug"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\ProgramData\\CMSAgent\\logs\\agent_.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 14,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "EventLog",
        "Args": {
          "source": "CMSAgentService",
          "logName": "Application",
          "manageEventSource": true
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  },
  "CMSAgentSettings": {
    "AppName": "CMSAgent",
    "Version": "1.1.0",
    "ServerUrl": "https://your-server.com:3000",
    "AgentSettings": {
      "StatusReportIntervalSec": 30,
      "EnableAutoUpdate": true,
      "AutoUpdateIntervalSec": 86400,
      "NetworkRetryMaxAttempts": 5,
      "NetworkRetryInitialDelaySec": 5,
      "TokenRefreshIntervalSec": 86400,
      "OfflineQueue": {
        "MaxSizeMb": 100,
        "MaxAgeHours": 24,
        "StatusReportsMaxCount": 1000,
        "CommandResultsMaxCount": 500,
        "ErrorReportsMaxCount": 200
      }
    },
    "HttpClientSettings": {
      "RequestTimeoutSec": 15
    },
    "WebSocketSettings": {
      "ReconnectDelayInitialSec": 5,
      "ReconnectDelayMaxSec": 60,
      "ReconnectAttemptsMax": null
    },
    "CommandExecutorSettings": {
      "DefaultTimeoutSec": 300,
      "MaxParallelCommands": 2,
      "MaxQueueSize": 100,
      "ConsoleEncoding": "utf-8"
    },
    "ResourceLimits": {
      "MaxCpuPercentage": 75,
      "MaxRamMegabytes": 512
    }
  }
}

```

**2. Runtime Configuration (Stored in `runtime_config/runtime_config.json`)**

```
{
  "agentId": "AGENT-XYZ123-DEVICEID",
  "room_config": {
    "roomName": "Meeting Room A",
    "posX": 10,
    "posY": 15
  },
  "agent_token_encrypted": "BASE64_ENCRYPTED_TOKEN_STRING"
}

```

**3. Storage Paths**

- `C:\\ProgramData\\CMSAgent`
    - `logs/`
    - `runtime_config/runtime_config.json`
    - `updates/`
        - `download/`
        - `extracted/`
        - `backup/`
    - `error_reports/` (Used as offline queue for error reports)
    - `offline_queue/` (Common directory for other offline queues: status_reports, command_results)

**4. Configuration Validation**

- Agent uses .NET Options Pattern to load and validate configuration sections from `appsettings.json` (e.g., `CMSAgentSettings`). Corresponding Options classes (e.g., `CmsAgentSettingsOptions.cs`) use Data Annotations to check data validity.
- **Example of .NET Options Pattern:** In `Program.cs` or service initialization location, configuration is bound:
    
    ```
    // Assuming builder.Configuration has loaded appsettings.json
    services.Configure<CmsAgentSettingsOptions>(
        builder.Configuration.GetSection("CMSAgentSettings")
    );
    // Then, CmsAgentSettingsOptions can be injected and used.
    // Properties in CmsAgentSettingsOptions can be decorated with Data Annotations
    // [Required], [Range(1, 3600)], [Url] to automatically validate when options are created.
    
    ```
    
- For `runtime_config.json`, agent will manually check the existence and basic format of required fields when loading.

## VIII. Security

**1. Token Encryption (`agent_token_encrypted` in `runtime_config.json`)**

- **Encryption:** When the `CMSAgent.exe configure` command receives `agentToken` (plain text) from server, before saving to `runtime_config.json`, this token will be encrypted. Using `System.Security.Cryptography.ProtectedData.Protect(Encoding.UTF8.GetBytes(plainToken), null, DataProtectionScope.LocalMachine)`. `userData` is `agentToken` converted to `byte[]`. `optionalEntropy` can be `null`. `scope` is `DataProtectionScope.LocalMachine`. The resulting encrypted `byte[]` will be converted to Base64 string for storage in JSON file.
- **Decryption:** When `CMSAgent.exe` (running as Windows Service) starts, read Base64 string `agent_token_encrypted` from `runtime_config.json`. Convert Base64 back to `byte[]`. Use `System.Security.Cryptography.ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine)`. The decrypted `byte[]` result will be converted back to `agentToken` string (UTF8).
- **Key management:** Encryption key is automatically managed by Windows through Data Protection API (DPAPI) and tied to the local machine when using `DataProtectionScope.LocalMachine`.

**2. Connection Security**

- Mandatory HTTPS for API and WSS for WebSocket.
- Consider certificate pinning.

**3. Directory Access Permissions and Setup**

- **Installation directory (`C:\\Program Files\\CMSAgent` or equivalent):**
    - Read & Execute permission for LocalSystem account (service running account) and Authenticated Users group.
    - Modify permission for Administrators and SYSTEM to allow updates and uninstallation.
    - During updates, `CMSUpdater.exe` (if running with LocalSystem privileges or elevated to Administrator) will have permission to overwrite files.
- **Data directory (`C:\\ProgramData\\CMSAgent`):**
    - LocalSystem account needs Full Control permission on this directory and subdirectories (`logs`, `runtime_config`, `updates`, `error_reports`, `offline_queue`).
    - Administrators group should have Full Control permission for management and review.
    - Regular users (e.g., Authenticated Users) should not have write permission to `runtime_config` directory to protect configuration and token. Log read permission can be considered depending on policy.
- **How to set permissions (verified and complete `icacls` commands):**
    - For main data directory:
        
        ```
        icacls "C:\ProgramData\CMSAgent" /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)F" /inheritance:r /Q
        
        ```
        
        *Explanation: `(OI)` - Object Inherit, `(CI)` - Container Inherit, `F` - Full Control. `/inheritance:r` - Remove inherited permissions before applying new ones. `/Q` - Quiet mode.*
        
    - For runtime configuration directory (restricted permissions for Administrators):
        
        ```
        icacls "C:\ProgramData\CMSAgent\runtime_config" /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)RX" /inheritance:r /Q
        
        ```
        
        *Explanation: `RX` - Read & Execute.*
        
    - *Note: These commands need to be executed with Administrator privileges during installation.*

**4. Minimizing Risks With LocalSystem Account**

- **Principle of Least Privilege:** Research possibility of using a custom service account with only the minimum necessary privileges. This is more complex in permission setup but more secure.
- **Remote Command Authentication and Authorization:**
    - Server should strongly authenticate requests from frontend before sending commands to agent.
    - Consider classifying commands by danger level. Dangerous commands (e.g., writing files, changing system configuration) may require additional authentication or be allowed only from senior administrators.
    - Agent can have a whitelist of safe commands or verify digital signatures of scripts/commands before execution.
- **Input Validation:** Agent must thoroughly validate all data received from server (especially command content) to avoid vulnerabilities like command injection.

**5. Unique Mutex Name**

- To avoid conflicts with other applications, Mutex name will include a unique identifier for the product or company.
- **Format:** `Global\\CMSAgentSingletonMutex_<YourCompanyOrProductGUID>`
    - Example: `Global\\CMSAgentSingletonMutex_E17A2F8D-9B74-4A6A-8E0A-3F9F7B1B3C5D`
    - This GUID will be created once and fixed in the agent code.

**6. Proactive Token Refresh**

- **If Server provides expiration time:** Agent will store and schedule token refresh before expiration.
- **If Server doesn't provide expiration time:** Agent will try to refresh token periodically (e.g., every 24 hours, configured in `appsettings.json` via `CMSAgentSettings:AgentSettings:TokenRefreshIntervalSec`) by sending `POST /api/agent/identify` with `forceRenewToken: true`. If this fails, agent will fall back to refresh mechanism when encountering 401 error from WebSocket/HTTP.

## IX. Logging Information

**1. Log File Locations:**

- **Agent Service:** `C:\\ProgramData\\CMSAgent\\logs\\agent_YYYYMMDD.log`. Number of days retained is configured in `appsettings.json` (e.g., `Serilog:WriteTo:File:Args:retainedFileCountLimit`).
- **Updater:** `C:\\ProgramData\\CMSAgent\\logs\\updater_YYYYMMDD_HHMMSS.log`.
- **Configuration process:** Writes to console and `configure_YYYYMMDD_HHMMSS.log` file.

**2. Log Level Configuration (via `appsettings.json`)**
Detailed Serilog configuration (including `MinimumLevel`, `Override`, `WriteTo`, `Enrich`) is set in the `"Serilog"` section of `appsettings.json` (see section VII.1).

**3. Sample Log Content and How to Read**
Each log line must include: Timestamp, Level, SourceContext (Namespace of logging class), Message, and Exception (if any).
Example:

```
2025-05-12 22:15:01.123 +07:00 [INF] [CMSAgent.Core.AgentService] Agent service starting... State: INITIALIZING
2025-05-12 22:15:03.456 +07:00 [DBG] [CMSAgent.Configuration.ConfigLoader] Runtime config loaded successfully from C:\ProgramData\CMSAgent\runtime_config\runtime_config.json
2025-05-12 22:15:05.789 +07:00 [INF] [CMSAgent.Communication.WebSocketConnector] Attempting WebSocket connection to wss://your-server.com:3000... State: AUTHENTICATING
2025-05-12 22:15:06.112 +07:00 [INF] [CMSAgent.Communication.WebSocketConnector] WebSocket connected and authenticated. State: CONNECTED
2025-05-12 22:15:08.990 +07:00 [ERR] [CMSAgent.Core.CommandExecutor] Failed to execute command cmd-uuid-xyz. Timeout expired after 300 seconds.
   System.TimeoutException: The operation has timed out.
   at CMSAgent.Commands.Handlers.ConsoleCommandHandler.ExecuteAsync(CommandRequest commandRequest, CancellationToken cancellationToken)

```

When debugging, look for `ERROR` or `FATAL` logs. Consider surrounding `WARN`, `INFO`, `DEBUG` logs to understand context.

**4. Windows Event Log**
Agent service will record important events (successful startup, shutdown, critical errors that can't be written to log file) to Windows Event Log (typically "Application" log) with a custom "Source" (Event source), e.g., "CMSAgentService", as configured in `appsettings.json`. Event Source registration will be performed during agent installation (with admin rights).

**5. Advanced feature: Remote Log Collection:**

- **Mechanism:** Server can request agent to send recent log files or specific log sections through a special command via WebSocket (`commandType: "system_get_logs"`) or a dedicated API (`POST /api/agent/upload-log`).
- **Safety:** This will be done securely, with authentication and limitations to prevent abuse. Agent only sends logs when receiving a valid request from authenticated server.
- **Compression:** Logs will be compressed (ZIP) before sending to reduce bandwidth.
- **Payload (example when using `POST /api/agent/report-error`):**
    
    ```
    {
        "error_type": "LOG_UPLOAD_REQUESTED",
        "error_message": "Log upload requested by server for specific date range/file",
        "error_details": {
            "log_filename": "agent_logs_20250510-20250512.zip",
            "log_content_base64": "UEsDBBQAAgAI..."
        },
        "timestamp": "2025-05-12T14:00:00Z"
    }
    
    ```
    
    Or, if using a dedicated endpoint like `POST /api/agent/upload-log`, request body could be `multipart/form-data` containing compressed log file.
    

## X. Error Handling and Troubleshooting

**1. Common HTTP Status Codes and Agent Handling:**

- **200 OK:** Request successful. Continue processing response.
- **204 No Content:** Request successful, no content returned. Agent considers it success.
- **400 Bad Request:** Invalid request from agent (missing field, wrong format). Agent handling: Log detailed request and response. Should not retry identical request. Notify user (if during configure) or report error to server (if during operation).
- **401 Unauthorized:** Authentication error (invalid/expired token). Agent handling: If configuring and MFA error: Let user retry input. If operating: Log. Agent tries to refresh token by calling POST `/identify` again (without `forceRenewToken`). If still fails, disconnect WebSocket, and retry entire connection/authentication process after increasing time interval (exponential backoff).
- **403 Forbidden:** Authenticated but no permission. Log, report error to server.
- **404 Not Found:** Endpoint doesn't exist or resource not found (e.g., update file not available). Log.
- **409 Conflict:** Resource conflict (e.g., trying to register position already in use). Agent handling (in `configure`): Notify user to choose different position.
- **429 Too Many Requests:** Server reports agent sending too many requests. Agent handling: Read `Retry-After` header (if present) and wait. If not, use exponential backoff mechanism before retrying.
- **500 Internal Server Error, 502 Bad Gateway, 503 Service Unavailable, 504 Gateway Timeout:** Error from server or network. Agent handling: Log. Retry request after increasing time interval (exponential backoff). Limit number of retries for a specific request (configured in `appsettings.json`).

**2. Agent Critical Error Handling:**

| Error                                      | Agent Action                                                                                                      | Log/Event Log (Level)                                          |
| :----------------------------------------- | :---------------------------------------------------------------------------------------------------------------- | :-------------------------------------------------------------- |
| Prolonged Network Disconnection            | Switch to `DISCONNECTED`, save offline queue, reduce reconnection frequency after 1 hour.                          | `DISCONNECTED` (Information), specific connection errors (Warning/Error) |
| Configuration File (`appsettings.json`) Corrupted/Invalid | Log error to Event Log. Agent cannot start or exits. Service does not continuously auto-restart.    | `CONFIG_LOAD_FAILED` or `CONFIG_VALIDATION_FAILED` (Fatal)    |
| `runtime_config.json` File Corrupted/Missing | Log error to Event Log. Agent cannot authenticate, switches to `ERROR` or exits.                                 | `CONFIG_LOAD_FAILED` (Fatal)                                  |
| Cannot Write Log (to file/Event Log)       | Try to log error to remaining log channel. If all fail, agent stops safely.                                        | Logging error (Error/Fatal to remaining channel)              |
| Unexpected Error (Unhandled Exception)    | Catch error, log detailed stack trace to Event Log. Try to report `UNHANDLED_EXCEPTION` error to server. Stop safely. | `UNHANDLED_EXCEPTION` (Fatal)                                |


**3. Common Troubleshooting Guide:**

- **Issue 1: Agent Service doesn't start / stops immediately.**
    - Check: Windows Event Viewer (Application, System logs) Look for errors related to "CMSAgentService". Agent Log File: `C:\\ProgramData\\CMSAgent\\logs\\`. Look for `ERROR` or `FATAL`. Service Account Permissions: Ensure LocalSystem has sufficient permissions. Configuration Files: Check existence and validity of `appsettings.json` and `runtime_config/runtime_config.json`. .NET Runtime: Ensure required version is installed. Dependencies: Ensure necessary DLLs are present. Mutex: Check Task Manager if another `CMSAgent.exe` is running.
- **Issue 2: Agent Service is running but no connection/data seen on Server.**
    - Check: Agent Log File: Connection, WebSocket, HTTP, authentication errors. `ServerUrl` in `appsettings.json`: Correct URL, server accessible. Firewall: On agent and server. `agent_token`: May be expired/invalid. Log should show HTTP 401 or `agent:ws_auth_failed`. Server Status: Ensure backend server and Socket.IO are operational.
- **Issue 3: Error during `CMSAgent.exe configure`.**
    - Check: Run with Administrator privileges. CLI Error Message: Read carefully. Connection to Server: Ensure machine can connect to `ServerUrl` (in `appsettings.json`). Input Information: Ensure room, coordinates, MFA code are entered correctly.
- **Issue 4: Self-update process fails.**
    - Check: Agent Log File: UpdateHandler logs. Updater Log File: `CMSUpdater.exe` logs in `C:\\ProgramData\\CMSAgent\\logs\\`. Disk Space: Ensure sufficient free space. Write Permissions: LocalSystem needs write permission to installation directory. Locked Files: An agent file might be in use.
- **Issue 5: Command sent from Server not executed by Agent or reports error.**
    - Check: Agent Log File: Logs related to CommandExecutor or specific CommandHandler. WebSocket Connection: Ensure agent is still connected. Command Content: Command might have wrong syntax. Execution Permission: Command is executed with LocalSystem privileges.
- **Issue 6: Cannot collect hardware information.**
    - Check: WMI Access Permission: Ensure LocalSystem account has WMI access. WMI Service: Open `services.msc`, check "Windows Management Instrumentation" service is running. Try restarting. Agent Log: Look for errors related to SystemMonitor or WMI access.
- **Issue 7: Agent consumes too much CPU/RAM.**
    - Check: Agent Log File (Debug/Verbose level): Identify which module or activity is causing it. Performance Profiler: Use .NET profiler tools for deeper analysis. `ResourceLimits` configuration in `appsettings.json`.

## XI. Appendix: Command Line Parameter Structure and Examples

### A. CMSAgent.exe

- **`configure`**: Configure agent initially or reconfigure.
    - Parameters: None. Always CLI interactive.
    - Operation: Request room information, server authentication (MFA if needed), save runtime configuration.
    - Permissions: Administrator.
    - Usage example: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" configure`
    - Error codes: 0 (Success), 1 (General error), 2 (Insufficient permissions), 3 (Canceled), 4 (Connection/server authentication error), 5 (Runtime config save error).
- **`uninstall`**: Remove agent.
    - Optional parameter: `-remove-data` (Delete agent data directory).
    - Operation: Stop service, unregister, delete files.
    - Permissions: Administrator.
    - Example: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" uninstall --remove-data`
    - *Explanation:* Uninstall agent and delete entire agent data directory at `C:\\ProgramData\\CMSAgent`.
    - Error codes: 0 (Success), 1 (General error), 2 (Insufficient permissions), 6 (Service stop/unregister error).
- **`start`**: Start agent Windows Service.
    - Permissions: Administrator.
    - Usage example: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" start`
    - Error codes: 0 (Success/Request sent to SCM), 1 (General error), 2 (Insufficient permissions), 7 (Service not installed/start error).
- **`stop`**: Stop agent Windows Service.
    - Permissions: Administrator.
    - Usage example: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" stop`
    - Error codes: 0 (Success/Request sent to SCM), 1 (General error), 2 (Insufficient permissions), 8 (Service not installed/stop error).
- **`debug`**: Run agent in current console.
    - Example: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" debug`
    - *Explanation:* Run agent in console mode instead of Windows Service. Logs will be displayed directly on console, useful for debugging and real-time activity monitoring.

### B. CMSUpdater.exe

- **Required parameters:**
    - `pid <process_id>`: PID of old CMSAgent.exe process to stop.
    - `new-agent-path "<new_agent_directory_path>"`: Path to directory containing extracted new agent files.
    - `current-agent-install-dir "<current_agent_installation_path>"`: Current installation directory path.
    - `updater-log-dir "<logs_directory_path>"`: Where to write updater log files.
    - `current-agent-version "<old_agent_version>"`: Current agent version (used for backup name).
- **Possible Error Codes (Exit Codes):**
    - 0: Update successful.
    - 10: Error: Cannot stop old agent.
    - 11: Error: Old agent backup failed.
    - 12: Error: New agent deployment failed.
    - 13: Error: New agent service start failed.
    - 14: Error: Rollback failed.
    - 15: Command line parameter error.
    - 16: Error: Timeout waiting for old agent to stop.
    - 99: Updater general undefined error.