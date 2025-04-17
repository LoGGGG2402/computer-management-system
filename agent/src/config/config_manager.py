# -*- coding: utf-8 -*-
"""
Configuration Manager module for the Computer Management System Agent.
Loads and provides access to configuration settings from a JSON file.
Designed to be instantiated and passed around (Dependency Injection).
"""
import json
import os
import logging
from typing import Any, Optional, Dict

logger = logging.getLogger(__name__)

class ConfigManager:
    """Loads and manages agent configuration from a file."""

    def __init__(self, config_path: Optional[str]): # Allow None for temp instance
        """
        Initializes the ConfigManager by loading the configuration file.

        Args:
            config_path (Optional[str]): The path to the agent configuration JSON file.
                                         If None, initializes with an empty config (for temp usage).

        Raises:
            FileNotFoundError: If the configuration file path is provided but does not exist.
            ValueError: If the configuration file is invalid JSON or essential keys are missing (unless config_path is None).
        """
        self._config_path = config_path
        self._config_data: Optional[Dict[str, Any]] = None

        if self._config_path is None:
             logger.debug("ConfigManager initialized without a config path (temporary instance).")
             self._config_data = {} # Initialize with empty dict
             # No validation needed for temp instance
        else:
             self._load_config()
             self._validate_config() # Validate only if path was provided
             logger.info(f"Configuration loaded successfully from: {self._config_path}")

    def _load_config(self):
        """Loads the configuration data from the JSON file."""
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
        """Performs basic validation of essential configuration keys."""
        if not self._config_data: # Should not happen if _load_config succeeded
             raise ValueError("Internal error: Config data not loaded after successful load.")

        # Reduced required keys for Phase 1, storage_path is now handled by StateManager
        required_keys = ['server_url']
        missing_keys = [key for key in required_keys if self.get(key) is None]

        if missing_keys:
            msg = f"Missing essential configuration keys: {', '.join(missing_keys)}"
            logger.critical(msg)
            raise ValueError(msg)

        # storage_path validation removed - handled by StateManager
        # if not isinstance(storage_path, str) or not storage_path: ...

        server_url = self.get('server_url')
        if not isinstance(server_url, str) or not server_url:
             msg = f"Invalid 'server_url' configuration: Must be a non-empty string."
             logger.critical(msg)
             raise ValueError(msg)

        logger.debug("Basic configuration validation passed.")

    def get(self, key_path: str, default: Any = None) -> Any:
        """
        Retrieves a configuration value using a dot-separated key path.

        Args:
            key_path (str): The dot-separated path to the configuration key (e.g., 'log_level.console').
            default (Any, optional): The default value to return if the key is not found. Defaults to None.

        Returns:
            Any: The configuration value or the default value.
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
                    # Trying to access a subkey of a non-dictionary element
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
        """Returns a copy of the entire loaded configuration dictionary."""
        return self._config_data.copy() if self._config_data else {}
