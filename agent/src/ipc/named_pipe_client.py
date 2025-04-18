"""
Named Pipe client for Inter-Process Communication (IPC).
"""
import json
from typing import List, Dict, Any, Optional
from src.utils.logger import get_logger

logger = get_logger(__name__)

try:
    import win32file
    import pywintypes
    WINDOWS_PIPE_SUPPORT = True
except ImportError:
    WINDOWS_PIPE_SUPPORT = False
    logger.warning("win32pipe or related modules not found. IPC client functionality will be disabled.")

from src.system.windows_utils import get_user_sid_string, is_running_as_admin

PIPE_NAME_TEMPLATE_SYSTEM = r'\\.\pipe\CMSAgentIPC_System'
PIPE_NAME_TEMPLATE_USER = r'\\.\pipe\CMSAgentIPC_User_{user_sid}'
PIPE_CONNECT_TIMEOUT_MS = 3000
PIPE_READ_TIMEOUT_MS = 5000
PIPE_BUFFER_SIZE = 4096

def _determine_pipe_name(is_admin: bool) -> Optional[str]:
    """
    Determines the pipe name based on admin privileges.
    
    :param is_admin: Whether target agent is expected to be running as admin
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

def send_force_command(is_admin: bool, new_args: List[str], agent_token: Optional[str]) -> Dict[str, Any]:
    """
    Connects to the running agent's named pipe and sends a force restart command.

    :param is_admin: Whether the target agent is expected to be running as admin
    :type is_admin: bool
    :param new_args: Command line arguments intended for the new instance
    :type new_args: List[str]
    :param agent_token: The agent token to use for authentication
    :type agent_token: Optional[str]
    :return: A dictionary containing the status from the server
    :rtype: Dict[str, Any]
    """
    if not WINDOWS_PIPE_SUPPORT:
        return {"status": "error", "message": "IPC unsupported (win32 modules missing)"}

    pipe_name = _determine_pipe_name(is_admin)
    if not pipe_name:
        return {"status": "error", "message": "Could not determine target pipe name"}

    if not agent_token:
        logger.error("Agent token not provided to send_force_command. Cannot authenticate IPC request.")
        return {"status": "error", "message": "IPC client missing agent token"}

    pipe_handle = None
    try:
        logger.info(f"Attempting to connect to IPC pipe: {pipe_name}")

        pipe_handle = win32file.CreateFile(
            pipe_name,
            win32file.GENERIC_READ | win32file.GENERIC_WRITE,
            0,
            None,
            win32file.OPEN_EXISTING,
            0,
            None
        )
        logger.info(f"Connected to pipe {pipe_name}.")

        request_payload = {
            "command": "force_restart",
            "token": agent_token,
            "new_args": new_args
        }
        request_str = json.dumps(request_payload)
        logger.debug(f"Sending IPC request: {request_str}")

        win32file.WriteFile(pipe_handle, request_str.encode('utf-8'))
        logger.debug("Request sent. Waiting for response...")

        hr, response_bytes = win32file.ReadFile(pipe_handle, PIPE_BUFFER_SIZE)
        if hr != 0:
             logger.error(f"ReadFile failed with error code: {hr}")
             return {"status": "error", "message": f"Pipe read error {hr}"}

        response_str = response_bytes.decode('utf-8')
        logger.debug(f"Received raw response: {response_str}")

        response_data = json.loads(response_str)
        logger.info(f"Received IPC response: {response_data}")
        if response_data.get("status") == "invalid_token":
            logger.error("IPC request rejected by server due to invalid token.")
        return response_data

    except pywintypes.error as e:
        if e.winerror == 2:
            logger.warning(f"IPC pipe {pipe_name} not found. Agent likely not running or in different context.")
            return {"status": "agent_not_running"}
        elif e.winerror == 231:
             logger.error(f"IPC pipe {pipe_name} is busy (Error 231). This is unexpected.")
             return {"status": "error", "message": "Pipe busy (unexpected)"}
        elif e.winerror == 232:
             logger.error(f"IPC pipe {pipe_name} closed prematurely by server (Error 232).")
             return {"status": "error", "message": "Pipe closed by server"}
        else:
            logger.error(f"Named pipe connection/communication error (winerror {e.winerror}): {e}", exc_info=True)
            return {"status": "error", "message": f"Pipe error: {e.strerror}"}
    except json.JSONDecodeError:
        logger.error("Failed to decode JSON response from IPC server.")
        return {"status": "error", "message": "Invalid JSON response from server"}
    except Exception as e:
        logger.error(f"Unexpected error during IPC communication: {e}", exc_info=True)
        return {"status": "error", "message": f"Unexpected client error: {e}"}
    finally:
        if pipe_handle:
            try:
                win32file.CloseHandle(pipe_handle)
                logger.debug("Closed pipe handle.")
            except pywintypes.error as e:
                 logger.warning(f"Error closing pipe handle: {e}")

