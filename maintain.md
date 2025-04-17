---

# Comprehensive Agent System Specification (Orchestration & Update)

## I. Introduction

This document details the design for a robust computer management agent system. It covers the agent's core runtime mechanics, local security, inter-process communication, automatic updates, and state management, along with the necessary backend and frontend components to support these functions. The goal is to create a reliable, secure, and manageable agent capable of operating autonomously and receiving updates efficiently.

---

## II. Agent Component (`cms_agent.exe`)

This section details the client-side application running on managed machines.

### A. Overall Goals (Agent Specific)

1.  **Autostart:** Start automatically with Windows based on running privileges.
2.  **Single Instance:** Ensure only one instance runs per context (System or User).
3.  **Data Security:** Protect local data (config, logs, secrets) via ACLs (Admin) or user profile isolation (User).
4.  **Controlled Restart:** Provide a secure `--force` mechanism via IPC.
5.  **Automatic Updates:** Update seamlessly via WebSocket (primary) and HTTP polling (fallback).
6.  **State Management:** Safely manage operational states (`IDLE`, `UPDATING_*`, `FORCE_RESTARTING`).
7.  **Configuration:** Manage configuration persistently and handle format changes across updates.
8.  **Resource Awareness:** Check essential resources like disk space.
9.  **Robust Logging:** Provide detailed logs for diagnostics and error reporting.

### B. Autostart (Using Registry)

*   **Method:** Write the agent's executable path to the Windows Registry.
*   **Privilege Detection:** The agent checks its own privileges (Admin/User) when registering/unregistering.
*   **Registration:**
    *   **Admin Privileges:** Write to `HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run`. (Effect: Starts for all users).
    *   **User Privileges:** Write to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. (Effect: Starts only for the current user).
    *   *Value:* Create a String Value (e.g., `MyLabAgent`) containing the full path to `cms_agent.exe`.
*   **Management:** Provide command-line arguments `--enable-autostart` and `--disable-autostart` to add/remove the corresponding Registry entry.
*   **Logging:** Log success or failure of Registry operations (include error codes on failure).

### C. Data Protection and Single Instance Guarantee

