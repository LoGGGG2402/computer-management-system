#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Main entry point for the Computer Management System Agent.
Initializes configuration, logging, dependencies, and starts the core Agent process.
"""
import os
import sys
import argparse
import logging

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

def main() -> int:
    """Main function to initialize and start the agent."""
    args = parse_arguments()
    logger = None # Initialize logger variable

    try:
        # 1. Initialize Configuration Manager
        print(f"Loading configuration from: {args.config}")
        config_manager = ConfigManager(args.config)

        # 2. Initialize Logging (uses loaded configuration)
        initialize_logging(config_manager, args.debug)
        logger = get_logger("agent.main") # Get logger after initialization
        logger.info("Configuration and Logging initialized successfully.")

        # 3. Initialize Core Components (Dependency Injection)
        logger.info("Initializing core components...")
        # State Manager (needs config)
        state_manager = StateManager(config_manager)
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
        agent = Agent(
            config_manager=config_manager,
            state_manager=state_manager,
            http_client=http_client,
            ws_client=ws_client,
            system_monitor=system_monitor,
            command_executor=command_executor
        )

        # 5. Start the Agent
        logger.info("Starting Agent main loop...")
        agent.start() # This method blocks until stopped or error

        logger.info("Agent has stopped.")
        return 0

    except FileNotFoundError as e:
         # Config file not found error from ConfigManager
         print(f"FATAL: Configuration file error: {e}. Agent cannot start.", file=sys.stderr)
         return 1
    except ValueError as e:
         # Invalid config, missing keys, storage path issues, etc.
         print(f"FATAL: Initialization error: {e}. Agent cannot start.", file=sys.stderr)
         # Log if logger was initialized
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
        # Handle Ctrl+C during initialization phase
        print("\nAgent startup interrupted by user (Ctrl+C). Exiting.", file=sys.stderr)
        if logger:
             logger.info("Agent startup interrupted by user (Ctrl+C).")
        return 0 # Clean exit code for user interruption
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

if __name__ == "__main__":
    exit_code = main()
    # Optional: Add a small delay or final log message before exiting
    # if exit_code != 0:
    #     print(f"Agent exited with error code: {exit_code}", file=sys.stderr)
    # else:
    #     print("Agent exited normally.")
    sys.exit(exit_code)
