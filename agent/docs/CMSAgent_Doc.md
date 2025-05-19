## CMSAgent Documentation

### 1. Overview

CMSAgent is a software application installed and running in the background on client computers (end-user machines or servers). Its main responsibilities include collecting system information, monitoring computer resource status, executing commands sent from a central management Server, and automatically updating to new versions when requested. The agent is designed to operate stably, efficiently, and safely, ensuring minimal impact on client machine performance.

### 2. Platform and Deployment

- **Development Platform:** Agent is developed using C# on .NET 8 LTS (Long-Term Support) platform. Choosing .NET 8 LTS ensures stability, high performance, and long-term support.
- **Supported Operating Systems:**
    - Windows 10 (version 1903 and above, only 64-bit architecture).
    - Windows 11 (only 64-bit architecture).
    - Windows Server 2016, 2019, 2022 (only 64-bit architecture).
- **Target Architecture:** `win-x64` (Windows 64-bit).
- **Deployment Form:**
    - **Self-Contained Deployment:** Agent will be packaged with .NET 8 Runtime. This means that the client machine does not need to install .NET Runtime separately before installing agent, simplifying the deployment process.
    - **Optimization of Deployment:**
        - Using the `PublishSingleFile=true` flag in the build process to create a single executable file (e.g., `CMSAgent.Service.exe`). This makes managing and distributing files easier, although it requires careful checking to ensure it does not negatively impact other aspects of the application.
        - Using the `IncludeNativeLibrariesForSelfExtract=true` flag when combined with `PublishSingleFile` to ensure that native libraries needed are embedded and self-extracted when running.
        - Using the `PublishReadyToRun=true` flag to compile source code into ReadyToRun format, improving the agent's startup time significantly.

### 3. Operating as a Windows Service

To ensure that the agent is always running and can be managed remotely, it will be installed and operated as a Windows Service.

- **Service Registration:**
    - **Service Name (ServiceName):** `CMSAgentService`. It can be considered to allow this name to be configured in the future if needed.
    - **Display Name (DisplayName):** "Computer Management System Agent". This name will appear in the Windows Services management interface.
    - **Service Description:** Provides a clear and understandable description of the agent's functionality, e.g., "Agent collects system information and executes tasks for the System Management System."
- **Startup Mode:** `Automatic`. The service will start automatically with the Windows operating system.
- **Service Run Account:** `LocalSystem`. This account provides the necessary permissions for the agent to perform system tasks such as reading hardware information, installing software, or restarting the machine.
- **Ensuring a Single Instance:**
    - Agent must use `System.Threading.Mutex` with a unique global identifier (e.g., `Global\\CMSAgentSingletonMutex_<GUID>`, where `<GUID>` is a unique identifier generated randomly) to prevent multiple instances of the agent from running simultaneously on the same machine.
    - Mutex checking must be performed right at the beginning of the agent's startup process (e.g., in the `Program.Main` method or at the beginning of `AgentWorker.StartAsync`). If another instance is detected (Mutex is held), the current agent instance must exit immediately and log the event.
- **Service Lifecycle and Core Logic Structure:**
    - Agent will be built based on the `Microsoft.Extensions.Hosting.BackgroundService` class of .NET. A main worker class (e.g., `AgentWorker` in the `CMSAgent.Service` project) will inherit from `BackgroundService`.
    - The `ExecuteAsync(CancellationToken stoppingToken)` method of the `AgentWorker` class will be the starting point, responsible for initializing, coordinating, and managing the lifecycle of a central dispatcher (ví dụ: `AgentCoreOrchestrator` from the `CMSAgent.Core` project).
    - The `AgentCoreOrchestrator` class will contain the core business logic of the agent. This includes starting and managing long-running tasks (such as monitoring resources, maintaining WebSocket connection, periodic checks) and coordinating the operation between different functional modules (communication, command execution, updating, etc.).
- **Graceful Shutdown:**
    - Agent must handle `stoppingToken` consistently in `AgentWorker.ExecuteAsync`. This token must be passed down to `AgentCoreOrchestrator` and all long-running tasks, loops, and I/O operations. This allows the service to stop safely and with control when requested by the Service Control Manager (SCM) of Windows or other signals.
    - Actions to be taken when stopping include: disconnecting network (WebSocket, HTTP), completing ongoing tasks (if possible within the allowed time), canceling running timers, releasing Mutex held, and logging the shutdown state before the process exits completely.
- **Handling Critical Errors:** In case of a critical error that cannot be recovered during startup or operation (e.g., configuration file corrupted, unable to create Mutex), the agent must log the error details and exit safely to avoid causing unwanted issues to the system.

### 4. Initial Setup and Configuration

The initial setup and configuration process is designed to be simple and secure.

- **Installer:**
    - Providing a single installer file (e.g., `Setup.CMSAgent.vX.Y.Z.exe`). This file will be created by an installer tool like Inno Setup.
    - The installer must require and run with Administrator permissions to be able to perform tasks such as writing to the Program Files directory, registering a Windows service, and setting up directory access permissions.
