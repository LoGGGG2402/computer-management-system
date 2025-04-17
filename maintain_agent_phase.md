## Phased Agent Implementation Plan

## This plan breaks down the agent upgrade process into logical phases, helping you focus on each part and test more easily.**Phase 1: Core Foundation and Structure*** **Goal:** Establish the basic structure, logging, configuration reading, privilege detection, and prepare for more complex features.

* **Key Implementation Steps:**

  1. **Dependencies & Structure:**

     - Add `pywin32`, `pyuac` to `requirements.txt`.

     - Create the proposed new directories and module files: `src/system/windows_utils.py`, `src/system/lock_manager.py`, `src/ipc/`, `src/core/agent_state.py`, `build_scripts/`.

  2. **Privilege Detection:**

     - Implement `windows_utils.is_running_as_admin()`.

  3. **Basic Storage Path Identification:**

     - In `StateManager`, implement logic to determine `storage_path` based on privileges (`ProgramData`/`LocalAppData`). _Setting ACLs is not needed yet_.

  4. **Config Loading:**

     - In `main.py`, ensure `ConfigManager` is initialized with the correct config path located within `storage_path`.

  5. **Logging:**

     - Ensure `setup_logger` works and writes logs to the `logs` subdirectory within `storage_path`. Configuration should be read from `config.json`.

  6. **Basic State Management:**

     - Define `enum AgentState` with initial states (`STARTING`, `IDLE`, `SHUTTING_DOWN`).

     - In `Agent`, add `_state`, `_state_lock`, `_set_state`. Transition state to `IDLE` after successful initialization.

  7. **Basic Lock File (Existence Check Only):**

     - In `LockManager`, implement `acquire` just to create the lock file (`os.open` with `O_CREAT | O_EXCL`) and write the PID.

     - Implement `release` to delete the file.

     - In `main.py`, use this `LockManager`. _Actual file locking or timestamps are not needed yet_.

* **Key Files/Modules Involved:** `requirements.txt`, `src/main.py`, `src/config/state_manager.py`, `src/config/config_manager.py`, `src/utils/logger.py`, `src/system/windows_utils.py`, `src/system/lock_manager.py`, `src/core/agent_state.py`, `src/core/agent.py`.

* **Testing:** Agent starts, creates the correct data directory based on privileges, writes logs, creates/deletes the basic lock file, exits normally.**Phase 2: Secure Single Instance and Data Protection*** **Goal:** Implement the full lock file mechanism to prevent multiple instances and protect the data directory.

* **Key Implementation Steps:**

  1. **`Implement Advanced Lock File (LockManager):`**

     - Add actual file locking logic using `msvcrt.locking` in `acquire` and `release`.

     - Add logic to write/read PID and Timestamp (ISO 8601) in `acquire` and `release`.

     - Implement the timestamp update thread/timer (`_start_timestamp_updater`, `_timestamp_update_loop`, `_stop_timestamp_updater`).

     - Implement detailed stale lock check logic (check PID existence using `psutil`, check timestamp age) in `acquire`.

     - Ensure `atexit.register(self.release)` is called correctly within `acquire`.

  2. **`Set ACLs (StateManager):`**

     - Fully implement the `_ensure_directory_permissions(is_admin)` method using `win32security` to set strict permissions on `storage_path` when running as Admin.

* **Key Files/Modules Involved:** `src/system/lock_manager.py`, `src/config/state_manager.py`, `src/system/windows_utils.py`, `src/main.py`.

* **Testing:** Cannot run two agent instances simultaneously. Stale locks are handled correctly (old lock deleted, new instance can start). The `ProgramData` directory has correct ACLs when installed/run as Admin. Agent still starts/exits normally. Timestamp in the lock file is updated.**Phase 3: Autostart and Configuration Migration*** **Goal:** Allow the agent to start automatically with Windows and handle configuration format changes.

* **Key Implementation Steps:**

  1. **`Implement Autostart (windows_utils.py, main.py):`**

     - Complete the `register_autostart` and `unregister_autostart` functions using `winreg`.

     - Complete the logic for handling `--enable-autostart`, `--disable-autostart` arguments in `main.py`.

  2. **`Implement Config Migration (ConfigManager):`**

     - Add the `_save_config`, `_backup_config` methods, and specific migration functions (`_migrate_config_vX_to_vY`).

     - Add logic to check `config_version` and call migrate in `ConfigManager.__init__`.

  3. **Basic Version Tracking:**

     - Add a `current_version` field to `config.json`. The agent reads and uses it (e.g., logs on startup). _Updating the version will be handled in a later phase_.

* **Key Files/Modules Involved:** `src/system/windows_utils.py`, `src/main.py`, `src/config/config_manager.py`.

* **Testing:** Running `--enable/disable-autostart` works correctly (check Registry). Agent autostarts after login (if enabled). Create an old version config file, run the agent, check that the config is backed up and migrated to the new version, and the agent still operates.**Phase 4: Controlled Restart (IPC)*** **Goal:** Implement the `--force` mechanism for safely restarting the agent.

* **Key Implementation Steps:**

  1. **`IPC Secret (windows_utils.py):`** Implement `manage_ipc_secret` using `win32cred` or `keyring`.

  2. **`Named Pipe Server (NamedPipeIPCServer in src/ipc/):`**

     - Implement logic to create the pipe with the correct name and ACLs.

     - Implement the loop for listening, reading/writing JSON, authenticating the secret, checking state (`busy_updating`), sending responses.

     - Integrate calling `Agent.graceful_shutdown` upon receiving a valid command.

  3. **`Named Pipe Client (src/ipc/, main.py):`**

     - Implement the `send_force_command` function for connecting, sending requests, and receiving responses.

     - In `main.py`, add logic to handle the `--force` argument, call `send_force_command`, process the returned status, and loop waiting for `lock_manager.acquire()`.

  4. **Integrate into Agent:** Initialize, start, stop `NamedPipeIPCServer`. Transition state to `FORCE_RESTARTING` upon receiving a valid IPC command before calling `graceful_shutdown`.

