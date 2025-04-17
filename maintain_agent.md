## Comprehensive Agent Development and Installation Plan

## **Goal:** Provide detailed guidance on developing the existing Python agent code to meet the requirements in `maintain.md`, including the updater, and setting up an automated build/packaging process using PyInstaller and Inno Setup.**Note:** This plan assumes you will add the necessary libraries (`pywin32`, `pyuac`) to `requirements.txt` and install them.**Part I: Overview and Preparation**1) **Dependencies:** Add `pywin32`, `pyuac>=0.0.3` to `requirements.txt`.

2) **Create New Modules (Agent Code):**

   - `src/system/windows_utils.py`: **Required.** Contains functions: `is_running_as_admin`, `register_autostart`, `unregister_autostart`, `set_directory_acls`, `manage_ipc_secret`, `get_user_sid_string`.

   - `src/system/lock_manager.py`: **Required.** Contains the `LockManager` class for managing the lock file.

   - `src/ipc/named_pipe_server.py`: **Required.** Class `NamedPipeIPCServer`.

   - `src/ipc/named_pipe_client.py`: **Required.** Function `send_force_command`.

   - `src/core/agent_state.py`: **Recommended.** Defines `enum AgentState`.

3) **Create Build Scripts Directory:**

   - At the project root (alongside `src`), create a new directory named `build_scripts`.

   - This directory will contain individual build scripts and the Inno Setup script file.

4) **Agent Code Principles:**

   - The agent must correctly determine its executable path (`sys.executable`) and data path (`storage_path` based on privileges) at runtime.

   - `ConfigManager` must read the config from `storage_path`, not relative to the `.exe` location.

   - The `config.json` file should _not_ be packaged into `cms_agent.exe`.**`Part II: Agent Code Development (cms_agent.exe)`**_(Detailed implementation steps within your existing Python code)_**`A. Autostart (maintain.md Section II.B)`**1) **`Check Privileges (src/system/windows_utils.py):`** Create function `is_running_as_admin() -> bool` using `ctypes` or `pyuac`.

2) **`Registry Operations (src/system/windows_utils.py):`** Create functions `register_autostart(exe_path, is_admin)` and `unregister_autostart(is_admin)` using `winreg`. Handle permission errors (`PermissionError`, `FileNotFoundError`).

3) **`Integrate into main.py:`** Add args `--enable-autostart`, `--disable-autostart`. Call the functions based on args and privileges, then exit. This logic needs to run _before_ `lock_manager.acquire()`.**`B. Data Protection & Single Instance (maintain.md Section II.C)`**1) **`Determine Storage Path (StateManager.__init__):`** Use `windows_utils.is_running_as_admin()` to choose `os.getenv('PROGRAMDATA')` or `os.getenv('LOCALAPPDATA')`. Create the absolute path `self.storage_path`. Call `self._ensure_directory_permissions()` after creating the directory.

2) **`Set ACLs (StateManager._ensure_directory_permissions):`** Only run if Admin. Use `win32security` to create a new SD, grant Full Control to SYSTEM/Admins, disable inheritance, apply to `self.storage_path`. Log details, handle `pywintypes.error`.

