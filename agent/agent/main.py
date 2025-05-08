"""
Main entry point for the Computer Management System Agent.
This script handles command-line arguments for service management,
agent configuration, and other utilities.
"""
import argparse
import logging
import os
import sys



current_script_path = os.path.abspath(__file__)
agent_dir = os.path.dirname(current_script_path) 
project_root_for_agent_module = os.path.dirname(agent_dir) 

if project_root_for_agent_module not in sys.path:
    sys.path.insert(0, project_root_for_agent_module)
if agent_dir not in sys.path: 
    sys.path.insert(0, agent_dir)

from agent.config import ConfigManager, StateManager
from agent.communication import HttpClient
from agent.system.windows_utils import get_executable_path
from agent.utils.logger import setup_logger, get_logger
from agent.system.directory_utils import determine_storage_path, setup_directory_structure
from agent.ui import ui_console


from agent.system.agent_service_handler import (
    run_service,
    AGENT_SERVICE_NAME,
    AGENT_SHUTDOWN_EVENT_NAME
)
from agent.system.windows_sync import open_named_event, set_named_event, close_handle
import win32event 


AGENT_CONFIG_FILENAME = "agent_config.json"
PROGRAMDATA_LOG_DIR_NAME = "logs" 



logger = get_logger("agent_main")

def _get_cli_config_file_path() -> str:
    """Determines the path to agent_config.json for CLI tools, relative to the executable."""
    exe_dir = os.path.dirname(get_executable_path()) 
    config_path = os.path.join(exe_dir, "config", AGENT_CONFIG_FILENAME)
    return config_path