* **Key Files/Modules Involved:** `src/system/windows_utils.py`, `src/ipc/named_pipe_server.py`, `src/ipc/named_pipe_client.py`, `src/core/agent.py`, `src/main.py`.

* **Testing:** Run instance A. Run instance B with `--force`. Instance A receives the command, logs, shuts down, and releases the lock. Instance B waits, successfully acquires the lock, and starts. Try running `--force` when A is in `UPDATING_*` state (need to simulate this state) -> B receives `busy_updating` and exits. Try with incorrect secret -> B receives `invalid_secret` and exits.**Phase 5: Update Mechanism (Agent Side)*** **Goal:** Implement the logic for receiving update triggers, downloading, verifying, and preparing for the updater.

* **Key Implementation Steps:**

  1. **Update Trigger (WS & Polling):**

     - Modify `WSClient` to handle the `agent:update_required` event.

     - In `Agent`, create the polling timer (`_polling_timer`) and callback to call `HttpClient.check_for_update`.

  2. **`HttpClient`**: Add `check_for_update` and `download_file` methods.

  3. **`Agent.initiate_update()`**:

     - Implement the full logic: check `IDLE` state, transition `UPDATING_*` states, check disk space, download, verify checksum/signature, extract updater (if needed).

  4. **Basic Error Reporting:**

     - Create `HttpClient.report_error`.

     - Create `_report_error_to_backend` function (buffering not needed yet).

     - Call this function on errors during the update process (download, verify...).

  5. **Launch Updater & Shutdown:**

     - In `initiate_update`, after preparations, call `subprocess.Popen` to run `updater.exe` (assume it exists somewhere for testing) with necessary arguments.

     - Call `self.graceful_shutdown()`.

* **Key Files/Modules Involved:** `src/core/agent.py`, `src/communication/ws_client.py`, `src/communication/http_client.py`, `src/utils/utils.py` (if checksum/verify helpers needed), `src/core/agent_state.py`.

* **Testing:** Simulate update triggers via WS or polling. Agent correctly performs download, verify steps (can use dummy files and checksums). Agent logs errors and reports to backend (mock backend endpoint) on failures. Agent calls shutdown and attempts to launch the updater (updater doesn't need to work correctly yet).**`Phase 6: Develop Updater (updater.exe)`*** **Goal:** Create the standalone `updater.exe` that correctly performs file replacement and rollback.

* **Key Implementation Steps:**

  1. Create `updater_main.py` file (outside `src`).

  2. Implement the full logic: Parse args, logging, wait for old PID, backup, replace, launch new, verify, rollback (if fail), cleanup (if success), self-delete.

* **Key Files/Modules Involved:** `updater_main.py`.

* **Testing:** Run the updater manually with simulated arguments. Check that it backups, replaces, launches, verifies, cleans up/rolls back as expected. Test error cases (insufficient permissions, file locked...).**Phase 7: Finalize Error Reporting and Build/Packaging*** **Goal:** Complete the error reporting mechanism and set up the automated build and packaging process.

* **Key Implementation Steps:**

  1. **Advanced Error Reporting:**

     - Finalize the `_report_error_to_backend` function in the Agent to include buffering logic to `error_reports.jsonl` when API calls fail.

     - Add logic to the polling timer to retry sending buffered errors.

  2. **`Create Build Scripts (build_scripts/):`**

     - Create `build_agent.py` using PyInstaller to build `src/main.py`. Configure hidden imports. **Do not** add config data.

     - Create `build_updater.py` using PyInstaller to build `updater_main.py`.

  3. **`Create Inno Setup Script (build_scripts/installer.iss):`**

     - Write the detailed `.iss` file, ensuring `Source` paths in `[Files]` point correctly to the `dist` directory created by the build scripts. Configure `PrivilegesRequired`, `Dirs`, `Registry`, `Files`, `UninstallRun` correctly.

  4. **`Create Orchestration Script (build_installer.py):`**

     - Write the Python script using `subprocess` to automate: cleaning `dist`, running `build_agent.py`, running `build_updater.py`, checking output, running `ISCC.exe` with `installer.iss`.

* **Key Files/Modules Involved:** `src/core/agent.py`, `src/communication/http_client.py`, `build_scripts/build_agent.py`, `build_scripts/build_updater.py`, `build_scripts/installer.iss`, `build_installer.py`.

* **Testing:** Running `build_installer.py` successfully creates `setup.exe`. Install the agent using `setup.exe` in both Admin/User modes. Check agent operation, autostart, data directory/permissions, uninstallation. Test the error buffering and retry mechanism.**Phase 8: Comprehensive Testing and Refinement*** **Goal:** Ensure all features work together harmoniously, stably, and securely.

* **Key Implementation Steps:**

  1. Perform end-to-end test scenarios: Successful update, failed update (download, verify, updater fail), rollback, force restart in various states, installation/uninstallation.

  2. Check performance and resource usage.

  3. Review security aspects (ACLs, IPC secret, signature verification if implemented).

  4. Refine default configurations, error messages, logging.

* **Key Files/Modules Involved:** Entire codebase, build scripts, install script.

* **Testing:** Perform on multiple environments/Windows versions.By breaking the project into these phases, you can manage progress, test incrementally, and ensure the final product quality.
