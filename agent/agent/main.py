#!/usr/bin/env python3
"""
Main entry point for the Computer Management System Agent.
"""
import os
import sys
import argparse
import shutil
import time
import logging
import logging.handlers

from agent.ipc import send_force_command

current_dir = os.path.dirname(os.path.abspath(__file__))
project_root = os.path.dirname(current_dir)
if project_root not in sys.path:
    sys.path.insert(0, project_root)
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

from agent.utils import setup_logger, get_logger
from agent.ui.ui_console import display_error

from agent.system import (
    LockManager,
    is_running_as_admin,
    register_autostart,
    unregister_autostart,
    setup_directory_structure,
)
from . import (
    ConfigManager,
    StateManager,
    HttpClient,
    WSClient,
    ServerConnector,
    SystemMonitor,
    Agent,
    CommandExecutor,
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Computer Management System Agent")
    default_config_name = "agent_config.json"
    parser.add_argument(
        "--config-name",
        type=str,
        default=default_config_name,
        help=f"Name of the agent configuration file within the storage directory (default: {default_config_name})",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug logging level FOR CONSOLE output.",
    )
    parser.add_argument(
        "--enable-autostart",
        action="store_true",
        help="Register the agent to start automatically with Windows (requires appropriate privileges) and exit.",
    )
    parser.add_argument(
        "--disable-autostart",
        action="store_true",
        help="Unregister the agent from starting automatically with Windows (requires appropriate privileges) and exit.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Request the currently running agent instance (if any) to shut down and then start this instance. Uses IPC.",
    )
    return parser.parse_args()


def main() -> int:
    """
    Main function to initialize and start the agent.

    :return: Exit code
    :rtype: int
    """
    # Step 1: Parse arguments (needed early for default app name and debug flag)
    args = parse_arguments()
    logger = None
    agent_instance = None
    config_manager = None
    state_manager = None
    lock_manager = None
    storage_path = None
    is_admin = is_running_as_admin()
    
    # Default app name in case config loading fails
    app_name_for_registry = "CMSAgent"
    
    # Try to get app name from config if available
    temp_config_path = None
    source_config_path = None
    if getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS"):
        executable_path = sys.executable
        base_dir = os.path.dirname(sys.executable)
    else:
        try:
            executable_path = os.path.abspath(sys.argv[0])
            base_dir = project_root
        except IndexError:
            display_error("Could not determine executable path.", "CRITICAL")
            return 1
    
    potential_temp_config = os.path.join(base_dir, "config", args.config_name)
    if os.path.exists(potential_temp_config):
        temp_config_path = potential_temp_config
        source_config_path = temp_config_path
    else:
        potential_temp_config_alt = os.path.join(base_dir, args.config_name)
        if os.path.exists(potential_temp_config_alt):
            temp_config_path = potential_temp_config_alt
            source_config_path = temp_config_path
    
    try:
        temp_config_manager = ConfigManager(temp_config_path) if temp_config_path else ConfigManager(None)
        app_name_for_registry = temp_config_manager.get("agent.app_name", "CMSAgent")
    except Exception as e:
        print(f"WARNING: Using default app name '{app_name_for_registry}' due to config load error: {e}", file=sys.stderr)

    # Step 2: FIRST PRIORITY - Set up directory structure
    try:
        storage_path = setup_directory_structure(app_name_for_registry)
        log_dir = os.path.join(storage_path, "logs")
        print(f"INFO: Storage path determined: {storage_path}")
        print(f"INFO: Log directory target: {log_dir}")
    except ValueError as e:
        display_error(f"Failed to set up storage directory structure: {e}", "CRITICAL")
        return 1
    except Exception as e:
        display_error(f"Unexpected error setting up storage directory: {e}", "CRITICAL")
        return 1

    # Step 3: SECOND PRIORITY - Initialize logger
    file_log_success = False
    try:
        logger, file_log_success = setup_logger(name="agent", log_directory_path=log_dir)
        
        if args.debug:
            console_handler_found = False
            for handler in logger.handlers:
                if isinstance(handler, logging.StreamHandler) and not isinstance(handler, logging.FileHandler):
                    handler.setLevel(logging.DEBUG)
                    logger.setLevel(min(logger.level, logging.DEBUG))
                    logger.debug("Console logging level set to DEBUG via --debug flag.")
                    console_handler_found = True
                    break
            if not console_handler_found:
                logger.warning("Could not find console handler to set DEBUG level via --debug flag.")

        logger.info("--- Agent Starting ---")
        if file_log_success:
            actual_log_path = "N/A"
            for handler in logger.handlers:
                if isinstance(handler, logging.handlers.RotatingFileHandler):
                    actual_log_path = handler.baseFilename
                    break
            logger.info(f"Logging initialized. File logging ENABLED to: {actual_log_path}")
        else:
            logger.warning(f"Logging initialized. File logging DISABLED (Check permissions/path: {log_dir}).")
    except Exception as e:
        display_error(f"Failed during logging initialization: {e}", "CRITICAL")
        if logger:
            logger.critical(f"Failed during logging initialization: {e}", exc_info=True)
        return 1
    
    # Handle autostart arguments if provided (early exit)
    if args.enable_autostart or args.disable_autostart:
        temp_logger = get_logger("agent.autostart")
        if args.enable_autostart:
            temp_logger.info(f"Registering autostart for {app_name_for_registry} at {executable_path}")
            success = register_autostart(app_name_for_registry, executable_path, is_admin)
            temp_logger.info(f"Autostart registration {'succeeded' if success else 'failed'}.")
            return 0 if success else 1
        elif args.disable_autostart:
            temp_logger.info(f"Unregistering autostart for {app_name_for_registry}")
            success = unregister_autostart(app_name_for_registry, is_admin)
            temp_logger.info(f"Autostart unregistration {'succeeded' if success else 'failed'}.")
            return 0 if success else 1

    # Step 4: Load configuration
    try:
        config_file_path = os.path.join(storage_path, args.config_name)
        if not os.path.exists(config_file_path):
            if source_config_path and os.path.exists(source_config_path):
                logger.info(f"Configuration file not found... Copying from {source_config_path}...")
                try:
                    shutil.copy2(source_config_path, config_file_path)
                    logger.info(f"Successfully copied config to {config_file_path}")
                except Exception as copy_err:
                    logger.critical(f"Failed to copy config file: {copy_err}", exc_info=True)
                    display_error(f"Failed to copy config file: {copy_err}", "CRITICAL")
                    return 1
            else:
                logger.critical(f"Config file missing in storage ('{config_file_path}') and source ('{source_config_path or 'N/A'}').")
                display_error(f"Config file missing in storage ('{config_file_path}') and source ('{source_config_path or 'N/A'}').", "CRITICAL")
                return 1
        config_manager = ConfigManager(config_file_path)
        logger.info(f"Using config file: {config_file_path}")
    except FileNotFoundError as e:
        logger.critical(f"Config file error: {e}", exc_info=True)
        display_error(f"Config file error: {e}", "CONFIG ERROR")
        return 1
    except ValueError as e:
        logger.critical(f"Config file error: {e}", exc_info=True)
        display_error(f"Config file error: {e}", "CONFIG ERROR")
        return 1
    except Exception as e:
        logger.critical(f"Unexpected error during config loading: {e}", exc_info=True)
        display_error(f"Unexpected error during config loading: {e}", "CONFIG ERROR")
        return 1

    # Step 5: Initialize state manager
    try:
        state_manager = StateManager(config_manager)
    except ValueError as e:
        logger.critical(f"Failed to initialize State Manager: {e}")
        display_error(f"Failed to initialize State Manager: {e}", "STATE ERROR")
        return 1
    except Exception as e:
        logger.critical(f"Unexpected error initializing State Manager: {e}", exc_info=True)
        display_error(f"Unexpected error initializing State Manager: {e}", "STATE ERROR")
        return 1

    # Handle force flag if provided
    proceed_normally = True
    if args.force:
        logger.info("'--force' argument detected. Attempting IPC request...")
        device_id = state_manager.get_device_id()
        agent_token = state_manager.load_token(device_id) if device_id else None
        if agent_token is None:
            agent_token = "FORCE_IPC_NO_TOKEN"
            logger.warning("No agent token found for IPC...")
        else:
            logger.info(f"Using agent token for IPC authentication.")
        ipc_response = send_force_command(is_admin, sys.argv, agent_token)
        status = ipc_response.get("status")
        if status == "acknowledged":
            logger.info("Running agent acknowledged restart request. Waiting for lock release (up to 60s)...")
            proceed_normally = True
            wait_start_time = time.monotonic()
            lock_acquired_after_wait = False
            temp_lock_manager = None
            while time.monotonic() - wait_start_time < 60:
                if not temp_lock_manager:
                    try:
                        temp_lock_manager = LockManager(storage_path)
                    except ValueError as e:
                        logger.error(f"Failed to initialize Lock Manager during wait: {e}")
                        time.sleep(1)
                        continue
                if temp_lock_manager.acquire():
                    logger.info("Successfully acquired lock after waiting...")
                    temp_lock_manager.release()
                    lock_acquired_after_wait = True
                    break
                else:
                    logger.debug("Lock still held... Waiting...")
                    time.sleep(1)
            if not lock_acquired_after_wait:
                logger.critical("Timed out waiting for lock release after --force. Exiting.")
                display_error("Timed out waiting for lock release after --force.", "LOCK ERROR")
                return 1
        elif status == "agent_not_running":
            logger.info("No running agent detected via IPC. Proceeding...")
            proceed_normally = True
        elif status == "busy_updating":
            logger.warning("Running agent is busy updating. Cannot force restart. Exiting.")
            display_error("Running agent is busy updating. Cannot force restart.", "BUSY ERROR")
            return 1
        elif status == "invalid_token":
            logger.error("IPC request failed: Invalid token. Exiting.")
            display_error("IPC request failed: Invalid token.", "IPC ERROR")
            return 1
        elif status == "error":
            error_msg = ipc_response.get("message", "Unknown IPC error")
            logger.error(f"IPC request failed: {error_msg}. Exiting.")
            display_error(f"IPC request failed: {error_msg}.", "IPC ERROR")
            return 1
        else:
            logger.error(f"Received unexpected IPC status: {status}. Exiting.")
            display_error(f"Received unexpected IPC status: {status}.", "IPC ERROR")
            return 1

    # Step 6: Acquire instance lock
    if proceed_normally:
        try:
            logger.info("Acquiring instance lock...")
            lock_manager = LockManager(storage_path)
            if not lock_manager.acquire():
                logger.critical("Failed to acquire instance lock. Another instance running? Use --force. Exiting.")
                display_error("Failed to acquire instance lock. Another instance running? Use --force.", "LOCK ERROR")
                return 1
            logger.info("Instance lock acquired successfully.")
        except ValueError as e:
            logger.critical(f"Failed to initialize Lock Manager: {e}", exc_info=True)
            display_error(f"Failed to initialize Lock Manager: {e}", "LOCK ERROR")
            return 1
        except Exception as e:
            logger.critical(f"Unexpected error acquiring lock: {e}", exc_info=True)
            display_error(f"Unexpected error acquiring lock: {e}", "LOCK ERROR")
            return 1

    # Step 7: Start the agent
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
            system_monitor=system_monitor,
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
            is_admin=is_admin,
        )
        logger.info("Starting Agent main loop...")
        agent_instance.start()
        logger.info("Agent main loop finished.")
        return 0

    except FileNotFoundError as e:
        logger.critical(f"Configuration file error after startup: {e}", exc_info=True)
        display_error(f"Configuration file error after startup: {e}", "CONFIG ERROR")
        return 1
    except ValueError as e:
        logger.critical(f"Initialization error: {e}", exc_info=True)
        display_error(f"Initialization error: {e}", "STARTUP ERROR")
        return 1
    except RuntimeError as e:
        logger.critical(f"Agent runtime error during initialization: {e}", exc_info=True)
        display_error(f"Agent runtime error during initialization: {e}", "RUNTIME ERROR")
        return 1
    except KeyboardInterrupt:
        logger.info("Agent startup/run interrupted by user (Ctrl+C). Initiating shutdown.")
        if agent_instance:
            agent_instance.graceful_shutdown()
        return 0
    except Exception as e:
        logger.critical(f"An unexpected critical error occurred during agent run: {e}", exc_info=True)
        display_error(f"An unexpected critical error occurred during agent run: {e}", "CRITICAL")
        return 1
    finally:
        if lock_manager and (not agent_instance or not agent_instance._running.is_set()):
            logger.info("Performing final lock release check in main.")
            lock_manager.release()
        logging.shutdown()
        print("--- Agent Exiting ---")


if __name__ == "__main__":
    exit_code = main()
    sys.exit(exit_code)
