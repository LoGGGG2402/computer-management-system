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
import atexit  # To ensure lock release on exit
import psutil  # For checking if PID exists

# --- Global Lock File Variables ---
_lock_file_handle = None
_lock_file_path = None

# --- Path Setup ---
# Ensure the 'src' directory is in the Python path
current_dir = os.path.dirname(os.path.abspath(__file__)) # src directory
project_root = os.path.dirname(current_dir) # agent directory
if project_root not in sys.path:
    sys.path.insert(0, project_root)
if current_dir not in sys.path:
     sys.path.insert(0, current_dir) # Add src directory itself if needed
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
# UI (No direct use in main, but Agent uses it)
# --- End Core Imports ---

def parse_arguments() -> argparse.Namespace:
    """Parses command line arguments for configuration path and debug mode."""
    parser = argparse.ArgumentParser(description='Computer Management System Agent')
    # Default config path relative to project root (agent directory)
    default_config_name = 'agent_config.json'
    default_config_path = os.path.join(project_root, 'config', default_config_name)
    parser.add_argument(
        '--config',
        type=str,
        default=default_config_path,
        help=f'Path to the agent configuration file (default: {default_config_path})'
    )
    parser.add_argument(
        '--debug',
        action='store_true',
        help='Enable debug logging to console (overrides config file setting).'
    )
    return parser.parse_args()

def initialize_logging(config: ConfigManager, debug_mode: bool):
    """
    Initializes the logging system based on configuration.
    Must be called *after* config_manager is initialized.
    Reads log levels from config.
    """
    # Read log levels from config, providing fallbacks
    console_level_name = config.get('log_level.console', 'INFO') # Default INFO
    file_level_name = config.get('log_level.file', 'DEBUG')     # Default DEBUG

    log_storage_path = config.get('storage_path')
    log_file = None

    # Override console level if debug flag is set
    if debug_mode:
        console_level_name = 'DEBUG'
        print("Debug mode enabled via command line (Console Log Level: DEBUG).")

    # Construct log file path using storage_path from config
    if log_storage_path:
        # Ensure storage_path is absolute or resolve relative to project root?
        # Assuming storage_path is relative to project root if not absolute
        if not os.path.isabs(log_storage_path):
             log_storage_path = os.path.join(project_root, log_storage_path)
             print(f"Resolved relative storage_path to: {log_storage_path}")

        log_dir = os.path.join(log_storage_path, 'logs')
        log_file = os.path.join(log_dir, 'agent.log')
    else:
        print("Warning: 'storage_path' not defined in config. File logging disabled.")

    # Setup the root logger for the agent using values from config (or defaults)
    setup_logger(
        name="agent", # Setup the root logger for the application
        console_level_name=console_level_name,
        file_level_name=file_level_name,
        log_file_path=log_file
    )

def release_lock():
    """Releases the lock file."""
    global _lock_file_handle, _lock_file_path
    logger = get_logger("agent.main") # Get logger instance
    if _lock_file_handle:
        try:
            # Release lock and close file
            _lock_file_handle.close()
            _lock_file_handle = None
            logger.debug(f"Closed lock file handle.")
            # Attempt to remove the lock file
            if _lock_file_path and os.path.exists(_lock_file_path):
                os.remove(_lock_file_path)
                logger.info(f"Removed lock file: {_lock_file_path}")
        except OSError as e:
            logger.error(f"Error releasing lock file {_lock_file_path}: {e}")
        except Exception as e:
            logger.error(f"Unexpected error releasing lock: {e}", exc_info=True)
    elif _lock_file_path and os.path.exists(_lock_file_path):
         # If handle is None but file exists, try removing it
         try:
             os.remove(_lock_file_path)
             logger.info(f"Removed potentially stale lock file: {_lock_file_path}")
         except OSError as e:
             logger.error(f"Error removing stale lock file {_lock_file_path}: {e}")

