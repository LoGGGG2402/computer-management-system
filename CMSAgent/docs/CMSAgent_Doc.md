# CMSAgent Documentation

## 1. Overview

CMSAgent is a software application that runs in the background on client computers (end-user machines or servers). Its primary responsibilities include collecting system information, monitoring resource usage, executing commands from a central management server, and performing automatic updates. The agent is designed for stability, efficiency, and security, minimizing performance impact on client machines.

## 2. Platform and Deployment

- **Development Platform:** Built using C# on .NET 8 LTS for stability, performance, and long-term support.
- **Supported Operating Systems:**
    - Windows 10 (version 1903 and above, 64-bit).
    - Windows 11 (64-bit).
    - Windows Server 2016, 2019, 2022 (64-bit).
- **Target Architecture:** `win-x64`.
- **Deployment Form:**
    - **Self-Contained Deployment:** Packaged with .NET 8 Runtime, eliminating the need for pre-installed .NET on client machines.
    - **Optimization:**
        - `PublishSingleFile=true`: Produces a single executable (`CMSAgent.Service.exe`) for easier distribution.
        - `IncludeNativeLibrariesForSelfExtract=true`: Embeds native libraries for self-extraction.
        - `PublishReadyToRun=true`: Compiles to ReadyToRun format for faster startup.

## 3. Operating as a Windows Service

The agent operates as a Windows Service for continuous operation and remote management.

- **Service Registration:**
    - **Service Name:** `CMSAgentService`.
    - **Display Name:** "Computer Management System Agent".
    - **Description:** "Collects system information and executes tasks for the Computer Management System."
- **Startup Mode:** `Automatic`.
- **Run Account:** `LocalSystem` for necessary system permissions.
- **Single Instance Enforcement:**
    - Uses `System.Threading.Mutex` with a unique identifier (`Global\CMSAgentSingletonMutex_<GUID>`).
    - Checks Mutex at startup in `Program.Main` or `AgentWorker.StartAsync`. Exits with a logged error if another instance is detected.
- **Service Lifecycle:**
    - Built on `Microsoft.Extensions.Hosting.BackgroundService` with a core worker class (`AgentWorker` in `CMSAgent.Service`).
    - `AgentWorker.ExecuteAsync` initializes and manages `AgentCoreOrchestrator` (in `CMSAgent.Core`), which handles business logic, including resource monitoring, WebSocket communication, and command execution.
- **Graceful Shutdown:**
    - Handles `stoppingToken` in `AgentWorker.ExecuteAsync`, propagated to `AgentCoreOrchestrator` and tasks.
    - Shutdown steps:
        1. Disconnect WebSocket (`socketToDispose.Disconnect()`).
        2. Complete or cancel ongoing tasks.
        3. Cancel timers.
        4. Release Mutex.
        5. Log shutdown.
- **Critical Error Handling:**
    - Logs and exits safely on unrecoverable errors (e.g., corrupted configuration, Mutex creation failure).

## 4. Initial Setup and Configuration

The setup process is streamlined for simplicity and security, with the agentToken issued only during the configure command and used persistently until reconfiguration.

- **Installer:**
    - Single executable (e.g., `Setup.CMSAgent.vX.Y.Z.exe`) created with Inno Setup.
    - Requires Administrator privileges for file operations and service registration.
- **Setup Procedure:**
    1. **File Deployment:**
        - Copies `CMSAgent.Service.exe` (self-contained) to `C:\Program Files\CMSAgent`.
        - Copies `CMSUpdater.exe` to `C:\Program Files\CMSAgent\Updater`.
    2. **Data Directory Structure:**
        - Creates `C:\ProgramData\CMSAgent` with subdirectories:
            - `logs`: Stores agent and updater logs.
            - `runtime_config`: Stores `runtime_config.json`.
            - `updates`: Contains `download`, `extracted`, and `backup` subdirectories.
            - `error_reports`: Stores error report files.
    3. **Permissions:**
        - Grants `Full Control` to `NT AUTHORITY\SYSTEM` on `C:\ProgramData\CMSAgent` using `icacls.exe`.
        - Grants `Read & Execute` to `BUILTIN\Administrators`.
        - Restricts write access to `runtime_config` for non-privileged users.
    4. **Service Registration:**
        - Registers `CMSAgentService` using `sc.exe create` with specified properties.
    5. **Event Log Source:**
        - Registers a source (`CMSAgentService`) for Windows Event Log.
