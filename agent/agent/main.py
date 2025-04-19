#!/usr/bin/env python3
"""
Main entry point for the Computer Management System Agent.
"""
import os
import sys
import argparse
import shutil
import time
from agent.ipc import send_force_command

current_dir = os.path.dirname(os.path.abspath(__file__))
project_root = os.path.dirname(current_dir)
if project_root not in sys.path:
    sys.path.insert(0, project_root)
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

from agent.utils import setup_logger, get_logger, get_file_logging_status
from agent.system import (
    LockManager, 
    is_running_as_admin, 
    register_autostart, 
    unregister_autostart,
    setup_directory_structure
)
from . import ConfigManager, StateManager, HttpClient, WSClient, ServerConnector, SystemMonitor, Agent, CommandExecutor

def parse_arguments() -> argparse.Namespace:
    """
    Parses command line arguments for configuration path and debug mode.
    
    :return: Parsed command line arguments
    :rtype: argparse.Namespace
    """
    parser = argparse.ArgumentParser(description='Computer Management System Agent')

    default_config_name = 'agent_config.json'

    parser.add_argument(
        '--config-name',
        type=str,
        default=default_config_name,
        help=f'Name of the agent configuration file within the storage directory (default: {default_config_name})'
    )
    parser.add_argument(
        '--debug',
        action='store_true',
        help='Enable debug logging to console (overrides config file setting).'
    )

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

    parser.add_argument(
        '--force',
        action='store_true',
        help='Request the currently running agent instance (if any) to shut down and then start this instance. Uses IPC.'
    )

    return parser.parse_args()

def initialize_logging(config: ConfigManager, storage_path: str, debug_mode: bool):
    """
    Initializes the logging system based on configuration and storage path.
    
    :param config: Configuration manager instance
    :type config: ConfigManager
    :param storage_path: Path to store logs
    :type storage_path: str
    :param debug_mode: Whether to enable debug mode
    :type debug_mode: bool
    """
    console_level_name = config.get('log_level.console', 'INFO')
    file_level_name = config.get('log_level.file', 'DEBUG')
    log_filename = config.get('log_filename', 'agent.log')

    log_file = None

    if debug_mode:
        console_level_name = 'DEBUG'

    if storage_path:
        log_dir = os.path.join(storage_path, 'logs')
        log_file = os.path.join(log_dir, log_filename)
    else:
        log_dir = os.path.join(project_root, 'logs')

    setup_logger(
        name="agent",
        console_level_name=console_level_name,
        file_level_name=file_level_name,
        log_file_path=log_file
    )

    logger = get_logger("agent.main")
    
    # Check log file status immediately after setup
    log_status = get_file_logging_status()
    
    if log_status["file_logging_enabled"]:
        for log_file_info in log_status["log_files"]:
            logger.info(f"Log file configured at: {log_file_info['path']}")
            if log_file_info.get("exists", False):
                logger.info(f"Log file exists and is {'writable' if log_file_info.get('writable', False) else 'not writable'}")
            else:
                logger.info(f"Log file does not exist yet but will be created when needed")
    else:
        if log_status.get("errors"):
            for error in log_status["errors"]:
                logger.warning(f"Log file error: {error}")
        logger.warning("File logging is not enabled - only logging to console")
        
    logger.info(f"Logging initialized successfully.")

