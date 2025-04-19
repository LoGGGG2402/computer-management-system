"""
Directory and permission management utilities for the agent.
"""
import os
import uuid

import win32security
import ntsecuritycon as win32con
import pywintypes

from ..utils import get_logger
from .windows_utils import is_running_as_admin

logger = get_logger(__name__)

def determine_storage_path(app_name: str) -> str:
    """
    Determines the appropriate storage path based on execution privileges.
    Uses ProgramData for Admin, LocalAppData for standard user.

    :param app_name: The application name for the directory
    :type app_name: str
    :return: The absolute path to the storage directory
    :rtype: str
    :raises: ValueError: If a suitable path cannot be determined
    """
    is_admin = is_running_as_admin()

    if is_admin:
        base_path = os.getenv('PROGRAMDATA')
        if not base_path:
             logger.error("Could not get PROGRAMDATA environment variable.")
             raise ValueError("Cannot determine ProgramData path for admin storage.")
        storage_dir = os.path.join(base_path, app_name)
        logger.debug(f"Running as Admin. Using ProgramData path: {storage_dir}")
    else:
        base_path = os.getenv('LOCALAPPDATA')
        if not base_path:
             logger.error("Could not get LOCALAPPDATA environment variable.")
             raise ValueError("Cannot determine LocalAppData path for user storage.")
        storage_dir = os.path.join(base_path, app_name)
        logger.debug(f"Running as User. Using LocalAppData path: {storage_dir}")

    return storage_dir

def ensure_directory_permissions(path: str, is_admin: bool):
    """
    Sets strict permissions (SYSTEM:F, Administrators:F) on the directory
    if running as Admin and win32security is available. Disables inheritance.
    
    :param path: Path to the directory to set permissions on
    :type path: str
    :param is_admin: Whether running as admin
    :type is_admin: bool
    """
    if not is_admin:
        logger.debug("Not running as admin, skipping ACL modification.")
        return

    logger.info(f"Setting ACLs for admin storage directory: {path}")
    try:
        sid_system = win32security.LookupAccountName("", "SYSTEM")[0]
        sid_admins = win32security.LookupAccountName("", "Administrators")[0]

        dacl = win32security.ACL()
        dacl.AddAccessAllowedAceEx(win32security.ACL_REVISION_DS,
                                   win32con.OBJECT_INHERIT_ACE | win32con.CONTAINER_INHERIT_ACE,
                                   win32con.GENERIC_ALL,
                                   sid_system)
        dacl.AddAccessAllowedAceEx(win32security.ACL_REVISION_DS,
                                   win32con.OBJECT_INHERIT_ACE | win32con.CONTAINER_INHERIT_ACE,
                                   win32con.GENERIC_ALL,
                                   sid_admins)

        sd = win32security.SECURITY_DESCRIPTOR()
        sd.SetSecurityDescriptorOwner(sid_admins, False)
        sd.SetSecurityDescriptorGroup(sid_system, False)
        sd.SetSecurityDescriptorDacl(True, dacl, False)

        security_info = win32security.DACL_SECURITY_INFORMATION | win32security.PROTECTED_DACL_SECURITY_INFORMATION | win32security.OWNER_SECURITY_INFORMATION | win32security.GROUP_SECURITY_INFORMATION
        win32security.SetFileSecurity(path, security_info, sd)

        logger.info(f"Successfully applied strict ACLs to {path}")

    except pywintypes.error as e:
        logger.error(f"Failed to set ACLs on {path}: {e}", exc_info=True)
    except Exception as e:
        logger.error(f"Unexpected error setting ACLs on {path}: {e}", exc_info=True)

def ensure_storage_directory(storage_path: str) -> None:
    """
    Ensures the storage directory exists, is accessible,
    and sets appropriate permissions if running as Admin.
    
    :param storage_path: Path to the storage directory
    :type storage_path: str
    :raises: ValueError: On critical errors
    """
    is_admin = is_running_as_admin()
    try:
        if not os.path.exists(storage_path):
             logger.info(f"Storage path '{storage_path}' does not exist. Creating.")
             os.makedirs(storage_path, exist_ok=True)
             logger.info(f"Successfully created storage directory: {storage_path}")
             ensure_directory_permissions(storage_path, is_admin)
        elif not os.path.isdir(storage_path):
             logger.critical(f"Configured storage path '{storage_path}' exists but is not a directory.")
             raise ValueError(f"Storage path '{storage_path}' is not a directory.")
        else:
             logger.debug(f"Storage directory '{storage_path}' already exists. Checking writability and permissions.")
             test_file = os.path.join(storage_path, f".writetest_{uuid.uuid4()}")
             try:
                  with open(test_file, 'w') as f:
                       f.write('test')
                  os.remove(test_file)
                  logger.debug(f"Storage directory '{storage_path}' appears writable.")
             except (IOError, OSError) as write_err:
                  logger.critical(f"Storage directory '{storage_path}' is not writable: {write_err}")
                  raise ValueError(f"Storage path '{storage_path}' is not writable.")
             ensure_directory_permissions(storage_path, is_admin)

    except PermissionError:
         logger.critical(f"Permission denied creating/accessing storage directory: {storage_path}")
         raise ValueError(f"Permission denied for storage path: {storage_path}")
    except OSError as e:
         logger.critical(f"OS error creating/accessing storage directory {storage_path}: {e}")
         raise ValueError(f"Could not create/access storage directory: {e}")
    except Exception as e:
         logger.critical(f"Unexpected error ensuring storage directory {storage_path}: {e}", exc_info=True)
         raise ValueError(f"Unexpected error ensuring storage directory: {e}")

def setup_directory_structure(app_name: str) -> str:
    """
    Sets up the directory structure for the application.
    This is a high-level function that should be called early in the application startup.
    
    :param app_name: The application name for the directory
    :type app_name: str
    :return: The path to the storage directory
    :rtype: str
    :raises: ValueError: If setup fails
    """
    try:
        # Determine the appropriate storage path
        storage_path = determine_storage_path(app_name)
        
        # Ensure the storage directory exists and has proper permissions
        ensure_storage_directory(storage_path)
        
        # Create logs directory
        logs_dir = os.path.join(storage_path, 'logs')
        if not os.path.exists(logs_dir):
            os.makedirs(logs_dir, exist_ok=True)
            logger.info(f"Created logs directory: {logs_dir}")
        
        return storage_path
        
    except Exception as e:
        logger.critical(f"Failed to set up directory structure: {e}", exc_info=True)
        raise ValueError(f"Failed to set up directory structure: {e}")