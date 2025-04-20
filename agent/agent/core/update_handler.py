# -*- coding: utf-8 -*-
"""
Update handler module for the Computer Management System Agent (Windows Only).

This module is responsible for handling update notifications, downloading updates,
verifying package integrity, and launching the updater process. It uses helper
methods to structure the update workflow and includes error handling and reporting.
"""
import os
import sys
import threading
import subprocess
import shutil
from pathlib import Path
from typing import Dict, Any, TYPE_CHECKING, Optional, Callable

from . import AgentState

if TYPE_CHECKING:
    from ..config import StateManager
    from ..communication import HttpClient, ServerConnector

from ..utils import get_logger
from ..utils.utils import check_disk_space, calculate_sha256, extract_package
from ..version import __version__ as current_version

logger = get_logger(__name__)

UPDATE_SUBDIR = "updates"
DEFAULT_PACKAGE_SIZE_CHECK_MB = 100
DEFAULT_AGENT_PY_NAME = "main.py"
DEFAULT_AGENT_EXE_NAME_WIN = "agent.exe" # Assumed executable name on Windows when frozen
UPDATER_SCRIPT_NAME = "updater_main.py"
PACKAGE_FILENAME_TEMPLATE = "agent_update_{version}.zip"

class UpdateHandler:
    """
    Handles agent update operations including downloading, verifying,
    extracting the package, and launching the external updater process.

    Uses a lock to prevent concurrent update attempts and interacts with
    external components for state management, HTTP requests, and error reporting.
    Designed specifically for Windows environments.
    """

    def __init__(self,
                 state_manager: 'StateManager',
                 http_client: 'HttpClient',
                 server_connector: 'ServerConnector',
                 set_state_callback: Callable[['AgentState'], bool],
                 shutdown_callback: Callable[[], None]):
        """
        Initialize the UpdateHandler.

        :param state_manager: Instance for accessing storage paths and potentially other state.
        :type state_manager: StateManager
        :param http_client: Instance for making HTTP requests (checking for updates, downloading files).
        :type http_client: HttpClient
        :param server_connector: Instance for reporting errors back to the central server.
        :type server_connector: ServerConnector
        :param set_state_callback: A callable function to update the agent's overall state (e.g., `set_state(AgentState.IDLE)`).
        :type set_state_callback: Callable[[AgentState], bool]
        :param shutdown_callback: A callable function to gracefully shut down the agent.
        :type shutdown_callback: Callable[[], None]
        """
        self.state_manager = state_manager
        self.http_client = http_client
        self.server_connector = server_connector
        self.set_state = set_state_callback
        self.initiate_shutdown = shutdown_callback
        self._update_lock = threading.Lock()

    def initiate_update(self, update_info: Dict[str, Any]):
        """
        Main orchestrator for the agent update process.

        This method acquires a lock to prevent concurrent updates. It calls helper
        methods sequentially to perform:
        1. Prerequisite checks (state, info, disk space).
        2. Package download.
        3. Package verification (checksum).
        4. Package extraction.
        5. Locating new and current agent executables.
        6. Locating the updater script.
        7. Launching the updater script.

        Error handling is delegated to helper methods and `_handle_update_error`.

        :param update_info: Dictionary containing update details received from the server.
                            Expected keys: 'version', 'download_url', 'checksum_sha256'.
        :type update_info: Dict[str, Any]
        """
        if not self._update_lock.acquire(blocking=False):
             logger.warning("Update lock could not be acquired. Another update may be in progress. Ignoring request.")
             return

        extracted_update_dir = None
        package_path = None

        try:
            if self.set_state(AgentState.UPDATING_STARTING) is False:
                logger.warning("Cannot start update: Agent is not in a state that allows starting an update.")
                return

            update_dir = self._check_prerequisites(update_info)
            if not update_dir:
                return

            version = update_info['version']
            download_url = update_info['download_url']
            expected_checksum = update_info['checksum_sha256']
            package_filename = PACKAGE_FILENAME_TEMPLATE.format(version=version)
            package_path = update_dir / package_filename
            extracted_update_dir = update_dir / f"new_agent_{version}"

            if not self._download_package(download_url, package_path):
                return

            if not self._verify_package(package_path, expected_checksum):
                return

            if not self._extract_package(package_path, extracted_update_dir):
                if package_path.exists():
                    try: package_path.unlink()
                    except OSError: logger.warning(f"Could not clean up package file {package_path} after failed extraction.")
                return

            # Determine expected agent filename (simplified for Windows)
            agent_filename = DEFAULT_AGENT_EXE_NAME_WIN if getattr(sys, 'frozen', False) else DEFAULT_AGENT_PY_NAME
            new_agent_exe_path = self._find_executable(extracted_update_dir, agent_filename)
            if not new_agent_exe_path:
                self._handle_update_error("UpdateExtractionFailed",
                                          f"Could not find agent executable ({agent_filename}) in extracted directory '{extracted_update_dir}'",
                                          cleanup_path=extracted_update_dir)
                if package_path.exists():
                    try: package_path.unlink()
                    except OSError: pass
                return

            current_agent_exe_path = self._get_current_agent_executable()
            if not current_agent_exe_path:
                 self._handle_update_error("UpdatePreparationFailed",
                                           "Could not determine current agent executable path",
                                           cleanup_path=extracted_update_dir)
                 if package_path.exists():
                     try: package_path.unlink()
                     except OSError: pass
                 return

            updater_script_path = None
            try:
                updater_search_dir_relative = Path(__file__).resolve().parent.parent.parent / "updater"
                updater_script_path = self._find_executable(updater_search_dir_relative, UPDATER_SCRIPT_NAME)
            except Exception:
                 logger.warning("Could not search for updater relative to current script path.")

            if not updater_script_path:
                 logger.info(f"Updater script not found relative to current code, searching in extracted package: {extracted_update_dir}")
                 updater_script_path = self._find_executable(extracted_update_dir, UPDATER_SCRIPT_NAME)

            if not updater_script_path:
                 self._handle_update_error("UpdatePreparationFailed",
                                           f"Could not find updater script ({UPDATER_SCRIPT_NAME})",
                                           cleanup_path=extracted_update_dir)
                 if package_path.exists():
                     try: package_path.unlink()
                     except OSError: pass
                 return

            storage_path = Path(self.state_manager.storage_path).resolve()
            if not self._launch_updater(updater_script_path, new_agent_exe_path, current_agent_exe_path, storage_path):
                 if extracted_update_dir and extracted_update_dir.exists():
                     try: shutil.rmtree(extracted_update_dir, ignore_errors=True)
                     except OSError: pass
                 if package_path and package_path.exists():
                     try: package_path.unlink()
                     except OSError: pass
                 return

            logger.info("Update process initiated successfully, awaiting agent shutdown.")

        except Exception as e:
             logger.critical(f"Unexpected critical error during initiate_update: {e}", exc_info=True)
             self._handle_update_error("UpdateCriticalError", f"Unexpected error: {e}",
                                       cleanup_path=extracted_update_dir)
             if package_path and package_path.exists():
                 try: package_path.unlink()
                 except OSError: pass

        finally:
            self._update_lock.release()
            logger.debug("Update lock released.")

    def check_for_updates_proactively(self, current_agent_version: str, current_agent_state: 'AgentState'):
        """
        Proactively checks the server for a new agent version and initiates the update if available.

        This method acquires a lock to prevent concurrent update checks/processes.
        It checks the agent's state before proceeding.

        :param current_agent_version: The currently running agent version string.
        :type current_agent_version: str
        :param current_agent_state: The current state object of the agent.
        :type current_agent_state: AgentState
        """
        if current_agent_state.name != "IDLE":
            logger.info(f"Skipping proactive update check: Agent is not in IDLE state (current state: {current_agent_state.name})")
            return

        if not self._update_lock.acquire(blocking=False):
            logger.warning("Proactive update check skipped: Update lock could not be acquired. Another update may be in progress.")
            return

        try:
            logger.info(f"Proactively checking for updates (current version: {current_agent_version})...")
            success, update_info = self.http_client.check_for_update(current_agent_version)

            if success:
                if update_info:
                    new_version = update_info.get('version')
                    logger.info(f"Proactive check found new version: {new_version}")
                    # Check if the found version is actually newer than the current one
                    # (The server might return the latest stable even if it's the same as current)
                    if new_version and new_version != current_agent_version:
                        update_thread = threading.Thread(
                            target=self.initiate_update,
                            args=(update_info,),
                            name="ProactiveUpdateThread"
                        )
                        update_thread.start()
                    else:
                        logger.info(f"Proactive check: Server reported version {new_version}, which is not newer than current {current_agent_version}. No update needed.")
                else:
                    # Success was True, but update_info was None/empty (e.g., 204 No Content)
                    logger.info("Proactive check: No new update available from server.")
            else:
                # Success was False, http_client already logged the error
                logger.warning("Proactive check: Failed to get update information from the server.")

        except Exception as e:
            logger.error(f"Unexpected error during proactive update check: {e}", exc_info=True)
            # Use _handle_update_error for consistent reporting, but don't pass cleanup path
            self._handle_update_error("UpdateCheckFailed", f"Unexpected error during proactive check: {e}")
        finally:
            self._update_lock.release()
            logger.debug("Proactive update check lock released.")

    def handle_new_version_event(self, payload: Dict[str, Any], current_agent_state: 'AgentState'):
        """
        Processes new version notifications, typically received via WebSocket.

        Checks if the agent is in an appropriate state (IDLE) and if the notified
        version differs from the currently running version. If conditions are met,
        it fetches detailed update information and starts the update process in a
        separate thread.

        :param payload: The event payload dictionary, expected to contain 'new_stable_version'.
        :type payload: Dict[str, Any]
        :param current_agent_state: The current state object of the agent (assumed to have a 'name' attribute).
        :type current_agent_state: AgentState
        """
        if current_agent_state.name != "IDLE":
            logger.info(f"Ignoring new version notification: Agent is not in IDLE state (current state: {current_agent_state.name})")
            return

        new_version = payload.get('new_stable_version')
        if not new_version:
            logger.warning("Ignoring new version notification: Missing 'new_stable_version' in payload")
            return

        if new_version == current_version:
            logger.info(f"Ignoring new version notification: Already running version {current_version}")
            return

        logger.info(f"New version notification received: {new_version} (current: {current_version})")

        # Use the proactive check logic which includes locking
        self.check_for_updates_proactively(current_version, current_agent_state)

    def _handle_update_error(self,
                             error_type: str,
                             error_message: str,
                             details: Optional[Dict[str, Any]] = None,
                             cleanup_path: Optional[Path] = None):
        """
        Centralized error handling for the update process.

        Logs the error, reports it via the `server_connector`, attempts to clean up
        a specified file or directory, and resets the agent state to IDLE via the callback.

        :param error_type: A string code representing the type of error (e.g., "UpdateDownloadFailed").
        :type error_type: str
        :param error_message: A human-readable description of the error.
        :type error_message: str
        :param details: Optional dictionary with additional context for the error report.
        :type details: Optional[Dict[str, Any]]
        :param cleanup_path: An optional Path object pointing to a file or directory to be removed.
        :type cleanup_path: Optional[Path]
        """
        logger.error(f"{error_type}: {error_message}")
        # Ensure server_connector is available before reporting
        if hasattr(self, 'server_connector') and self.server_connector:
            self.server_connector.report_error_to_backend(error_type, error_message, details)
        else:
            logger.warning("Server connector not available, cannot report error to backend.")
            
        if cleanup_path and cleanup_path.exists():
            try:
                if cleanup_path.is_file():
                    cleanup_path.unlink()
                    logger.info(f"Cleaned up file: {cleanup_path}")
                elif cleanup_path.is_dir():
                    shutil.rmtree(cleanup_path, ignore_errors=True)
                    logger.info(f"Attempted cleanup of directory: {cleanup_path}")
            except Exception as e:
                logger.warning(f"Exception during cleanup of {cleanup_path}: {e}")
        self.set_state(AgentState.IDLE)

    def _check_prerequisites(self, update_info: Dict[str, Any]) -> Optional[Path]:
        """
        Verifies prerequisites before starting the actual update download.

        Checks for required keys in `update_info`, ensures the storage path is
        available, creates the update subdirectory, and checks for sufficient disk space.

        :param update_info: The update information dictionary received from the server.
                            Expected keys: 'version', 'download_url', 'checksum_sha256'.
        :type update_info: Dict[str, Any]
        :return: The Path object for the update directory if all checks pass, otherwise None.
        :rtype: Optional[Path]
        """
        required_keys = ['version', 'download_url', 'checksum_sha256']
        if not all(key in update_info for key in required_keys):
            self._handle_update_error("UpdateStartFailed",
                                      f"Missing required information in update_info. Got keys: {list(update_info.keys())}")
            return None

        storage_path_str = self.state_manager.storage_path
        if not storage_path_str:
            self._handle_update_error("UpdateResourceCheckFailed", "Cannot determine storage path for update")
            return None
        storage_path = Path(storage_path_str)
        update_dir = storage_path / UPDATE_SUBDIR

        try:
            update_dir.mkdir(parents=True, exist_ok=True)
        except OSError as e:
            self._handle_update_error("UpdateResourceCheckFailed", f"Failed to create update directory '{update_dir}': {e}")
            return None
        except Exception as e:
             self._handle_update_error("UpdateResourceCheckFailed", f"Unexpected error creating update directory '{update_dir}': {e}")
             return None


        required_bytes = DEFAULT_PACKAGE_SIZE_CHECK_MB * 1024 * 1024
        space_ok, error_message = check_disk_space(str(update_dir), required_bytes)
        if not space_ok:
            self._handle_update_error("UpdateResourceCheckFailed", error_message)
            return None

        logger.debug("Prerequisites check passed.")
        return update_dir

    def _download_package(self, download_url: str, package_path: Path) -> bool:
        """
        Downloads the update package file from the specified URL.

        Removes any pre-existing file at the target path before starting the download.
        Updates the agent state during the download process.

        :param download_url: The URL from which to download the package.
        :type download_url: str
        :param package_path: The local file path where the package should be saved.
        :type package_path: Path
        :return: True if the download completes successfully, False otherwise.
        :rtype: bool
        """
        self.set_state(AgentState.UPDATING_DOWNLOADING)
        logger.info(f"Downloading update package from {download_url} to {package_path}")

        if package_path.exists():
            try:
                package_path.unlink()
                logger.debug(f"Removed existing package file: {package_path}")
            except OSError as e:
                self._handle_update_error("UpdateDownloadFailed", f"Failed to remove existing package file '{package_path}': {e}")
                return False
            except Exception as e:
                 self._handle_update_error("UpdateDownloadFailed", f"Unexpected error removing existing package file '{package_path}': {e}")
                 return False


        download_success, error_message = self.http_client.download_file(download_url, str(package_path))
        if not download_success:
            self._handle_update_error("UpdateDownloadFailed", f"Download failed: {error_message}", cleanup_path=package_path)
            return False

        logger.info("Download complete.")
        return True

    def _verify_package(self, package_path: Path, expected_checksum: str) -> bool:
        """
        Verifies the integrity of the downloaded package using SHA256 checksum.

        Compares the calculated checksum of the local file with the expected checksum
        provided in the update information. Handles checksum calculation errors.

        :param package_path: Path to the downloaded package file.
        :type package_path: Path
        :param expected_checksum: The expected SHA256 checksum string (hexadecimal).
        :type expected_checksum: str
        :return: True if the checksum matches, False otherwise or if calculation fails.
        :rtype: bool
        """
        self.set_state(AgentState.UPDATING_VERIFYING)
        logger.info(f"Verifying checksum for {package_path}")

        checksum_ok, checksum_result = calculate_sha256(str(package_path))

        if not checksum_ok:
            self._handle_update_error("UpdateChecksumMismatch", f"Failed to calculate checksum: {checksum_result}", cleanup_path=package_path)
            return False

        if checksum_result.lower() != expected_checksum.lower():
            details = {
                "expected_checksum": expected_checksum,
                "actual_checksum": checksum_result
            }
            self._handle_update_error("UpdateChecksumMismatch", "Checksum mismatch", details, cleanup_path=package_path)
            return False

        logger.info("Checksum verified successfully.")
        return True

    def _extract_package(self, package_path: Path, extract_dir: Path) -> bool:
        """
        Extracts the contents of the downloaded package file (assumed zip)
        into the specified directory.

        Cleans the target extraction directory before extraction begins.

        :param package_path: Path to the downloaded package file (e.g., zip).
        :type package_path: Path
        :param extract_dir: The directory where the package contents should be extracted.
        :type extract_dir: Path
        :return: True if extraction completes successfully, False otherwise.
        :rtype: bool
        """
        self.set_state(AgentState.UPDATING_EXTRACTING_UPDATER)
        logger.info(f"Extracting package {package_path} to {extract_dir}")

        try:
            if extract_dir.exists():
                shutil.rmtree(extract_dir, ignore_errors=True)
                logger.debug(f"Attempted cleanup of existing extraction directory: {extract_dir}")

            extract_dir.mkdir(parents=True, exist_ok=True)

            extract_success, extract_error = extract_package(str(package_path), str(extract_dir))

            if not extract_success:
                self._handle_update_error("UpdateExtractionFailed", f"Extraction failed: {extract_error}", cleanup_path=extract_dir)
                return False

        except Exception as e:
            self._handle_update_error("UpdateExtractionFailed", f"Extraction failed with exception: {e}", cleanup_path=extract_dir)
            return False

        logger.info("Package extracted successfully.")
        return True

    def _launch_updater(self, updater_script_path: Path, new_agent_exe_path: Path, current_agent_exe_path: Path, storage_dir: Path) -> bool:
        """
        Constructs the command and launches the external updater process in a detached state on Windows.

        Uses the system's Python interpreter to run the updater script.

        :param updater_script_path: The absolute Path to the `updater_main.py` script.
        :type updater_script_path: Path
        :param new_agent_exe_path: The absolute Path to the newly extracted agent executable.
        :type new_agent_exe_path: Path
        :param current_agent_exe_path: The absolute Path to the currently running agent executable.
        :type current_agent_exe_path: Path
        :param storage_dir: The absolute Path to the storage directory for the updater's logs/errors.
        :type storage_dir: Path
        :return: True if the `subprocess.Popen` call was executed without immediate exception, False otherwise.
                 Note: This does not guarantee the updater runs successfully, only that it was launched.
        :rtype: bool
        """
        self.set_state(AgentState.UPDATING_PREPARING_SHUTDOWN)
        current_pid = os.getpid()

        updater_cmd = [
            sys.executable,
            str(updater_script_path),
            "--pid", str(current_pid),
            "--new_agent", str(new_agent_exe_path),
            "--current_agent", str(current_agent_exe_path),
            "--storage_dir", str(storage_dir)
        ]

        try:
            logger.info(f"Launching updater with command: {' '.join(updater_cmd)}")

            creation_flags = subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP

            subprocess.Popen(
                updater_cmd,
                creationflags=creation_flags,
                close_fds=True
            )
            logger.info("Updater process launched successfully. Initiating graceful agent shutdown.")

            shutdown_thread = threading.Thread(target=self.initiate_shutdown, name="UpdateShutdownThread")
            shutdown_thread.start()
            return True

        except FileNotFoundError:
             self._handle_update_error("UpdateLaunchFailed", f"Failed to launch updater: Executable or script not found. Command: {' '.join(updater_cmd)}")
             return False
        except Exception as e:
            self._handle_update_error("UpdateLaunchFailed", f"Failed to launch updater: {e}")
            return False

    def _find_executable(self, search_dir: Path, executable_name: str) -> Optional[Path]:
        """
        Recursively searches for a file with the given name within a directory.

        :param search_dir: The root directory Path object to start the search from.
        :type search_dir: Path
        :param executable_name: The exact filename to search for.
        :type executable_name: str
        :return: The full Path object to the first found executable, or None if not found.
        :rtype: Optional[Path]
        """
        logger.debug(f"Searching for '{executable_name}' within '{search_dir}'")
        if not search_dir.is_dir():
            logger.warning(f"Search directory '{search_dir}' does not exist or is not a directory.")
            return None

        for item in search_dir.rglob(executable_name):
             if item.is_file():
                 logger.info(f"Found '{executable_name}' at: {item}")
                 return item

        logger.warning(f"'{executable_name}' not found within '{search_dir}'")
        return None

    def _get_current_agent_executable(self) -> Optional[Path]:
        """
        Determines the absolute path to the currently running agent executable.

        Handles both frozen (e.g., PyInstaller executable) and script-based execution.
        For script-based execution, it assumes a specific project structure relative
        to this file's location. Assumes Windows environment.

        :return: A resolved Path object to the current executable, or None if it cannot be determined or verified.
        :rtype: Optional[Path]
        """
        try:
            if getattr(sys, 'frozen', False):
                current_path = Path(sys.executable).resolve()
                logger.debug(f"Determined current agent (frozen): {current_path}")
                if not current_path.exists():
                     logger.error(f"sys.executable path does not exist: {current_path}")
                     return None
                return current_path
            else:
                try:
                    project_root = Path(__file__).resolve().parent.parent
                    current_path = (project_root / DEFAULT_AGENT_PY_NAME).resolve()
                    logger.debug(f"Determined current agent (script): {current_path}")
                    if not current_path.is_file():
                         logger.error(f"Could not verify current agent script path (does not exist or not a file): {current_path}")
                         alt_path = Path(__file__).resolve().parent.parent.parent / DEFAULT_AGENT_PY_NAME
                         if alt_path.is_file():
                              logger.warning(f"Using alternative script path: {alt_path}")
                              return alt_path.resolve()
                         return None
                    return current_path
                except Exception as path_e:
                     logger.error(f"Error resolving script path based on __file__: {path_e}")
                     return None
        except Exception as e:
            logger.error(f"Unexpected error determining current agent executable path: {e}")
            return None

