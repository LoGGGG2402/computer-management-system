"""
Windows-specific utilities for the Computer Management System Agent.
"""
import os
import ctypes
import sys
import win32security
import win32api
import win32ts
import pywintypes
from typing import Optional, List, Tuple

from agent.utils import get_logger

logger = get_logger(__name__)


PIPE_NAME_TEMPLATE_SYSTEM = r'\\.\pipe\CMSAgentIPC_System'


def is_running_as_system() -> bool:
    """
    Checks if the current process is running under the SYSTEM account.
    
    :return: True if running as SYSTEM, False otherwise
    :rtype: bool
    """
    try:
        token = win32security.OpenProcessToken(win32api.GetCurrentProcess(), win32security.TOKEN_QUERY)
        user_sid, _ = win32security.GetTokenInformation(token, win32security.TokenUser)
        system_sid = win32security.CreateWellKnownSid(win32security.WinLocalSystemSid)
        is_system = user_sid.IsEqual(system_sid)
        win32api.CloseHandle(token)
        logger.debug(f"System account check result: {is_system}")
        return is_system
    except Exception as e:
        logger.error(f"Unexpected error checking if running as SYSTEM: {e}", exc_info=True)
        return False


def get_executable_path() -> str:
    """
    Determines the absolute path to the currently running executable.
    Handles cases for PyInstaller packed executables and standard Python scripts.
    """
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        
        return os.path.abspath(sys.executable)
    else:
        
        
        
        
        
        
        try:
            
            
            path = sys.executable
            
            if "python.exe" in path.lower() or "pythonw.exe" in path.lower():
                
                
                import __main__
                if hasattr(__main__, '__file__') and __main__.__file__ is not None:
                    
                    return os.path.abspath(__main__.__file__)
            return os.path.abspath(path) 
        except Exception:
            
            return os.path.abspath(sys.argv[0])


def get_logged_in_users() -> List[Tuple[str, str]]:
    """
    Gets a list of currently logged-in users on Windows.
    
    :return: List of tuples containing (username, sessionId) for logged-in users
    :rtype: List[Tuple[str, str]]
    """
    users = []
    
    try:
        
        sessions = win32ts.WTSEnumerateSessions(win32ts.WTS_CURRENT_SERVER_HANDLE)
        
        for session in sessions:
            session_id = session['SessionId']
            
            
            if session_id == 0:
                continue
                
            
            if session['State'] == win32ts.WTSActive:
                try:
                    user_name = win32ts.WTSQuerySessionInformation(
                        win32ts.WTS_CURRENT_SERVER_HANDLE,
                        session_id,
                        win32ts.WTSUserName
                    )
                    if user_name:
                        users.append((user_name, str(session_id)))
                except (pywintypes.error, Exception) as e:
                    logger.debug(f"Could not get username for session {session_id}: {e}")
            
    except Exception as e:
        logger.error(f"Error enumerating user sessions: {e}")
        
    return users


def get_windows_version() -> Tuple[int, int, int]:
    """
    Gets the Windows version as a tuple (major, minor, build).
    
    :return: Tuple containing (major_version, minor_version, build_number)
    :rtype: Tuple[int, int, int]
    """
    try:
        major = sys.getwindowsversion().major
        minor = sys.getwindowsversion().minor
        build = sys.getwindowsversion().build
        return (major, minor, build)
    except Exception as e:
        logger.error(f"Error getting Windows version: {e}")
        return (0, 0, 0)