1.  **Storage Location:**
    *   **Admin Privileges:** `C:\ProgramData\MyLabAgent\` (Example). This shared location requires explicit protection.
    *   **User Privileges:** `%LOCALAPPDATA%\MyLabAgent\` (Example: `C:\Users\<Username>\AppData\Local\MyLabAgent\`). Relies on default user profile protections.
    *   *Definition:* This location is referred to as the "protected data directory".

2.  **Directory Protection (ACLs):**
    *   **Admin Privileges:** The agent (on first run or periodically) must:
        1.  Create the protected data directory if it doesn't exist.
        2.  Set strict ACLs: Grant Full Control only to `SYSTEM` and `Administrators`.
        3.  Disable inheritance.
        4.  Remove or explicitly Deny access for other groups (`Users`, `Everyone`).
        5.  Log success or failure of ACL setup.
    *   **User Privileges:** No custom ACLs needed; relies on default Windows protection for `%LOCALAPPDATA%`.

3.  **Single Instance Guarantee (Lock File):**
    *   **File:** `agent.lock` located inside the protected data directory.
    *   **Content:** Contains the Process ID (PID) of the running agent and a regularly updated timestamp (ISO 8601 format recommended).
    *   **Mechanism (`acquire_lock()`):** On agent startup:
        1.  Determine the full path to `agent.lock`.
        2.  **Attempt to create and acquire an exclusive file lock** on `agent.lock`.
        3.  **If successful:**
            *   Write current PID and current timestamp to the file.
            *   Start a background task/timer to periodically update the timestamp in the locked file (e.g., every 60 seconds). Ensure this update is atomic if possible.
            *   Register `release_lock()` for graceful shutdown.
            *   Log "Acquired lock successfully".
            *   Proceed with startup.
        4.  **If failed (file exists and is locked):**
            *   Log "Lock file exists, checking status...".
            *   Attempt to read PID (PID_Old) and timestamp (Timestamp_Old) from `agent.lock`. Handle read errors.
            *   Check if the process with PID_Old is still running (e.g., `OpenProcess`, `GetExitCodeProcess`).
            *   Check if Timestamp_Old is excessively old (e.g., older than 3 times the update interval, like > 3 minutes).
            *   **If (PID_Old process is *not* running) OR (Timestamp_Old is too old):**
                *   Consider the lock "stale".
                *   Log "Detected stale lock file (PID: PID_Old, Timestamp: Timestamp_Old). Attempting to take over."
                *   Attempt to delete the stale `agent.lock` file. Handle potential errors (e.g., OS holding the lock briefly after crash).
                *   Retry from Step 2 (attempt to create/lock a new file).
            *   **If PID_Old process *is* running AND Timestamp_Old is recent:**
                *   Another instance is active.
                *   Log "Another instance (PID: PID_Old) is running. Exiting."
                *   Exit the current process cleanly (e.g., return code indicating "already running").
    *   **Mechanism (`release_lock()`):** Called during graceful shutdown.
        1.  Stop the timestamp update timer/task.
        2.  Release the exclusive file lock.
        3.  Delete the `agent.lock` file.
        4.  Log "Lock released successfully". Handle errors.

### D. State Management

*   **Core States:**
    *   `STARTING`: Initial state before lock acquisition and basic setup.
    *   `IDLE`: Agent is running normally, connected (if possible), and ready for commands.
    *   `FORCE_RESTARTING`: Agent received a valid `--force` command and is shutting down.
    *   `UPDATING_STARTING`: Update process initiated, checking prerequisites.
    *   `UPDATING_DOWNLOADING`: Downloading the new agent package.
    *   `UPDATING_VERIFYING`: Verifying checksum and/or code signature.
    *   `UPDATING_EXTRACTING_UPDATER`: Extracting `updater.exe` (if bundled).
    *   `UPDATING_PREPARING_SHUTDOWN`: Preparing for graceful shutdown before launching updater.
    *   `SHUTTING_DOWN`: Generic state during graceful shutdown process.
    *   `STOPPED`: Agent has finished shutdown.
*   **Implementation:** Use a single state variable protected by a thread-safe mechanism (mutex/lock). All state transitions must be logged.
*   **Behavior:**
    *   Incoming commands/triggers (WebSocket update, IPC `--force`, polling result) **must** check the current state before acting.
    *   Ignore update triggers (`update_required` message or polling result) if state is not `IDLE`. Log the ignored trigger.
    *   Ignore `--force` IPC requests if state is `UPDATING_*`. Log the ignored request.
    *   State transitions must be atomic and logged.

### E. Configuration Management

1.  **Storage:** Core configuration (e.g., `backend_url`, `agent_id`, `current_version`, `log_level`, `polling_interval`, `show_update_notifications`) stored in a file (e.g., `config.json`) within the protected data directory.
2.  **Agent ID & Version:** `agent_id` is generated once and persists. `current_version` is updated *after* a successful update (potentially by the new agent reading its own compiled-in version or an accompanying file).
3.  **Updates & Configuration:** The `updater.exe` **must not** modify the `config.json` file. It only replaces the executable.
4.  **Configuration Migration:**
    *   If a new agent version requires configuration changes (new fields, format changes):
        1.  The *new* agent, on its first startup after update, detects the old configuration version/format.
        2.  It **backs up** the existing file (e.g., `config.json` -> `config.json.YYYYMMDDHHMMSS.bak`).
        3.  It reads the old config, merges/transforms settings into the new format in memory.
        4.  It writes the complete, new `config.json` file.
        5.  Logs the migration process (success/failure). Handles migration errors gracefully (e.g., attempt to revert from backup, log critical error, report to backend, potentially halt startup).

### F. Controlled Restart (`--force` via IPC)

1.  **Goal:** Allow a new instance (B) to request the running instance (A) to shut down gracefully, so B can start with potentially new arguments.
2.  **Mechanism:** Windows Named Pipes.
3.  **Pipe Naming:** Predictable name, potentially incorporating scope. Examples:
    *   Admin: `\\.\pipe\MyLabAgent_IPC_SYSTEM`
    *   User: `\\.\pipe\MyLabAgent_IPC_USER_<UserSID>` (Retrieve SID programmatically).
4.  **Security:**
    *   **Pipe ACLs (Instance A Creation):**
        *   **Admin:** Allow connections from `SYSTEM`, `Administrators`.
        *   **User:** Allow connections from the current `User SID`, `SYSTEM`, `Administrators`.
    *   **IPC Secret Authentication:**
        *   A strong, random secret generated by Instance A if not already present.
        *   **Storage:** Windows Credential Manager.
            *   **Admin:** Local Machine scope (`CRED_PERSIST_LOCAL_MACHINE`). Target: `MyLabAgent/IPCSecret/SYSTEM`.
            *   **User:** User Logon scope (`CRED_PERSIST_ENTERPRISE` or `CRED_PERSIST_LOGON`). Target: `MyLabAgent/IPCSecret/USER`.
        *   Agent reads/writes the secret corresponding to its *own* running context.

**IPC Message Structure:**
*   **Request (B to A):** JSON `{"secret": "...", "new_args": [...]}`.
*   **Response (A to B):** JSON
    *   `{"status": "acknowledged"}`: Request accepted, shutting down.
    *   `{"status": "busy_updating", "target_version": "x.y.z"}`: Ignored due to ongoing update to version x.y.z.
    *   `{"status": "invalid_secret"}`: Invalid secret.
    *   `{"status": "error", "message": "..."}`: Other processing error.

5.  **Workflow:**
    1.  **Instance B (`cms_agent.exe --force [optional_args]`) Starts:** Logs intent.
    2.  **B Determines Context:** Identifies if it's running as Admin or User to determine Pipe Name and Credential Manager scope.
    3.  **B Reads IPC Secret:** Reads secret from the *corresponding* Credential Manager scope. Handles errors (log if not found).
    4.  **B Connects to Pipe:** Attempts connection to Instance A's pipe. **Includes a connection timeout** (e.g., 5 seconds). Logs success/failure.
    5.  **If Connection Fails (Timeout, Denied, Not Found):** Log error. B might cautiously proceed to attempt `acquire_lock` (which handles stale locks) or exit with error.
    6.  **If Connection Succeeds:**
        *   **B Sends Command:** Sends JSON message: `{"secret": "...", "new_args": ["--loglevel", "debug"]}`.
        *   **B Waits for Response:** Waits for a JSON response from A (with a short timeout, e.g., 3-5 seconds).
    7.  **Instance B Processes Response:**
        *   **If response `{"status": "busy_updating", ...}` received:**
            *   Log "Instance A reported it is busy updating to version [target_version]. Cannot force restart."
            *   **Display message to user:** (e.g., print to console) "The running agent is currently updating to version [target_version] and cannot be restarted now. Please try again later."
            *   Exit Instance B cleanly (e.g., specific exit code for "busy updating").
        *   **If response `{"status": "acknowledged"}` received:**
            *   Log "Instance A acknowledged --force request. Waiting for lock release."
            *   Proceed to Step 9 (Wait for Lock Release).
        *   **If response `{"status": "invalid_secret"}` received:**
            *   Log critical error "IPC secret mismatch. Ensure Instance B has the correct secret or Instance A's secret storage is intact."
            *   Exit Instance B with an error code.
        *   **If other error response or timeout waiting for response:**
            *   Log warning/error "Did not receive expected acknowledgment from Instance A (Timeout=[timeout]s). Assuming it might be unresponsive or older version. Proceeding to wait for lock release."
            *   Proceed to Step 9 (Wait for Lock Release).
    8.  **Instance B Waits for Lock Release:** B enters a loop attempting `acquire_lock()`. **Includes a timeout** (e.g., 30-60 seconds). This step is only reached if A acknowledged or if B assumes A might shut down despite no ack.
    9.  **If B acquires lock within timeout:** Log success. Proceeds with its own startup, applying `new_args` if Instance A stored them (mechanism for passing args needs definition - maybe A writes to a temp file before shutdown?).
    10. **If B times out waiting for lock:** 
        *   Log "Timeout waiting for Instance A (PID read from lock: [PID_Old]) to release lock after --force attempt. Instance A might be unresponsive. Instance B will now exit."
        *   Log critical error: "Could not acquire lock, another instance is running and unresponsive after force request."
        *   Exit Instance B with a specific error code indicating failure to start due to existing unresponsive instance.

### G. Agent Update Process (Internal)

1.  **Receive `update_required` Trigger:** From WebSocket (Section III) or Fallback Polling (Section H).
2.  **Check State:** Verify agent is in `IDLE` state. If not, log and ignore.
3.  **Transition State:** `UPDATING_STARTING`.
4.  **Log:** "Update process started for version [new_version]". Parse `new_version`, `download_url`, `checksum` from trigger payload.
5.  **Resource Check:** Verify sufficient disk space in a temporary download location (e.g., subfolder in `%TEMP%`). Log error, report to backend, revert state to `IDLE` if insufficient.
6.  **Transition State:** `UPDATING_DOWNLOADING`.
7.  **Download:** Download package from `download_url` to temp path. Log progress/completion. On error: log, report error, delete partial file, revert state to `IDLE`.
8.  **Transition State:** `UPDATING_VERIFYING`.
9.  **Verify Checksum:** Calculate SHA256 of downloaded file. Compare with expected `checksum`. On mismatch: log critical error, delete file, report error, revert state to `IDLE`. Log success.
10. **Verify Code Signature (Recommended):** Verify signature validity/trust. On invalid: log critical error, delete file, report error, revert state to `IDLE`. Log success.
11. **Transition State:** `UPDATING_EXTRACTING_UPDATER` (Skip if updater not bundled).
12. **Extract Updater:** Extract `updater.exe` from the downloaded package to a known temporary location (e.g., `%TEMP%\MyLabAgent_updater.exe`). Handle extraction errors (log, report, revert state).
13. **Transition State:** `UPDATING_PREPARING_SHUTDOWN`.
14. **User Notification (Optional):** If configured (User mode), display non-blocking Windows notification about the pending update.
15. **Prepare & Initiate Shutdown:**
    *   Log "Preparing to launch updater and shut down."
    *   Initiate `graceful_shutdown()`.
16. **Launch Updater:**
    *   Start the extracted `updater.exe` process (detached).
    *   Pass arguments: `"<path_to_new_agent_package>" "<full_path_to_current_agent.exe>" <current_PID> "<path_to_log_directory>"`.
    *   Log the launch command and arguments.
17. **Agent Exits:** `graceful_shutdown` completes, `release_lock` is called, process terminates.

### H. Fallback Update Check (Polling)

1.  **Trigger:** Periodically (e.g., every 4 hours, interval from `config.json`) and/or if WebSocket is disconnected for a prolonged time.
2.  **Process:**
    *   If state is `IDLE`, make HTTP GET request to Backend endpoint (e.g., `/api/agents/check_update?agentId=[agent_id]&version=[current_version]`).
    *   Handle network errors gracefully (log, retry later).
    *   If response is HTTP 200 with JSON payload (`new_version`, `download_url`, `checksum`):
        *   Log "Update found via polling."
        *   Initiate the internal update process (Section G).
    *   If response is HTTP 204 No Content or other non-update indication: Log "No update found via polling."

### I. Graceful Shutdown (`graceful_shutdown()`)

*   **Triggered by:** Valid `--force` request, start of update process.
*   **Purpose:** Cleanly release all resources.
*   **Implementation:** Use `try...finally` blocks extensively.
*   **Steps:**
    1.  Log "Initiating graceful shutdown..." Change state to `SHUTTING_DOWN`.
    2.  Signal background threads/tasks to stop (WebSocket listener, polling timer, timestamp updater). Wait briefly for them.
    3.  Close Named Pipe listener.
    4.  Close WebSocket connection cleanly.
    5.  Flush log buffers. Close log file handles.
    6.  Close any other application-specific resources (DB connections, etc.).
    7.  Call `release_lock()` (Releases file lock, deletes `agent.lock`).
    8.  Log "Shutdown complete. Exiting."
    9.  Terminate the process (e.g., `sys.exit(0)`).

### J. Resource Checks

*   Implement checks for sufficient disk space before:
    *   Starting download (Section G, Step 5).
    *   Writing potentially large log entries (within logging framework).

### K. Logging & Error Reporting

1.  **Local Logging:**
    *   Use a robust logging library (e.g., Serilog, log4net, Python's logging).
    *   Support levels (VERBOSE, DEBUG, INFO, WARN, ERROR, FATAL). Configurable via `config.json`.
    *   Log to files within the protected data directory.
    *   Implement log rotation (size-based and/or time-based).
    *   Log key events: Startup/Shutdown, State Transitions, Lock Actions (Acquire, Release, Stale Detected, Update Timestamp), IPC Events (Connection, Auth Success/Fail, Command Received), Update Process Steps, Errors, Config Migration, Autostart changes, ACL changes.
2.  **Error Reporting to Backend:**
    *   **Endpoint:** Dedicated HTTP POST (e.g., `/api/agents/report_error`).
    *   **Triggers:** Critical failures (update checksum/signature mismatch, update download fail, updater launch fail, config migration fail, unhandled exceptions, rollback event).
    *   **Payload (JSON):** `{ "agent_id": "...", "agent_version": "...", "timestamp": "...", "error_type": "...", "failed_version": "..." (optional), "message": "...", "log_snippet": "..." }`.
    *   **Buffering:** If the POST fails (network error), store the report locally (e.g., `error_reports.jsonl` in data dir) and retry sending periodically when connectivity is restored.

---

## III. Updater Component (`updater.exe`)

This is a small, standalone executable responsible for the actual file replacement during an update.

### A. Purpose

Safely replace the old agent executable with the downloaded new version and relaunch the agent.

### B. Distribution

**Recommended:** Bundled *inside* the main agent package/executable. The running agent extracts it to a temporary location before launching it.

### C. Logic

1.  **Initialization:**
    *   Parse command line arguments: `"<path_to_new_agent_package>" "<full_path_to_current_agent.exe>" <old_PID> "<path_to_log_directory>"`.
    *   Setup its own logging to a dedicated file (e.g., `updater.log`) in the specified log directory or `%TEMP%`. Log "Updater started with args: [...]".
2.  **Wait for Old Agent Exit:**
    *   Use `PID_Old`. Periodically check if the process exists (e.g., `OpenProcess`).
    *   **Implement Timeout:** (e.g., 30-60 seconds).
    *   If timeout expires: Log critical error. Optionally attempt `taskkill /F /PID <PID_Old>` (requires privileges). Proceed cautiously or exit with error code.
    *   If process exits cleanly: Log "Old agent process (PID: PID_Old) exited."
3.  **Backup Old Agent:**
    *   Rename `current_agent.exe` -> `current_agent.exe.old`. Handle errors (file locked, permissions). Log result. If fails, exit with error.
4.  **Replace Agent File:**
    *   Move/Copy the `new_agent_package` to `current_agent.exe` path. Handle errors (permissions, disk full). Log result. If fails, initiate rollback (Step 7) or exit with error.
5.  **Launch New Agent:**
    *   Start the `current_agent.exe` (which is now the new version). Log command and success/failure.
6.  **Verification (Recommended):**
    *   Wait briefly (e.g., 5-15 seconds).
    *   Check if the new agent process is running (find process by name/path).
    *   *Advanced:* Check if `agent.lock` exists and its timestamp is recent.
    *   Log verification result ("New agent started successfully." or "New agent failed verification.").
7.  **Rollback (If Verification Fails):**
    *   If verification failed:
        *   Log "Initiating rollback."
        *   Attempt to terminate the failed new agent process.
        *   Delete the failed `current_agent.exe`.
        *   Rename `current_agent.exe.old` back to `current_agent.exe`.
        *   Attempt to relaunch the *old* agent (now restored).
        *   Log rollback outcome ("Rollback successful, old agent restarted." or "Rollback failed."). Exit with a specific error code indicating rollback.
8.  **Cleanup (If Update Successful):**
    *   If update and verification were successful:
        *   Delete the backup file `current_agent.exe.old`.
        *   Delete the originally downloaded `new_agent_package` from its temporary location.
        *   Log cleanup actions.
9.  **Self-Delete (Optional/Advanced):**
    *   The updater needs to delete itself. This often requires techniques like:
        *   Launching `cmd.exe /c ping 127.0.0.1 -n 2 > nul & del /q /f <path_to_updater.exe>` just before exiting.
        *   Having the *new* agent delete the known temporary updater path on its successful startup.
10. **Exit:** Log "Updater finished." Exit code 0 for success, non-zero for failure or rollback.

---

## IV. Backend Component (Server-Side)

This section describes the server-side logic required to manage agents and updates.

### A. Version Repository

*   **Storage:** Secure location (filesystem, S3, Azure Blob) to store agent executable packages/files.
*   **Organization:** Files named consistently by version, OS, architecture (e.g., `agent-windows-amd64-1.2.0.exe`).
*   **Access:** Backend service needs read access to serve downloads and write access for uploads.

### B. Version Metadata Store

*   **Method:** Database (preferred for scalability and querying) or a version-controlled configuration file.
*   **Schema/Data:**
    *   `version` (String, e.g., "1.2.0", unique index)
    *   `os` (String, e.g., "windows")
    *   `arch` (String, e.g., "amd64")
    *   `download_url` (String, URL to the file in the repository)
    *   `checksum_sha256` (String)
    *   `release_notes` (Text, optional)
    *   `upload_timestamp` (DateTime)
    *   `is_stable` (Boolean, indicates general availability)
    *   `rollout_rules` (JSON/Text, optional, defining target groups, percentages, specific agent IDs)

### C. WebSocket Server

*   **Endpoint:** e.g., `wss://your_backend.com/ws`
*   **On Connection:**
    *   Receive `agentId` and `version` from query parameters or initial message.
    *   Authenticate agent if necessary.
    *   Store WebSocket connection reference mapped to `agent_id` and `version`.
    *   Log connection event.
    *   **Trigger Initial Version Check:** Query Metadata Store for the latest applicable version for this agent (considering `is_stable` and `rollout_rules`). Compare with agent's `version`. If newer version found, send `update_required` message (see Section D).