def main() -> int:
    """
    Main function to initialize and start the agent.
    
    :return: Exit code
    :rtype: int
    """
    args = parse_arguments()
    logger = None
    agent_instance = None
    config_manager = None
    state_manager = None
    lock_manager = None

    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        executable_path = sys.executable
    else:
        try:
            executable_path = os.path.abspath(sys.argv[0])
        except IndexError:
            return 1

    is_admin = is_running_as_admin()

    temp_config_path = None
    source_config_path = None
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        base_dir = os.path.dirname(sys.executable)
    else:
        base_dir = project_root
    potential_temp_config = os.path.join(base_dir, 'config', args.config_name)
    if os.path.exists(potential_temp_config):
        temp_config_path = potential_temp_config
        source_config_path = temp_config_path

    else:
        potential_temp_config_alt = os.path.join(base_dir, args.config_name)
        if os.path.exists(potential_temp_config_alt):
            temp_config_path = potential_temp_config_alt
            source_config_path = temp_config_path

    temp_config_manager = ConfigManager(temp_config_path) if temp_config_path else ConfigManager(None)
    app_name_for_registry = temp_config_manager.get('agent.app_name', 'CMSAgent')

    if args.enable_autostart or args.disable_autostart:
        if args.enable_autostart:
            success = register_autostart(app_name_for_registry, executable_path, is_admin)
            return 0 if success else 1
        elif args.disable_autostart:
            success = unregister_autostart(app_name_for_registry, is_admin)
            return 0 if success else 1

    # Initialize basic logging until we have a proper storage path
    setup_logger(
        name="agent",
        console_level_name="DEBUG" if args.debug else "INFO"
    )
    
    logger = get_logger("agent.main")
    logger.info("--- Agent Starting ---")
    
    # Setup directory structure early in the startup process
    try:
        storage_path = setup_directory_structure(app_name_for_registry)
        logger.info(f"Directory structure set up successfully at {storage_path}")
    except ValueError as e:
        logger.critical(f"Failed to set up directory structure: {e}")
        return 1

    try:
        config_file_path = os.path.join(storage_path, args.config_name)

        if not os.path.exists(config_file_path):
            if source_config_path and os.path.exists(source_config_path):
                logger.info(f"Configuration file not found in storage path. Copying from {source_config_path}...")

                try:
                    shutil.copy2(source_config_path, config_file_path)
                    logger.info(f"Successfully copied configuration to {config_file_path}")
                except Exception as copy_err:
                    logger.critical(f"Failed to copy configuration file: {copy_err}", exc_info=True)
                    return 1
            else:
                logger.critical(f"Config file missing in storage and source location.")
                return 1

        config_manager = ConfigManager(config_file_path)
        # No longer need to store storage_path in config as it's determined programmatically
    except FileNotFoundError as e:
        logger.critical(f"Configuration file not found: {e}", exc_info=True)        
        return 1
    except ValueError as e:
        logger.critical(f"Configuration file error: {e}", exc_info=True)
        return 1
    except Exception as e:
        logger.critical(f"Unexpected error during configuration loading: {e}", exc_info=True)
        return 1

    try:
        # Set up the logging system with the proper configuration and storage path
        initialize_logging(config_manager, storage_path, args.debug)
        logger = get_logger("agent.main")
        logger.info(f"Using storage path: {storage_path}")
        logger.info(f"Using configuration file: {config_file_path}")
    except Exception as e:
        logger.critical(f"Unexpected error during logging initialization: {e}", exc_info=True)
        return 1

    try:
        state_manager = StateManager(config_manager)
    except ValueError as e:
        logger.critical(f"Failed to initialize State Manager: {e}")
        return 1
    except Exception as e:
        logger.critical(f"Unexpected error initializing State Manager: {e}", exc_info=True)
        return 1

    proceed_normally = True
    if args.force:
        logger.info("'--force' argument detected. Attempting IPC request to running instance.")

        device_id = state_manager.get_device_id()
        agent_token = state_manager.load_token(device_id) if device_id else None
        
        if agent_token is None:
            agent_token = "123"
            logger.info("No agent token found, using default token for IPC authentication")
        else:
            logger.info(f"Using agent token for IPC authentication: {'Found' if agent_token else 'None'}")

        ipc_response = send_force_command(is_admin, sys.argv, agent_token)
        status = ipc_response.get("status")

        if status == "acknowledged":
            logger.info("Running agent acknowledged restart request. Waiting for lock release (up to 60s)...")
            proceed_normally = False
            wait_start_time = time.monotonic()
            lock_acquired_after_wait = False
            while time.monotonic() - wait_start_time < 60:
                if not lock_manager:
                    try:
                        lock_manager = LockManager(storage_path)
                    except ValueError as e:
                        logger.critical(f"Failed to initialize Lock Manager while waiting for forced restart: {e}")
                        return 1

                if lock_manager.acquire():
                    logger.info("Successfully acquired lock after waiting for forced restart.")
                    lock_acquired_after_wait = True
                    break
                else:
                    logger.debug("Lock still held by previous instance. Waiting...")
                    time.sleep(1)

            if not lock_acquired_after_wait:
                logger.critical("Timed out waiting for previous agent instance to release the lock after --force request. Exiting.")
                return 1

        elif status == "agent_not_running":
            logger.info("No running agent detected via IPC. Proceeding with normal startup.")
            proceed_normally = True
        elif status == "busy_updating":
            logger.warning("Running agent is busy updating. Cannot force restart now. Exiting.")
            return 1
        elif status == "invalid_secret":
            logger.error("IPC request failed: Invalid secret. Check if secrets are corrupted or mismatched. Exiting.")
            return 1
        elif status == "error":
            error_msg = ipc_response.get("message", "Unknown IPC error")
            logger.error(f"IPC request failed: {error_msg}. Exiting.")
            return 1
        else:
            logger.error(f"Received unexpected IPC status: {status}. Exiting.")
            return 1

    if proceed_normally:
        try:
            logger.info("Acquiring instance lock...")
            lock_manager = LockManager(storage_path)
            if not lock_manager.acquire():
                logger.critical("Failed to acquire instance lock. Another instance might be running. Use --force to attempt restart. Exiting.")
                return 1
            logger.info("Instance lock acquired successfully.")
        except ValueError as e:
            logger.critical(f"Failed to initialize Lock Manager: {e}", exc_info=True)
            return 1
        except Exception as e:
            logger.critical(f"Unexpected error acquiring lock: {e}", exc_info=True)
            return 1
    elif not lock_manager:
        logger.critical("Internal error: Lock should have been acquired during --force wait, but manager is None. Exiting.")
        return 1

    try:
        logger.info("Initializing remaining components...")
        state_manager.ensure_device_id()

        http_client = HttpClient(config_manager)
        ws_client = WSClient(config_manager)
        system_monitor = SystemMonitor()

        server_connector = ServerConnector(
            config_manager=config_manager,
            state_manager=state_manager,
            http_client=http_client,
            ws_client=ws_client,
            system_monitor=system_monitor
        )

        command_executor = CommandExecutor(ws_client, config_manager)

        logger.info("Creating Agent instance...")
        agent_instance = Agent(
            config_manager=config_manager,
            state_manager=state_manager,
            ws_client=ws_client,
            command_executor=command_executor,
            lock_manager=lock_manager,
            server_connector=server_connector,
            is_admin=is_admin
        )

        logger.info("Starting Agent main loop...")
        agent_instance.start()

        logger.info("Agent has stopped normally.")
        return 0

    except FileNotFoundError as e:
        logger.critical(f"Configuration file error: {e}", exc_info=True)
        return 1
    except ValueError as e:
        logger.critical(f"Initialization error: {e}", exc_info=True)
        return 1
    except RuntimeError as e:
        logger.critical(f"Agent runtime error during initialization: {e}", exc_info=True)
        return 1
    except KeyboardInterrupt:
        logger.info("Agent startup interrupted by user (Ctrl+C).")
        return 0
    except Exception as e:
        logger.critical(f"An unexpected critical error occurred during initialization: {e}", exc_info=True)
        return 1
    finally:
        if lock_manager:
            lock_manager.release()
        logger.info("--- Agent Exiting ---")

if __name__ == "__main__":
    exit_code = main()
    sys.exit(exit_code)