- **Initial Configuration (via `CMSAgent.Service.exe configure`):**
    1. **AgentId Management:**
        - Reads `C:\ProgramData\CMSAgent\runtime_config\runtime_config.json`.
        - If `AgentId` is missing or invalid, generates a new one using `System.Guid.NewGuid().ToString()`.
    2. **User Interaction:**
        - Displays `AgentId`.
        - Prompts for location: `RoomName` (string), `PosX` (integer), `PosY` (integer).
    3. **Server Identification:**
        - Sends `POST /api/agent/identify` with:
            
            ```json
            {
              "agentId": "string",
              "positionInfo": {
                "roomName": "string",
                "posX": "integer",
                "posY": "integer"
              }
            }
            ```
            
        - Handles responses:
            - `status: "mfa_required"`: Proceeds to MFA flow.
            - `status: "success"`: Receives `agentToken`, which remains valid until the next configure.
            - `status: "position_error"`: Prompts user to re-enter location and retries.
            - Other errors: Logs and allows retry or exit.
    4. **MFA Authentication (if required):**
        - Prompts for 6-character `mfaCode`.
        - Sends `POST /api/agent/verify-mfa` with:
            
            ```json
            {
              "agentId": "string",
              "mfaCode": "string"
            }
            ```
            
        - Handles responses:
            - `status: "success"`: Receives `agentToken`, valid until next configure.
            - `401 Unauthorized`: Prompts for retry.
            - Other errors: Logs and notifies user.
    5. **Configuration Storage:**
        - Encrypts `agentToken` using DPAPI (`DataProtectionScope.LocalMachine`).
        - Saves `AgentId`, `RoomConfig` (`roomName`, `posX`, `posY`), and `agent_token_encrypted` to `runtime_config.json` using safe file writing (temporary file and rename).
- **Service Startup:**
    - Starts `CMSAgentService` using `sc.exe start CMSAgentService`.

## 5. Collecting System Information and Monitoring Resources

The agent collects hardware information and monitors resources.

- **Initial Hardware Collection:**
    - After WebSocket connection (`CONNECTED` state) and update check, collects:
        - OS: Name, version, architecture.
        - CPU: Manufacturer, model, cores, threads, clock speed.
        - GPU: Manufacturer, model, VRAM.
        - RAM: Total physical RAM (MB).
        - Disk: Total and available space (MB, typically C: drive).
    - Sends via `POST /api/agent/hardware-info` with:
        
        ```json
        {
          "total_disk_space": "integer",
          "gpu_info": "string",
          "cpu_info": "string",
          "total_ram": "integer",
          "os_info": "string"
        }
        
        ```
        
    - Retries on failure with logged errors.
- **Real-Time Monitoring:**
    - Uses `System.Diagnostics.PerformanceCounter` for:
        - CPU Usage (%).
        - RAM Usage (%).
        - Disk Usage (% on C: drive).
    - Handles initialization exceptions gracefully.
- **Periodic Reporting:**
    - In `CONNECTED` state, sends `agent:status_update` WebSocket event every `StatusReportIntervalSec` (e.g., 60 seconds).
    - Payload:
        
        ```json
        {
          "cpuUsage": "number",
          "ramUsage": "number",
          "diskUsage": "number"
        }
        
        ```
        

## 6. Communicating with the Server

The agent uses WebSocket (Socket.IO over WSS) for real-time communication and HTTPS for other requests. The agentToken is issued during configure and used persistently without refresh.

- **WebSocket Channel:**
    - **Connection:**
        - Connects to `wss://<ServerUrl>/socket.io`.
        - Sends headers:
            - `X-Client-Type: "agent"`.
            - `X-Agent-ID: <agentId>`.
            - `Authorization: Bearer <decrypted_agent_token>`.
        - Listens for:
            - `connect`: Authentication success, enters `CONNECTED` state.
            - `auth_error`: Logs error, notifies administrator to run `CMSAgent.Service.exe configure` to obtain a new token.
    - **Reconnection:**
        - Uses exponential backoff with jitter, configurable via `appsettings.json` (min/max wait, attempts).
        - On persistent `auth_error`, suspends reconnection attempts and logs for manual reconfiguration.
    - **Agent-Emitted Events:**
        - `agent:status_update`: Resource metrics.
        - `agent:command_result`: Command execution results.
    - **Server-Emitted Events:**
        - `command:execute`: Command execution request.
        - `agent:new_version_available`: Update notification.