*   **On Message:** Handle potential pings/pongs or other agent messages.
*   **On Disconnect:** Remove connection reference, log disconnection.
*   **Notify Agents:** When a new version is uploaded and marked applicable:
    *   Iterate through *all* connected agents.
    *   For each agent, check if the new version is applicable and newer than its current version.
    *   If yes, send the `update_required` message to that specific agent.

### D. HTTP/S API Endpoints

1.  **`POST /api/agents/upload`**
    *   **Auth:** Requires Admin privileges.
    *   **Request:** `multipart/form-data` with agent file, `version`, `os`, `arch`, `notes`.
    *   **Processing:**
        *   Validate input, check permissions.
        *   Calculate checksum.
        *   Store file in Version Repository.
        *   Update Version Metadata Store.
        *   Optionally trigger WebSocket notification process (Section C).
    *   **Response:** 200 OK or error code.

2.  **`GET /api/agents/check_update`**
    *   **Auth:** Optional (agent ID might suffice).
    *   **Request Params:** `agentId`, `version`, `os`, `arch`.
    *   **Processing:**
        *   Query Metadata Store for the latest applicable version for this agent (considering rules).
        *   Compare with request `version`.
    *   **Response:**
        *   If update needed: 200 OK with JSON payload: `{ "new_version": "...", "download_url": "...", "checksum": "..." }`.
        *   If no update needed: 204 No Content.

