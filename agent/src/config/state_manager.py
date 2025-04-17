# -*- coding: utf-8 -*-
"""
State Manager module for the Computer Management System Agent.
Handles loading, saving, and accessing agent state including device ID,
room configuration, and agent token (using keyring or file storage).
Accepts ConfigManager instance for configuration values.
"""
import os
import sys
import logging
import uuid
import socket
from typing import Dict, Any, Optional

# Configuration and Utilities
from src.config.config_manager import ConfigManager # Import class for type hinting
from src.utils.utils import load_json, save_json
from src.system.windows_utils import is_running_as_admin # Added for privilege check

# Imports for ACLs
try:
    import win32security
    import win32api
    import ntsecuritycon as win32con
    import pywintypes
    WINDOWS_ACL_SUPPORT = True
except ImportError:
    WINDOWS_ACL_SUPPORT = False
    logger.warning("win32security or related modules not found. ACL setting will be skipped.")

# Optional keyring support
try:
    import keyring # type: ignore
    KEYRING_AVAILABLE = True
except ImportError:
    KEYRING_AVAILABLE = False
    keyring = None # Define keyring as None if not available

logger = logging.getLogger(__name__)

# Constants for keyring service and token filename (fallback)
TOKEN_SERVICE_NAME = "ComputerManagementSystemAgent"
TOKEN_FILENAME = "agent_token.json" # Fallback token storage

