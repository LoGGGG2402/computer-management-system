"""
Windows specific utility functions for the agent.
"""
import ctypes
import os
from typing import Optional
import winreg
import win32security
import win32api
import pywintypes

from ..utils import get_logger
from .. import __app_name__


logger = get_logger(__name__)

# Named pipe constants for IPC
PIPE_NAME_TEMPLATE_SYSTEM = r'\\.\pipe\CMSAgentIPC_System'
PIPE_NAME_TEMPLATE_USER = r'\\.\pipe\CMSAgentIPC_User_{user_sid}'

def is_running_as_admin() -> bool:
    """
    Checks if the current process is running with administrative privileges.

    :return: True if running as admin, False otherwise
    :rtype: bool
    """
    try:
        is_admin = ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception as e:
         logger.error(f"Unexpected error checking admin status: {e}", exc_info=True)
         is_admin = False
    logger.debug(f"Admin check result: {is_admin}")
    return is_admin

def _get_registry_hive(is_admin: bool) -> int:
    """
    Gets the appropriate registry hive based on privilege level.

    :param is_admin: Whether running with admin privileges
    :type is_admin: bool
    :return: Registry hive constant
    :rtype: int
    """
    return winreg.HKEY_LOCAL_MACHINE if is_admin else winreg.HKEY_CURRENT_USER

def register_autostart(exe_path: str, is_admin: bool) -> bool:
    """
    Registers the agent executable to run on Windows startup.

    :param exe_path: The full path to the agent executable
    :type exe_path: str
    :param is_admin: Whether the agent is running with admin privileges
    :type is_admin: bool
    :return: True if registration was successful or already exists, False otherwise
    :rtype: bool
    """
    app_name = __app_name__.replace(" ", "_").replace("-", "_").capitalize()
    if not exe_path or not os.path.exists(exe_path):
         logger.error(f"Cannot register autostart: Executable path '{exe_path}' is invalid or does not exist.")
         return False

    key_path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"
    hive = _get_registry_hive(is_admin)
    scope = "all users (HKLM)" if is_admin else "current user (HKCU)"

    logger.info(f"Attempting to register autostart for {scope} using path: {exe_path} under name: {app_name}")

    try:
        with winreg.OpenKey(hive, key_path, 0, winreg.KEY_WRITE | winreg.KEY_READ) as key:
            try:
                existing_path, _ = winreg.QueryValueEx(key, app_name)
                if existing_path == exe_path:
                    logger.info(f"Autostart entry '{app_name}' already exists and points to the correct path.")
                    return True
                else:
                    logger.warning(f"Autostart entry '{app_name}' exists but points to '{existing_path}'. Overwriting.")
            except FileNotFoundError:
                pass

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

def unregister_autostart(is_admin: bool) -> bool:
    """
    Unregisters the agent executable from running on Windows startup.

    :param is_admin: Whether the agent is running with admin privileges
    :type is_admin: bool
    :return: True if unregistration was successful or entry didn't exist, False otherwise
    :rtype: bool
    """
    key_path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"
    hive = _get_registry_hive(is_admin)
    scope = "all users (HKLM)" if is_admin else "current user (HKCU)"
    app_name = __app_name__.replace(" ", "_").replace("-", "_").capitalize()
    logger.info(f"Attempting to unregister autostart entry '{app_name}' for {scope}.")

    try:
        with winreg.OpenKey(hive, key_path, 0, winreg.KEY_WRITE) as key:
            try:
                winreg.DeleteValue(key, app_name)
                logger.info(f"Successfully unregistered autostart entry '{app_name}' for {scope}.")
                return True
            except FileNotFoundError:
                logger.info(f"Autostart entry '{app_name}' not found for {scope}. Nothing to unregister.")
                return True
    except PermissionError:
        logger.error(f"Permission denied trying to write to registry key: {hive}\\{key_path}. Run as administrator if targeting HKLM.")
        return False
    except OSError as e:
        logger.error(f"OS error accessing registry for autostart unregistration: {e}", exc_info=True)
        return False
    except Exception as e:
        logger.error(f"Unexpected error during autostart unregistration: {e}", exc_info=True)
        return False

def get_user_sid_string() -> Optional[str]:
    """
    Gets the SID string for the current user.

    :return: Current user's SID string or None if retrieval fails
    :rtype: Optional[str]
    """
    try:
        token = win32security.OpenProcessToken(win32api.GetCurrentProcess(), win32security.TOKEN_QUERY)
        user_sid, _ = win32security.GetTokenInformation(token, win32security.TokenUser)
        sid_string = win32security.ConvertSidToStringSid(user_sid)
        win32api.CloseHandle(token)
        logger.debug(f"Retrieved current user SID: {sid_string}")
        return sid_string
    except (pywintypes.error, OSError, AttributeError) as e:
        logger.error(f"Failed to get current user SID: {e}", exc_info=True)
        return None

def determine_pipe_name(is_admin: bool) -> Optional[str]:
    """
    Determines the pipe name based on admin privileges.
    
    :param is_admin: Whether the process is running as admin
    :type is_admin: bool
    :return: Appropriate pipe name or None if it cannot be determined
    :rtype: Optional[str]
    """
    if is_admin:
        return PIPE_NAME_TEMPLATE_SYSTEM
    else:
        user_sid = get_user_sid_string()
        if user_sid:
            return PIPE_NAME_TEMPLATE_USER.format(user_sid=user_sid)
        else:
            logger.error("Failed to get user SID for non-admin pipe name.")
            return None
