"""
Console user interface utilities for the Computer Management System Agent.
Handles terminal-based user interactions for configuration and MFA workflows.
"""
import os
import sys
import getpass
from typing import Optional, Dict, Any


COLORS = {
    'RESET': '\033[0m',
    'RED': '\033[91m',
    'GREEN': '\033[92m',
    'YELLOW': '\033[93m',
    'BLUE': '\033[94m',
    'MAGENTA': '\033[95m',
    'CYAN': '\033[96m',
    'WHITE': '\033[97m',
    'BOLD': '\033[1m'
}


def _supports_color() -> bool:
    """
    Determine if the current terminal supports color output.
    Assumes Windows environment where recent versions support ANSI colors.
    
    :return: True if color is supported, False otherwise
    :rtype: bool
    """
    
    
    try:
        
        import winreg
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, 
                           r'SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion') as key:
            build = int(winreg.QueryValueEx(key, 'CurrentBuildNumber')[0])
            if build >= 14931:
                return True
            
        return False  
    except (ImportError, OSError): 
        
        return False


def colored_text(text: str, color: str) -> str:
    """
    Wraps text with ANSI color codes if supported.
    
    :param text: Text to colorize
    :type text: str
    :param color: Color name from COLORS dict
    :type color: str
    :return: Colorized text if supported, original text otherwise
    :rtype: str
    """
    if not _supports_color() or color not in COLORS:
        return text
    
    return COLORS[color] + text + COLORS['RESET']


def display_error(message: str, error_type: str = "ERROR") -> None:
    """
    Displays an error message to the console with proper formatting.
    
    :param message: The error message to display
    :type message: str
    :param error_type: Type of error for the prefix
    :type error_type: str
    """
    error_prefix = colored_text(f"[{error_type}]", "RED")
    print(f"{error_prefix} {message}", file=sys.stderr)


def display_info(message: str, info_type: str = "INFO") -> None:
    """
    Displays an informational message to the console with proper formatting.
    
    :param message: The message to display
    :type message: str
    :param info_type: Type of info for the prefix
    :type info_type: str
    """
    info_prefix = colored_text(f"[{info_type}]", "BLUE")
    print(f"{info_prefix} {message}")


def display_success(message: str) -> None:
    """
    Displays a success message to the console with proper formatting.
    
    :param message: The success message to display
    :type message: str
    """
    success_prefix = colored_text("[SUCCESS]", "GREEN")
    print(f"{success_prefix} {message}")


def prompt_for_room_config() -> Optional[Dict[str, Any]]:
    """
    Prompts the user to enter room configuration details.
    
    :return: Room configuration dict or None if canceled
    :rtype: Optional[Dict[str, Any]]
    """
    print("\n" + colored_text("=== Room Configuration ===", "BOLD"))
    print("Please enter the following information to configure the agent:")
    
    try:
        room_name = input("Room name: ").strip()
        if not room_name:
            display_error("Room name cannot be empty.")
            return None
        
        pos_x_str = input("Position X coordinate (default: 0): ").strip()
        pos_y_str = input("Position Y coordinate (default: 0): ").strip()
        
        try:
            pos_x = int(pos_x_str) if pos_x_str else 0
            pos_y = int(pos_y_str) if pos_y_str else 0
        except ValueError:
            display_error("Coordinates must be valid numbers. Using defaults (0,0).")
            pos_x, pos_y = 0, 0
        
        return {
            "room": room_name,
            "position": {
                "x": pos_x,
                "y": pos_y
            }
        }
    except KeyboardInterrupt:
        print("\nCanceled by user.")
        return None
    except Exception as e:
        display_error(f"Error during room configuration input: {e}")
        return None


def prompt_for_mfa() -> Optional[str]:
    """
    Prompts the user to enter an MFA verification code.
    
    :return: MFA code or None if canceled
    :rtype: Optional[str]
    """
    print("\n" + colored_text("=== Multi-Factor Authentication ===", "BOLD"))
    
    try:
        print("Please enter the verification code sent to your email or displayed in your authenticator app.")
        mfa_code = input("Verification code: ").strip()
        
        if not mfa_code:
            display_error("Verification code cannot be empty.")
            return None
        
        return mfa_code
    except KeyboardInterrupt:
        print("\nCanceled by user.")
        return None
    except Exception as e:
        display_error(f"Error during MFA input: {e}")
        return None