- **Setup Procedure (performed by the installer):**
    1. **Unzipping Files:** Copying the agent's files (including `CMSAgent.Service.exe` already published as self-contained and `CMSUpdater.exe`) into the specified installation directory (e.g., `C:\Program Files\CMSAgent` and `C:\Program Files\CMSAgent\Updater` for `CMSUpdater.exe`).
    2. **Creating Data Directory Structure:** Creating `C:\ProgramData\CMSAgent` and necessary subdirectories inside it:
        - `logs`: Storing agent and updater log files.
        - `runtime_config`: Storing agent dynamic configuration files.
        - `updates`: Storing downloaded update packages, extracting, and backing up. Includes subdirectories: `download`, `extracted`, `backup`.
        - `error_reports`: Storing detailed error reports in file format.
    3. **Setting Directory Permissions:** Using the `icacls.exe` tool (or equivalent .NET APIs) to grant `Full Control` permissions to the `NT AUTHORITY\SYSTEM` account (the service account) on `C:\ProgramData\CMSAgent` and its subdirectories. At the same time, granting appropriate permissions (e.g., Read & Execute) to `BUILTIN\Administrators`. Restricting write access to the `runtime_config` directory for regular users to protect agent configuration.
    4. **Registering Windows Service:** Using the `sc.exe create` tool (or .NET service management APIs) to create a Windows service with the properties defined in Section 3 (ServiceName, DisplayName, StartMode, Account).
    5. **Registering Event Log Source:** Registering a separate event source for the agent in Windows Event Log so that the agent can log important messages or errors to it.
- **Initial Configuration (via command `CMSAgent.Service.exe configure` called by the installer after completing the above steps):**
    1. **Reading/Creating AgentId:**
        - Trying to read the `C:\ProgramData\CMSAgent\runtime_config\runtime_config.json` configuration file.
        - If the file does not exist, or `AgentId` is missing or invalid in the file, the agent will create a new `AgentId` using `System.Guid.NewGuid().ToString()`.
    2. **CLI Interface Interaction:**
        - Displaying the `AgentId` just read or newly created for the user.
        - Requesting the user to input the machine's location: `RoomName` (room name), `PosX` (X coordinate), `PosY` (Y coordinate).
    3. **Authentication with Server (Identify Flow):**
        - Agent sends a `POST` request to the `/api/agent/identify` endpoint of the Server.
        - Payload of the request:
            
            ```
            {
              "agentId": "...", // AgentId has been identified
              "positionInfo": {
                "roomName": "...", // User input location information
                "posX": ...,
                "posY": ...
              }
            }
            ```
            
        - Handling the response from the Server:
            - If `status: "mfa_required"`: Server requires two-factor authentication (MFA). Agent switches to MFA Authentication Flow (Section 4.3.4).
            - If `status: "position_error"`: The location information is invalid or already occupied. Agent informs the user, asks them to re-enter the location information, and retries step 4.3.3.1.
            - If `status: "success"` and `agentToken` is received: Agent has been successfully identified (it could be a new agent or an agent previously registered and server provided a new token). Proceed to Section 4.3.5.
            - If `status: "success"` (agent already exists, no new token is provided in this response): Agent has been identified. Notification to the user. Proceed to Section 4.3.5 (agent will use the old token if available, or a new token may be needed if the old one is no longer valid).
            - If any other error occurs (e.g., network error, server 500): Logging detailed error, notifying the user, and allowing the user to retry or exit the configuration process.
    4. **MFA Authentication Flow (if required by the Server):**
        - Agent requests the user to enter MFA code (One-Time Password - OTP).
        - Agent sends a `POST` request to the `/api/agent/verify-mfa` endpoint of the Server.
        - Payload of the request:
            
            ```
            {
              "agentId": "...", // AgentId has been identified
              "mfaCode": "..." // User input MFA code
            }
            ```
            
        - Handling the response from the Server:
            - If `status: "success"` and `agentToken` is received: MFA authentication is successful. Proceed to Section 4.3.5.
            - If any error occurs (e.g., HTTP 401 Unauthorized - invalid MFA code or expired): Notifying the user, asking them to retry step 4.3.4.1.
            - If any other error occurs: Logging detailed error, notifying the user.
    5. **Saving Runtime Configuration:**
        - If the agent receives a new `agentToken` (from Identify flow or MFA flow), this token must be encrypted before being saved. Using Windows' Data Protection API (DPAPI) with `DataProtectionScope.LocalMachine` to encrypt the token.
        - Saving (`updating`) the `AgentId`, `RoomConfig` (location information `roomName`, `posX`, `posY`), and `agent_token_encrypted` (encrypted token) into the `C:\ProgramData\CMSAgent\runtime_config\runtime_config.json` file. Using safe file writing (e.g., writing to a temporary file then renaming) to avoid corrupting the configuration file if an error occurs in between.
- **Starting the Service:** After the `configure` command completes successfully and the configuration file has been saved, the installer must execute the `CMSAgentService` service startup command (e.g., using `sc.exe start CMSAgentService`).

### 5. Collecting System Information and Monitoring Resources

One of the core responsibilities of the agent is collecting system information and monitoring resource usage.

- **Initial Hardware Information Collection:**
    - After the agent's initial startup (or after reconfiguration), the WebSocket connection to the Server has been successfully established and the agent has been authenticated (state `CONNECTED`), and after performing initial update check (Section 8.1), the agent will collect detailed information about the hardware of the client machine.
    - The information to be collected includes:
        - Operating System: Name (e.g., Windows 10 Pro), version, architecture (e.g., 64-bit).
        - CPU: Manufacturer, model, clock speed, number of cores, number of threads.
        - GPU: Manufacturer, model, VRAM capacity.
        - RAM: Total physical RAM.
        - Disk: Total disk space and available space (usually C: drive).
    - The agent will send this information to the Server via a `POST` request to the `/api/agent/hardware-info` endpoint. The payload of the request will be a JSON object containing the collected information.
    - The agent must handle any potential errors that may occur when sending this information (e.g., network error). If it fails to send, the agent must log an error and retry after a certain period.
- **Real-Time Resource Monitoring:**
    - The agent will use `System.Diagnostics.PerformanceCounter` from .NET to continuously monitor important resource usage metrics:
        - CPU Usage (%): Percentage of total CPU usage.
        - RAM Usage (%): Percentage of physical RAM currently in use.
        - Disk Usage (%): Percentage of space used on the C: drive.
    - Initializing these `PerformanceCounter` objects must be done carefully, as they may throw exceptions (e.g., on some versions of Windows or due to system configuration).