3) **`Manage Lock File (src/system/lock_manager.py - New):`**

   - Create class `LockManager`.

   - `__init__(self, storage_path)`:

     - Import `os`, `msvcrt`, `datetime`, `time`, `threading`, `psutil`, `logging`, `atexit`.

     - Store `self.logger = logging.getLogger(...)`.

     - Calculate `self.lock_file_path = os.path.join(storage_path, "agent.lock")`.

     - Initialize state: `self._lock_fd = None`, `self._updater_thread = None`, `self._stop_event = threading.Event()`.

   - `acquire(self) -> bool`:

     - Implement detailed logic (from previous plan's II.B.3 - Modify `acquire_lock` and `except FileExistsError`) using `self.lock_file_path`, `self.logger`, `self._lock_fd`, `self._stop_event`. Use `os.open(..., O_CREAT | O_EXCL | O_RDWR)`, `msvcrt.locking(..., msvcrt.LK_NBLCK, 1)`.

     - On successful lock, store `self._lock_fd = fd`.

     - Write PID and ISO timestamp (`os.write`, `os.fsync`).

     - Call `self._start_timestamp_updater()`.

     - Register `atexit.register(self.release)`.

     - Return True/False. Handle stale lock check (using `psutil.pid_exists`, timestamp age comparison, attempting to re-lock).

   - `release(self)`:

     - Implement detailed logic (from previous plan's II.B.3 - Modify `release_lock`).

     - Call `self._stop_timestamp_updater()`.

     - Unlock (`msvcrt.LK_UNLCK`), close (`os.close`), delete file. Set `self._lock_fd = None`.

   - `_start_timestamp_updater(self)`: Create and start daemon thread running `self._timestamp_update_loop`. Store thread handle in `self._updater_thread`.

   - `_stop_timestamp_updater(self)`: Set `self._stop_event`, `join()` the `self._updater_thread`.

   - `_timestamp_update_loop(self)`: Loop `while not self._stop_event.wait(60)`. Inside, use `self._lock_fd` to lock (`LK_LOCK`), seek, write timestamp, fsync, truncate, unlock (`LK_UNLCK`). Handle errors.

4) **`Integrate into main.py:`**

   - Import `LockManager` from `src.system.lock_manager`.

   - **Remove** functions `acquire_lock`, `release_lock`, `start_lock_timestamp_updater`, `_lock_timestamp_update_loop` and related global variables from `main.py`.

   - After `state_manager` is initialized and `storage_path` is available:

     - `lock_manager = LockManager(storage_path)`

     - `if not lock_manager.acquire():`

       - `logger.critical("Failed to acquire lock. Agent exiting.")`

       - `sys.exit(1)`

     - _`(No need for atexit.register here anymore as it's done in LockManager.acquire)`_

   - Pass `lock_manager` to the `Agent` constructor.**`C. State Management (maintain.md Section II.D)`**1) **`src/core/agent_state.py`** (New): Define `enum AgentState`. **Required** states include: `STARTING`, `IDLE`, `FORCE_RESTARTING`, `UPDATING_STARTING`, `UPDATING_DOWNLOADING`, `UPDATING_VERIFYING`, `UPDATING_EXTRACTING_UPDATER`, `UPDATING_PREPARING_SHUTDOWN`, `SHUTTING_DOWN`, `STOPPED`. Defining these states accurately manages the agent's complex operational flow.

2) **`src/core/agent.py`** (`Agent`):

   - Import `AgentState`, `threading`.

   - Add `self._state = AgentState.STARTING`, `self._state_lock = threading.Lock()`.

   - Add method `_set_state(self, new_state)` with lock and detailed logging `f"State transition: {self._state.name} -> {new_state.name}"`.

   - Use `_set_state` at crucial logic transition points.

   - Check `self._state` (using `with self._state_lock:`) before handling sensitive triggers. Log and ignore if state is inappropriate.**`D. Configuration Management (maintain.md Section II.E)`**1) **Version Tracking:** Store `current_version` in `config.json`. Agent reads on startup and sends to Backend. The _new_ agent _after a successful update_ updates this field in `config.json`.

2) **`Configuration Migration (ConfigManager):`**

   - Add methods `_save_config(self, config_data)` and `_backup_config(self)`.

   - Add specific migration methods, e.g., `_migrate_config_v1_to_v2(self, old_config)`.

   - In `__init__`, after `_load_config`: Add logic to check `config_version`, call backup, migrate, save if needed. Handle critical migration errors (log critical, report error to backend, potentially stop agent).**`E. Controlled Restart (--force via IPC) (maintain.md Section II.F)`**1) **`IPC Secret (src/system/windows_utils.py):`** Create function `manage_ipc_secret(action='get'/'create', context='USER'/'SYSTEM')` using `win32cred` (or `keyring`). Handle `ERROR_NOT_FOUND`.

