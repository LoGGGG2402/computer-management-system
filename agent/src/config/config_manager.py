"""
Configuration manager module for the Computer Management System Agent.
This module provides a centralized configuration system used by all other modules.
"""
import os
import json
import shutil
from typing import Dict, Any, Optional, Union

from src.utils.logger import get_logger

# Get logger for this module
logger = get_logger(__name__)

class ConfigManager:
    """Configuration manager for the agent system."""
    _instance = None
    
    def __new__(cls, *args, **kwargs):
        """Implement the Singleton pattern to ensure only one config instance exists."""
        if cls._instance is None:
            cls._instance = super(ConfigManager, cls).__new__(cls)
            cls._instance._initialized = False
        return cls._instance
    
    def __init__(self, config_path: Optional[str] = None, debug: bool = False):
        """
        Initialize the configuration manager.
        
        Args:
            config_path (str, optional): Path to configuration file
            debug (bool): Enable debug mode
        """
        # Skip initialization if already initialized
        if self._initialized:
            return
            
        self._initialized = True
        self._config_path = config_path
        self._config = {}
        self._defaults = {
            "server_url": "http://localhost:3000",
            "monitoring_interval": 60,
            "reconnect_delay": 5,
            "storage_path": "../storage",
            "log_level": {
                "console": "INFO",
                "file": "DEBUG"
            },
            "modules": {
                "system_monitor": {
                    "enabled": True,
                    "interval": 60
                },
                "network_monitor": {
                    "enabled": True,
                    "interval": 120
                },
                "process_monitor": {
                    "enabled": True,
                    "interval": 60,
                    "track_top_processes": 10
                }
            }
        }
        
        # Set debug flag
        self._debug = debug
        
        # Load configuration if path provided
        if config_path:
            self.load_config(config_path)
            
    def initialize(self, config_path: str, debug: bool = False) -> bool:
        """
        Initialize the configuration manager.
        
        Args:
            config_path (str): Path to configuration file
            debug (bool): Enable debug mode
            
        Returns:
            bool: True if initialization succeeded
        """
        self._config_path = config_path
        self._debug = debug
        
        # Try to load existing config
        if not self.load_config(config_path):
            # If loading failed, create a new default config
            return self.create_default_config()
            
        return True
        
    def load_config(self, config_path: str) -> bool:
        """
        Load configuration from file.
        
        Args:
            config_path (str): Path to the configuration file
            
        Returns:
            bool: True if loaded successfully
        """
        try:
            if not os.path.exists(config_path):
                logger.warning(f"Configuration file not found: {config_path}")
                return False
                
            with open(config_path, 'r') as f:
                config = json.load(f)
                
            # Validate required configuration fields
            required_fields = ['server_url', 'monitoring_interval', 'storage_path']
            missing_fields = [field for field in required_fields if field not in config]
            
            if missing_fields:
                logger.error(f"Missing required configuration fields: {missing_fields}")
                return False
                
            # Convert relative paths to absolute
            if not os.path.isabs(config['storage_path']):
                config_dir = os.path.dirname(config_path)
                config['storage_path'] = os.path.abspath(os.path.join(config_dir, config['storage_path']))
                
            # Ensure storage directory exists
            os.makedirs(config['storage_path'], exist_ok=True)
            
            # Override debug setting if provided
            if self._debug:
                config['debug'] = True
                
            # Store the loaded configuration
            self._config = config
            
            logger.info("Configuration loaded successfully")
            logger.debug(f"Configuration: {config}")
            return True
            
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse configuration file: {e}")
            return False
        except Exception as e:
            logger.error(f"Error loading configuration: {e}")
            return False
            
    def create_default_config(self) -> bool:
        """
        Create a default configuration file.
        
        Returns:
            bool: True if created successfully
        """
        try:
            # Ensure the config directory exists
            config_dir = os.path.dirname(self._config_path)
            os.makedirs(config_dir, exist_ok=True)
            
            # Create a copy of the default configuration
            config = self._defaults.copy()
            
            # Add debug flag if set
            if self._debug:
                config['debug'] = True
                
            # Convert relative storage path to absolute
            if not os.path.isabs(config['storage_path']):
                config['storage_path'] = os.path.abspath(os.path.join(config_dir, config['storage_path']))
                
            # Create storage directory
            os.makedirs(config['storage_path'], exist_ok=True)
            
            # Write configuration to file
            with open(self._config_path, 'w') as f:
                json.dump(config, f, indent=4)
                
            # Store the configuration
            self._config = config
            
            logger.info(f"Created default configuration at: {self._config_path}")
            return True
            
        except Exception as e:
            logger.error(f"Error creating default configuration: {e}")
            return False
            
    def get(self, key: str, default: Any = None) -> Any:
        """
        Get a configuration value by key.
        
        Args:
            key (str): Configuration key (supports dot notation for nested access)
            default (Any): Default value if key not found
            
        Returns:
            Any: Configuration value or default
        """
        if not key:
            return default
            
        # Support dot notation for nested values (e.g., "modules.system_monitor.enabled")
        if '.' in key:
            parts = key.split('.')
            value = self._config
            for part in parts:
                if isinstance(value, dict) and part in value:
                    value = value[part]
                else:
                    return default
            return value
            
        return self._config.get(key, default)
        
    def set(self, key: str, value: Any) -> bool:
        """
        Set a configuration value.
        
        Args:
            key (str): Configuration key (supports dot notation for nested access)
            value (Any): Value to set
            
        Returns:
            bool: True if set successfully
        """
        if not key:
            return False
            
        # Support dot notation for nested values
        if '.' in key:
            parts = key.split('.')
            config = self._config
            
            # Navigate to the nested dictionary
            for i, part in enumerate(parts[:-1]):
                if part not in config:
                    config[part] = {}
                elif not isinstance(config[part], dict):
                    config[part] = {}
                config = config[part]
                
            # Set the value
            config[parts[-1]] = value
        else:
            self._config[key] = value
            
        return self.save_config()
        
    def save_config(self) -> bool:
        """
        Save the current configuration to file.
        
        Returns:
            bool: True if saved successfully
        """
        try:
            if not self._config_path:
                logger.error("No configuration path set")
                return False
                
            # Create backup of existing config
            if os.path.exists(self._config_path):
                backup_path = f"{self._config_path}.bak"
                shutil.copy2(self._config_path, backup_path)
                
            # Write configuration to file
            with open(self._config_path, 'w') as f:
                json.dump(self._config, f, indent=4)
                
            logger.info("Configuration saved successfully")
            return True
            
        except Exception as e:
            logger.error(f"Error saving configuration: {e}")
            return False
            
    def get_all(self) -> Dict[str, Any]:
        """
        Get the entire configuration dictionary.
        
        Returns:
            Dict[str, Any]: Configuration dictionary
        """
        return self._config.copy()
        
    def reset_to_default(self) -> bool:
        """
        Reset configuration to default values.
        
        Returns:
            bool: True if reset successfully
        """
        try:
            self._config = self._defaults.copy()
            
            # Add debug flag if set
            if self._debug:
                self._config['debug'] = True
                
            return self.save_config()
            
        except Exception as e:
            logger.error(f"Error resetting configuration: {e}")
            return False
            
    def setup_first_run(self) -> bool:
        """
        Set up configuration for first run.
        Interactive setup that prompts user for essential configuration.
        
        Returns:
            bool: True if setup successfully
        """
        try:
            print("\n" + "="*50)
            print("COMPUTER MANAGEMENT SYSTEM - AGENT SETUP")
            print("="*50)
            print("\nWelcome to the Computer Management System Agent setup.")
            print("This will configure the agent for first use.\n")
            
            # Prompt for server URL
            default_url = self.get('server_url', 'http://localhost:3000')
            server_url = input(f"Enter server URL [{default_url}]: ").strip()
            if not server_url:
                server_url = default_url
                
            # Prompt for monitoring interval
            default_interval = self.get('monitoring_interval', 60)
            interval_input = input(f"Enter monitoring interval in seconds [{default_interval}]: ").strip()
            if interval_input and interval_input.isdigit():
                monitoring_interval = int(interval_input)
            else:
                monitoring_interval = default_interval
                
            # Update configuration with user inputs
            self.set('server_url', server_url)
            self.set('monitoring_interval', monitoring_interval)
            
            print("\nConfiguration complete! Agent is ready to start.")
            return True
            
        except KeyboardInterrupt:
            print("\nSetup interrupted by user.")
            return False
        except Exception as e:
            logger.error(f"Error during first run setup: {e}")
            print(f"\nError during setup: {e}")
            return False

# Create a global instance
config_manager = ConfigManager()