def _run_configure_command(args: argparse.Namespace):
    """Handles the 'configure' CLI command."""
    
    log_dir_base = determine_storage_path() 
    log_dir = os.path.join(log_dir_base, PROGRAMDATA_LOG_DIR_NAME)
    try:
        os.makedirs(log_dir, exist_ok=True)
        
        cli_logger, file_log_ok = setup_logger(
            name="agent_cli_configure",
            log_directory_path=log_dir,
            level=logging.INFO, 
            for_cli=True
        )
        if not file_log_ok:
            cli_logger.warning(f"File logging for configure command could not be established at {log_dir}")
    except Exception as e:
        print(f"ERROR: Could not set up logging: {e}", file=sys.stderr)
        
        cli_logger = get_logger("agent_cli_configure") 
        cli_logger.error(f"Failed to initialize file logging: {e}")

    cli_logger.info("Starting agent configuration process...")
    cli_logger.info(f"Room: {args.room}, Pos X: {args.pos_x}, Pos Y: {args.pos_y}, MFA from arg: {'Yes' if args.mfa_code else 'No'}")

    try:
        
        setup_directory_structure()
        
        config_file_path = _get_cli_config_file_path()
        if not os.path.exists(config_file_path):
            cli_logger.error(f"Configuration file {AGENT_CONFIG_FILENAME} not found at {config_file_path}.")
            print(f"ERROR: Configuration file {AGENT_CONFIG_FILENAME} not found at expected location: {config_file_path}", file=sys.stderr)
            sys.exit(1)
        
        config_manager = ConfigManager(config_file_path=config_file_path)
        state_manager = StateManager() 
        http_client = HttpClient(config_manager, state_manager) 

        device_id = state_manager.ensure_device_id()
        cli_logger.info(f"Ensured Device ID: {device_id}")

        
        room_config = {
            "room": args.room, 
            "pos_x": str(args.pos_x), 
            "pos_y": str(args.pos_y)
        }
        
        
        
        identify_payload_room_config = {
            "roomName": args.room,
            "posX": str(args.pos_x),
            "posY": str(args.pos_y)
        }

        cli_logger.info("Attempting to identify agent with the server...")
        
        api_call_success, response_data = http_client.identify_agent(device_id, identify_payload_room_config) 

        if not api_call_success:
            
            error_msg = response_data.get("message", "Failed to identify agent: Connection error or invalid response from server.")
            cli_logger.error(f"Agent identification API call failed. Server/Connection Message: {error_msg}")
            print(f"ERROR: {error_msg}", file=sys.stderr)
            sys.exit(1)

        
        agent_token: str = ""
        server_status = response_data.get("status")
        server_message = response_data.get("message", "")

        if server_status == "success":
            agent_token = response_data.get("agentToken", "")
            if agent_token:
                cli_logger.info("Agent identified successfully. New token received.")
            else:
                cli_logger.info("Agent identified successfully by server (already registered). No new token issued.")
        elif server_status == "mfa_required":
            cli_logger.info("MFA is required by the server.")
            mfa_code = args.mfa_code
            if not mfa_code:
                cli_logger.info("MFA code not provided via arguments, prompting user.")
                try:
                    mfa_code = ui_console.prompt_for_mfa()
                    if not mfa_code: 
                        cli_logger.warning("User did not provide an MFA code at the prompt.")
                        print("MFA code is required but was not provided. Configuration aborted.", file=sys.stderr)
                        sys.exit(1)
                except Exception as e:
                    cli_logger.error(f"Error during MFA prompt: {e}", exc_info=True)
                    print(f"An error occurred while prompting for MFA: {e}", file=sys.stderr)
                    sys.exit(1)
            
            cli_logger.info("Verifying MFA code with the server...")
            mfa_api_call_success, mfa_response_data = http_client.verify_mfa(device_id, mfa_code)

            if not mfa_api_call_success:
                error_msg = mfa_response_data.get("message", "MFA verification failed: Connection error or invalid server response.")
                cli_logger.error(f"MFA verification API call failed. Server/Connection Message: {error_msg}")
                print(f"ERROR: {error_msg}", file=sys.stderr)
                sys.exit(1)

            if mfa_response_data.get("status") == "success":
                agent_token = mfa_response_data.get("agentToken", "")
                if not agent_token:
                    cli_logger.error("MFA verification reported success by server but no token was provided.")
                    print("ERROR: MFA successful, but no token received. Configuration cannot proceed.", file=sys.stderr)
                    sys.exit(1)
                cli_logger.info("MFA verification successful. Token received.")
            else: 
                error_msg = "MFA verification failed."
                if mfa_response_data and mfa_response_data.get("message"):
                    error_msg += f" Server message: {mfa_response_data['message']}"
                else:
                    error_msg += f" Server status: {mfa_response_data.get('status', 'Unknown')}"
                cli_logger.error(error_msg)
                print(f"ERROR: {error_msg}", file=sys.stderr)
                sys.exit(1)
        elif server_status == "position_error":
            error_msg = f"Position error: {server_message}" if server_message else "Invalid position for agent."
            cli_logger.error(error_msg)
            print(f"ERROR: {error_msg}", file=sys.stderr)
            sys.exit(1)
        elif server_status == "error": 
            error_msg = f"Server error: {server_message}" if server_message else "An unknown error occurred on the server during identification."
            cli_logger.error(error_msg)
            print(f"ERROR: {error_msg}", file=sys.stderr)
            sys.exit(1)
        else: 
            cli_logger.error(f"Unexpected status from server during identification: {server_status}. Response: {response_data}")
            print(f"ERROR: Unexpected response from server: {server_status}", file=sys.stderr)
            sys.exit(1)

        
        
        state_manager_room_config = {
            "name": args.room, 
            "pos_x": str(args.pos_x), 
            "pos_y": str(args.pos_y)
        }
        if state_manager.save_room_config(state_manager_room_config):
            cli_logger.info(f"Room configuration saved: {state_manager_room_config}")
        else:
            cli_logger.error("Failed to save room configuration.")
            print("ERROR: Could not save room configuration to state file.", file=sys.stderr)
            sys.exit(1) 

        if agent_token: 
            if state_manager.save_token(device_id, agent_token):
                cli_logger.info("Agent token saved successfully.")
            else:
                cli_logger.error("Failed to save agent token.")
                print("ERROR: Could not save agent token to state file / keyring.", file=sys.stderr)
                sys.exit(1) 
            print("Agent configured successfully and new token saved.")
        elif server_status == "success": 
             cli_logger.info("Agent successfully identified with the server. No new token issued (agent likely already registered with a valid token). Room configuration has been updated locally.")
             print("Agent identified successfully with server. Room configuration updated. Existing token (if any) remains in use.")
        
        cli_logger.info("Agent configuration process completed.")
        sys.exit(0)

    except Exception as e:
        cli_logger.critical(f"An unexpected error occurred during configuration: {e}", exc_info=True)
        print(f"FATAL ERROR during configuration: {e}", file=sys.stderr)
        sys.exit(1)

