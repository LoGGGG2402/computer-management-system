"""
Console User Interface module for the Computer Management System Agent.
Handles interactions like prompting for MFA codes and room configuration.
"""
from typing import Optional, Dict, Any, TYPE_CHECKING

if TYPE_CHECKING:
    from ..config import StateManager

from ..utils import get_logger

logger = get_logger(__name__)

def prompt_for_mfa() -> Optional[str]:
    """
    Prompts the user to enter an MFA code via the console.

    Handles basic input validation (non-empty). Allows alphanumeric codes.

    :return: The MFA code entered by the user, or None if input was cancelled
    :rtype: Optional[str]
    """
    print("\n" + "="*50)
    print("COMPUTER MANAGEMENT SYSTEM - AGENT REGISTRATION")
    print("="*50)
    print("\nMulti-Factor Authentication (MFA) code is required to register this agent.")
    print("Please contact your system administrator to obtain the MFA code.")
    print("Once you have the code, enter it below.\n")

    while True:
        try:
            mfa_code = input("Enter MFA code: ").strip()

            if not mfa_code:
                print("MFA code cannot be empty. Please try again.")
                continue

            logger.info("MFA code entered by user.")
            return mfa_code

        except (KeyboardInterrupt, EOFError):
            logger.warning("MFA input cancelled by user.")
            print("\nMFA input has been cancelled.")
            return None
        except Exception as e:
            logger.error(f"Error during MFA input: {e}", exc_info=True)
            print("An error occurred. Please try again.")

def display_registration_success() -> None:
    """
    Displays a success message to the console after agent registration.
    """
    print("\n" + "="*50)
    print("AGENT REGISTRATION SUCCESSFUL")
    print("="*50)
    print("\nThis agent has been successfully registered with the management system.")
    print("The agent will now connect to the server and begin monitoring.\n")

    logger.info("Agent registration successful - success message displayed to user.")

def is_room_config_valid(config: Optional[Dict[str, Any]]) -> bool:
    """
    Checks if the provided room config dictionary is valid.
    
    :param config: The room configuration dictionary to validate
    :type config: Optional[Dict[str, Any]]
    :return: True if configuration is valid, False otherwise
    :rtype: bool
    """
    return (
        config and isinstance(config, dict) and
        isinstance(config.get('room'), str) and config['room'].strip() and
        isinstance(config.get('position'), dict) and
        isinstance(config['position'].get('x'), int) and config['position']['x'] >= 0 and
        isinstance(config['position'].get('y'), int) and config['position']['y'] >= 0
    )

def prompt_room_config_ui() -> Optional[Dict[str, Any]]:
    """
    Prompts the user for room configuration via console with validation.
    
    :return: The validated room configuration or None if cancelled
    :rtype: Optional[Dict[str, Any]]
    """
    print("\n" + "="*50)
    print("ROOM CONFIGURATION - COMPUTER MANAGEMENT SYSTEM")
    print("="*50)
    print("\nPlease enter the room configuration information for this computer.")
    print("This information is used to locate the computer on the management map.")
    print("Press Ctrl+C to cancel.\n")

    while True:
        try:
            room = input("Enter Room Identifier (e.g., Lab01, OfficeA): ").strip()
            if not room:
                print("Error: Room identifier cannot be empty. Please try again.")
                continue

            while True:
                pos_x_str = input("Enter X position in room (integer >= 0): ").strip()
                if pos_x_str.isdigit():
                    pos_x = int(pos_x_str)
                    break
                else:
                    print("Error: X position must be a non-negative integer. Please try again.")

            while True:
                pos_y_str = input("Enter Y position in room (integer >= 0): ").strip()
                if pos_y_str.isdigit():
                    pos_y = int(pos_y_str)
                    break
                else:
                    print("Error: Y position must be a non-negative integer. Please try again.")

            config = {'room': room, 'position': {'x': pos_x, 'y': pos_y}}
            logger.info(f"Room configuration entered by user: {config}")
            
            if is_room_config_valid(config):
                 return config
            else:
                 logger.error(f"Internal validation failed for entered config: {config}")
                 print("Internal error: Configuration data is invalid. Please try again.")
                 continue

        except (KeyboardInterrupt, EOFError):
            logger.warning("Room configuration input cancelled by user.")
            print("\nRoom configuration input has been cancelled.")
            return None
        except Exception as e:
            logger.error(f"Error during room config input: {e}", exc_info=True)
            print(f"An unexpected error occurred: {e}. Please try again.")

def get_or_prompt_room_config(state_manager: 'StateManager') -> Optional[Dict[str, Any]]:
    """
    Gets room config from StateManager. If missing or invalid, prompts the user
    via the UI function and saves the new config via StateManager.

    :param state_manager: The state manager instance to use for loading/saving
    :type state_manager: StateManager
    :return: The valid room configuration, or None if it cannot be obtained
    :rtype: Optional[Dict[str, Any]]
    """
    if not state_manager:
        logger.critical("StateManager instance is required for get_or_prompt_room_config.")
        return None

    current_config = state_manager.get_room_config()

    if is_room_config_valid(current_config):
        logger.info(f"Loaded valid Room Configuration: {current_config}")
        return current_config
    elif current_config:
         logger.warning(f"Existing room configuration in state file is invalid: {current_config}. Prompting user.")
    else:
         logger.info("Room configuration not found in state file. Prompting user...")

    new_config = prompt_room_config_ui()

    if new_config:
        if is_room_config_valid(new_config):
            logger.info("Saving newly prompted room configuration...")
            if state_manager.save_room_config(new_config):
                logger.info(f"Saved new room configuration: {new_config}")
                return new_config
            else:
                logger.error("Failed to save new room configuration via StateManager. Using for this session only.")
                return new_config
        else:
             logger.error(f"Configuration entered by user is invalid (post-prompt validation failed): {new_config}. Cannot proceed.")
             return None
    else:
        logger.critical("Room configuration prompt was cancelled by the user.")
        return None
