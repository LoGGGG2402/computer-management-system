# -*- coding: utf-8 -*-
"""
Update handler module for the Computer Management System Agent (Windows Only).

This module is responsible for handling update notifications, downloading updates,
verifying package integrity, potentially replacing the updater itself,
and launching the updater process. It uses helper methods to structure the update
workflow and includes error handling and reporting.
"""
import os
import sys
import threading
import subprocess
import shutil
import time
import logging
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
DEFAULT_AGENT_PY_NAME = "__main__.py"
DEFAULT_AGENT_EXE_NAME_WIN = "agent.exe"
UPDATER_SCRIPT_NAME = "updater_main.py"
UPDATE_EXECUTABLE_NAME = "updater.exe"
PACKAGE_FILENAME_TEMPLATE = "agent_update_{version}.zip"
AGENT_SUBDIR_IN_PACKAGE = "agent"
UPDATER_SUBDIR_IN_PACKAGE = "updater"

def move_path(src: Path, dst: Path, max_retries=3, delay=1) -> bool:
    """Moves a file or directory with a retry mechanism.

    Uses `shutil.move`. Retries the operation up to `max_retries` times
    with a `delay` between attempts if an `OSError` occurs (common if the
    destination file is briefly locked).

    :param src: The source path (file or directory) to move.
    :type src: pathlib.Path
    :param dst: The destination path. If it exists, it will be overwritten.
    :type dst: pathlib.Path
    :param max_retries: Maximum number of move attempts. Defaults to 3.
    :type max_retries: int
    :param delay: Delay in seconds between retry attempts. Defaults to 1.
    :type delay: int
    :return: True if the move was successful, False otherwise.
    :rtype: bool
    """
    if not src.exists():
        logging.error(f"Source path for move does not exist: {src}")
        return False

    for attempt in range(max_retries):
        try:
            if not dst.parent.exists():
                dst.parent.mkdir(parents=True, exist_ok=True)
            if dst.is_file():
                dst.unlink()
            elif dst.is_dir():
                 shutil.rmtree(dst, ignore_errors=True)

            shutil.move(str(src), str(dst))
            logging.info(f"Successfully moved '{src}' to '{dst}' (attempt {attempt + 1})")
            return True
        except (OSError, shutil.Error) as e:
            logging.warning(f"Move attempt {attempt + 1}/{max_retries} failed for '{src}' -> '{dst}': {e}")
            if attempt < max_retries - 1:
                time.sleep(delay)
            else:
                logging.error(f"Failed to move '{src}' to '{dst}' after {max_retries} attempts.")
                return False
    return False

