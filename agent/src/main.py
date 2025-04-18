#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Main entry point for the Computer Management System Agent.
Initializes configuration, logging, dependencies, and starts the core Agent process.
Includes logic to ensure only one instance runs using a lock file.
"""
import os
import sys
import argparse
import shutil  # Added for file copying

# --- Global Lock File Variables ---
_lock_manager_instance = None  # Keep track of the lock manager instance for atexit

# --- Path Setup ---
# Ensure the 'src' directory is in the Python path
current_dir = os.path.dirname(os.path.abspath(__file__))  # src directory
project_root = os.path.dirname(current_dir)  # agent directory
if project_root not in sys.path:
    sys.path.insert(0, project_root)
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)  # Add src directory itself if needed
# --- End Path Setup ---

# --- Core Imports ---
# Configuration
from src.config.config_manager import ConfigManager
from src.config.state_manager import StateManager
# Communication
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient
# Monitoring
from src.monitoring.system_monitor import SystemMonitor
# Core Logic
from src.core.agent import Agent
from src.core.command_executor import CommandExecutor
# Utilities
from src.utils.logger import setup_logger, get_logger
# System & Lock Management
from src.system.lock_manager import LockManager
from src.system.windows_utils import is_running_as_admin, register_autostart, unregister_autostart  # Added autostart functions
# --- End Core Imports ---

def parse_arguments() -> argparse.Namespace:
    """Parses command line arguments for configuration path and debug mode."""
    parser = argparse.ArgumentParser(description='Computer Management System Agent')

    # Default config *filename* (path is now determined dynamically)
    default_config_name = 'agent_config.json'

    parser.add_argument(
        '--config-name',  # Changed from --config
        type=str,
        default=default_config_name,
        help=f'Name of the agent configuration file within the storage directory (default: {default_config_name})'
    )
    parser.add_argument(
        '--debug',
        action='store_true',
        help='Enable debug logging to console (overrides config file setting).'
    )

    # --- Autostart Arguments --- START
    parser.add_argument(
        '--enable-autostart',
        action='store_true',
        help='Register the agent to start automatically with Windows (requires appropriate privileges) and exit.'
    )
    parser.add_argument(
        '--disable-autostart',
        action='store_true',
        help='Unregister the agent from starting automatically with Windows (requires appropriate privileges) and exit.'
    )
    # --- Autostart Arguments --- END

    return parser.parse_args()

def initialize_logging(config: ConfigManager, storage_path: str, debug_mode: bool):
    """
    Initializes the logging system based on configuration and storage path.
    Must be called *after* config_manager is initialized and storage_path is known.
    Reads log levels from config.
    """
    # Read log levels from config, providing fallbacks
    console_level_name = config.get('log_level.console', 'INFO')  # Default INFO
    file_level_name = config.get('log_level.file', 'DEBUG')  # Default DEBUG
    log_filename = config.get('log_filename', 'agent.log')  # Get log filename from config

    log_file = None

    # Override console level if debug flag is set
    if debug_mode:
        console_level_name = 'DEBUG'
        print("Debug mode enabled via command line (Console Log Level: DEBUG).")

    # Construct log file path using the determined storage_path
    if storage_path:
        log_dir = os.path.join(storage_path, 'logs')  # Logs subdirectory within storage_path
        log_file = os.path.join(log_dir, log_filename)
        print(f"Logging to file: {log_file}")  # Early print for visibility
    else:
        # This case should ideally not happen if StateManager succeeded
        print("Warning: Storage path not available. File logging disabled.")

    # Setup the root logger for the agent using values from config (or defaults)
    setup_logger(
        name="agent",  # Setup the root logger for the application
        console_level_name=console_level_name,
        file_level_name=file_level_name,
        log_file_path=log_file
    )

def cleanup_resources():
    """Function registered with atexit to release the lock."""
    global _lock_manager_instance
    logger = get_logger("agent.main")  # Get logger instance
    if _lock_manager_instance:
        logger.info("Releasing lock via atexit handler...")
        _lock_manager_instance.release()
        logger.info("Lock released via atexit handler.")
    else:
        logger.debug("No lock manager instance found in atexit handler.")

def main() -> int:
    """Main function to initialize and start the agent."""
    global _lock_manager_instance  # Allow modification
    args = parse_arguments()
    logger = None  # Initialize logger variable
    agent_instance = None  # Keep track of agent instance for cleanup
    config_manager = None  # Initialize config manager variable
    state_manager = None  # Initialize state manager variable

    # --- Determine Executable Path --- START
    # This is needed early for autostart registration
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        # Running as a bundled executable (PyInstaller)
        executable_path = sys.executable
    else:
        # Running as a script
        try:
            executable_path = os.path.abspath(sys.argv[0])
        except IndexError:
            print("FATAL: Cannot determine executable path from sys.argv.", file=sys.stderr)
            return 1
    print(f"Determined executable path: {executable_path}")
    # --- Determine Executable Path --- END

    # Create a temporary config manager (might fail if file not found, uses defaults)
    temp_config_path = None
    source_config_path = None  # Store the path of the config used for initial setup
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        base_dir = os.path.dirname(sys.executable)
    else:
        base_dir = project_root
    potential_temp_config = os.path.join(base_dir, 'config', args.config_name)
    if os.path.exists(potential_temp_config):
        temp_config_path = potential_temp_config
        source_config_path = temp_config_path  # Found the source config
        print(f"Using temporary config path for initial setup: {temp_config_path}")
    else:
        # If not found in config/, maybe it's directly in base_dir (less likely but possible)
        potential_temp_config_alt = os.path.join(base_dir, args.config_name)
        if os.path.exists(potential_temp_config_alt):
            temp_config_path = potential_temp_config_alt
            source_config_path = temp_config_path  # Found the source config
            print(f"Using temporary config path (base dir) for initial setup: {temp_config_path}")

    temp_config_manager = ConfigManager(temp_config_path) if temp_config_path else ConfigManager(None)
    app_name_for_registry = temp_config_manager.get('agent.app_name', 'CMSAgent')  # Get app name early

    # 2. Initialize State Manager (determines storage_path)
    try:
        print("Initializing state manager...")
        state_manager = StateManager(temp_config_manager)  # Pass temp config
        storage_path = state_manager.storage_path  # Get determined storage path
        print(f"Storage path determined: {storage_path}")
    except ValueError as e:
        print(f"FATAL: Failed to initialize State Manager: {e}", file=sys.stderr)
        return 1
    except Exception as e:
        print(f"FATAL: Unexpected error initializing State Manager: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        return 1

    # --- Handle Autostart Arguments --- START
    # This needs to run *before* logging is fully initialized and *before* the lock
    # because these actions should be quick and exit immediately.
    if args.enable_autostart or args.disable_autostart:
        is_admin = is_running_as_admin()
        print(f"Running with admin privileges: {is_admin}")
        if args.enable_autostart:
            print(f"Attempting to enable autostart for: {executable_path} with name {app_name_for_registry}")
            success = register_autostart(app_name_for_registry, executable_path, is_admin)  # Pass app_name
            print(f"Enable autostart result: {'Success' if success else 'Failed'}")
            return 0 if success else 1
        elif args.disable_autostart:
            print(f"Attempting to disable autostart for name {app_name_for_registry}...")
            success = unregister_autostart(app_name_for_registry, is_admin)  # Pass app_name
            print(f"Disable autostart result: {'Success' if success else 'Failed'}")
            return 0 if success else 1
    # --- Handle Autostart Arguments --- END

    # 3. Initialize Real Configuration Manager (using path inside storage_path)
    try:
        config_file_path = os.path.join(storage_path, args.config_name)
        print(f"Target configuration path: {config_file_path}")

        # --- Copy config if it doesn't exist in storage_path --- START
        if not os.path.exists(config_file_path):
            logger_provisional = get_logger("agent.main")  # Get logger early for this message
            if source_config_path and os.path.exists(source_config_path):
                logger_provisional.info(f"Configuration file not found in storage path. Copying from {source_config_path}...")
                print(f"Configuration file not found in storage path. Copying from {source_config_path}...")
                try:
                    # Ensure target directory exists (should have been created by StateManager, but double-check)
                    os.makedirs(storage_path, exist_ok=True)
                    shutil.copy2(source_config_path, config_file_path)  # copy2 preserves metadata
                    logger_provisional.info(f"Successfully copied configuration to {config_file_path}")
                except Exception as copy_err:
                    print(f"FATAL: Failed to copy configuration file from {source_config_path} to {config_file_path}: {copy_err}", file=sys.stderr)
                    if logger_provisional:
                        logger_provisional.critical(f"Failed to copy configuration file: {copy_err}", exc_info=True)
                    return 1  # Cannot proceed without config
            else:
                # Source config wasn't found either
                print(f"FATAL: Configuration file '{args.config_name}' not found in storage path '{storage_path}' and no source configuration file could be located.", file=sys.stderr)
                if logger_provisional:
                    logger_provisional.critical(f"Config file missing in storage and source location.")
                return 1  # Cannot proceed
        # --- Copy config if it doesn't exist in storage_path --- END

        print(f"Loading configuration from: {config_file_path}")
        config_manager = ConfigManager(config_file_path)
    except FileNotFoundError as e:
        print(f"FATAL: Configuration file error: {e}. Agent cannot start.", file=sys.stderr)
        print(f"Ensure '{args.config_name}' exists in '{storage_path}'.", file=sys.stderr)
        return 1
    except ValueError as e:
        print(f"FATAL: Configuration error: {e}. Agent cannot start.", file=sys.stderr)
        return 1
    except Exception as e:
        print(f"FATAL: Unexpected error loading configuration: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        return 1

    # 4. Initialize Logging (uses loaded configuration and storage_path)
    try:
        initialize_logging(config_manager, storage_path, args.debug)
        logger = get_logger("agent.main")  # Get logger after initialization
        logger.info("--- Agent Starting ---")
        logger.info(f"Using storage path: {storage_path}")
        logger.info(f"Using configuration file: {config_file_path}")
        logger.info("Logging initialized successfully.")
    except Exception as e:
        print(f"FATAL: Failed to initialize logging: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        return 1

    # --- Acquire Lock ---
    try:
        logger.info("Acquiring instance lock...")
        _lock_manager_instance = LockManager(storage_path)
        if not _lock_manager_instance.acquire():
            logger.critical("Failed to acquire instance lock. Exiting.")
            return 1
        logger.info("Instance lock acquired successfully.")
    except ValueError as e:
        logger.critical(f"Failed to initialize Lock Manager: {e}", exc_info=True)
        print(f"FATAL: Failed to initialize Lock Manager: {e}", file=sys.stderr)
        return 1
    except Exception as e:
        logger.critical(f"Unexpected error acquiring lock: {e}", exc_info=True)
        print(f"FATAL: Unexpected error acquiring lock: {e}", file=sys.stderr)
        return 1

    try:
        # 5. Initialize Remaining Components
        logger.info("Initializing remaining components...")
        state_manager.ensure_device_id()  # Now uses the determined storage_path

        http_client = HttpClient(config_manager)
        ws_client = WSClient(config_manager)
        system_monitor = SystemMonitor()
        command_executor = CommandExecutor(ws_client, config_manager)

        # 6. Initialize the Agent Core (Inject all dependencies)
        logger.info("Creating Agent instance...")
        agent_instance = Agent(
            config_manager=config_manager,
            state_manager=state_manager,
            http_client=http_client,
            ws_client=ws_client,
            system_monitor=system_monitor,
            command_executor=command_executor,
            lock_manager=_lock_manager_instance
        )

        # 7. Start the Agent
        logger.info("Starting Agent main loop...")
        agent_instance.start()

        logger.info("Agent has stopped normally.")
        return 0

    except FileNotFoundError as e:
        print(f"FATAL: Configuration file error: {e}. Agent cannot start.", file=sys.stderr)
        logger.critical(f"Configuration file error: {e}", exc_info=True)
        return 1
    except ValueError as e:
        print(f"FATAL: Initialization error: {e}. Agent cannot start.", file=sys.stderr)
        logger.critical(f"Initialization error: {e}", exc_info=True)
        return 1
    except RuntimeError as e:
        print(f"FATAL: Agent runtime error during initialization: {e}. Agent cannot start.", file=sys.stderr)
        logger.critical(f"Agent runtime error during initialization: {e}", exc_info=True)
        return 1
    except KeyboardInterrupt:
        print("\nAgent startup interrupted by user (Ctrl+C). Exiting.", file=sys.stderr)
        if logger:
            logger.info("Agent startup interrupted by user (Ctrl+C).")
        return 0
    except Exception as e:
        print(f"FATAL: An unexpected critical error occurred during initialization: {e}", file=sys.stderr)
        if logger:
            logger.critical(f"An unexpected critical error occurred during initialization: {e}", exc_info=True)
        else:
            import traceback
            traceback.print_exc()
        return 1
    finally:
        if agent_instance and agent_instance._running.is_set():
            logger.info("Ensuring agent is stopped in main finally block...")
            agent_instance.stop()

if __name__ == "__main__":
    exit_code = main()
    print(f"Agent process exiting with code {exit_code}.")
    sys.exit(exit_code)