- **HTTPS Channel:**
    - Uses `IHttpClientFactory` with Polly for retries (configurable retries, wait times).
    - Sends headers for authenticated requests:
        - `X-Agent-ID: <agentId>`.
        - `Authorization: Bearer <decrypted_agent_token>`.
    - **Endpoints:**
        - `POST /api/agent/identify`: Used only during configure.
        - `POST /api/agent/verify-mfa`: Used only during configure.
        - `POST /api/agent/hardware-info`: Submits hardware data.
        - `GET /api/agent/check-update`: Checks for updates.
        - `POST /api/agent/report-error`: Reports errors.
        - `GET /api/agent/agent-packages/:filename`: Downloads update packages.
- **Error Handling:**
    - On `401 Unauthorized` (HTTP) or `auth_error` (WebSocket), logs error and notifies administrator to run `CMSAgent.Service.exe configure`. Does not attempt to refresh token automatically.
- **Offline Queue:**
    - Queues `agent:status_update`, `agent:command_result`, and `report-error` using `System.Threading.Channels`.
    - Limits queue size/age (configurable).
    - Stores `report-error` payloads in `C:\ProgramData\CMSAgent\error_reports` as JSON files with timestamp/GUID names.
    - Processes queue on reconnection, provided the token remains valid.
    - **Error Report Storage:**
        - Each error report is stored as a separate JSON file in `C:\ProgramData\CMSAgent\error_reports`
        - File naming format: `error_YYYYMMDD_HHMMSS_<GUID>.json`
        - File content includes:
            ```json
            {
              "type": "string",
              "message": "string",
              "details": "object",
              "timestamp": "string (ISO 8601)",
              "agentId": "string",
              "agentVersion": "string"
            }
            ```
        - Files are automatically cleaned up after successful transmission
        - Maximum storage duration: 7 days
        - Maximum total storage size: 100MB
- **Agent Token Characteristics:**
    - **Persistence**: Agent tokens are permanent and do not expire
    - **No Refresh Required**: Unlike user authentication tokens, agent tokens do not require periodic refresh
    - **One-time Generation**: Tokens are generated once during agent registration/identification and remain valid until explicitly revoked
    - **Revocation Only**: The only way to invalidate an agent token is through explicit revocation by an administrator
    - **Token Storage**: Agent tokens are cryptographically secure, unique identifiers assigned to each registered agent
    - **Token Validation**: Each API request is validated against the database to ensure the token is valid and matches the claimed agent ID
    - **Security Events**: Any suspicious activity, such as invalid token usage or unexpected agent behavior, is logged and may trigger security alerts
    - **Token Revocation**: Administrator users can manually revoke agent tokens through the admin interface if an agent is compromised or needs to be redeployed

## 7. Executing Commands from a Remote Location

The agent handles remote commands.

- **Receiving Commands:**
    - Listens for `command:execute` WebSocket event with:
        
        ```json
        {
          "command": "string",
          "commandId": "string (uuid)",
          "commandType": "string (default: 'console')"
        }
        
        ```
        
- **Command Queue:**
    - Uses `System.Threading.Channels` with `MaxQueueSize`.
    - Rejects new commands if full, reports via `POST /api/agent/report-error`.
- **Handling Commands:**
    - Processes sequentially or in parallel (up to `MaxParallelCommands`).
    - Uses `CommandHandlerFactory` for `commandType`.
- **Command Types:**
    - **CONSOLE:**
        - Executes in CMD/PowerShell (`parameters.use_powershell`).
        - Captures `stdout`, `stderr`, `exitCode`.
        - Timeout: `parameters.timeout_sec` or default.
    - **SYSTEM_ACTION:**
        - Actions: `Restart`, `Shutdown`, `LogOff`.
        - Parameters: `force`, `delay_sec`.
    - **SOFTWARE_INSTALL:**
        - Downloads from `parameters.package_url`, verifies `parameters.checksum_sha256`.
        - Runs with `parameters.install_arguments`.
    - **SOFTWARE_UNINSTALL:**
        - Uses `parameters.package_name` or `parameters.product_code`.
        - Applies `parameters.uninstall_arguments`.
    - **GET_LOGS:**
        - Collects logs, compresses before sending.
