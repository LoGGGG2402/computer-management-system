# -*- coding: utf-8 -*-
"""
Windows specific utility functions for the agent.
"""
import ctypes
import os
import sys
import uuid
from typing import Optional # Added Optional

# Import the centralized logger
from src.utils.logger import get_logger

try:
    import winreg
    import win32security
    WINDOWS_ACL_SUPPORT = True
except ImportError:
    WINDOWS_ACL_SUPPORT = False
    get_logger(__name__).warning("win32security module not found. ACL functionality will be limited.")
    winreg = None
    get_logger(__name__).warning("winreg module not found. Autostart functionality will be disabled.")

# Get a properly configured logger instance
logger = get_logger(__name__)

# --- Constants ---

def is_running_as_admin() -> bool:
    """
    Checks if the current process is running with administrative privileges.

    Returns:
        bool: True if running as admin, False otherwise.
    """
    try:
        is_admin = (os.getuid() == 0) # Linux/macOS check (will fail on Windows)
    except AttributeError:
        # Windows check
        try:
            is_admin = ctypes.windll.shell32.IsUserAnAdmin() != 0
        except AttributeError:
            logger.warning("Could not determine admin status using ctypes. Assuming not admin.")
            is_admin = False
        except Exception as e:
             logger.error(f"Unexpected error checking admin status: {e}", exc_info=True)
             is_admin = False # Fail safe
    logger.debug(f"Admin check result: {is_admin}")
    return is_admin

# --- Autostart Functions ---

def _get_run_key_path(is_admin: bool) -> str:
    """Gets the appropriate registry key path based on privilege level."""
    if is_admin:
        # HKEY_LOCAL_MACHINE for all users (requires admin)
        return r"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"
    else:
        # HKEY_CURRENT_USER for the current user only
        return r"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"

def _get_registry_hive(is_admin: bool) -> int:
    """Gets the appropriate registry hive based on privilege level."""
    return winreg.HKEY_LOCAL_MACHINE if is_admin else winreg.HKEY_CURRENT_USER

def register_autostart(app_name: str, exe_path: str, is_admin: bool) -> bool:
    """
    Registers the agent executable to run on Windows startup.

    Args:
        app_name (str): The name to use for the registry key.
        exe_path (str): The full path to the agent executable.
        is_admin (bool): Whether the agent is running with admin privileges.
                         Determines HKLM (all users) or HKCU (current user).

    Returns:
        bool: True if registration was successful or already exists, False otherwise.
    """
    if not winreg:
        logger.error("Cannot register autostart: winreg module is not available.")
        return False
    if not exe_path or not os.path.exists(exe_path):
         logger.error(f"Cannot register autostart: Executable path '{exe_path}' is invalid or does not exist.")
         return False

    key_path = _get_run_key_path(is_admin)
    hive = _get_registry_hive(is_admin)
    scope = "all users (HKLM)" if is_admin else "current user (HKCU)"

    logger.info(f"Attempting to register autostart for {scope} using path: {exe_path} under name: {app_name}")

    try:
        # Open the key, creating it if it doesn't exist
        with winreg.OpenKey(hive, key_path, 0, winreg.KEY_WRITE | winreg.KEY_READ) as key:
            # Check if the value already exists and matches
            try:
                existing_path, _ = winreg.QueryValueEx(key, app_name)
                if existing_path == exe_path:
                    logger.info(f"Autostart entry '{app_name}' already exists and points to the correct path.")
                    return True
                else:
                    logger.warning(f"Autostart entry '{app_name}' exists but points to '{existing_path}'. Overwriting.")
            except FileNotFoundError:
                # Value doesn't exist, proceed to set it
                pass

            # Set the value (name = app_name, type = REG_SZ, data = exe_path)
            winreg.SetValueEx(key, app_name, 0, winreg.REG_SZ, exe_path)
            logger.info(f"Successfully registered autostart entry '{app_name}' for {scope}.")
            return True
    except PermissionError:
        logger.error(f"Permission denied trying to write to registry key: {hive}\\{key_path}. Run as administrator if targeting HKLM.")
        return False
    except OSError as e:
        logger.error(f"OS error accessing registry for autostart registration: {e}", exc_info=True)
        return False
    except Exception as e:
        logger.error(f"Unexpected error during autostart registration: {e}", exc_info=True)
        return False

def unregister_autostart(app_name: str, is_admin: bool) -> bool:
    """
    Unregisters the agent executable from running on Windows startup.

    Args:
        app_name (str): The name used for the registry key.
        is_admin (bool): Whether the agent is running with admin privileges.
                         Determines HKLM or HKCU.

    Returns:
        bool: True if unregistration was successful or entry didn't exist, False otherwise.
    """
    if not winreg:
        logger.error("Cannot unregister autostart: winreg module is not available.")
        return False

    key_path = _get_run_key_path(is_admin)
    hive = _get_registry_hive(is_admin)
    scope = "all users (HKLM)" if is_admin else "current user (HKCU)"

    logger.info(f"Attempting to unregister autostart entry '{app_name}' for {scope}.")

    try:
        with winreg.OpenKey(hive, key_path, 0, winreg.KEY_WRITE) as key:
            try:
                winreg.DeleteValue(key, app_name)
                logger.info(f"Successfully unregistered autostart entry '{app_name}' for {scope}.")
                return True
            except FileNotFoundError:
                logger.info(f"Autostart entry '{app_name}' not found for {scope}. Nothing to unregister.")
                return True # Considered success if it doesn't exist
    except PermissionError:
        logger.error(f"Permission denied trying to write to registry key: {hive}\\{key_path}. Run as administrator if targeting HKLM.")
        return False
    except OSError as e:
        logger.error(f"OS error accessing registry for autostart unregistration: {e}", exc_info=True)
        return False
    except Exception as e:
        logger.error(f"Unexpected error during autostart unregistration: {e}", exc_info=True)
        return False

# --- User SID Function (Still needed for Pipe Name) ---

def get_user_sid_string() -> Optional[str]:
    """Gets the SID string for the current user."""
    try:
        import win32api
        import pywintypes
        token = win32security.OpenProcessToken(win32api.GetCurrentProcess(), win32security.TOKEN_QUERY)
        user_sid, _ = win32security.GetTokenInformation(token, win32security.TokenUser)
        sid_string = win32security.ConvertSidToStringSid(user_sid)
        win32api.CloseHandle(token)
        logger.debug(f"Retrieved current user SID: {sid_string}")
        return sid_string
    except ImportError:
        logger.error("Failed to get user SID: win32api or pywintypes module not found.")
        return None
    except (pywintypes.error, OSError, AttributeError) as e:
        logger.error(f"Failed to get current user SID: {e}", exc_info=True)
        return None

# Placeholder for future functions from maintain_agent.md
# def set_directory_acls(path): pass
# def manage_ipc_secret(action='get', context='USER'): pass
# def get_user_sid_string(): pass
