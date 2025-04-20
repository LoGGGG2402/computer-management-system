"""
Directory and permission management utilities for the agent.
"""
import os
import uuid

import win32security
import ntsecuritycon as win32con

from .windows_utils import is_running_as_admin
from .. import __app_name__

def determine_storage_path() -> str:
    """
    Determines the appropriate storage path based on execution privileges.
    Uses ProgramData for Admin, LocalAppData for standard user.

    :return: The absolute path to the storage directory
    :rtype: str
    :raises: ValueError: If a suitable path cannot be determined
    """
    is_admin = is_running_as_admin()

    if is_admin:
        base_path = os.getenv('PROGRAMDATA')
        if not base_path:
            raise ValueError("Cannot determine ProgramData path for admin storage.")
        storage_dir = os.path.join(base_path, __app_name__)
    else:
        base_path = os.getenv('LOCALAPPDATA')
        if not base_path:
            raise ValueError("Cannot determine LocalAppData path for user storage.")
        storage_dir = os.path.join(base_path, __app_name__)

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
        return

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
    except Exception:
        pass

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
            os.makedirs(storage_path, exist_ok=True)
            ensure_directory_permissions(storage_path, is_admin)
        elif not os.path.isdir(storage_path):
            raise ValueError(f"Storage path '{storage_path}' is not a directory.")
        else:
            test_file = os.path.join(storage_path, f".writetest_{uuid.uuid4()}")
            try:
                with open(test_file, 'w') as f:
                    f.write('test')
                os.remove(test_file)
            except (IOError, OSError):
                raise ValueError(f"Storage path '{storage_path}' is not writable.")
            ensure_directory_permissions(storage_path, is_admin)

    except PermissionError:
        raise ValueError(f"Permission denied for storage path: {storage_path}")
    except OSError as e:
        raise ValueError(f"Could not create/access storage directory: {e}")
    except Exception as e:
        raise ValueError(f"Unexpected error ensuring storage directory: {e}")

def setup_directory_structure() -> str:
    """
    Sets up the directory structure for the application.
    This is a high-level function that should be called early in the application startup.
    
    :return: The path to the storage directory
    :rtype: str
    :raises: ValueError: If setup fails
    """
    try:
        # Determine the appropriate storage path
        storage_path = determine_storage_path()
        
        # Ensure the storage directory exists and has proper permissions
        ensure_storage_directory(storage_path)
        
        # Create logs directory
        logs_dir = os.path.join(storage_path, 'logs')
        if not os.path.exists(logs_dir):
            os.makedirs(logs_dir, exist_ok=True)

        # Create errors directory
        errors_dir = os.path.join(storage_path, 'errors')
        if not os.path.exists(errors_dir):
            os.makedirs(errors_dir, exist_ok=True)
        
        return storage_path
        
    except Exception as e:
        raise ValueError(f"Failed to set up directory structure: {e}")