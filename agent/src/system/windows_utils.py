# -*- coding: utf-8 -*-
"""
Windows specific utility functions for the agent.
"""
import ctypes
import os
import logging
import uuid
import sys
try:
    import winreg
    import win32security
    import win32cred # For IPC secret management
    import win32api
    import ntsecuritycon as win32con
    import pywintypes
    WINDOWS_ACL_SUPPORT = True
    WINDOWS_CRED_SUPPORT = True
except ImportError:
    WINDOWS_ACL_SUPPORT = False
    WINDOWS_CRED_SUPPORT = False
    logging.getLogger(__name__).warning("win32security or win32cred modules not found. ACL/IPC Secret functionality will be limited.")
    winreg = None
    logging.getLogger(__name__).warning("winreg module not found. Autostart functionality will be disabled.")

logger = logging.getLogger(__name__)

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

# --- IPC Secret Management ---

# Use a consistent target name format
IPC_SECRET_TARGET_NAME_SYSTEM = "CMSAgent/IPCSecret/System"
IPC_SECRET_TARGET_NAME_USER_TEMPLATE = "CMSAgent/IPCSecret/User/{user_sid}"

def get_user_sid_string() -> Optional[str]:
    """Gets the SID string for the current user."""
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

def _get_ipc_target_name(is_admin: bool) -> Optional[str]:
    """Determines the appropriate Credential Manager target name based on privilege."""
    if is_admin:
        return IPC_SECRET_TARGET_NAME_SYSTEM
    else:
        user_sid = get_user_sid_string()
        if user_sid:
            return IPC_SECRET_TARGET_NAME_USER_TEMPLATE.format(user_sid=user_sid)
        else:
            logger.error("Cannot determine IPC target name: Failed to get user SID.")
            return None

def manage_ipc_secret(action: str = 'get', is_admin: bool = None) -> Optional[str]:
    """
    Manages the IPC secret stored in Windows Credential Manager.

    Args:
        action (str): 'get', 'create', or 'delete'.
        is_admin (bool | None): Specifies the context (System/Admin or User).
                                If None, attempts to detect current privilege level.

    Returns:
        Optional[str]: The secret if action is 'get' or 'create', None otherwise or on error.
    """
    if not WINDOWS_CRED_SUPPORT:
        logger.error("Cannot manage IPC secret: win32cred module not available.")
        return None

    if is_admin is None:
        is_admin = is_running_as_admin() # Detect if not specified

    target_name = _get_ipc_target_name(is_admin)
    if not target_name:
        return None # Error logged in _get_ipc_target_name

    context = "System" if is_admin else "User"
    logger.debug(f"Managing IPC secret for {context} context (Target: {target_name}), Action: {action}")

    if action == 'get' or action == 'create':
        try:
            # Try to read existing credential
            cred = win32cred.CredRead(target_name, win32cred.CRED_TYPE_GENERIC)
            secret = cred['CredentialBlob'].decode('utf-8')
            logger.debug(f"Successfully retrieved existing IPC secret for {context}.")
            return secret
        except pywintypes.error as e:
            # ERROR_NOT_FOUND is expected if it doesn't exist yet
            if e.winerror == 1168: # ERROR_NOT_FOUND
                if action == 'create':
                    logger.info(f"IPC secret not found for {context}. Creating a new one.")
                    try:
                        new_secret = str(uuid.uuid4())
                        win32cred.CredWrite({
                            'TargetName': target_name,
                            'Type': win32cred.CRED_TYPE_GENERIC,
                            'UserName': 'CMSAgentIPC', # Informational
                            'CredentialBlob': new_secret.encode('utf-8'),
                            'Persist': win32cred.CRED_PERSIST_LOCAL_MACHINE if is_admin else win32cred.CRED_PERSIST_ENTERPRISE, # Persist across logons
                            'Comment': f'IPC Secret for CMS Agent ({context})'
                        }, 0)
                        logger.info(f"Successfully created and stored new IPC secret for {context}.")
                        return new_secret
                    except pywintypes.error as write_e:
                        logger.error(f"Failed to write new IPC secret for {context}: {write_e}", exc_info=True)
                        return None
                    except Exception as write_e:
                         logger.error(f"Unexpected error writing new IPC secret for {context}: {write_e}", exc_info=True)
                         return None
                else: # action == 'get'
                    logger.warning(f"IPC secret not found for {context} (Target: {target_name}).")
                    return None
            else:
                logger.error(f"Error reading IPC secret for {context} (Target: {target_name}): {e}", exc_info=True)
                return None
        except Exception as e:
             logger.error(f"Unexpected error reading IPC secret for {context} (Target: {target_name}): {e}", exc_info=True)
             return None

    elif action == 'delete':
        try:
            win32cred.CredDelete(target_name, win32cred.CRED_TYPE_GENERIC, 0)
            logger.info(f"Successfully deleted IPC secret for {context} (Target: {target_name}).")
            return None # Success for delete is None
        except pywintypes.error as e:
            if e.winerror == 1168: # ERROR_NOT_FOUND
                logger.info(f"IPC secret not found for {context} (Target: {target_name}). Nothing to delete.")
                return None # Success if not found
            else:
                logger.error(f"Error deleting IPC secret for {context} (Target: {target_name}): {e}", exc_info=True)
                return None
        except Exception as e:
             logger.error(f"Unexpected error deleting IPC secret for {context} (Target: {target_name}): {e}", exc_info=True)
             return None

    else:
        logger.error(f"Invalid action specified for manage_ipc_secret: {action}")
        return None

# Placeholder for future functions from maintain_agent.md
# def set_directory_acls(path): pass
# def manage_ipc_secret(action='get', context='USER'): pass
# def get_user_sid_string(): pass
