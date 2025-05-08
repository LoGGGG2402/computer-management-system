"""
Named Pipe server for Inter-Process Communication (IPC).
"""

import json
import threading
import time
import win32pipe
import win32file
import win32security
import pywintypes
import ntsecuritycon as win32con
from typing import Optional, TYPE_CHECKING

from agent.utils import get_logger
from agent.system.windows_utils import get_user_sid_string, determine_pipe_name
from agent.core import AgentState

if TYPE_CHECKING:
    from agent.core.agent import Agent

logger = get_logger(__name__)

# Pipe configuration constants
PIPE_BUFFER_SIZE = 4096
PIPE_TIMEOUT_MS = 5000

class NamedPipeIPCServer(threading.Thread):
    """
    Runs a Named Pipe server in a separate thread to listen for force restart commands.
    """
    def __init__(self, agent_instance: 'Agent', is_admin: bool, agent_token: Optional[str]):
        """
        Initializes the Named Pipe IPC Server.
        
        :param agent_instance: The main Agent instance (used for callbacks)
        :type agent_instance: Agent
        :param is_admin: Indicates if the agent is running with admin privileges
        :type is_admin: bool
        :param agent_token: The agent's authentication token for IPC
        :type agent_token: Optional[str]
        """
        super().__init__(name="IPCServerListener")
        self.daemon = True

        self.agent = agent_instance
        self.is_admin = is_admin
        self.pipe_name = determine_pipe_name(is_admin)
        if not self.pipe_name:
             raise ValueError("Could not determine pipe name for IPC server.")

        self.pipe_handle = None
        self._stop_event = threading.Event()
        self.agent_token = agent_token

        if not self.agent_token:
            logger.warning("Agent token is missing. IPC functionality will be disabled.")
        else:
            logger.debug("Using agent token for IPC authentication.")

        self.active_connections = {}
        self.connections_lock = threading.Lock()

    def update_token(self, new_token: str):
        """
        Updates the agent token used for IPC authentication.
        
        :param new_token: The new agent token to use for authentication
        :type new_token: str
        """
        if not new_token:
            logger.warning("Attempted to update agent token with empty value.")
            return False
        
        logger.debug("Updating IPC server agent token...")
        self.agent_token = new_token
        logger.info("IPC server token updated successfully.")
        return True

    def _create_pipe_security_attributes(self) -> Optional[pywintypes.SECURITY_ATTRIBUTES]:
        """
        Creates security attributes to restrict pipe access.
        
        :return: Security attributes object or None if creation fails
        :rtype: Optional[pywintypes.SECURITY_ATTRIBUTES]
        """
        try:
            sd = win32security.SECURITY_DESCRIPTOR()
            dacl = win32security.ACL()

            sid_system = win32security.LookupAccountName("", "SYSTEM")[0]
            sid_admins = win32security.LookupAccountName("", "Administrators")[0]

            if self.is_admin:
                dacl.AddAccessAllowedAce(win32security.ACL_REVISION, win32con.FILE_ALL_ACCESS, sid_system)
                dacl.AddAccessAllowedAce(win32security.ACL_REVISION, win32con.FILE_ALL_ACCESS, sid_admins)
                sd.SetSecurityDescriptorOwner(sid_admins, False)
            else:
                user_sid_string = get_user_sid_string()
                if not user_sid_string: return None
                user_sid = win32security.ConvertStringSidToSid(user_sid_string)
                dacl.AddAccessAllowedAce(win32security.ACL_REVISION, win32con.FILE_ALL_ACCESS, user_sid)
                sd.SetSecurityDescriptorOwner(user_sid, False)

            sd.SetSecurityDescriptorDacl(True, dacl, False)

            sa = win32security.SECURITY_ATTRIBUTES()
            sa.SECURITY_DESCRIPTOR = sd
            sa.bInheritHandle = False
            logger.debug(f"Created security attributes for pipe {self.pipe_name}")
            return sa
        except (pywintypes.error, OSError, AttributeError) as e:
            logger.error(f"Failed to create security attributes for pipe: {e}", exc_info=True)
            return None

    def start(self):
        """
        Starts the IPC server listener thread.
        """
        if not self.agent_token:
            logger.warning("Cannot start IPC server: Agent token is missing.")
            return

        if self.is_alive():
            logger.warning("IPC server listener thread already running.")
            return

        self._stop_event.clear()
        super().start()
        logger.info(f"Named Pipe IPC Server thread started, listening on {self.pipe_name}")

    def run(self):
        """
        Main loop for the named pipe server thread.
        """
        logger.info(f"Named Pipe IPC Server thread started. Listening on {self.pipe_name}")

        sa = self._create_pipe_security_attributes()
        if not sa:
            logger.critical("Failed to create security attributes. IPC server cannot start securely.")
            return

        while not self._stop_event.is_set():
            self._pipe_handle = None
            try:
                self._pipe_handle = win32pipe.CreateNamedPipe(
                    self.pipe_name,
                    win32pipe.PIPE_ACCESS_DUPLEX,
                    win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
                    1,
                    PIPE_BUFFER_SIZE,
                    PIPE_BUFFER_SIZE,
                    0,
                    sa
                )
                logger.debug(f"Pipe {self.pipe_name} created. Waiting for client connection...")

                if self._stop_event.is_set(): break

                win32pipe.ConnectNamedPipe(self._pipe_handle, None)

                if self._stop_event.is_set(): break

                logger.info(f"Client connected to pipe {self.pipe_name}.")

                self._handle_client_request()

            except pywintypes.error as e:
                if e.winerror == 232:
                     logger.warning(f"Pipe {self.pipe_name} closing or client disconnected prematurely (Error 232).")
                elif e.winerror == 536:
                     logger.debug(f"Pipe {self.pipe_name} already connected, likely race condition. Continuing.")
                     self._close_pipe_handle()
                     time.sleep(0.1)
                     continue
                else:
                     logger.error(f"Named pipe error (winerror {e.winerror}): {e}", exc_info=True)
                if self._pipe_handle:
                    self._close_pipe_handle()
                time.sleep(1)

            except Exception as e:
                logger.error(f"Unexpected error in Named Pipe server loop: {e}", exc_info=True)
                if self._pipe_handle:
                    self._close_pipe_handle()
                time.sleep(1)

            finally:
                if self._pipe_handle:
                    try:
                        win32pipe.DisconnectNamedPipe(self._pipe_handle)
                        logger.debug("Disconnected client.")
                    except pywintypes.error as disc_e:
                         if disc_e.winerror != 232:
                              logger.warning(f"Error disconnecting pipe client: {disc_e}")
                    finally:
                         self._close_pipe_handle()

        logger.info("Named Pipe IPC Server thread finished.")

    def _handle_client_request(self):
        """
        Reads request, authenticates, processes, and sends response.
        """
        request_data = None
        response = {"status": "error", "message": "Internal server error"}

        try:
            logger.debug("Reading data from pipe...")
            hr, data_bytes = win32file.ReadFile(self._pipe_handle, PIPE_BUFFER_SIZE)
            if hr != 0:
                 logger.error(f"ReadFile failed with error code: {hr}")
                 response = {"status": "error", "message": f"Pipe read error {hr}"}
                 return

            request_str = data_bytes.decode('utf-8')
            logger.debug(f"Received raw request: {request_str}")
            request_data = json.loads(request_str)

            client_token = request_data.get('token')
            if not client_token or client_token != self.agent_token:
                logger.warning("IPC request received with invalid or missing agent token.")
                response = {"status": "invalid_token"}
                try:
                    response_str = json.dumps(response)
                    logger.debug(f"Sending auth failure response: {response_str}")
                    win32file.WriteFile(self._pipe_handle, response_str.encode('utf-8'))
                except Exception as e_resp:
                    logger.error(f"Failed to send auth failure response: {e_resp}", exc_info=True)
                return

            command = request_data.get('command')
            if command == 'force_restart':
                logger.info("Received valid 'force_restart' command via IPC.")

                current_state = self.agent.get_state()
                if current_state in [AgentState.UPDATING_STARTING, AgentState.UPDATING_DOWNLOADING,
                                     AgentState.UPDATING_VERIFYING, AgentState.UPDATING_EXTRACTING_UPDATER,
                                     AgentState.UPDATING_PREPARING_SHUTDOWN]:
                    logger.warning(f"Agent is currently updating (State: {current_state.name}). Rejecting force_restart.")
                    response = {"status": "busy_updating"}
                else:
                    logger.info("Acknowledging force_restart command. Initiating shutdown.")
                    response = {"status": "acknowledged"}
                    threading.Timer(0.1, self.agent.request_restart).start()
            else:
                logger.warning(f"Received unknown IPC command: {command}")
                response = {"status": "unknown_command"}

        except json.JSONDecodeError:
            logger.error("Failed to decode JSON from IPC request.")
            response = {"status": "error", "message": "Invalid JSON format"}
        except KeyError as e:
             logger.error(f"Missing expected key in IPC request: {e}")
             response = {"status": "error", "message": f"Missing key: {e}"}
        except Exception as e:
            logger.error(f"Error processing client request: {e}", exc_info=True)

        finally:
            if response.get("status") != "invalid_token":
                try:
                    response_str = json.dumps(response)
                    logger.debug(f"Sending response: {response_str}")
                    win32file.WriteFile(self._pipe_handle, response_str.encode('utf-8'))
                except pywintypes.error as e:
                     if e.winerror == 232:
                          logger.warning("Client disconnected before response could be sent (Error 232).")
                     else:
                          logger.error(f"Failed to write response to pipe: {e}", exc_info=True)
                except Exception as e:
                     logger.error(f"Unexpected error sending response: {e}", exc_info=True)

    def _close_pipe_handle(self):
        """
        Safely closes the current pipe handle.
        """
        if self._pipe_handle:
            try:
                win32file.CloseHandle(self._pipe_handle)
                logger.debug(f"Closed pipe handle for {self.pipe_name}")
            except pywintypes.error as e:
                 logger.warning(f"Error closing pipe handle: {e}")
            finally:
                 self._pipe_handle = None

    def stop(self):
        """
        Signals the server thread to stop listening and cleans up.
        """
        logger.info("Stopping Named Pipe IPC Server...")
        self._stop_event.set()

        if self.pipe_name:
             def dummy_connect():
                  try:
                       logger.debug("Attempting dummy connection to unblock server...")
                       handle = win32file.CreateFile(
                            self.pipe_name,
                            win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                            0, None,
                            win32file.OPEN_EXISTING,
                            0, None
                       )
                       win32file.CloseHandle(handle)
                       logger.debug("Dummy connection successful and closed.")
                  except pywintypes.error as e:
                       if e.winerror not in [2, 231]:
                            logger.warning(f"Error during dummy pipe connection: {e}")
                  except Exception as e:
                       logger.warning(f"Unexpected error during dummy pipe connection: {e}")

             dummy_thread = threading.Thread(target=dummy_connect, daemon=True)
             dummy_thread.start()
             dummy_thread.join(timeout=1.0)

        self._close_pipe_handle()
        logger.info("Named Pipe IPC Server stop sequence initiated.")