- **Timeout:**
    - Terminates commands exceeding timeout, reports error.
- **Reporting Results:**
    - Sends `agent:command_result` with:
        
        ```json
        {
          "commandId": "string (uuid)",
          "commandType": "string",
          "success": "boolean",
          "result": {
            "stdout": "string",
            "stderr": "string",
            "exitCode": "integer",
            "errorMessage": "string",
            "errorCode": "string"
          }
        }
        
        ```
        

## 8. Automatic Agent Update

The agent updates automatically.

- **Activation:**
    - **Periodic/Initialization:**
        - Checks `GET /api/agent/check-update?current_version=<version>` after WebSocket connection.
        - Repeats per `AutoUpdateIntervalSec` if `EnableAutoUpdate` is true.
    - **Server Notification:**
        - Handles `agent:new_version_available` with:
            
            ```json
            {
              "status": "success",
              "update_available": true,
              "version": "string",
              "download_url": "string",
              "checksum_sha256": "string",
              "notes": "string"
            }
            
            ```
            
- **Update Procedure (CMSAgent.Service):**
    1. Enters `UPDATING` state.
    2. Sends `agent:update_status` with `{ "status": "update_started", "target_version": "<new_version>" }`.
    3. Downloads package to `C:\ProgramData\CMSAgent\updates\download`. Reports `DownloadFailed` on error.
    4. Verifies `checksum_sha256`. Reports `ChecksumMismatch` if invalid.
    5. Extracts to `C:\ProgramData\CMSAgent\updates\extracted\<new_version>`. Reports `ExtractionFailed` on error.
    6. Starts `CMSUpdater.exe` with:
        - `-new-version "<new_version>"`: Version string of the new Agent to be installed
        - `-old-version "<current_version>"`: Version string of the current Agent
        - `-source-path "<extracted_path>"`: Path to the extracted new version files
        - `-service-wait-timeout <timeout>`: Optional timeout for service operations (default: 60s)
        - `-watchdog-period <period>`: Optional monitoring duration (default: 120s)
        - Reports `UpdateLaunchFailed` on error.
    7. Initiates graceful shutdown.
- **CMSUpdater.exe Procedure:**
    1. **Parameters:**
        - `-new-version`: Version string of the new Agent to be installed (e.g., "1.1.0")
        - `-old-version`: Version string of the old Agent (e.g., "1.0.0")
        - `-source-path`: Path to the directory containing the extracted files of the new Agent version (e.g., "C:\ProgramData\CMSAgent\updates\extracted\1.1.0")
        - `-service-wait-timeout`: Optional timeout duration (seconds) to wait for old Agent to stop or new Agent to start (default: 60 seconds)
        - `-watchdog-period`: Optional duration (seconds) for CMSUpdater to monitor the new Agent after startup (default: 120 seconds)
    2. Logs to `C:\ProgramData\CMSAgent\logs\updater_YYYYMMDD_HHMMSS.log`.
    3. Waits for old agent to stop.
    4. Backs up to `C:\ProgramData\CMSAgent\updates\backup\<current_version>`.
    5. Replaces files in `current-agent-install-dir`.
    6. Starts new service. Rolls back on failure.
    7. Monitors new agent (watchdog, 1-2 minutes). Rolls back if unstable.
    8. Cleans up directories on success.
- **Success Reporting:**
    - New agent sends `agent:update_status` with `{ "status": "update_success", "current_version": "<new_version>" }`.
- **Error Handling:**
    - Skips problematic versions using `VersionIgnoreManager` in `C:\ProgramData\CMSAgent\runtime_config\ignored_versions.json`.

## 9. Agent Configuration

- **appsettings.json (`C:\Program Files\CMSAgent`):**
    - Static configurations:
        - Serilog settings.
        - `ServerUrl`, `ApiPath`.
        - `Version`.
        - Intervals: `StatusReportIntervalSec`, `AutoUpdateIntervalSec`, `TokenRefreshIntervalSec`.
        - Retry policies, WebSocket parameters, command settings, queue limits.
    - Uses .NET Options Pattern with validation.