2) **`Named Pipe Server (src/ipc/named_pipe_server.py and src/core/agent.py):`**

   - Create class `NamedPipeIPCServer(threading.Thread)`.

   - `__init__`: Get `is_admin`, create `pipe_name` (use `windows_utils.get_user_sid_string()` if user), get `ipc_secret`.

   - `run`: Create pipe (`CreateNamedPipe`) with correct ACLs (use `win32security`). Loop `ConnectNamedPipe`, read/write (`ReadFile`/`WriteFile`), parse JSON, authenticate secret, check `agent._state`, send JSON response (`acknowledged`, `busy_updating`, `invalid_secret`). If `acknowledged`, call `Agent.graceful_shutdown()`. Close handle on stop.

   - `stop`: Method to stop the thread.

   - In `Agent.__init__`: Accept `lock_manager` parameter.

   - In `Agent.start`: Create and start `self._ipc_server`.

   - In `Agent.graceful_shutdown`: Call `self._ipc_server.stop()`, `self._ipc_server.join()`.

3) **`Named Pipe Client (src/ipc/named_pipe_client.py and src/main.py):`**

   - Create function `send_force_command(new_args)`.

   - Connect pipe (`CreateFile` with timeout). Handle `ERROR_PIPE_BUSY`, `ERROR_FILE_NOT_FOUND`.

   - Set pipe mode (`SetNamedPipeHandleState`).

   - Send JSON request (`WriteFile`).

   - Read JSON response (`ReadFile` with timeout). Handle timeout/parse errors.

   - Close handle. Return status.

   - In `main.py`: If `args.force`, call `send_force_command`. Process returned status. If proceeding -> loop calling `lock_manager.acquire()` with a long timeout.**`F. Agent Update Process (maintain.md Sections II.G & II.H)`**1) **Update Trigger:**

   - **WS:** Modify `WSClient` to handle `agent:update_required` event and call callback in `Agent`.

   - **Polling:** In `Agent`, create timer `_polling_timer` calling `HttpClient.check_for_update` periodically. Stop timer on shutdown.

2) **`HttpClient`**: Add methods `check_for_update(agent_id, version)` and `download_file(url, save_path)`.

3) **`Agent.initiate_update(update_info)`**:

   - Check `IDLE` state.

   - Execute `UPDATING_*` state transitions.

   - Check disk space (`psutil`). Report error to backend if insufficient.

   - Call `HttpClient.download_file`. Report error to backend on failure.

   - Verify checksum (`hashlib`). Report error to backend if mismatch.

   - Verify signature (if implemented). Report error to backend on failure.

   - Extract `updater.exe` (if needed, use `zipfile`). Report error to backend on failure.

   - Launch `updater.exe` (`subprocess.Popen` DETACHED) with args: `package_path`, `current_exe_path`, `current_pid`, `log_dir`. Report error to backend on launch failure.

   - Call `self.graceful_shutdown()`.**`G. Graceful Shutdown (maintain.md Section II.I)`**1) **`Modify Agent.graceful_shutdown():`**

   - Set state `SHUTTING_DOWN`.

   - Stop all timers/threads (status, polling, **IPC**) using events and `join(timeout)`.

   - Disconnect WS (`ws_client.disconnect()`).

   - Close IPC Pipe (`_ipc_server.stop()`).

   - Flush logs (`logging.shutdown()`).

   - **`Call self.lock_manager.release()`** (Using the passed `LockManager` instance).

   - Log "Shutdown complete".

   - `sys.exit(0)`.

2) **`atexit`**: Keep `atexit.register(lock_manager.release)` in `main.py` (registering the instance method).**`H. Resource Checks (maintain.md Section II.J)`**1) **Implementation:** Add `psutil.disk_usage(temp_dir).free` check in `Agent.initiate_update` before download. Log and report error if insufficient.**`I. Logging & Error Reporting (maintain.md Section II.K)`**1) **Local Logging:** Configure `RotatingFileHandler` correctly. Log all critical events detailedly with appropriate levels.