3.  **`GET /downloads/agent-{os}-{arch}-{version}.ext` (Example)**
    *   **Auth:** Optional (signed URLs, tokens if needed).
    *   Serves the actual agent binary from the Version Repository. Include `Content-Type` and `Content-Disposition` headers.

4.  **`POST /api/agents/report_error`**
    *   **Auth:** Optional (agent ID might suffice).
    *   **Request Body:** JSON payload from agent (Section II.K.2).
    *   **Processing:** Log the received error report. Store it in a database or monitoring system for analysis.
    *   **Response:** 200 OK or error code.

### E. Staggered Rollout Logic

*   Implemented within the Backend logic that determines the "latest applicable version" for an agent (used by WebSocket connection check and `/api/agents/check_update`).
*   Reads `rollout_rules` from the Metadata Store.
*   Applies rules based on agent ID, version, group, or random percentage assignment to decide if a specific agent should receive the newest version or an older stable one.

---

## V. Frontend Component (Admin UI)

This section describes the user interface for administrators.

### A. Core Functionality

1.  **Upload New Agent Version:**
    *   Provides a form to:
        *   Select the agent package/executable file.
        *   Enter version string (e.g., "1.2.1").
        *   Select OS/Architecture.
        *   Enter optional release notes.
    *   Submits data via `multipart/form-data` POST to the Backend's `/api/agents/upload` endpoint.
    *   Displays success or error messages from the Backend.