def acquire_lock(storage_path: str) -> bool:
    """Attempts to acquire a lock file to ensure single instance."""
    global _lock_file_handle, _lock_file_path
    logger = get_logger("agent.main") # Get logger instance

    if not storage_path or not os.path.isdir(storage_path):
         logger.critical(f"Invalid storage path '{storage_path}' for lock file. Cannot ensure single instance.")
         print(f"FATAL: Invalid storage path '{storage_path}' provided for lock file.", file=sys.stderr)
         return False

    _lock_file_path = os.path.join(storage_path, "agent.lock")
    logger.debug(f"Attempting to acquire lock file: {_lock_file_path}")

    try:
        # Atomically create and open the file for writing
        # O_EXCL ensures creation fails if file exists
        fd = os.open(_lock_file_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        _lock_file_handle = os.fdopen(fd, 'w')

        # Write current PID to the lock file
        pid = str(os.getpid())
        _lock_file_handle.write(pid)
        _lock_file_handle.flush() # Ensure PID is written
        logger.info(f"Acquired lock file: {_lock_file_path} with PID: {pid}")
        # Register the release function to be called on exit
        atexit.register(release_lock)
        return True

    except FileExistsError:
        logger.warning(f"Lock file {_lock_file_path} already exists. Checking PID...")
        try:
            with open(_lock_file_path, 'r') as f:
                existing_pid_str = f.read().strip()
            if not existing_pid_str.isdigit():
                 raise ValueError("Lock file does not contain a valid PID.")
            
            existing_pid = int(existing_pid_str)
            
            if psutil.pid_exists(existing_pid):
                # Check if the process is actually the agent
                try:
                    proc = psutil.Process(existing_pid)
                    # Basic check: is the process name similar?
                    proc_name = proc.name().lower()
                    is_agent = 'python' in proc_name or 'agent' in proc_name 
                    
                    if is_agent: # Assume it's our agent if PID exists and looks like python/agent
                        logger.critical(f"Another agent instance (PID: {existing_pid}) appears to be running.")
                        print(f"ERROR: Another instance of the agent (PID: {existing_pid}) seems to be running.", file=sys.stderr)
                        return False
                    else:
                         logger.warning(f"PID {existing_pid} exists but doesn't look like the agent process ('{proc_name}'). Treating lock as potentially stale.")
                         # Proceed to treat as stale lock
                    
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                     # Process died between pid_exists and Process() or we lack permissions
                     logger.warning(f"Process with PID {existing_pid} existed but is now gone or inaccessible. Treating lock as stale.")
                     # Proceed to treat as stale lock
            else:
                 logger.warning(f"Process with PID {existing_pid} from lock file does not exist. Treating lock as stale.")

            # --- Stale Lock Handling ---
            logger.warning(f"Attempting to remove stale lock file: {_lock_file_path}")
            os.remove(_lock_file_path)
            # Retry acquiring the lock
            return acquire_lock(storage_path)

        except (IOError, ValueError, OSError) as e:
            logger.error(f"Error checking existing lock file {_lock_file_path}: {e}. Assuming another instance is running.", exc_info=True)
            print(f"ERROR: Could not verify existing lock file {_lock_file_path}. Please check manually. {e}", file=sys.stderr)
            return False
        except Exception as e: # Catch any other unexpected errors during check
             logger.critical(f"Unexpected error checking lock file {_lock_file_path}: {e}", exc_info=True)
             print(f"FATAL: Unexpected error checking lock file {_lock_file_path}. {e}", file=sys.stderr)
             return False
             
    except Exception as e: # Catch other errors during initial os.open
        logger.critical(f"Failed to create or lock file {_lock_file_path}: {e}", exc_info=True)
        print(f"FATAL: Could not create lock file {_lock_file_path}. {e}", file=sys.stderr)
        return False

def main() -> int:
    """Main function to initialize and start the agent."""
    args = parse_arguments()
    logger = None # Initialize logger variable
    agent_instance = None # Keep track of agent instance for cleanup

    try:
        # 1. Initialize Configuration Manager
        print(f"Loading configuration from: {args.config}")
        config_manager = ConfigManager(args.config)

        # 2. Initialize Logging (uses loaded configuration)
        initialize_logging(config_manager, args.debug)
        logger = get_logger("agent.main") # Get logger after initialization
        logger.info("Configuration and Logging initialized successfully.")

        # 3. Initialize State Manager (needed for lock file path)
        logger.info("Initializing state manager...")
        state_manager = StateManager(config_manager)
        storage_path = state_manager.storage_path # Get validated storage path

        # --- Acquire Lock ---
        if not acquire_lock(storage_path):
             # Error message already printed by acquire_lock
             return 1 # Exit if lock cannot be acquired
        # --- Lock Acquired ---

        # Ensure device ID exists early (catches storage errors)
        state_manager.ensure_device_id()

        # HTTP Client (needs config)
        http_client = HttpClient(config_manager)
        # WebSocket Client (needs config)
        ws_client = WSClient(config_manager)
        # System Monitor (no direct dependencies needed at init)
        system_monitor = SystemMonitor()
        # Command Executor (needs ws_client and config)
        command_executor = CommandExecutor(ws_client, config_manager)
        # UI Console functions are used directly by Agent, no instance needed here

        # 4. Initialize the Agent Core (Inject all dependencies)
        logger.info("Creating Agent instance...")
        agent_instance = Agent( # Assign to agent_instance
            config_manager=config_manager,
            state_manager=state_manager,
            http_client=http_client,
            ws_client=ws_client,
            system_monitor=system_monitor,
            command_executor=command_executor
        )

        # 5. Start the Agent
        logger.info("Starting Agent main loop...")
        agent_instance.start() # This method blocks until stopped or error

        logger.info("Agent has stopped.")
        return 0

    except FileNotFoundError as e:
         # Config file not found error from ConfigManager
         print(f"FATAL: Configuration file error: {e}. Agent cannot start.", file=sys.stderr)
         return 1
    except ValueError as e:
         # Invalid config, missing keys, storage path issues, etc.
         # Logger might not be fully initialized here if error is early
         print(f"FATAL: Initialization error: {e}. Agent cannot start.", file=sys.stderr)
         if logger:
              logger.critical(f"Initialization error: {e}", exc_info=True)
         return 1
    except RuntimeError as e:
         # Agent specific init errors (e.g., couldn't get room config/device ID)
         print(f"FATAL: Agent runtime error during initialization: {e}. Agent cannot start.", file=sys.stderr)
         if logger:
              logger.critical(f"Agent runtime error during initialization: {e}", exc_info=True)
         return 1
    except KeyboardInterrupt:
        # Handle Ctrl+C during initialization phase (before agent.start loop)
        print("\nAgent startup interrupted by user (Ctrl+C). Exiting.", file=sys.stderr)
        if logger:
             logger.info("Agent startup interrupted by user (Ctrl+C).")
        # Lock will be released by atexit handler
        return 0 
    except Exception as e:
        # Catch any other unexpected critical errors during setup
        print(f"FATAL: An unexpected critical error occurred during initialization: {e}", file=sys.stderr)
        if logger:
            logger.critical(f"An unexpected critical error occurred during initialization: {e}", exc_info=True)
        else:
             # If logging failed, print traceback to stderr
             import traceback
             traceback.print_exc()
        return 1
    finally:
         # Explicitly release lock here as a fallback, though atexit should handle it
         # release_lock() # atexit is generally preferred
         # Ensure agent stop is called if instance was created but start loop failed/exited unexpectedly
         if agent_instance and agent_instance._running.is_set():
              logger.info("Ensuring agent is stopped in main finally block...")
              agent_instance.stop()
         logger.info("Main function exiting.")

if __name__ == "__main__":
    exit_code = main()
    # Optional: Add a small delay or final log message before exiting
    # if exit_code != 0:
    #     print(f"Agent exited with error code: {exit_code}", file=sys.stderr)
    # else:
    #     print("Agent exited normally.")
    sys.exit(exit_code)
