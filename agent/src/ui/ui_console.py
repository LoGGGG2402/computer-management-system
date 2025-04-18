# -*- coding: utf-8 -*-
"""
Console User Interface module for the Computer Management System Agent.
Handles interactions like prompting for MFA codes and room configuration.
"""
from typing import Optional, Dict, Any

# Import StateManager for type hinting, will be passed as argument
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from src.config.state_manager import StateManager

# Import get_logger from the centralized logger
from src.utils.logger import get_logger

# Get a properly configured logger instance
logger = get_logger(__name__)

# --- MFA Related Functions ---

def prompt_for_mfa() -> Optional[str]:
    """
    Prompts the user to enter an MFA code via the console.

    Handles basic input validation (non-empty). Allows alphanumeric codes.

    Returns:
        Optional[str]: The MFA code entered by the user, or None if input was cancelled (Ctrl+C/EOF).
    """
    print("\n" + "="*50)
    print("COMPUTER MANAGEMENT SYSTEM - AGENT REGISTRATION")
    print("="*50)
    print("\nYêu cầu mã Xác thực Đa yếu tố (MFA) để đăng ký agent này.")
    print("Vui lòng liên hệ quản trị viên hệ thống để nhận mã MFA.")
    print("Sau khi nhận được mã, hãy nhập mã đó vào bên dưới.\n")

    while True:
        try:
            mfa_code = input("Nhập mã MFA: ").strip()

            if not mfa_code:
                print("Mã MFA không được để trống. Vui lòng thử lại.")
                continue

            # No longer checking isdigit() - allows alphanumeric codes
            logger.info("MFA code entered by user.")
            return mfa_code

        except (KeyboardInterrupt, EOFError):
            logger.warning("MFA input cancelled by user.")
            print("\nThao tác nhập MFA đã bị hủy.")
            return None # Indicate cancellation
        except Exception as e:
            logger.error(f"Error during MFA input: {e}", exc_info=True)
            print("Đã xảy ra lỗi. Vui lòng thử lại.")
            # Loop continues

def display_registration_success() -> None:
    """Displays a success message to the console after agent registration."""
    print("\n" + "="*50)
    print("ĐĂNG KÝ AGENT THÀNH CÔNG")
    print("="*50)
    print("\nAgent này đã được đăng ký thành công với hệ thống quản lý.")
    print("Agent bây giờ sẽ kết nối đến máy chủ và bắt đầu giám sát.\n")

    logger.info("Agent registration successful - success message displayed to user.")

# --- Room Configuration Related Functions ---

def is_room_config_valid(config: Optional[Dict[str, Any]]) -> bool:
    """Checks if the provided room config dictionary is valid."""
    return (
        config and isinstance(config, dict) and # Check if it's a dict first
        isinstance(config.get('room'), str) and config['room'].strip() and # Room name must be non-empty string
        isinstance(config.get('position'), dict) and
        # Allow 0 as a valid position
        isinstance(config['position'].get('x'), int) and config['position']['x'] >= 0 and # Ensure non-negative ints
        isinstance(config['position'].get('y'), int) and config['position']['y'] >= 0
    )

def prompt_room_config_ui() -> Optional[Dict[str, Any]]:
    """Prompts the user for room configuration via console with validation."""
    print("\n" + "="*50)
    print("CẤU HÌNH PHÒNG - HỆ THỐNG QUẢN LÝ MÁY TÍNH")
    print("="*50)
    print("\nVui lòng nhập thông tin cấu hình phòng cho máy tính này.")
    print("Thông tin này được sử dụng để định vị máy tính trên sơ đồ quản lý.")
    print("Nhấn Ctrl+C để hủy bỏ.\n")

    while True:
        try:
            # Prompt for Room Name
            room = input("Nhập Định danh Phòng (ví dụ: Lab01, OfficeA): ").strip()
            if not room:
                print("Lỗi: Định danh phòng không được để trống. Vui lòng thử lại.")
                continue

            # Prompt for Position X
            while True:
                pos_x_str = input("Nhập Vị trí X trong phòng (số nguyên >= 0): ").strip()
                if pos_x_str.isdigit():
                    pos_x = int(pos_x_str)
                    break
                else:
                    print("Lỗi: Vị trí X phải là một số nguyên không âm. Vui lòng thử lại.")

            # Prompt for Position Y
            while True:
                pos_y_str = input("Nhập Vị trí Y trong phòng (số nguyên >= 0): ").strip()
                if pos_y_str.isdigit():
                    pos_y = int(pos_y_str)
                    break
                else:
                    print("Lỗi: Vị trí Y phải là một số nguyên không âm. Vui lòng thử lại.")

            # Construct and return valid config
            config = {'room': room, 'position': {'x': pos_x, 'y': pos_y}}
            logger.info(f"Room configuration entered by user: {config}")
            # Basic validation before returning
            if is_room_config_valid(config):
                 return config
            else:
                 # Should not happen with the input loops above, but as a safeguard
                 logger.error(f"Internal validation failed for entered config: {config}")
                 print("Lỗi nội bộ: Dữ liệu cấu hình không hợp lệ. Vui lòng thử lại.")
                 continue


        except (KeyboardInterrupt, EOFError):
            logger.warning("Room configuration input cancelled by user.")
            print("\nThao tác nhập cấu hình phòng đã bị hủy.")
            return None
        except Exception as e: # Catch unexpected errors during input
            logger.error(f"Error during room config input: {e}", exc_info=True)
            print(f"Đã xảy ra lỗi không mong muốn: {e}. Vui lòng thử lại.")
            # Loop continues


def get_or_prompt_room_config(state_manager: 'StateManager') -> Optional[Dict[str, Any]]:
    """
    Gets room config from StateManager. If missing or invalid, prompts the user
    via the UI function and saves the new config via StateManager.

    Args:
        state_manager (StateManager): The state manager instance to use for loading/saving.

    Returns:
        Optional[Dict[str, Any]]: The valid room configuration, or None if it cannot be obtained.
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

    # Prompt user for new config using the UI function
    new_config = prompt_room_config_ui()

    if new_config:
        # Validate the newly prompted config (redundant check, but safe)
        if is_room_config_valid(new_config):
            logger.info("Saving newly prompted room configuration...")
            if state_manager.save_room_config(new_config):
                logger.info(f"Saved new room configuration: {new_config}")
                return new_config
            else:
                logger.error("Failed to save new room configuration via StateManager. Using for this session only.")
                # Still return it, but it won't persist
                return new_config
        else:
             # This case should ideally be prevented by validation within prompt_room_config_ui
             logger.error(f"Configuration entered by user is invalid (post-prompt validation failed): {new_config}. Cannot proceed.")
             return None
    else:
        # User cancelled prompt
        logger.critical("Room configuration prompt was cancelled by the user.")
        return None # Indicate cancellation/failure