2) **Error Reporting:**

   - Create `HttpClient.report_error(payload)`.

   - Create function `_report_error_to_backend(...)` in `Agent`: Call API, if fails -> write error to buffer file (`error_reports.jsonl` using `json.dump`, open file 'a').

   - Add logic to retry sending from buffer in the polling timer.

   - Call `_report_error_to_backend` from critical `except` blocks (update, migrate, IPC, unhandled exceptions...).**`Part III: Develop Updater (updater.exe)`**1) **`Create updater_main.py file:`** (Place outside `src`).

2) **Implement Logic:**

   - Use `argparse` to receive args: `package_path`, `current_exe_path`, `old_pid`, `log_dir`.

   - Setup `logging` to write to `updater.log`.

   - Use `psutil.pid_exists` and `time.sleep` to wait for `old_pid` to exit (with timeout, e.g., 60s). If timeout, log error, try `taskkill`, or exit with error.

   - Use `os.rename` for backup (`.old`). Handle `PermissionError`.

   - Use `shutil.move` or `copy2` for replacement. Handle errors. If error -> call `rollback()`.

   - Use `subprocess.Popen` to launch the new agent.

   - Add `verify_new_agent()` function: `time.sleep(10)`, check new process with `psutil`, check `agent.lock` existence and recent timestamp. Return True/False.

   - If `verify_new_agent()` returns False -> call `rollback()`.

   - Add `rollback()` function: Log, kill new process (if running), `os.remove` new exe, `os.rename` `.old` back, try launching old agent. Exit with error code.

   - If verification succeeds -> call `cleanup()`: `os.remove` the `.old` file, `os.remove` the `package_path`.

   - Add self-delete logic (`cmd.exe /c del ...`) before `sys.exit()`.

   - `sys.exit(0)` on success, non-zero on error/rollback.**Part IV: Automated Build and Packaging Process**1) **`Create Agent Build Script (build_scripts/build_agent.py):`**

   - PyInstaller script building only `src/main.py`.

   - Define correct `entry_script`, `output_dir`, `work_dir`.

   - Define comprehensive `hidden_imports` list.

   - Call `PyInstaller.__main__.run(...)`.

2) **`Create Updater Build Script (build_scripts/build_updater.py):`**

   - PyInstaller script building only `updater_main.py`.

   - Define correct `entry_script`, `output_dir`, `work_dir`.

   - Minimal `hidden_imports`.

   - Call `PyInstaller.__main__.run(...)`.

3) **`Create Inno Setup Script (build_scripts/installer.iss):`**

   - Write the detailed `.iss` file.

   - In `[Files]`, `Source:` must use correct relative paths (`"..\\dist\\..."`).

4) **`Create Build Orchestration Script (build_installer.py - at root):`**

   - Use `subprocess`, `os`, `shutil`.

   - Define script paths, `dist` dir, `ISCC.exe` path.

   - Create `run_command` helper function for subprocess execution and error checking.

   - Main flow: Clean `dist` -> run `build_agent.py` -> run `build_updater.py` -> check output -> run `ISCC.exe` with `installer.iss`.

5) **Remove Old Script:** Delete the original `build_executable.py`.**How to Use:**1) Complete agent & updater code development.

2) Create the scripts in `build_scripts`.

3) Create `build_installer.py` at the root.

4) Run `python build_installer.py` from the project root.

5) Get the final `setup.exe` installer.**Part V: Summary and Notes*** **Testing:** Extremely important! Test each feature, update flows, rollback, IPC, install/uninstall (Admin/User), error cases thoroughly.

* **Security:** ACLs, IPC secret, code signing, HTTPS/WSS.

* **Error Handling:** Detailed logging, backend error reporting, error buffering.This plan provides the most detailed possible implementation steps for each requirement in `maintain.md` based on your current code structure and the proposed build/install process.