- **runtime_config.json (`C:\ProgramData\CMSAgent\runtime_config`):**
    - Dynamic configurations:
        - `agentId`.
        - `room_config` (`roomName`, `posX`, `posY`).
        - `agent_token_encrypted`.
    - Restricted access to `LocalSystem` and `Administrators`.

## 10. Logging and Diagnostics

- **Library:** Serilog.
- **Outputs:**
    - **File:** `C:\ProgramData\CMSAgent\logs\agent_YYYYMMDD.log` (rolling).
    - **Windows Event Log:** Errors, Fatal, Warnings under `CMSAgentService`.
    - **Console:** In `debug` mode.
- **Log Format:**
    - Timestamp, Log Level, SourceContext, ThreadId, Message, Exception.
- **Remote Collection:**
    - Supports `GET_LOGS` command, compresses logs.
- **Updater Logging:**
    - Logs to `C:\ProgramData\CMSAgent\logs\updater_YYYYMMDD_HHMMSS.log`.

## 11. Security

- **Token Encryption:** Uses DPAPI (`DataProtectionScope.LocalMachine`) for `agent_token`.
- **Communication:**
    - HTTPS for HTTP APIs, WSS for WebSocket.
    - Verifies server SSL/TLS certificates.
- **Permissions:**
    - `Full Control` for `LocalSystem` on `C:\ProgramData\CMSAgent`.
    - `Read & Execute` for `Administrators` on `C:\Program Files\CMSAgent`.
- **LocalSystem Safety:**
    - Relies on server-side command authorization.
    - Validates inputs to prevent injection or traversal.
- **Update Security:**
    - Verifies `checksum_sha256`.
    - Recommends digital signatures for executables and packages.
- **Mutex:** Uses unique `Global\CMSAgentSingletonMutex_<GUID>`.

## 12. Command Line Interface (CLI)

- **`configure`:**
    - Runs setup/reconfiguration.
    - Interacts via console for `AgentId`, location, MFA.
    - Saves to `runtime_config.json`.
- **`debug`:**
    - Runs agent logic in console, logs to stdout.
    - Bypasses service registration.

## 13. Non-functional Requirements

- **Performance:**
    - Low idle resource usage.
    - Optimized monitoring and async I/O.
    - Fast command response.
- **Stability:**
    - Long-term operation without restarts.
    - Auto-reconnects on network issues.
    - Reliable updates with rollback.
- **Maintainability:**
    - Modular code with DI.
    - Extensible design.
    - Unit tests for core logic.

## 14. Agent API Interface (Summary)

- **HTTP API:**
    - **Authentication:**
        - `POST /api/agent/identify`: Returns `mfa_required`, `success`, or `position_error`.
        - `POST /api/agent/verify-mfa`: Returns `agentToken`.
    - **Information Updates:**
        - `POST /api/agent/hardware-info`: Submits hardware data.
        - `POST /api/agent/report-error`: Reports errors (e.g., `DownloadFailed`, `ChecksumMismatch`).
    - **Versioning:**
        - `GET /api/agent/check-update`: Checks for updates.
        - `GET /api/agent/agent-packages/:filename`: Downloads packages.
- **WebSocket API (`/socket.io`):**
    - **Connection:** Authenticates with headers.
    - **Agent Events:** `agent:status_update`, `agent:command_result`.
    - **Server Events:** `command:execute`, `agent:new_version_available`.

## 15. Main Activity Flow (Summary)

- **Initialization and Registration:**
    - Starts as service, checks Mutex.
    - Reads/creates `AgentId` in `runtime_config.json`.
    - Sends `POST /api/agent/identify`, handles MFA if required.
    - Encrypts and saves `agentToken`.
- **Daily Operation:**
    - Establishes WebSocket connection.
    - Checks updates, sends hardware info.
    - Loops: handles commands, monitors resources, reports status, manages errors, shuts down gracefully.
- **Auto-Update:**
    - Checks updates or receives `agent:new_version_available`.
    - Downloads, verifies, extracts, launches `CMSUpdater.exe`.
    - `CMSUpdater.exe` backs up, replaces files, starts new agent, monitors, cleans up.