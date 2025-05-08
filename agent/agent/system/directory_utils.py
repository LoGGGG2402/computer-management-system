"""
Utilities for determining and creating the Agent's directory structure.
Handles different storage locations based on platform and user permissions.
"""
import os
import sys
import ctypes
import logging
from typing import Optional, Tuple
from agent.utils import get_logger

logger = get_logger(__name__)


PROGRAMDATA_BASE_DIR = os.environ.get('PROGRAMDATA', 'C:\\ProgramData')
AGENT_DATA_DIR_NAME = "CMSAgent"
DEFAULT_USER_DATA_DIR = os.path.expanduser(os.path.join("~", ".cms-agent"))

CSIDL_COMMON_APPDATA = 0x0023
CSIDL_LOCAL_APPDATA = 0x001c
SHGFP_TYPE_CURRENT = 0


def _is_windows() -> bool:
    """
    Checks if the current operating system is Windows.
    
    :return: Always True as the code assumes Windows
    :rtype: bool
    """
    return True


def determine_storage_path(app_name: str = "CMSAgent", system_wide: bool = True) -> str:
    """
    Determines the appropriate storage path for application data.
    Now assumes Windows. If system_wide is True, uses ProgramData.
    Otherwise, uses user's Local AppData.
    """
    if system_wide:
        
        path_buf = ctypes.create_unicode_buffer(ctypes.wintypes.MAX_PATH)
        ctypes.windll.shell32.SHGetFolderPathW(None, CSIDL_COMMON_APPDATA, None, SHGFP_TYPE_CURRENT, path_buf)
        base_path = os.path.join(path_buf.value, app_name)
    else:
        
        path_buf = ctypes.create_unicode_buffer(ctypes.wintypes.MAX_PATH)
        ctypes.windll.shell32.SHGetFolderPathW(None, CSIDL_LOCAL_APPDATA, None, SHGFP_TYPE_CURRENT, path_buf)
        base_path = os.path.join(path_buf.value, app_name)
    
    try:
        os.makedirs(base_path, exist_ok=True)
    except OSError as e:
        logger.error(f"Error creating or accessing storage path {base_path}: {e}")
        raise
    return base_path


def setup_directory_structure() -> str:
    """
    Sets up the directory structure for the agent, creating necessary subdirectories.
    
    :return: Base storage path that was set up
    :rtype: str
    """
    storage_path = determine_storage_path()
    if not storage_path:
        logger.critical("Failed to determine storage path")
        raise RuntimeError("Failed to determine agent storage path")
    
    
    subdirs = ["config", "logs", "error_reports", "cache", "updates"]
    for subdir in subdirs:
        subdir_path = os.path.join(storage_path, subdir)
        try:
            os.makedirs(subdir_path, exist_ok=True)
            logger.debug(f"Created/verified directory: {subdir_path}")
        except Exception as e:
            logger.error(f"Failed to create directory {subdir_path}: {e}")
    
    return storage_path


def set_directory_permissions_for_system(directory_path: str) -> bool:
    """
    Sets permissions on a directory to allow the SYSTEM account full access.
    Windows-specific function. Used for service installation.
    
    :param directory_path: Path to the directory
    :type directory_path: str
    :return: True if successful, False otherwise
    :rtype: bool
    """
    if not _is_windows():
        logger.warning("set_directory_permissions_for_system() is Windows-specific")
        return False
    
    try:
        import win32security
        import win32con
        import win32file
        
        
        system_sid = win32security.CreateWellKnownSid(win32security.WinLocalSystemSid)
        administrators_sid = win32security.CreateWellKnownSid(win32security.WinBuiltinAdministratorsSid)
        
        
        sd = win32security.GetFileSecurity(
            directory_path, 
            win32security.DACL_SECURITY_INFORMATION
        )
        
        
        dacl = win32security.ACL()
        
        
        dacl.AddAccessAllowedAce(
            win32security.ACL_REVISION,
            win32con.FILE_ALL_ACCESS,
            system_sid
        )
        
        
        dacl.AddAccessAllowedAce(
            win32security.ACL_REVISION,
            win32con.FILE_ALL_ACCESS,
            administrators_sid
        )
        
        
        sd.SetSecurityDescriptorDacl(1, dacl, 0)
        
        
        win32security.SetFileSecurity(
            directory_path,
            win32security.DACL_SECURITY_INFORMATION,
            sd
        )
        
        logger.info(f"Set SYSTEM and Administrators permissions on: {directory_path}")
        return True
        
    except ImportError:
        logger.error("win32security module not available, cannot set directory permissions")
        return False
    except Exception as e:
        logger.error(f"Failed to set directory permissions: {e}", exc_info=True)
        return False