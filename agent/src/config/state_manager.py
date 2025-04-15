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
# from src.config.config_manager import config_manager # No longer using global instance
from src.config.config_manager import ConfigManager # Import class for type hinting
from src.utils.utils import load_json, save_json

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
        Initialize the StateManager.

        Args:
            config (ConfigManager): The configuration manager instance.

        Raises:
            ValueError: If storage path is not configured or invalid.
        """
        self.config = config
        self.storage_path = self.config.get('storage_path')
        self.state_filename = self.config.get('agent.state_filename', 'agent_state.json')

        if not self.storage_path:
            # This should have been caught by ConfigManager validation, but double-check
            raise ValueError("Storage path is not configured. StateManager cannot operate.")

        # Ensure storage path exists
        try:
            if not os.path.exists(self.storage_path):
                 logger.info(f"Storage path '{self.storage_path}' does not exist. Creating.")
                 os.makedirs(self.storage_path, exist_ok=True)
            elif not os.path.isdir(self.storage_path):
                 raise ValueError(f"Configured storage path '{self.storage_path}' exists but is not a directory.")
        except PermissionError:
             logger.critical(f"Permission denied creating storage directory: {self.storage_path}")
             raise ValueError(f"Permission denied for storage path: {self.storage_path}")
        except OSError as e:
             logger.critical(f"Error creating storage directory {self.storage_path}: {e}")
             raise ValueError(f"Could not create storage directory: {e}")


        self.state_filepath = os.path.join(self.storage_path, self.state_filename)
        self._state_cache: Optional[Dict[str, Any]] = None # In-memory cache for state file
        logger.info(f"StateManager initialized. State file: {self.state_filepath}")

    # --- Internal State File Handling ---

    def _load_state_from_file(self) -> Dict[str, Any]:
        """Loads the agent state dictionary from the JSON file."""
        logger.debug(f"Loading agent state from: {self.state_filepath}")
        state = load_json(self.state_filepath)
        # load_json now returns {} for empty file, None for errors/not found
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
            # Invalidate cache as save failed
            self._state_cache = None
            return False

    # --- Device ID Management ---

    def _generate_device_id(self) -> str:
        """Generates a new unique device ID."""
        try:
            # Use platform.node() for potentially more reliable hostname
            # import platform
            # hostname = platform.node()
            hostname = socket.gethostname() # Keep original method for now
            mac_int = uuid.getnode()
            # Check for common invalid MAC address patterns
            if mac_int == uuid.UUID('00000000-0000-0000-0000-000000000000').int or \
               (mac_int >> 40) == 0 or mac_int == 0: # Check for 00:00:00:00:00:00 or similar issues
                logger.warning("Could not get valid MAC address, using hostname and random UUID part for device ID.")
                # Generate a more random fallback ID
                fallback_id = f"ANM-{hostname}-{str(uuid.uuid4())}"
                logger.info(f"Generated fallback device ID: {fallback_id}")
                return fallback_id
            else:
                # Format MAC address properly
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

        if not device_id or not isinstance(device_id, str): # Also check type
            if device_id:
                 logger.warning(f"Invalid device_id found in state: {device_id}. Regenerating.")
            else:
                 logger.info("Device ID not found in state. Generating new ID.")

            device_id = self._generate_device_id()
            if not self._update_state("device_id", device_id):
                 # If saving fails, we can't guarantee persistence
                 logger.critical("Failed to save newly generated device ID to state file!")
                 # Return the generated ID for this session, but log the error
                 # Raising an error might be too strict if the agent can function temporarily
                 # raise RuntimeError("Failed to save generated Device ID.")
                 print("CẢNH BÁO: Không thể lưu Device ID mới vào file trạng thái. Agent có thể hoạt động không chính xác sau khi khởi động lại.", file=sys.stderr)
            else:
                 logger.info(f"Generated and saved new Device ID: {device_id}")
        else:
             logger.debug(f"Using existing Device ID: {device_id}")

        return device_id

    def get_device_id(self) -> Optional[str]:
        """Gets the device ID from the state cache."""
        # ensure_device_id should be called first by the Agent to populate the state
        state = self._get_current_state()
        return state.get("device_id")

    # --- Room Config Management ---

    def get_room_config(self) -> Optional[Dict[str, Any]]:
        """Gets the room configuration dictionary from the state cache."""
        state = self._get_current_state()
        room_config = state.get("room_config")
        # Basic validation on retrieval
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
        # Validation should happen *before* calling this, but add a basic check
        if not isinstance(room_config, dict):
             logger.error(f"Invalid room_config type provided for saving: {type(room_config)}")
             return False
        # Assume caller validated the structure (room, position.x, position.y)
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

        # --- Try Keyring First ---
        if KEYRING_AVAILABLE and keyring:
            try:
                keyring.set_password(TOKEN_SERVICE_NAME, agent_id, token)
                logger.info(f"Token securely saved to keyring for agent_id: {agent_id}")
                # Optionally remove old file token if migrating
                self._remove_token_from_file(agent_id)
                return True
            except Exception as e:
                logger.error(f"Failed to save token to keyring for agent_id {agent_id}: {e}. Falling back to file.", exc_info=False)

        # --- Fallback to JSON File ---
        token_file_path = self._get_token_fallback_path()
        if not token_file_path:
             logger.error("Cannot save token to file: Storage path not available.")
             return False # Cannot save if keyring failed and path is bad

        try:
            # Load existing tokens carefully
            token_data = load_json(token_file_path)
            if token_data is None: # Handle load error
                 logger.error(f"Failed to load existing token file {token_file_path} before saving. Aborting file save.")
                 return False
            if not isinstance(token_data, dict): # Ensure it's a dict
                 logger.warning(f"Token file {token_file_path} is not a dictionary. Overwriting with new data.")
                 token_data = {}

            token_data[agent_id] = token
            if save_json(token_data, token_file_path):
                logger.info(f"Token saved to file for agent_id: {agent_id} (Keyring not used or failed)")
                # Windows doesn't use 0o600 permissions, use NTFS attributes instead
                try:
                    # On Windows, can use attrib command to mark as hidden if needed
                    # This is optional and only adds a minor security benefit
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

        # --- Try Keyring First ---
        if KEYRING_AVAILABLE and keyring:
            try:
                token = keyring.get_password(TOKEN_SERVICE_NAME, agent_id)
                if token:
                    logger.info(f"Token loaded securely from keyring for agent_id: {agent_id}")
                    return token
                else:
                    logger.debug(f"Token not found in keyring for agent_id: {agent_id}. Checking file fallback.")
            except Exception as e:
                logger.error(f"Failed to load token from keyring for agent_id {agent_id}: {e}. Checking file fallback.", exc_info=False) # Don't need full traceback

        # --- Fallback to JSON File ---
        token_file_path = self._get_token_fallback_path()
        if not token_file_path:
             logger.warning(f"Cannot check token file fallback for agent_id {agent_id}: Storage path not available.")
             return None # Cannot check file

        try:
            token_data = load_json(token_file_path)
            if isinstance(token_data, dict) and agent_id in token_data:
                token = token_data[agent_id]
                if isinstance(token, str) and token: # Basic validation
                    logger.info(f"Token loaded from file for agent_id: {agent_id} (Not found or error in keyring)")
                    # Optionally migrate to keyring if found in file and keyring is available
                    if KEYRING_AVAILABLE and keyring:
                        logger.info(f"Migrating token for {agent_id} from file to keyring...")
                        # Use the main save_token method which prefers keyring
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
            return # No file to remove from

        try:
            token_data = load_json(token_file_path)
            # Ensure token_data is a dict before proceeding
            if isinstance(token_data, dict) and agent_id in token_data:
                del token_data[agent_id]
                if save_json(token_data, token_file_path):
                    logger.debug(f"Removed token for {agent_id} from file {token_file_path}.")
                else:
                    logger.warning(f"Failed to save token file after removing {agent_id}.")
        except Exception as e:
            logger.warning(f"Error trying to remove token for {agent_id} from file: {e}")
