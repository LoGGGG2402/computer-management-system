"""
Named Pipe client for Inter-Process Communication (IPC).
"""
import json
from typing import List, Dict, Any, Optional
from agent.utils.logger import get_logger

logger = get_logger(__name__)

import win32file
import pywintypes

from agent.system.windows_utils import determine_pipe_name

# Pipe connection configuration constants
PIPE_CONNECT_TIMEOUT_MS = 3000
PIPE_READ_TIMEOUT_MS = 5000
PIPE_BUFFER_SIZE = 4096

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
    pipe_name = determine_pipe_name(is_admin)
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

