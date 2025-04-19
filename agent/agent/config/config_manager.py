"""
Configuration Manager module for the Computer Management System Agent.
"""
import json
import os
import datetime
import shutil
from typing import Any, Optional, Dict
from ..utils import get_logger

logger = get_logger(__name__)

class ConfigManager:
    """
    Loads and manages agent configuration from a file.
    """
    CURRENT_CONFIG_VERSION = 1

    def __init__(self, config_path: Optional[str]):
        """
        Initializes the ConfigManager by loading the configuration file.
        
        :param config_path: The path to the agent configuration JSON file
        :type config_path: Optional[str]
        :raises: FileNotFoundError if the configuration file path is provided but does not exist
        :raises: ValueError if the configuration file is invalid JSON or essential keys are missing
        """
        self._config_path = config_path
        self._config_data: Optional[Dict[str, Any]] = None
        self._migration_performed = False

        if self._config_path is None:
             logger.debug("ConfigManager initialized without a config path (temporary instance).")
             self._config_data = {}
        else:
             self._load_config()
             self._check_and_migrate_config()
             self._validate_config()
             logger.info(f"Configuration loaded successfully from: {self._config_path}")
             if self._migration_performed:
                 logger.info("Configuration migration was performed.")
             logger.info(f"Using configuration version: {self.get('agent.config_version', 'N/A')}")

    def _load_config(self):
        """
        Loads the configuration data from the JSON file.
        
        :raises: FileNotFoundError if the file doesn't exist
        :raises: ValueError if there are JSON parsing errors
        """
        if not os.path.exists(self._config_path):
            logger.critical(f"Configuration file not found: {self._config_path}")
            raise FileNotFoundError(f"Configuration file not found: {self._config_path}")

        try:
            with open(self._config_path, 'r', encoding='utf-8') as f:
                self._config_data = json.load(f)
            if not isinstance(self._config_data, dict):
                raise ValueError("Configuration file content is not a valid JSON object.")
        except json.JSONDecodeError as e:
            logger.critical(f"Error decoding JSON from config file {self._config_path}: {e}")
            raise ValueError(f"Invalid JSON in configuration file: {e}") from e
        except (IOError, OSError) as e:
            logger.critical(f"Error reading config file {self._config_path}: {e}")
            raise ValueError(f"Could not read configuration file: {e}") from e
        except Exception as e:
            logger.critical(f"Unexpected error loading config from {self._config_path}: {e}", exc_info=True)
            raise ValueError(f"Unexpected error loading configuration: {e}") from e

    def _validate_config(self):
        """
        Performs basic validation of essential configuration keys.
        
        :raises: ValueError if required keys are missing or invalid
        """
        if not self._config_data:
             raise ValueError("Internal error: Config data not loaded after successful load.")

        required_keys = ['server_url']
        missing_keys = [key for key in required_keys if self.get(key) is None]

        if missing_keys:
            msg = f"Missing essential configuration keys: {', '.join(missing_keys)}"
            logger.critical(msg)
            raise ValueError(msg)

        server_url = self.get('server_url')
        if not isinstance(server_url, str) or not server_url:
             msg = f"Invalid 'server_url' configuration: Must be a non-empty string."
             logger.critical(msg)
             raise ValueError(msg)

        config_version = self.get('agent.config_version')
        if not isinstance(config_version, int) or config_version <= 0:
             msg = f"Invalid or missing 'agent.config_version' in configuration: Must be a positive integer."
             logger.critical(msg)

        logger.debug("Basic configuration validation passed.")

    def _backup_config(self) -> Optional[str]:
        """
        Creates a timestamped backup of the current config file.
        
        :return: Path to the backup file or None if backup failed
        :rtype: Optional[str]
        """
        if not self._config_path or not os.path.exists(self._config_path):
            logger.error("Cannot backup config: Config path is invalid or file does not exist.")
            return None

        backup_dir = os.path.dirname(self._config_path)
        base_name = os.path.basename(self._config_path)
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_filename = f"{base_name}.backup_{timestamp}"
        backup_path = os.path.join(backup_dir, backup_filename)

        try:
            shutil.copy2(self._config_path, backup_path)
            logger.info(f"Configuration backed up successfully to: {backup_path}")
            return backup_path
        except Exception as e:
            logger.error(f"Failed to create configuration backup at {backup_path}: {e}", exc_info=True)
            return None

    def _save_config(self, config_data: Dict[str, Any]) -> bool:
        """
        Saves the provided configuration data back to the config file.
        
        :param config_data: Configuration data to save
        :type config_data: Dict[str, Any]
        :return: True if saved successfully, False otherwise
        :rtype: bool
        """
        if not self._config_path:
            logger.error("Cannot save config: Config path is not set.")
            return False

        try:
            temp_path = self._config_path + ".tmp"

            with open(temp_path, 'w', encoding='utf-8') as f:
                json.dump(config_data, f, indent=4)

            try:
                if os.path.exists(self._config_path):
                    os.remove(self._config_path)
                os.rename(temp_path, self._config_path)
            except OSError as e:
                 logger.warning(f"Atomic rename failed ({e}), falling back to copy for saving config.")
                 shutil.copy2(temp_path, self._config_path)
                 os.remove(temp_path)

            logger.info(f"Configuration saved successfully to: {self._config_path}")
            return True
        except (IOError, OSError, json.JSONDecodeError) as e:
            logger.error(f"Failed to save configuration to {self._config_path}: {e}", exc_info=True)
            if os.path.exists(temp_path):
                try:
                    os.remove(temp_path)
                except OSError:
                    pass
            return False

    def _check_and_migrate_config(self):
        """
        Checks the config version and applies migrations if necessary.
        
        :raises: ValueError if migration fails
        """
        if not self._config_data or not self._config_path:
            logger.debug("Skipping config migration check: No config data or path.")
            return

        loaded_version = self.get('agent.config_version', 0)

        if not isinstance(loaded_version, int) or loaded_version < 0:
            logger.warning(f"Invalid 'agent.config_version' ({loaded_version}) found. Attempting migration from version 0.")
            loaded_version = 0

        if loaded_version < self.CURRENT_CONFIG_VERSION:
            logger.info(f"Configuration version mismatch: Found v{loaded_version}, expected v{self.CURRENT_CONFIG_VERSION}. Starting migration...")
            backup_path = self._backup_config()
            if not backup_path:
                logger.critical("Configuration backup failed. Aborting migration process to prevent data loss.")
                raise ValueError("Configuration backup failed. Cannot proceed with migration.")

            try:
                current_data = self._config_data.copy()

                if loaded_version < 1:
                     if 'agent' not in current_data: current_data['agent'] = {}
                     current_data['agent']['config_version'] = 1
                     logger.info("Migrated config data structure to v1 (set version number).")
                     loaded_version = 1

                if loaded_version == self.CURRENT_CONFIG_VERSION:
                    logger.info(f"Migration to v{self.CURRENT_CONFIG_VERSION} appears successful. Saving updated configuration...")
                    if self._save_config(current_data):
                        self._config_data = current_data
                        self._migration_performed = True
                        logger.info("Configuration successfully migrated and saved.")
                    else:
                        logger.critical("Failed to save migrated configuration! Agent might use old config or fail.")
                        raise ValueError("Failed to save migrated configuration.")
                else:
                    logger.critical(f"Configuration migration finished, but ended at v{loaded_version} instead of v{self.CURRENT_CONFIG_VERSION}. Migration logic might be incomplete.")
                    raise ValueError("Configuration migration failed to reach the current version.")

            except Exception as e:
                logger.critical(f"Error during configuration migration: {e}", exc_info=True)
                logger.critical(f"Migration failed. Original config backed up at: {backup_path}. Agent startup aborted.")
                raise ValueError(f"Configuration migration failed: {e}") from e
        elif loaded_version > self.CURRENT_CONFIG_VERSION:
            logger.warning(f"Configuration file version (v{loaded_version}) is newer than agent's supported version (v{self.CURRENT_CONFIG_VERSION}). Agent may not function correctly.")
        else:
            logger.debug(f"Configuration version (v{loaded_version}) matches agent's expected version (v{self.CURRENT_CONFIG_VERSION}). No migration needed.")

    def get(self, key_path: str, default: Any = None) -> Any:
        """
        Retrieves a configuration value using a dot-separated key path.
        
        :param key_path: The dot-separated path to the configuration key
        :type key_path: str
        :param default: The default value to return if the key is not found
        :type default: Any
        :return: The configuration value or the default value
        :rtype: Any
        """
        if not self._config_data:
            logger.error("Attempted to get config value before configuration was loaded.")
            return default

        keys = key_path.split('.')
        value = self._config_data
        try:
            for key in keys:
                if isinstance(value, dict):
                    value = value[key]
                else:
                    logger.debug(f"Key path '{key_path}' leads to non-dictionary element at '{key}'.")
                    return default
            return value
        except KeyError:
            logger.debug(f"Configuration key not found: '{key_path}'. Returning default: {default}")
            return default
        except Exception as e:
            logger.error(f"Unexpected error accessing config key '{key_path}': {e}", exc_info=True)
            return default

    @property
    def all_config(self) -> Dict[str, Any]:
        """
        Returns a copy of the entire loaded configuration dictionary.
        
        :return: Copy of configuration dictionary
        :rtype: Dict[str, Any]
        """
        return self._config_data.copy() if self._config_data else {}