def _run_force_restart_command(args: argparse.Namespace):
    """Handles the 'force-restart' CLI command."""
    
    cli_logger, _ = setup_logger(name="agent_cli_force_restart", level=logging.INFO, for_cli=True)

    cli_logger.info(f"Attempting to signal agent service to restart using event: {AGENT_SHUTDOWN_EVENT_NAME}")
    event_handle = None
    try:
        
        event_handle = open_named_event(AGENT_SHUTDOWN_EVENT_NAME, desired_access=win32event.EVENT_MODIFY_STATE)
        if not event_handle:
            cli_logger.error(f"Failed to open named event '{AGENT_SHUTDOWN_EVENT_NAME}'. The service might not be running or accessible.")
            print(f"ERROR: Could not open the shutdown event. Ensure the agent service is installed and running.", file=sys.stderr)
            sys.exit(1)

        if set_named_event(event_handle):
            cli_logger.info(f"Successfully signaled event '{AGENT_SHUTDOWN_EVENT_NAME}'. Agent service should restart shortly.")
            print("Force restart signal sent to the agent service.")
            sys.exit(0)
        else:
            cli_logger.error(f"Failed to set named event '{AGENT_SHUTDOWN_EVENT_NAME}'.")
            print("ERROR: Failed to send the restart signal to the agent service.", file=sys.stderr)
            sys.exit(1)
    except Exception as e:
        cli_logger.error(f"Error during force-restart operation: {e}", exc_info=True)
        print(f"ERROR: An unexpected error occurred: {e}", file=sys.stderr)
        sys.exit(1)
    finally:
        if event_handle:
            close_handle(event_handle)

def main():
    """
    Main function to parse arguments and dispatch commands.
    """
    parser = argparse.ArgumentParser(description="Computer Management System Agent CLI.")
    subparsers = parser.add_subparsers(dest='command', help='Available commands')

    
    configure_parser = subparsers.add_parser('configure', help='Configure agent room and authentication.')
    configure_parser.add_argument('--room', required=True, help='Name of the room the computer is in.')
    configure_parser.add_argument('--pos-x', default='0', help='X position in the room map (optional).')
    configure_parser.add_argument('--pos-y', default='0', help='Y position in the room map (optional).')
    configure_parser.add_argument('--mfa-code', help='MFA code if required by the server (optional).')
    configure_parser.set_defaults(func=_run_configure_command)

    
    restart_parser = subparsers.add_parser('force-restart', help='Signal the running agent service to restart.')
    restart_parser.set_defaults(func=_run_force_restart_command)

    if len(sys.argv) > 1 and sys.argv[1] in ['configure', 'force-restart']:
        args = parser.parse_args()
        if hasattr(args, 'func'):
            args.func(args)
        else: 
            parser.print_help()
            sys.exit(1)
    else: 
        run_service()

if __name__ == '__main__':
    try:
        setup_logger(name="agent_bootstrap", level=logging.INFO, for_cli=True)
    except Exception as e:
        print(f"Initial bootstrap logging setup failed: {e}", file=sys.stderr)
        logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        logger.error(f"Initial bootstrap logging setup failed: {e}", exc_info=True)

    main()