### B. Potential Enhancements

*   Dashboard showing connected agents, their versions, and online status (data retrieved from Backend APIs).
*   View update progress/status based on agent states reported or error logs.
*   Interface to manage rollout rules (define groups, set percentages).
*   View agent error reports submitted to the Backend.
*   Interface to manage stable vs. beta versions.

---

## VI. Implementation Notes & Considerations

*   **Error Handling:** Meticulous error handling is required in all components (Agent, Updater, Backend). Use `try...catch...finally` and specific error codes.
*   **Security:**
    *   Use HTTPS for all communication. Use WSS (Secure WebSocket).
    *   Protect backend endpoints with proper authentication and authorization.
    *   Validate all input rigorously (file types, sizes, versions, parameters).
    *   Ensure ACLs and Credential Manager usage on the Agent are correctly implemented.
    *   Code signing agent/updater executables is highly recommended.
*   **Concurrency:** Agent and Backend must handle concurrent operations safely (mutexes, locks, atomic operations where needed).
*   **Testing:** Develop a comprehensive testing strategy covering:
    *   Unit tests for individual functions.
    *   Integration tests for Agent-Backend communication.
    *   End-to-end tests simulating the full update process under various conditions (success, download failure, verification failure, updater failure, rollback, `--force` during update, network interruptions).
    *   Security testing.
*   **Dependencies:** Choose robust libraries for communication (HTTP, WebSocket), logging, file operations, JSON parsing, etc.

---