- **Periodic Resource Status Report:**
    - When the agent is in the `CONNECTED` state (successfully connected and authenticated with the Server via WebSocket), it will send the resource usage metrics it has been monitoring to the Server.
    - This is done through WebSocket communication, using the `agent:status_update` event.
    - The frequency of sending status reports is configured in the `appsettings.json` file (parameter `StatusReportIntervalSec`, e.g., every 60 seconds).
    - The payload of the `agent:status_update` event will have the following format:
        
        ```
        {
          "cpuUsage": 15.5, // number
          "ramUsage": 45.2, // number
          "diskUsage": 60.1 // number
        }
        
        ```
        

### 6. Communicating with the Server

Agent uses two main channels for communication with the Server: WebSocket for real-time communication and HTTP/HTTPS for other requests.

- **WebSocket Channel (Socket.IO over WSS - WebSocket Secure):**
    - **Connecting and Authenticating:**
        1. Agent will attempt to establish a WebSocket connection to the Server's URL configured (e.g., `wss://<server_url>/socket.io`).
        2. During the WebSocket "handshake", agent must send the following headers:
            - `X-Client-Type: agent`
            - `X-Agent-Id: <agentId>` (AgentId taken from `runtime_config.json`)
            - `Authorization: Bearer <decrypted_agent_token>` (Token decrypted from `runtime_config.json`)
        3. Agent will listen for events from the Server to know the result of authentication:
            - `agent:ws_auth_success`: Authentication successful. Agent switches to `CONNECTED` state and can start sending/receiving data.
            - `agent:ws_auth_failed`: Authentication failed (e.g., invalid token, agentId mismatch). Agent needs to handle this error, e.g., trying to refresh token via HTTP API (Section 6.3) and then reconnecting to WebSocket.
    - **Reliability:** Agent must have a mechanism to automatically reconnect when the WebSocket connection is lost. The reconnection strategy should use "exponential backoff" (increasing wait time after each failure) combined with "jitter" (adding a small random delay between reconnection attempts) to avoid multiple agents simultaneously trying to reconnect, causing server overload. The parameters of this strategy (minimum wait time, maximum wait time, number of reconnection attempts) should be configurable.
    - **Agent Emits Events:** Events that the agent proactively sends to the Server over WebSocket:
        - `agent:status_update`: Sending resource status report (Section 5.3).
        - `agent:command_result`: Sending the result of executing a command from a remote location (Section 7.6).
    - **Server Receives Events:** Events that the agent listens to from the Server over WebSocket:
        - `agent:ws_auth_success`: Confirming WebSocket connection success.
        - `agent:ws_auth_failed`: Logging WebSocket authentication error.
        - `command:execute`: Server requesting agent to execute a command from a remote location (Section 7.1).
        - `agent:new_version_available`: Server notifying about a new agent version (Section 8.1.2).
- **HTTP/HTTPS Channel:**
    - **Setting Up:** Agent should use `IHttpClientFactory` to manage `HttpClient` instances, optimizing socket usage and connection management. The Polly library should be integrated to implement retry policies for failed HTTP requests (e.g., retrying when encountering temporary network issues or server 5xx errors). The parameters of the retry policy (number of retry attempts, wait time between retry attempts) should be configurable. **HTTPS must be used** for all HTTP communications to ensure data security.
    - **Authentication:** For requests that require authentication, agent must send the following headers:
        - `X-Agent-Id: <agentId>`
        - `Authorization: Bearer <decrypted_agent_token>`
    - **API Endpoints (Agent calls Server):**
        - `POST /api/agent/identify`: To identify the agent and get initial token or to refresh if needed (Section 4.3.3, 6.3).
        - `POST /api/agent/verify-mfa`: To verify MFA code (Section 4.3.4).
        - `POST /api/agent/hardware-info`: To send initial hardware information (Section 5.1.2).
        - `GET /api/agent/check-update?current_version=<version>`: To check if there is a new agent version (Section 8.1.1).
        - `POST /api/agent/report-error`: To send detailed error report to the Server (Section 9).
        - `GET /download/agent-packages/:filename`: To download agent update package (Section 8.2.2). (Note: API documentation states `/api/agent/agent-packages/:filename`, which needs to be consistent)
        - `POST /api/agent/upload-log`: (Or using `report-error` with `error_type: "LOG_UPLOAD_REQUESTED"`) To upload log file as requested from the Server.