class UpdateHandler:
    """
    Handles agent update operations including downloading, verifying,
    extracting the package, potentially replacing the updater, and launching
    the external updater process.

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
        self._is_frozen = getattr(sys, 'frozen', False)

    def initiate_update(self, update_info: Dict[str, Any]):
        """
        Main orchestrator for the agent update process.

        Handles prerequisites, download, verification, extraction,
        potential updater replacement, and launching the update.
        Acquires a lock to prevent concurrent updates.

        :param update_info: Dictionary containing update details received from the server.
                            Expected keys: 'version', 'download_url', 'checksum_sha256'.
        """
        if not self._update_lock.acquire(blocking=False):
             logger.warning("Update lock could not be acquired. Another update is likely in progress. Ignoring request.")
             return

        extracted_update_dir: Optional[Path] = None
        package_path: Optional[Path] = None

        try:
            if not self.set_state(AgentState.UPDATING_STARTING):
                logger.warning("Cannot start update: Agent is not in a state that allows starting an update (e.g., not IDLE).")
                return

            update_dir = self._check_prerequisites(update_info)
            if not update_dir: return

            version = update_info['version']
            download_url = update_info['download_url']
            expected_checksum = update_info['checksum_sha256']
            package_filename = PACKAGE_FILENAME_TEMPLATE.format(version=version)
            package_path = update_dir / package_filename
            extracted_update_dir = update_dir / f"new_agent_{version}"

            if not self._download_package(download_url, package_path): return
            if not self._verify_package(package_path, expected_checksum): return
            if not self._extract_package(package_path, extracted_update_dir):
                self._cleanup_on_error(package_path=package_path)
                return

            agent_filename = DEFAULT_AGENT_EXE_NAME_WIN if self._is_frozen else DEFAULT_AGENT_PY_NAME
            new_agent_exe_path = self._find_executable(extracted_update_dir, agent_filename, subdirs_to_check=[AGENT_SUBDIR_IN_PACKAGE])
            if not new_agent_exe_path:
                self._handle_update_error("UpdateExtractionFailed",
                                          f"Could not find new agent executable ({agent_filename}) in '{extracted_update_dir}'",
                                          cleanup_paths=[extracted_update_dir, package_path])
                return

            current_agent_exe_path = self._get_current_agent_executable()
            if not current_agent_exe_path:
                 self._handle_update_error("UpdatePreparationFailed",
                                           "Could not determine current agent executable path.",
                                           cleanup_paths=[extracted_update_dir, package_path])
                 return

            updater_filename = UPDATE_EXECUTABLE_NAME if self._is_frozen else UPDATER_SCRIPT_NAME
            new_updater_path = self._find_executable(extracted_update_dir, updater_filename, subdirs_to_check=[UPDATER_SUBDIR_IN_PACKAGE])
            current_updater_path = self._get_current_updater_executable()

            updater_path_to_launch: Optional[Path] = None

            if new_updater_path and current_updater_path:
                logger.info(f"Found new updater in package ({new_updater_path}) and current updater ({current_updater_path}). Attempting replacement.")
                self.set_state(AgentState.UPDATING_REPLACING_UPDATER)
                if move_path(new_updater_path, current_updater_path):
                    logger.info(f"Successfully replaced current updater with the new one.")
                    updater_path_to_launch = current_updater_path
                else:
                    logger.warning(f"Failed to replace updater at '{current_updater_path}' with '{new_updater_path}'. Will attempt to launch the original current updater.")
                    updater_path_to_launch = current_updater_path
            elif current_updater_path:
                logger.info(f"Using current updater: {current_updater_path}. No new updater found in package or replacement not attempted.")
                updater_path_to_launch = current_updater_path
            elif new_updater_path:
                logger.warning(f"Current updater not found. Will attempt to launch the new updater directly from extraction path: {new_updater_path}")
                updater_path_to_launch = new_updater_path
            else:
                self._handle_update_error("UpdatePreparationFailed",
                                          f"Could not find any updater ('{updater_filename}') - neither current nor in the package.",
                                          cleanup_paths=[extracted_update_dir, package_path])
                return

            storage_path = Path(self.state_manager.storage_path).resolve()
            if not self._launch_updater(updater_path_to_launch, new_agent_exe_path, current_agent_exe_path, storage_path):
                 self._cleanup_on_error(extracted_update_dir=extracted_update_dir, package_path=package_path)
                 return

            logger.info("Update process successfully initiated, launching updater and preparing agent shutdown.")

        except Exception as e:
             logger.critical(f"Unexpected critical error during initiate_update: {e}", exc_info=True)
             self._handle_update_error("UpdateCriticalError", f"Unexpected error: {e}",
                                       cleanup_paths=[extracted_update_dir, package_path])
        finally:
            self._update_lock.release()
            logger.debug("Update lock released.")

    def check_for_updates_proactively(self, current_agent_state: 'AgentState'):
        """
        Proactively checks the server for a new agent version and initiates the update if available.

        This method checks the agent's state before proceeding. It no longer acquires the main update lock itself.
        The initiated update process will handle locking.

        :param current_agent_state: The current state object of the agent.
        :type current_agent_state: AgentState
        """
        if current_agent_state != AgentState.IDLE:
            logger.info(f"Skipping proactive update check: Agent is not in IDLE state (current state: {current_agent_state.name})")
            return

        try:
            current_version_local = current_version
            logger.info(f"Proactively checking for updates (current version: {current_version_local})...")
            success, update_info = self.http_client.check_for_update(current_version_local)

            if success:
                if update_info:
                    new_version = update_info.get('version')
                    logger.info(f"Proactive check found update info for version: {new_version}")
                    if new_version and new_version != current_version_local:
                        logger.info(f"Newer version {new_version} found. Initiating update.")
                        update_thread = threading.Thread(
                            target=self.initiate_update,
                            args=(update_info,),
                            name="ProactiveUpdateThread"
                        )
                        update_thread.start()
                    else:
                        logger.info(f"Proactive check: Server reported version {new_version}, which is not newer than current {current_version_local}. No update needed.")
                else:
                    logger.info("Proactive check: No new update available from server.")
            else:
                logger.warning("Proactive check: Failed to get update information from the server.")

        except Exception as e:
            logger.error(f"Unexpected error during proactive update check: {e}", exc_info=True)

    def handle_new_version_event(self, payload: Dict[str, Any], current_agent_state: 'AgentState'):
        """
        Processes new version notifications, typically received via WebSocket.

        Checks if the agent is in IDLE state and if the notified version differs
        from the current one. If so, triggers a proactive check which will then
        initiate the update if applicable.

        :param payload: The event payload dictionary, expected to contain 'new_stable_version'.
        :param current_agent_state: The current state object of the agent.
        """
        if current_agent_state != AgentState.IDLE:
            logger.info(f"Ignoring new version notification: Agent is not in IDLE state (current state: {current_agent_state.name})")
            return

        new_version = payload.get('new_stable_version')
        if not new_version:
            logger.warning("Ignoring new version notification: Missing 'new_stable_version' in payload")
            return

        if new_version == current_version:
            logger.info(f"Ignoring new version notification: Already running version {current_version}")
            return

        logger.info(f"New version notification received: {new_version} (current: {current_version}). Triggering update check.")
        self.check_for_updates_proactively(current_agent_state)

    def _handle_update_error(self,
                             error_type: str,
                             error_message: str,
                             details: Optional[Dict[str, Any]] = None,
                             cleanup_paths: Optional[list[Optional[Path]]] = None):
        """
        Centralized error handling for the update process.

        Logs the error, reports it, attempts cleanup, and resets agent state.

        :param error_type: A string code representing the type of error.
        :param error_message: A human-readable description of the error.
        :param details: Optional dictionary with additional context.
        :param cleanup_paths: List of optional Paths to clean up (files or directories).
        """
        logger.error(f"Update Error [{error_type}]: {error_message}")
        if details:
            logger.error(f"Error Details: {details}")

        if hasattr(self, 'server_connector') and self.server_connector:
             try:
                 self.server_connector.report_error_to_backend(error_type, error_message, details)
             except Exception as report_err:
                 logger.error(f"Failed to report error '{error_type}' to backend: {report_err}")
        else:
            logger.warning("Server connector not available, cannot report error to backend.")

        self._cleanup_on_error(*(cleanup_paths or []))

        self.set_state(AgentState.IDLE)

    def _cleanup_on_error(self, *paths_to_clean: Optional[Path]):
        """Helper to attempt cleanup of specified paths."""
        if not paths_to_clean:
            return
        for path in paths_to_clean:
            if path and path.exists():
                logger.info(f"Attempting cleanup of: {path}")
                try:
                    if path.is_file():
                        path.unlink()
                    elif path.is_dir():
                        shutil.rmtree(path, ignore_errors=True)
                    logger.debug(f"Cleanup successful for: {path}")
                except OSError as e:
                    logger.warning(f"Failed to clean up path {path}: {e}")
                except Exception as e:
                    logger.warning(f"Unexpected error during cleanup of {path}: {e}")

    def _check_prerequisites(self, update_info: Dict[str, Any]) -> Optional[Path]:
        """Verifies prerequisites: update info, storage path, disk space."""
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
            self._handle_update_error("UpdateResourceCheckFailed", error_message if error_message else "Insufficient disk space")
            return None

        logger.debug("Prerequisites check passed.")
        return update_dir

    def _download_package(self, download_url: str, package_path: Path) -> bool:
        """Downloads the update package file."""
        if not self.set_state(AgentState.UPDATING_DOWNLOADING): return False
        logger.info(f"Downloading update package from {download_url} to {package_path}")

        if package_path.exists():
            logger.warning(f"Package file already exists: {package_path}. Removing before download.")
            try:
                package_path.unlink()
            except OSError as e:
                self._handle_update_error("UpdateDownloadFailed", f"Failed to remove existing package file '{package_path}': {e}")
                return False
            except Exception as e:
                 self._handle_update_error("UpdateDownloadFailed", f"Unexpected error removing existing package file '{package_path}': {e}")
                 return False

        download_success, error_msg = self.http_client.download_file(download_url, str(package_path))
        if not download_success:
            self._handle_update_error("UpdateDownloadFailed", f"Download failed: {error_msg}",
                                      cleanup_paths=[package_path])
            return False

        logger.info("Download complete.")
        return True

    def _verify_package(self, package_path: Path, expected_checksum: str) -> bool:
        """Verifies the package integrity using SHA256 checksum."""
        if not self.set_state(AgentState.UPDATING_VERIFYING): return False
        logger.info(f"Verifying checksum for {package_path}")

        checksum_ok, checksum_result = calculate_sha256(str(package_path))

        if not checksum_ok or not isinstance(checksum_result, str):
            self._handle_update_error("UpdateChecksumMismatch", f"Failed to calculate checksum: {checksum_result}",
                                      cleanup_paths=[package_path])
            return False

        if checksum_result.lower() != expected_checksum.lower():
            details = {
                "expected_checksum": expected_checksum,
                "actual_checksum": checksum_result
            }
            self._handle_update_error("UpdateChecksumMismatch", "Checksum mismatch", details,
                                      cleanup_paths=[package_path])
            return False

        logger.info("Checksum verified successfully.")
        return True

    def _extract_package(self, package_path: Path, extract_dir: Path) -> bool:
        """Extracts the package contents (assumed zip)."""
        if not self.set_state(AgentState.UPDATING_EXTRACTING_UPDATER): return False
        logger.info(f"Extracting package {package_path} to {extract_dir}")

        try:
            if extract_dir.exists():
                logger.warning(f"Extraction directory '{extract_dir}' already exists. Removing before extraction.")
                shutil.rmtree(extract_dir, ignore_errors=False)

            extract_success, extract_error = extract_package(str(package_path), str(extract_dir))

            if not extract_success:
                self._handle_update_error("UpdateExtractionFailed", f"Extraction failed: {extract_error}",
                                          cleanup_paths=[extract_dir])
                return False

        except Exception as e:
            self._handle_update_error("UpdateExtractionFailed", f"Extraction failed with exception: {e}",
                                      cleanup_paths=[extract_dir])
            return False

        logger.info("Package extracted successfully.")
        return True

    def _launch_updater(self, updater_script_path: Path, new_agent_exe_path: Path, current_agent_exe_path: Path, storage_dir: Path) -> bool:
        """Constructs the command and launches the external updater process."""
        if not self.set_state(AgentState.UPDATING_PREPARING_SHUTDOWN): return False
        current_pid = os.getpid()

        if updater_script_path.suffix.lower() == '.py':
            updater_cmd = [
                sys.executable,
                str(updater_script_path.resolve()),
            ]
        elif updater_script_path.suffix.lower() == '.exe':
             updater_cmd = [str(updater_script_path.resolve())]
        else:
             self._handle_update_error("UpdateLaunchFailed", f"Updater path is neither .py nor .exe: {updater_script_path}")
             return False

        updater_cmd.extend([
            "--pid", str(current_pid),
            "--new_agent", str(new_agent_exe_path.resolve()),
            "--current_agent", str(current_agent_exe_path.resolve()),
            "--storage_dir", str(storage_dir.resolve())
        ])

        try:
            logger.info(f"Launching updater with command: {' '.join(updater_cmd)}")

            creation_flags = subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP

            subprocess.Popen(
                updater_cmd,
                creationflags=creation_flags,
                close_fds=True,
            )
            logger.info("Updater process launched successfully. Initiating graceful agent shutdown.")

            shutdown_thread = threading.Thread(target=self.initiate_shutdown, name="UpdateShutdownThread")
            shutdown_thread.start()
            return True

        except FileNotFoundError:
             self._handle_update_error("UpdateLaunchFailed", f"Failed to launch updater: Executable or script not found at '{updater_cmd[0]}'. Command: {' '.join(updater_cmd)}")
             return False
        except Exception as e:
            self._handle_update_error("UpdateLaunchFailed", f"Failed to launch updater with command {' '.join(updater_cmd)}: {e}")
            return False

    def _find_executable(self, search_dir: Path, executable_name: str, subdirs_to_check: Optional[list[str]] = None) -> Optional[Path]:
        """
        Searches for a file within a directory, prioritizing specified subdirectories.

        :param search_dir: The root directory Path object to start the search from.
        :param executable_name: The exact filename to search for (e.g., "agent.exe").
        :param subdirs_to_check: Optional list of subdirectory names to check first (e.g., ["agent", "updater"]).
        :return: The resolved absolute Path object to the first found executable, or None.
        """
        logger.debug(f"Searching for '{executable_name}' within '{search_dir}'")
        if not search_dir.is_dir():
            logger.warning(f"Search directory '{search_dir}' does not exist or is not a directory.")
            return None

        search_dir = search_dir.resolve()

        if subdirs_to_check:
            for subdir_name in subdirs_to_check:
                possible_path = search_dir / subdir_name / executable_name
                if possible_path.is_file():
                    logger.info(f"Found '{executable_name}' at primary location: {possible_path}")
                    return possible_path

        direct_path = search_dir / executable_name
        if direct_path.is_file():
             logger.info(f"Found '{executable_name}' directly in search dir: {direct_path}")
             return direct_path

        logger.debug(f"'{executable_name}' not found in primary locations, performing recursive search in '{search_dir}'...")
        try:
            for item in search_dir.rglob(executable_name):
                 if item.is_file() and item.name == executable_name:
                     logger.info(f"Found '{executable_name}' via rglob at: {item}")
                     return item.resolve()
        except Exception as e:
            logger.warning(f"Error during recursive search for '{executable_name}' in '{search_dir}': {e}")

        logger.warning(f"'{executable_name}' not found within '{search_dir}' or its common subdirectories.")
        return None

    def _get_project_root(self) -> Optional[Path]:
        """Tries to determine the project root directory."""
        try:
            handlers_dir = Path(__file__).resolve().parent
            agent_dir = handlers_dir.parent
            if (self._is_frozen):
                project_root = agent_dir
            else:
                project_root = agent_dir.parent
            if not project_root.is_dir():
                logger.warning(f"Project root '{project_root}' is not a directory.")
                return None
            if not (project_root / "agent").is_dir():
                logger.warning(f"Project root '{project_root}' does not contain 'agent' directory.")
                return None
            if not (project_root / "updater").is_dir():
                logger.warning(f"Project root '{project_root}' does not contain 'updater' directory.")
                return None
            logger.debug(f"Project root determined: {project_root}")
            return project_root
        except Exception as e:
            logger.error(f"Error determining project root: {e}")
            return None

    def _get_current_agent_executable(self) -> Optional[Path]:
        """Determines the absolute path to the currently running agent executable/script."""
        try:
            if self._is_frozen:
                current_path = Path(sys.executable).resolve()
                logger.debug(f"Determined current agent (frozen): {current_path}")
            else:
                project_root = self._get_project_root()
                if not project_root:
                     logger.error("Could not determine project root to find current agent script.")
                     return None
                current_path = (project_root / "agent" / DEFAULT_AGENT_PY_NAME).resolve()
                if not current_path.is_file():
                     current_path = (project_root / DEFAULT_AGENT_PY_NAME).resolve()
                logger.debug(f"Determined current agent (script): {current_path}")

            if not current_path.exists():
                 logger.error(f"Determined current agent path does not exist: {current_path}")
                 return None
            if not current_path.is_file():
                 logger.error(f"Determined current agent path is not a file: {current_path}")
                 return None

            return current_path

        except Exception as e:
            logger.error(f"Unexpected error determining current agent executable path: {e}", exc_info=True)
            return None

    def _get_current_updater_executable(self) -> Optional[Path]:
        """Determines the absolute path to the currently installed updater executable/script."""
        updater_filename = UPDATE_EXECUTABLE_NAME if self._is_frozen else UPDATER_SCRIPT_NAME
        try:
            if self._is_frozen:
                agent_exe_path = Path(sys.executable).resolve()
                updater_path = agent_exe_path.parent / updater_filename
                logger.debug(f"Looking for current updater (frozen) at: {updater_path}")
            else:
                project_root = self._get_project_root()
                if not project_root:
                     logger.error("Could not determine project root to find current updater script.")
                     return None
                updater_path = (project_root / UPDATER_SUBDIR_IN_PACKAGE / UPDATER_SCRIPT_NAME).resolve()
                logger.debug(f"Looking for current updater (script) at: {updater_path}")

            if updater_path.is_file():
                 logger.info(f"Found current updater at: {updater_path}")
                 return updater_path
            else:
                 logger.warning(f"Current updater ('{updater_filename}') not found at expected location: {updater_path}")
                 return None

        except Exception as e:
            logger.error(f"Unexpected error determining current updater path: {e}", exc_info=True)
            return None