class StateManager:
    """Manages persistent agent state (device ID, room config, token)."""

    def __init__(self, config: ConfigManager):
        """
        Initialize the StateManager. Determines storage path based on privileges.

        Args:
            config (ConfigManager): The configuration manager instance.

        Raises:
            ValueError: If storage path cannot be determined or created.
        """
        self.config = config
        self.state_filename = self.config.get('agent.state_filename', 'agent_state.json')

        # --- Determine Storage Path based on Privileges (Phase 1) ---
        self.storage_path = self._determine_storage_path()
        logger.info(f"Determined storage path: {self.storage_path}")

        # Ensure storage path exists and is writable
        self._ensure_storage_directory()

        self.state_filepath = os.path.join(self.storage_path, self.state_filename)
        self._state_cache: Optional[Dict[str, Any]] = None # In-memory cache for state file
        logger.info(f"StateManager initialized. State file: {self.state_filepath}")

    def _determine_storage_path(self) -> str:
        """
        Determines the appropriate storage path based on execution privileges.
        Uses ProgramData for Admin, LocalAppData for standard user.

        Returns:
            str: The absolute path to the storage directory.

        Raises:
            ValueError: If a suitable path cannot be determined.
        """
        app_name = self.config.get('agent.app_name', 'CMSAgent') # Get app name from config or default
        is_admin = is_running_as_admin()

        if is_admin:
            base_path = os.getenv('PROGRAMDATA')
            if not base_path:
                 logger.error("Could not get PROGRAMDATA environment variable.")
                 raise ValueError("Cannot determine ProgramData path for admin storage.")
            storage_dir = os.path.join(base_path, app_name)
            logger.debug(f"Running as Admin. Using ProgramData path: {storage_dir}")
        else:
            base_path = os.getenv('LOCALAPPDATA')
            if not base_path:
                 logger.error("Could not get LOCALAPPDATA environment variable.")
                 raise ValueError("Cannot determine LocalAppData path for user storage.")
            storage_dir = os.path.join(base_path, app_name)
            logger.debug(f"Running as User. Using LocalAppData path: {storage_dir}")

        return storage_dir

    def _ensure_storage_directory(self):
        """
        Ensures the determined storage directory exists, is accessible,
        and sets appropriate permissions if running as Admin.
        Raises ValueError on critical errors.
        """
        is_admin = is_running_as_admin() # Check privileges once
        try:
            if not os.path.exists(self.storage_path):
                 logger.info(f"Storage path '{self.storage_path}' does not exist. Creating.")
                 # Create directory with default permissions first
                 os.makedirs(self.storage_path, exist_ok=True)
                 logger.info(f"Successfully created storage directory: {self.storage_path}")
                 # Set permissions AFTER creation if admin
                 self._ensure_directory_permissions(is_admin)
            elif not os.path.isdir(self.storage_path):
                 logger.critical(f"Configured storage path '{self.storage_path}' exists but is not a directory.")
                 raise ValueError(f"Storage path '{self.storage_path}' is not a directory.")
            else:
                 logger.debug(f"Storage directory '{self.storage_path}' already exists. Checking writability and permissions.")
                 # Check writability first
                 test_file = os.path.join(self.storage_path, f".writetest_{uuid.uuid4()}")
                 try:
                      with open(test_file, 'w') as f:
                           f.write('test')
                      os.remove(test_file)
                      logger.debug(f"Storage directory '{self.storage_path}' appears writable.")
                 except (IOError, OSError) as write_err:
                      logger.critical(f"Storage directory '{self.storage_path}' is not writable: {write_err}")
                      raise ValueError(f"Storage path '{self.storage_path}' is not writable.")
                 # Ensure permissions are correct if admin, even if directory existed
                 self._ensure_directory_permissions(is_admin)

        except PermissionError:
             logger.critical(f"Permission denied creating/accessing storage directory: {self.storage_path}")
             raise ValueError(f"Permission denied for storage path: {self.storage_path}")
        except OSError as e:
             logger.critical(f"OS error creating/accessing storage directory {self.storage_path}: {e}")
             raise ValueError(f"Could not create/access storage directory: {e}")
        except Exception as e:
             logger.critical(f"Unexpected error ensuring storage directory {self.storage_path}: {e}", exc_info=True)
             raise ValueError(f"Unexpected error ensuring storage directory: {e}")

    def _ensure_directory_permissions(self, is_admin: bool):
        """
        Sets strict permissions (SYSTEM:F, Administrators:F) on the storage directory
        if running as Admin and win32security is available. Disables inheritance.
        """
        if not is_admin:
            logger.debug("Not running as admin, skipping ACL modification.")
            return
        if not WINDOWS_ACL_SUPPORT:
            logger.warning("win32security not available, cannot set directory ACLs.")
            return

        logger.info(f"Setting ACLs for admin storage directory: {self.storage_path}")
        try:
            # Get SIDs for well-known accounts
            sid_system = win32security.LookupAccountName("", "SYSTEM")[0]
            sid_admins = win32security.LookupAccountName("", "Administrators")[0]

            # Create a new DACL (Discretionary Access Control List)
            dacl = win32security.ACL()
            dacl.AddAccessAllowedAceEx(win32security.ACL_REVISION_DS,
                                       win32con.OBJECT_INHERIT_ACE | win32con.CONTAINER_INHERIT_ACE,
                                       win32con.GENERIC_ALL, # Full Control
                                       sid_system)
            dacl.AddAccessAllowedAceEx(win32security.ACL_REVISION_DS,
                                       win32con.OBJECT_INHERIT_ACE | win32con.CONTAINER_INHERIT_ACE,
                                       win32con.GENERIC_ALL, # Full Control
                                       sid_admins)

            # Create a new Security Descriptor (SD)
            sd = win32security.SECURITY_DESCRIPTOR()
            sd.SetSecurityDescriptorOwner(sid_admins, False) # Set Administrators as owner
            sd.SetSecurityDescriptorGroup(sid_system, False) # Set SYSTEM as group
            # Apply the DACL, protecting it from inheritance (True means protected)
            sd.SetSecurityDescriptorDacl(True, dacl, False) # True=DACL present, False=Defaulted

            # Apply the security descriptor to the directory
            # SE_FILE_OBJECT is used for files and directories
            # DACL_SECURITY_INFORMATION indicates that the DACL is being set
            # PROTECTED_DACL_SECURITY_INFORMATION ensures inheritance is blocked
            security_info = win32security.DACL_SECURITY_INFORMATION | win32security.PROTECTED_DACL_SECURITY_INFORMATION | win32security.OWNER_SECURITY_INFORMATION | win32security.GROUP_SECURITY_INFORMATION
            win32security.SetFileSecurity(self.storage_path, security_info, sd)

            logger.info(f"Successfully applied strict ACLs to {self.storage_path}")

        except pywintypes.error as e:
            logger.error(f"Failed to set ACLs on {self.storage_path}: {e}", exc_info=True)
        except Exception as e:
            logger.error(f"Unexpected error setting ACLs on {self.storage_path}: {e}", exc_info=True)

    # --- Internal State File Handling ---

    def _load_state_from_file(self) -> Dict[str, Any]:
        """Loads the agent state dictionary from the JSON file."""
        logger.debug(f"Loading agent state from: {self.state_filepath}")
        state = load_json(self.state_filepath)
        return state if isinstance(state, dict) else {}

    def _save_state_to_file(self, state: Dict[str, Any]) -> bool:
        """Saves the agent state dictionary to the JSON file."""
        logger.debug(f"Saving agent state to: {self.state_filepath}")
        return save_json(state, self.state_filepath)

    def _get_current_state(self) -> Dict[str, Any]:
        """Gets the current state, loading from file if not cached."""
        if self._state_cache is None:
            self._state_cache = self._load_state_from_file()
        return self._state_cache

    def _update_state(self, key: str, value: Any) -> bool:
        """Updates a specific key in the state and saves the entire state."""
        current_state = self._get_current_state().copy() # Work on a copy
        current_state[key] = value
        if self._save_state_to_file(current_state):
            self._state_cache = current_state # Update cache on successful save
            logger.debug(f"Updated state key '{key}' and saved successfully.")
            return True
        else:
            logger.error(f"Failed to save state after updating key '{key}'.")
            self._state_cache = None
            return False

    # --- Device ID Management ---

    def _generate_device_id(self) -> str:
        """Generates a new unique device ID."""
        try:
            hostname = socket.gethostname()
            mac_int = uuid.getnode()
            if mac_int == uuid.UUID('00000000-0000-0000-0000-000000000000').int or \
               (mac_int >> 40) == 0 or mac_int == 0:
                logger.warning("Could not get valid MAC address, using hostname and random UUID part for device ID.")
                fallback_id = f"ANM-{hostname}-{str(uuid.uuid4())}"
                logger.info(f"Generated fallback device ID: {fallback_id}")
                return fallback_id
            else:
                mac_str = ':'.join(("%012X" % mac_int)[i:i+2] for i in range(0, 12, 2))
                device_id = f"ANM-{hostname}-{mac_str}"
                logger.info(f"Generated device ID based on hostname and MAC: {device_id}")
                return device_id
        except Exception as e:
            logger.error(f"Error generating device ID components: {e}. Using fallback UUID.", exc_info=True)
            fallback_id = f"ANM-Fallback-{str(uuid.uuid4())}"
            logger.info(f"Generated fallback device ID due to error: {fallback_id}")
            return fallback_id

    def ensure_device_id(self) -> str:
        """
        Ensures a device ID exists in the state file, generating and saving one if necessary.

        Returns:
            str: The existing or newly generated device ID.

        Raises:
            RuntimeError: If the device ID cannot be obtained or saved.
        """
        state = self._get_current_state()
        device_id = state.get("device_id")

        if not device_id or not isinstance(device_id, str):
            if device_id:
                 logger.warning(f"Invalid device_id found in state: {device_id}. Regenerating.")
            else:
                 logger.info("Device ID not found in state. Generating new ID.")

            device_id = self._generate_device_id()
            if not self._update_state("device_id", device_id):
                 logger.critical("Failed to save newly generated device ID to state file!")
                 print("CẢNH BÁO: Không thể lưu Device ID mới vào file trạng thái. Agent có thể hoạt động không chính xác sau khi khởi động lại.", file=sys.stderr)
            else:
                 logger.info(f"Generated and saved new Device ID: {device_id}")
        else:
             logger.debug(f"Using existing Device ID: {device_id}")

        return device_id

    def get_device_id(self) -> Optional[str]:
        """Gets the device ID from the state cache."""
        state = self._get_current_state()
        return state.get("device_id")

    # --- Room Config Management ---

    def get_room_config(self) -> Optional[Dict[str, Any]]:
        """Gets the room configuration dictionary from the state cache."""
        state = self._get_current_state()
        room_config = state.get("room_config")
        if room_config and isinstance(room_config, dict):
             return room_config
        elif room_config:
             logger.warning(f"Room config found in state but is not a dictionary: {room_config}. Ignoring.")
             return None
        else:
             return None

    def save_room_config(self, room_config: Dict[str, Any]) -> bool:
        """Saves the provided room configuration to the state file."""
        logger.info(f"Attempting to save room configuration: {room_config}")
        if not isinstance(room_config, dict):
             logger.error(f"Invalid room_config type provided for saving: {type(room_config)}")
             return False
        return self._update_state("room_config", room_config)

    # --- Token Management ---

    def _get_token_fallback_path(self) -> Optional[str]:
        """Gets the full path for the fallback token file."""
        if not self.storage_path:
            return None
        return os.path.join(self.storage_path, TOKEN_FILENAME)

    def save_token(self, agent_id: str, token: str) -> bool:
        """
        Saves the agent token securely (keyring preferred, file fallback).

        Args:
            agent_id (str): The device ID (used as username in keyring).
            token (str): The token (password) to save.

        Returns:
            bool: True if saved successfully, False otherwise.
        """
        if not agent_id or not token:
            logger.error("Cannot save token: Agent ID and token cannot be empty.")
            return False

        if KEYRING_AVAILABLE and keyring:
            try:
                keyring.set_password(TOKEN_SERVICE_NAME, agent_id, token)
                logger.info(f"Token securely saved to keyring for agent_id: {agent_id}")
                self._remove_token_from_file(agent_id)
                return True
            except Exception as e:
                logger.error(f"Failed to save token to keyring for agent_id {agent_id}: {e}. Falling back to file.", exc_info=False)

        token_file_path = self._get_token_fallback_path()
        if not token_file_path:
             logger.error("Cannot save token to file: Storage path not available.")
             return False

        try:
            token_data = load_json(token_file_path)
            if token_data is None:
                 logger.error(f"Failed to load existing token file {token_file_path} before saving. Aborting file save.")
                 return False
            if not isinstance(token_data, dict):
                 logger.warning(f"Token file {token_file_path} is not a dictionary. Overwriting with new data.")
                 token_data = {}

            token_data[agent_id] = token
            if save_json(token_data, token_file_path):
                logger.info(f"Token saved to file for agent_id: {agent_id} (Keyring not used or failed)")
                try:
                    import subprocess
                    subprocess.run(["attrib", "+H", token_file_path], 
                                  creationflags=subprocess.CREATE_NO_WINDOW,
                                  check=False)
                    logger.debug(f"Set token file {token_file_path} to hidden attribute")
                except Exception as e:
                    logger.warning(f"Could not set hidden attribute on token file {token_file_path}: {e}")
                return True
            else:
                logger.error(f"Failed to save token to file {token_file_path} for agent_id: {agent_id}")
                return False
        except Exception as e:
            logger.error(f"Unexpected error saving token to file for agent_id {agent_id}: {e}", exc_info=True)
            return False

    def load_token(self, agent_id: str) -> Optional[str]:
        """
        Loads the agent token (keyring preferred, file fallback).

        Args:
            agent_id (str): The device ID to retrieve the token for.

        Returns:
            Optional[str]: The token if found, None otherwise.
        """
        if not agent_id:
            logger.error("Cannot load token: Agent ID cannot be empty.")
            return None

        if KEYRING_AVAILABLE and keyring:
            try:
                token = keyring.get_password(TOKEN_SERVICE_NAME, agent_id)
                if token:
                    logger.info(f"Token loaded securely from keyring for agent_id: {agent_id}")
                    return token
                else:
                    logger.debug(f"Token not found in keyring for agent_id: {agent_id}. Checking file fallback.")
            except Exception as e:
                logger.error(f"Failed to load token from keyring for agent_id {agent_id}: {e}. Checking file fallback.", exc_info=False)

        token_file_path = self._get_token_fallback_path()
        if not token_file_path:
             logger.warning(f"Cannot check token file fallback for agent_id {agent_id}: Storage path not available.")
             return None

        try:
            token_data = load_json(token_file_path)
            if isinstance(token_data, dict) and agent_id in token_data:
                token = token_data[agent_id]
                if isinstance(token, str) and token:
                    logger.info(f"Token loaded from file for agent_id: {agent_id} (Not found or error in keyring)")
                    if KEYRING_AVAILABLE and keyring:
                        logger.info(f"Migrating token for {agent_id} from file to keyring...")
                        self.save_token(agent_id, token)
                    return token
                else:
                     logger.warning(f"Invalid token data found in file for agent_id {agent_id}.")
                     return None
            else:
                logger.debug(f"Token not found in file {token_file_path} for agent_id: {agent_id}")
                return None
        except Exception as e:
            logger.error(f"Unexpected error loading token from file for agent_id {agent_id}: {e}", exc_info=True)
            return None

    def _remove_token_from_file(self, agent_id: str):
        """Internal helper to remove token from the JSON file (e.g., after migration)."""
        token_file_path = self._get_token_fallback_path()
        if not token_file_path or not os.path.exists(token_file_path):
            return

        try:
            token_data = load_json(token_file_path)
            if isinstance(token_data, dict) and agent_id in token_data:
                del token_data[agent_id]
                if save_json(token_data, token_file_path):
                    logger.debug(f"Removed token for {agent_id} from file {token_file_path}.")
                else:
                    logger.warning(f"Failed to save token file after removing {agent_id}.")
        except Exception as e:
            logger.warning(f"Error trying to remove token for {agent_id} from file: {e}")