- **Refreshing Token Actively and Reacting:**
    - **Actively:** Agent should have a periodic mechanism to refresh `agentToken`. The refresh interval (`TokenRefreshIntervalSec`) is configured in the `appsettings.json`. When the time comes, the agent will send a `POST /api/agent/identify` request with an additional parameter `forceRenewToken: true` (must be clearly defined in the API). If a new token is received, the agent will encrypt and update it in the `runtime_config.json`.
    - **Reactively:** When the agent receives a `401 Unauthorized` HTTP API error, or a `agent:ws_auth_failed` event from WebSocket, this may indicate that the current token is no longer valid. In this case, the agent will automatically try to refresh the token by sending a `POST /api/agent/identify` request (no need for `forceRenewToken: true`, as the Server will decide whether to provide a new token or not based on the agent's state).
- **Offline Data Queue (Out-of-Band Data):**
    - To ensure that important data is not lost when the agent temporarily loses connection with the Server, the agent should implement an out-of-band data queue.
    - Types of data that can be queued include: `agent:status_update`, `agent:command_result`, and error reports (`POST /api/agent/report-error`).
    - The `System.Threading.Channels` class of .NET can be used to implement this queue in memory. The queue should have limits on size (number of items) or age of items to prevent excessive memory consumption. These limits should be configurable.
    - For error reports (`report-error`), instead of storing the entire payload in memory, the agent can store them as separate JSON files in the `C:\ProgramData\CMSAgent\error_reports\` directory. The file names can include a timestamp and a GUID for uniqueness.
    - When the network connection is restored and the agent has successfully authenticated with the Server, it will automatically handle the queue, sending the stored data to the Server in order.

### 7. Executing Commands from a Remote Location

Agent has the ability to receive and execute commands sent from the Server.

- **Receiving Commands:** Agent listens for the `command:execute` event from the Server through the WebSocket channel. The payload of this event usually contains the following information:
    
    ```
    {
      "commandId": "string (uuid)", // ID of the command
      "command": "string",         // Main command content
      "commandType": "string",     // Command type (e.g., "CONSOLE", "SYSTEM_ACTION")
      "parameters": { ... }      // Additional parameters for the command
    }
    
    ```
    
- **Command Queue:** To process commands in order and avoid overloading, agent should use a command queue (e.g., implemented using `System.Threading.Channels`). This queue should have a maximum size that can be configured (`MaxQueueSize` in `appsettings.json`). If the queue is full, the agent may refuse to accept new commands and report an error to the Server (or notify the Server that the agent is busy).
- **Handling Commands:**
    - Agent can handle commands one at a time or in parallel with control (allowing a certain number of commands to run simultaneously, e.g., `MaxParallelCommands` configured in `appsettings.json`).
    - A `CommandHandlerFactory` should be used to create specific `handler` objects based on the received `commandType`. Each `handler` will be responsible for executing the logic of the corresponding command type.
- **Command Types (`commandType`):**
    - **`CONSOLE`:**
        - Executing commands in the Windows command prompt (CMD or PowerShell).
        - Parameters (`parameters`):
            - `use_powershell`: `boolean` (default `false`). If `true`, the command will be executed using PowerShell; otherwise, it will use CMD.
            - `timeout_sec`: `int` (default value is reasonable, e.g., 60 seconds). Maximum time allowed for the command to run.
        - Agent will capture `stdout` (standard output), `stderr` (standard error), and `exitCode` (exit code) of the command process.
        - **Security:** The command will be executed with the permissions of the `LocalSystem` account. This requires strict control on the Server side regarding which commands are allowed to be sent to the agent to prevent security issues.
    - **`SYSTEM_ACTION`:**
        - Performing system actions.
        - Command (`command` field): `Restart`, `Shutdown`, `LogOff`.
        - Parameters (`parameters`):
            - `force`: `boolean` (default `false`). If `true`, the action will be performed immediately even if there is an application running without saving data.
            - `delay_sec`: `int` (default 0). Time delay (in seconds) before performing the action.
    - **`SOFTWARE_INSTALL`:**
        - Installing software for all users on the client machine.
        - Required parameters (`parameters`):
            - `package_url`: `string` (HTTPS URL to download the installation package).
            - `checksum_sha256`: `string` (SHA256 checksum of the installation package to verify integrity).
        - Optional parameters (`parameters`):
            - `install_arguments`: `string` (Command line arguments for silent/unattended installation, e.g., `/S`, `/qn`).
            - `expected_exit_codes`: `array of int` (List of exit codes considered successful, default is `[0]`).
        - Procedure:
            1. Downloading the installation package from `package_url`.
            2. Verifying `checksum_sha256`. (Optional: adding a digital signature check if available).
            3. Running the installer with `install_arguments` (usually silent/unattended mode).
            4. Monitoring `exitCode` and comparing it with `expected_exit_codes`.
    - **`SOFTWARE_UNINSTALL`:**
        - Uninstalling software.
        - Required parameters (`parameters`):
            - `package_name`: `string` (Name of the software to be uninstalled as displayed in Add/Remove Programs).
            - Or `product_code`: `string` (MSI package's ProductCode).
        - Optional parameters (`parameters`):
            - `uninstall_arguments`: `string` (Command line arguments for silent/unattended uninstallation).
            - `expected_exit_codes`: `array of int`.
    - **`GET_LOGS`:**
        - Collecting and sending agent's log files (or other specified log files) to the Server.
        - Parameters (`parameters`):
            - `log_type`: `string` (e.g., "agent", "updater", "specific_file").
            - `date_from`, `date_to`: `string` (Date format, to filter logs by time range).
            - `file_path`: `string` (If `log_type` is "specific_file", this is the path to the specific log file).
        - Agent should compress log files before sending them to the Server to reduce transmission size.
- **Timeout:** Each command must have a maximum timeout (which can be configured globally or per command type). If a command runs longer than the allowed timeout, the agent must safely terminate the running process and report an error to the Server.
- **Reporting Results:** After a command completes (successfully or unsuccessfully), the agent must send the result back to the Server via WebSocket using the `agent:command_result` event. The payload of this event should include:
    
    ```
    {
      "commandId": "string (uuid)", // ID of the original command
      "commandType": "string",      // Type of command executed
      "success": "boolean",         // Was the command successful
      "result": {
        "stdout": "string",         // (if any)
        "stderr": "string",         // (if any)
        "exitCode": "integer",      // (if any)
        "errorMessage": "string",   // (if success=false)
        "errorCode": "string"       // (Agent's internal error code, if any)
      }
    }
    ```
    

### 8. Automatic Agent Update

Agent must have the ability to automatically update to a newer version when requested by the Server or according to a schedule.

- **Activation of Update:**
    1. **Periodic/Initialization:**
        - After the agent starts, if it successfully establishes a WebSocket connection and authenticates with the Server, it will perform an initial update check.
        - Agent sends a `GET` request to `/api/agent/check-update?current_version=<version>` (where `<version>` is the current version of the agent, e.g., "1.0.5").
        - After that, if `EnableAutoUpdate` in `appsettings.json` is `true`, the agent will check for updates periodically according to the `AutoUpdateIntervalSec` configured in `appsettings.json`.
    2. **By Server Command:** The Server can notify the agent about a new version by sending an `agent:new_version_available` event over WebSocket. The payload of this event must contain all necessary information for the update:
        
        ```
        {
          "status": "success",
          "update_available": true,
          "version": "string", // New version
          "download_url": "string", // URL to download the update package
          "checksum_sha256": "string", // Checksum of the package
          "notes": "string" // Release notes (optional)
        }
        ```
        
- **Update Procedure (performed by `CMSAgent.Service`):**
    1. **Transitioning State:** Agent transitions to `UPDATING` state.
    2. **Notifying Server:** Sending an `agent:update_status` event with payload `{ "status": "update_started", "target_version": "<new_version>" }` over WebSocket.
    3. **Downloading Update Package:** Downloading the update package (usually a `.zip` file) from `download_url` (received from the `check-update` API or `agent:new_version_available` event) and saving it in `C:\ProgramData\CMSAgent\updates\download\`.
        - Handling download failure: If the download fails (e.g., network error, invalid URL), the agent reports an error to the Server (e.g., `agent:update_status` with `{ "status": "update_failed", "error_type": "DownloadFailed", "message": "..." }`) and may log detailed information to `/api/agent/report-error`.
    4. **Verifying Update Package:**
        - Calculating the SHA256 checksum of the downloaded file.
        - Comparing it with the `checksum_sha256` received from the Server.
        - If the checksums do not match, the update package may be corrupted or altered. The agent must report an error to the Server (e.g., `agent:update_status` with `{ "status": "update_failed", "error_type": "ChecksumMismatch" }`), delete the downloaded file, and stop the update process for this version.
        - (Optional: Advanced): If the package is digitally signed, the agent can perform a digital signature check to ensure the integrity and origin of the package.
    5. **Extracting Update Package:** Unzipping the `.zip` file content into a temporary directory in `C:\ProgramData\CMSAgent\updates\extracted\<new_version>\` (e.g., `C:\ProgramData\CMSAgent\updates\extracted\1.1.0\`).
    6. **Starting `CMSUpdater.exe`:**
        - Agent will prioritize finding and starting `CMSUpdater.exe` from within the just-unzipped update package (i.e., `C:\ProgramData\CMSAgent\updates\extracted\<new_version>\Updater\CMSUpdater.exe`). If not found, it may use `CMSUpdater.exe` already present in the agent's current installation directory (`C:\Program Files\CMSAgent\Updater\CMSUpdater.exe`).
        - Passing necessary parameters to `CMSUpdater.exe` via command line:
            - `-pid <current_agent_pid>`: Process ID of the currently running `CMSAgent.Service` process, so that `CMSUpdater.exe` can wait for it to finish completely.
            - `-new-agent-version <new_ver>`: New version string (e.g., "1.1.0").
    7. **Old Agent Self-Shutdown:** After `CMSUpdater.exe` starts successfully, the currently running `CMSAgent.Service` process will begin the "graceful shutdown" (shutting down the machine gracefully) as described in Section 3.2.5.3 to allow `CMSUpdater.exe` to perform the file replacement.
- **`CMSUpdater.exe` Procedure (Standalone Console App):**`CMSUpdater.exe` is a small console application, standalone, responsible for performing the final steps of the update process, when the main agent has stopped.
    1. **Determining Implied Parameters and Input:**
        - `updater-log-dir`: Path to the updater's log directory. Default is `C:\ProgramData\CMSAgent\logs`.
        - `current-agent-install-dir`: Path to the agent's current installation directory. Default is `C:\Program Files\CMSAgent`. `CMSUpdater.exe` may be located in a subdirectory of this path called `Updater`.
        - `new-agent-path`: Path to the agent's new version files that have been unzipped. Determined based on the `-new-agent-version` parameter and the convention of storing files.
        - `current-agent-version`: `CMSUpdater.exe` needs a mechanism to determine the current version of the agent running before updating. This can be done by:
            - Old agent creating a `version.txt` file containing its version at a fixed location before shutting down.
            - Reading the metadata (file version) of the old agent's executable (`CMSAgent.Service.exe`) if `CMSUpdater.exe` can access it before the old agent shuts down completely.
    2. **Logging:** `CMSUpdater.exe` must log its own activity into a separate file in the log directory it has determined (e.g., `C:\ProgramData\CMSAgent\logs\updater_YYYYMMDD_HHMMSS.log`).
    3. **Waiting for Old Agent to Stop:** Using `Process.GetProcessById(<current_agent_pid>)` and `WaitForExit()` (with a reasonable timeout) to ensure that the old agent process has finished before proceeding. If the old agent does not stop within the timeout, `CMSUpdater.exe` logs an error and may exit (or try to kill the process if configured).
    4. **Backing Up Old Agent:** Backing up the entire contents of the old agent's installation directory (`current-agent-install-dir`) to a backup directory, e.g., `C:\ProgramData\CMSAgent\updates\backup\<current_agent_version>\`. Handling errors if backup fails (e.g., insufficient disk space, permission issues).
    5. **Replacing Files:**
        - Deleting old files and directories in `current-agent-install-dir` (excluding the `Updater` directory if `CMSUpdater.exe` is running from there and not in the new update package, and the `backup` directory).
        - Copying all files and directories from `new-agent-path` (containing the new agent version) into `current-agent-install-dir`.
        - Handling errors if the deletion or copying fails. If there is a critical error, consider performing rollback (Section 8.3.6).
    6. **Starting New Agent Service:** Using `sc.exe start CMSAgentService` (or equivalent API) to start the agent service with the new version. If starting the service fails, `CMSUpdater.exe` must perform rollback: restoring files from the backup directory and attempting to start the service with the old version.
    7. **Watchdog (Monitoring New Agent):** After starting the new agent, `CMSUpdater.exe` may monitor the new agent service's status for a short period of time (e.g., 1-2 minutes, configurable). It can check if the process is running smoothly, without crashes. If it detects a new service that is not stable, `CMSUpdater.exe` will perform rollback.
    8. **Cleanup:** If the update process is successful and the new agent service is running smoothly, `CMSUpdater.exe` will delete the backup directory of the old version (`C:\ProgramData\CMSAgent\updates\backup\<current_agent_version>\`) and any temporary files in `C:\ProgramData\CMSAgent\updates\download\` and `C:\ProgramData\CMSAgent\updates\extracted\`.
- **New Agent Successfully Reports:** After the new agent version starts, if it successfully establishes a WebSocket connection and authenticates with the Server, it will send an `agent:update_status` event with payload `{ "status": "update_success", "current_version": "<new_version>" }` over WebSocket.
- **Handling Update Errors Continuously (Update Loop Prevention):** If a specific update version consistently causes errors during installation or makes the new agent unstable (leading to rollback), the agent must have a mechanism to temporarily skip that version. This can be achieved using a `VersionIgnoreManager` to store a list of versions to skip in a JSON file (e.g., `C:\ProgramData\CMSAgent\runtime_config\ignored_versions.json`). The Server should also have logic to not request updates for versions that have been skipped in a certain time period.

### 9. Agent Configuration

Agent uses two types of configuration files:

- **`appsettings.json` (stored in the agent's installation directory, e.g., `C:\Program Files\CMSAgent\`):**
    - Contains static configurations, often set up when deploying and rarely changed.
    - Examples of configurations:
        - Serilog configuration (log format, log level, sinks).
        - `ServerUrl`: URL of the management Server (e.g., `https://cms.example.com`).
        - `ApiPath`: Root path for HTTP APIs (e.g., `/api`).
        - `Version`: Current version of the agent (can be updated automatically by `CMSUpdater.exe`).
        - Various time intervals (e.g., `StatusReportIntervalSec`, `AutoUpdateIntervalSec`, `TokenRefreshIntervalSec`).
        - Configuration for retry policies (number of retry attempts, wait time).
        - Configuration for WebSocket (connection timeout, exponential backoff parameters).
        - Configuration for command execution (e.g., `MaxQueueSize`, `MaxParallelCommands`, default command timeout).
        - Resource limits (e.g., maximum size of the offline data queue).
    - Agent should use .NET Options Pattern (`IOptions<T>`) and Data Annotations to load and validate these configurations when starting up.
- **`runtime_config.json` (stored in the agent's data directory, e.g., `C:\ProgramData\CMSAgent\runtime_config\`):**
    - Contains dynamic configurations, which can change during the agent's operation.
    - Examples of configurations:
        - `agentId`: Unique identifier of the agent.
        - `room_config`: Location information (`roomName`, `posX`, `posY`).
        - `agent_token_encrypted`: Token encrypted using DPAPI.
    - This file is created or updated by the `configure` command (Section 4.3) or when the agent refreshes its token (Section 6.3).
    - Access to this file and its containing directory must be strictly limited (only `LocalSystem` and `Administrators` have write/read permissions).

### 10. Logging and Diagnostics

Logging is an important part for monitoring the agent's activity and diagnosing errors.

- **Logging Library:** Serilog is a powerful and flexible logging library for .NET, so it is used.
- **Output (Sinks):** Serilog configuration to write logs to multiple outputs:
    - **File:** Logging to files with rolling mechanism (creating new files daily or based on size). Log file path: `C:\ProgramData\CMSAgent\logs\agent_YYYYMMDD.log` (e.g., `agent_20250519.log`).
    - **Windows Event Log:** Logging important messages (Error, Fatal, and optionally Warning) to Windows Event Log with a registered Source (e.g., "CMSAgentService").
    - **Console:** When the agent is running in debug mode (e.g., `CMSAgent.Service.exe debug`), logs should be output to the current console window for easier monitoring.
- **Log Format:** Each log line should include useful information:
    - Timestamp (exact date and time).
    - Log Level (e.g., Information, Warning, Error, Debug, Verbose).
    - SourceContext (name of the class or module generating the log).
    - ThreadId (ID of the executing thread).
    - Message (log content).
    - Exception (exception details, including stack trace, if any).
- **Remote Log Collection:** Agent must support the ability to collect and send its log files (and those of `CMSUpdater.exe`) to the Server when requested. This can be done through the `GET_LOGS` command (Section 7.4.5). Log files should be compressed (ZIP) before being sent.
- **Logging for `CMSUpdater.exe`:** As mentioned in Section 8.3.2, `CMSUpdater.exe` must log its own activity into a separate file in the `C:\ProgramData\CMSAgent\logs\` directory, e.g., `updater_YYYYMMDD_HHMMSS.log`. This is crucial for diagnosing issues related to the automatic update process.

### 11. Security

Security is a critical aspect for an agent running with LocalSystem privileges and the ability to execute commands remotely.

- **Encrypting Token:** `agent_token` stored in `runtime_config.json` must be encrypted using DPAPI with `DataProtectionScope.LocalMachine`. This ensures that the token can only be decrypted on the same machine and by the same user account (or LocalSystem).
- **Secure Communication:**
    - HTTPS must be used for all HTTP API communications.
    - WSS (WebSocket Secure) must be used for all WebSocket communications.
    - Agent must verify the SSL/TLS certificate of the Server to prevent Man-in-the-Middle (MITM) attacks.
    - (Optional: Consider Certificate Pinning for enhanced security, but it requires careful management to avoid issues when the Server's certificate changes).
- **Directory Permissions:** Setting strict NTFS permissions for the agent's installation directory (`C:\Program Files\CMSAgent\`) and data directory (`C:\ProgramData\CMSAgent\`) to only allow necessary permissions for corresponding accounts (e.g., `LocalSystem` has Full Control on the data directory, Users only have Read & Execute permissions on the installation directory).
- **Minimizing LocalSystem Risk:** Since the agent runs with `LocalSystem` privileges (highest on the machine), it is important to have measures in place to minimize risk:
    - **Strong Remote Command Execution Authorization:** The Server must have a strong authorization mechanism to ensure only legitimate administrators can send commands to the agent.
    - **Strict Input Validation:** Agent must strictly validate all inputs received from the Server (e.g., command parameters, URLs to be fetched) before using them to prevent security issues such as command injection or path traversal.
- **Security of Updates:**
    - **Verifying SHA256 Checksum:** It is mandatory to verify the checksum of the update package before extracting and installing it (Section 8.2.3).
    - **Digital Signature:** Strongly recommending digital signing (code signing) for both `CMSAgent.Service.exe`, `CMSUpdater.exe`, and update packages (`.zip`). Agent should verify this digital signature during the update process.
- **Unique Mutex Name:** The name of the Mutex used to ensure a single instance must have a unique GUID to avoid conflicts with other applications on the system.

### 12. Command Line Interface (CLI) - `CMSAgent.Service.exe`

In addition to running as a service, `CMSAgent.Service.exe` also provides some CLI commands for managing and troubleshooting. The installation, uninstallation, starting, and stopping of the service will primarily be done through the installer (e.g., Inno Setup) and Windows Service Control Manager (SCM), but these CLI commands are useful for specific tasks.

- **`configure`:**
    - Running the initial configuration setup as described in Section 4.3. This command can also be used to reconfigure the agent if needed.
    - Interacting with the user through the console to input necessary information (e.g., location information, MFA code if requested).
    - Communicating with the Server to authenticate the agent and get `agentToken`.
    - Saving configuration (including `agentId` and `agent_token_encrypted`) into the `runtime_config.json` file.
- **`debug`:**
    - Running the main logic of the agent in the current console window instead of running as a Windows Service.
    - All logs (usually configured to be written to a file or Event Log) will be displayed directly on the console.
    - This mode is very useful for development, troubleshooting, and monitoring the agent's activity in real-time without needing to install it as a service.
    - In this mode, the agent does not interact with the Service Control Manager (SCM) to register or start as a service. It simply runs the logic of its worker class.

### 13. Non-functional Requirements

These are requirements that are not directly related to the specific functionality but are important for the quality and user experience.

- **Performance:**
    - **Low resource usage when idle:** When there is no active task or command being executed, the agent must use very low CPU and RAM levels to avoid impacting the user's machine performance.
    - **Non-disruptive monitoring:** The process of monitoring resources (CPU, RAM, Disk) must be optimized to avoid causing significant load on the system.
    - **Immediate response:** The agent must respond quickly when receiving commands from the Server and start executing them promptly.
    - **Optimization:** Using efficient `async/await` for I/O-bound tasks (e.g., network communication, reading/writing files). Careful memory management to avoid memory leaks. Considering using `System.Threading.Channels` for internal queues for better performance and backpressure management.
- **Stability and Reliability:**
    - **Long-term stable operation:** The agent must be designed to operate stably over a long period without needing to restart.
    - **Automatic recovery from network errors:** Ability to automatically reconnect and recover from temporary network issues.
    - **Reliable update process:** The automatic update process must be reliable, with the ability to roll back if a critical error occurs.
    - **Internal health monitoring:** The agent may implement a mechanism for internal health monitoring and reporting potential issues to the Server (e.g., number of crashes, continuous connection issues).
- **Maintainability:**
    - **Modular source code:** Source code must be organized into modules, clear projects, and easy to understand.
    - **Easy extensibility:** The design must allow for easy addition of new command types, new types of collected information, or new features in the future.
    - **Using Interfaces and Dependency Injection (DI):** Leveraging interfaces and DI to reduce dependencies between components and increase testability.
    - **Unit Tests:** Unit tests must be provided for important agent logic components to ensure quality and early detection of errors.

### 14. Agent API Interface (Summary from `agent_api.md`)

Agent communicates with the Server through HTTP API and WebSocket API endpoints defined in detail in the `agent_api.md` documentation. Below is a summary of the main endpoints and events:

- **Agent HTTP API:**
    - **Authenticating Agent:**
        - `POST /api/agent/identify`: Identifying the agent with the system, requesting `agentId` and `positionInfo`. Returns `status: "mfa_required"` if MFA is needed, `status: "success"` with `agentToken` if already registered or no MFA is needed, or `status: "position_error"` if the location is invalid.
        - `POST /api/agent/verify-mfa`: Verifying the MFA code provided by the user. Requires `agentId` and `mfaCode`. Returns `status: "success"` with `agentToken` if the code is valid.
    - **Updating Agent Information:**
        - `POST /api/agent/hardware-info`: Agent sends detailed hardware information (`total_disk_space`, `gpu_info`, `cpu_info`, `total_ram`, `os_info`). Requires header `X-Agent-ID` and `Authorization: Bearer <agent_token>`.
        - `POST /api/agent/report-error`: Agent reports an error encountered. Requires `type` (error type), `message`, and `details` (optional). Requires header `X-Agent-ID` and `Authorization: Bearer <agent_token>`. Standard types for update errors: `"DownloadFailed"`, `"ChecksumMismatch"`, `"ExtractionFailed"`, `"UpdateLaunchFailed"`, `"StartAgentFailed"`, `"UpdateGeneralFailure"`.
    - **Agent Version:**
        - `GET /api/agent/check-update`: Checking if there is a newer agent version. Requires query parameter `current_version`. Returns new version information (`version`, `download_url`, `checksum_sha256`, `notes`) if available, or 204 No Content if not. Requires header `X-Agent-ID` and `Authorization: Bearer <agent_token>`.
        - `GET /api/agent/agent-packages/:filename`: Downloading agent update package. Requires path parameter `filename`. Requires header `X-Agent-ID` and `Authorization: Bearer <agent_token>`.
- **Agent WebSocket API (Connected to `/socket.io`):**
    - **Connecting and Authenticating:** When setting up a WebSocket connection, agent sends the following headers: `X-Client-Type: "agent"`, `Authorization: Bearer <agent_token>`, `X-Agent-ID: string (agentId)`. Server responds via events `connect` (success) or `connect_error` (failure).
    - **Agent Emits Events:**
        - `agent:status_update`: Sending current resource usage (`cpuUsage`, `ramUsage`, `diskUsage`).
        - `agent:command_result`: Sending the result of executing a command from the Server (`commandId`, `commandType`, `success`, `result: {stdout, stderr, exitCode}`).
    - **Server Emits Events:**
        - `command:execute`: Server requesting agent to execute a command (`command`, `commandId`, `commandType`).
        - `agent:new_version_available`: Server notifying about a new agent version (`version`, `download_url`, `checksum_sha256`, `notes`).

### 15. Main Activity Flow (Summary from `Flow.md`)

The `Flow.md` documentation uses Mermaid diagrams to visually represent the main activity flows of the agent. These flows include:

- **Agent Initialization and Registration Flow:**
    1. Agent starts (as a Windows Service).
    2. Checking Mutex to ensure only one instance is running. If Mutex already exists, agent exits.
    3. Reading `runtime_config.json`.
    4. If `AgentId` does not exist, creating a new one (GUID).
    5. Sending a `POST /api/agent/identify` request to the Server with `agentId` and `positionInfo` (if this is the initial configuration or requested reconfiguration).
    6. Handling the response from the Server:
        - If `status: "mfa_required"`: Requesting MFA from the user, then sending `POST /api/agent/verify-mfa`. If successful, receiving `agentToken`. If an error occurs, retrying MFA.
        - If `status: "success"`: Receiving `agentToken` (if granted).
        - If `status: "position_error"`: Notifying about location error, asking user to reconfigure, and retrying the Identify step.
        - If any other error occurs: Logging.
    7. Encoding `agentToken` using DPAPI.
    8. Saving `AgentId`, `RoomConfig`, and `agent_token_encrypted` into `runtime_config.json`.
    9. Agent completes initialization and is ready to operate.
- **Agent's Daily Operation Flow:**
    1. Setting up a WebSocket connection to the Server (`wss://ServerUrl/socket.io`) with authentication headers.
    2. If WebSocket connection/authentication is successful:
        - Performing initial agent update check (`GET /api/agent/check-update`). If there is an update, proceeding to the Update Flow.
        - If no update (or after initial update check), collecting detailed hardware information and sending it to the Server (`POST /api/agent/hardware-info`).
    3. Agent enters the main loop, listening for events from the Server and performing background tasks:
        - **Handling `command:execute` events:** Receiving commands, adding them to the queue, processing the command, and sending `agent:command_result`.
        - **Handling `agent:new_version_available` events:** If a new version notification is received, proceeding to the Update Flow.
        - **Resource Monitoring:** Periodic (according to `StatusReportIntervalSec`), collecting `cpuUsage`, `ramUsage`, `diskUsage`.
        - **Resource Reporting:** Sending `agent:status_update` event with resource data over WebSocket.
        - **Error Handling:** Logging any errors encountered.
        - **Handling Disconnection/Stopping:** When a stopping signal is received (e.g., `stoppingToken`), performing graceful shutdown (disconnecting, completing tasks, releasing Mutex, logging).
    4. If the WebSocket connection fails or authentication fails, attempting to reconnect using an exponential backoff strategy. If the authentication issue persists, the connection attempt may be temporarily suspended.
- **Agent Auto-Update Flow:**
    1. Agent receives update information (from `check-update` API or `agent:new_version_available` event), including `version`, `download_url`, `checksum_sha256`.
    2. Downloading the update package (`.zip`) from `download_url`. If there is an error, reporting `DownloadFailed`.
    3. Verifying `checksum_sha256`. If it does not match, reporting `ChecksumMismatch`.
    4. Unzipping the update package into a temporary directory (`updates/extracted/<new_version>`). If there is an error, reporting `ExtractionFailed`.
    5. Starting `CMSUpdater.exe` (preferring the new package) with parameters (`-pid`, `-new-agent-version`). If there is an error, reporting `UpdateLaunchFailed`.
    6. Old agent (CMSAgent.Service) begins the shutdown process (graceful shutdown).
    7. **`CMSUpdater.exe` Flow:**
        - Logging activity.
        - Waiting for the old agent to finish completely.
        - Backing up the old agent's installation directory.
        - Deleting old files and replacing them with files from the new update package.
        - Starting the new agent service. If there is an error, performing rollback (restoring from backup, restarting old agent).
        - Watchdog: Monitoring the new agent. If the new agent crashes or becomes unstable, performing rollback.
        - Cleaning up temporary files and backup directories (if the update is successful).
        - Exiting.
    8. New agent starts, establishes WebSocket connection, and authenticates.
    9. Server confirms that the new agent has connected and is operating (e.g., via `agent:update_status: "update_success"`).
    10. Handling continuous update errors by skipping the problematic version.