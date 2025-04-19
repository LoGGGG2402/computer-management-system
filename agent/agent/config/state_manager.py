"""
State Manager module for managing persistent agent state.
"""
import os
import uuid
import socket
from typing import Dict, Any, Optional, TYPE_CHECKING

from ..utils import get_logger, save_json, load_json
from ..system import determine_storage_path

if TYPE_CHECKING:
    from . import ConfigManager

logger = get_logger(__name__)



try:
    import keyring
    KEYRING_AVAILABLE = True
except ImportError:
    KEYRING_AVAILABLE = False
    keyring = None

TOKEN_SERVICE_NAME = "ComputerManagementSystemAgent"
TOKEN_FILENAME = "agent_token.json"

class StateManager:
    """
    Manages persistent agent state (device ID, room config, token).
    """

    def __init__(self, config: 'ConfigManager'):
        """
        Initialize the StateManager. Determines storage path based on privileges.

        :param config: The configuration manager instance
        :type config: ConfigManager
        :raises: ValueError: If storage path cannot be determined or created
        """
        self.config = config
        self.state_filename = self.config.get('agent.state_filename', 'agent_state.json')

        # Determine storage path based on app name and privileges
        app_name = self.config.get('agent.app_name', 'CMSAgent')
        self.storage_path = determine_storage_path(app_name)
        logger.info(f"Using storage path: {self.storage_path}")

        self.state_filepath = os.path.join(self.storage_path, self.state_filename)
        self._state_cache: Optional[Dict[str, Any]] = None
        logger.info(f"StateManager initialized. State file: {self.state_filepath}")

    def _load_state_from_file(self) -> Dict[str, Any]:
        """
        Loads the agent state dictionary from the JSON file.
        
        :return: State dictionary
        :rtype: Dict[str, Any]
        """
        logger.debug(f"Loading agent state from: {self.state_filepath}")
        state = load_json(self.state_filepath)
        return state if isinstance(state, dict) else {}

    def _save_state_to_file(self, state: Dict[str, Any]) -> bool:
        """
        Saves the agent state dictionary to the JSON file.
        
        :param state: State dictionary to save
        :type state: Dict[str, Any]
        :return: True if save succeeded, False otherwise
        :rtype: bool
        """
        logger.debug(f"Saving agent state to: {self.state_filepath}")
        return save_json(state, self.state_filepath)

    def _get_current_state(self) -> Dict[str, Any]:
        """
        Gets the current state, loading from file if not cached.
        
        :return: Current state dictionary
        :rtype: Dict[str, Any]
        """
        if self._state_cache is None:
            self._state_cache = self._load_state_from_file()
        return self._state_cache

    def _update_state(self, key: str, value: Any) -> bool:
        """
        Updates a specific key in the state and saves the entire state.
        
        :param key: The key to update
        :type key: str
        :param value: The value to set
        :type value: Any
        :return: True if update and save succeeded, False otherwise
        :rtype: bool
        """
        current_state = self._get_current_state().copy()
        current_state[key] = value
        if self._save_state_to_file(current_state):
            self._state_cache = current_state
            logger.debug(f"Updated state key '{key}' and saved successfully.")
            return True
        else:
            logger.error(f"Failed to save state after updating key '{key}'.")
            self._state_cache = None
            return False

    def _generate_device_id(self) -> str:
        """
        Generates a new unique device ID.
        
        :return: Generated device ID
        :rtype: str
        """
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

        :return: The existing or newly generated device ID
        :rtype: str
        :raises: RuntimeError: If the device ID cannot be obtained or saved
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

            else:
                 logger.info(f"Generated and saved new Device ID: {device_id}")
        else:
             logger.debug(f"Using existing Device ID: {device_id}")

        return device_id

    def get_device_id(self) -> Optional[str]:
        """
        Gets the device ID from the state cache.
        
        :return: Device ID or None if not found
        :rtype: Optional[str]
        """
        state = self._get_current_state()
        return state.get("device_id")

    def get_room_config(self) -> Optional[Dict[str, Any]]:
        """
        Gets the room configuration dictionary from the state cache.
        
        :return: Room configuration or None if not found or invalid
        :rtype: Optional[Dict[str, Any]]
        """
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
        """
        Saves the provided room configuration to the state file.
        
        :param room_config: The room configuration to save
        :type room_config: Dict[str, Any]
        :return: True if save succeeded, False otherwise
        :rtype: bool
        """
        logger.info(f"Attempting to save room configuration: {room_config}")
        if not isinstance(room_config, dict):
             logger.error(f"Invalid room_config type provided for saving: {type(room_config)}")
             return False
        return self._update_state("room_config", room_config)

    def _get_token_fallback_path(self) -> Optional[str]:
        """
        Gets the full path for the fallback token file.
        
        :return: Path to token file or None if storage path not available
        :rtype: Optional[str]
        """
        if not self.storage_path:
            return None
        return os.path.join(self.storage_path, TOKEN_FILENAME)

    def save_token(self, agent_id: str, token: str) -> bool:
        """
        Saves the agent token securely (keyring preferred, file fallback).

        :param agent_id: The device ID (used as username in keyring)
        :type agent_id: str
        :param token: The token (password) to save
        :type token: str
        :return: True if saved successfully, False otherwise
        :rtype: bool
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

        :param agent_id: The device ID to retrieve the token for
        :type agent_id: str
        :return: The token if found, None otherwise
        :rtype: Optional[str]
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
        """
        Internal helper to remove token from the JSON file (e.g., after migration).
        
        :param agent_id: The agent ID whose token should be removed
        :type agent_id: str
        """
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
