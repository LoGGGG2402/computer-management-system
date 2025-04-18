# -*- coding: utf-8 -*-
"""
Named Pipe server for Inter-Process Communication (IPC).
Allows a new agent instance (--force) to request the running instance to shut down.
"""

import threading
import logging
import json
import time
import sys
import os
from typing import TYPE_CHECKING, Optional, Callable

# Windows specific imports
try:
    import win32pipe
    import win32file
    import win32api
    import win32security
    import ntsecuritycon as win32con
    import pywintypes
    WINDOWS_PIPE_SUPPORT = True
except ImportError:
    WINDOWS_PIPE_SUPPORT = False
    logging.getLogger(__name__).warning("win32pipe or related modules not found. IPC functionality will be disabled.")

# Local imports
from src.system.windows_utils import get_user_sid_string, manage_ipc_secret, is_running_as_admin
from src.core.agent_state import AgentState # Assuming AgentState enum exists

# Type hint for Agent class without circular import
if TYPE_CHECKING:
    from src.core.agent import Agent

logger = logging.getLogger(__name__)

# Pipe name format
PIPE_NAME_TEMPLATE_SYSTEM = r'\\.\pipe\CMSAgentIPC_System'
PIPE_NAME_TEMPLATE_USER = r'\\.\pipe\CMSAgentIPC_User_{user_sid}'
PIPE_BUFFER_SIZE = 4096 # 4KB buffer for messages
PIPE_TIMEOUT_MS = 5000 # 5 seconds timeout for read/write

class NamedPipeIPCServer(threading.Thread):
    """
    Runs a Named Pipe server in a separate thread to listen for force restart commands.
    """
    def __init__(self, agent_instance: 'Agent', is_admin: bool):
        """
        Initializes the Named Pipe server.

        Args:
            agent_instance (Agent): Reference to the main Agent instance.
            is_admin (bool): Whether the agent is running with admin privileges.
        """
        super().__init__(name="NamedPipeIPCServerThread", daemon=True)
        if not WINDOWS_PIPE_SUPPORT:
            raise ImportError("Cannot start NamedPipeIPCServer: win32 modules not available.")

        self.agent = agent_instance
        self.is_admin = is_admin
        self._stop_event = threading.Event()
        self._pipe_handle = None

        # Determine pipe name
        self.pipe_name = self._determine_pipe_name()
        if not self.pipe_name:
            raise ValueError("Could not determine pipe name.")

        # Get IPC secret (should have been created by main process)
        self.ipc_secret = manage_ipc_secret(action='get', is_admin=self.is_admin)
        if not self.ipc_secret:
            # Attempt to create it if missing - this might happen in rare cases
            logger.warning("IPC secret not found by server thread, attempting creation.")
            self.ipc_secret = manage_ipc_secret(action='create', is_admin=self.is_admin)
            if not self.ipc_secret:
                 raise ValueError("Could not get or create IPC secret.")

        logger.info(f"Named Pipe IPC Server initialized. Pipe: {self.pipe_name}, Context: {'Admin' if is_admin else 'User'}")

    def _determine_pipe_name(self) -> Optional[str]:
        """Determines the pipe name based on admin privileges."""
        if self.is_admin:
            return PIPE_NAME_TEMPLATE_SYSTEM
        else:
            user_sid = get_user_sid_string()
            if user_sid:
                return PIPE_NAME_TEMPLATE_USER.format(user_sid=user_sid)
            else:
                logger.error("Failed to get user SID for non-admin pipe name.")
                return None

    def _create_pipe_security_attributes(self) -> Optional[pywintypes.SECURITY_ATTRIBUTES]:
        """Creates security attributes to restrict pipe access."""
        try:
            sd = win32security.SECURITY_DESCRIPTOR()
            dacl = win32security.ACL()

            # Get SIDs
            sid_system = win32security.LookupAccountName("", "SYSTEM")[0]
            sid_admins = win32security.LookupAccountName("", "Administrators")[0]

            if self.is_admin:
                # Allow SYSTEM and Administrators full access
                dacl.AddAccessAllowedAce(win32security.ACL_REVISION, win32con.FILE_ALL_ACCESS, sid_system)
                dacl.AddAccessAllowedAce(win32security.ACL_REVISION, win32con.FILE_ALL_ACCESS, sid_admins)
                # Set owner to Administrators
                sd.SetSecurityDescriptorOwner(sid_admins, False)
            else:
                # Allow current user full access
                user_sid_string = get_user_sid_string()
                if not user_sid_string: return None # Error logged previously
                user_sid = win32security.ConvertStringSidToSid(user_sid_string)
                dacl.AddAccessAllowedAce(win32security.ACL_REVISION, win32con.FILE_ALL_ACCESS, user_sid)
                # Set owner to current user
                sd.SetSecurityDescriptorOwner(user_sid, False)

            # Apply DACL to the security descriptor
            sd.SetSecurityDescriptorDacl(True, dacl, False) # True=DACL present

            # Create security attributes
            sa = win32security.SECURITY_ATTRIBUTES()
            sa.SECURITY_DESCRIPTOR = sd
            sa.bInheritHandle = False
            logger.debug(f"Created security attributes for pipe {self.pipe_name}")
            return sa
        except (pywintypes.error, OSError, AttributeError) as e:
            logger.error(f"Failed to create security attributes for pipe: {e}", exc_info=True)
            return None

    def run(self):
        """Main loop for the named pipe server thread."""
        logger.info(f"Named Pipe IPC Server thread started. Listening on {self.pipe_name}")

        sa = self._create_pipe_security_attributes()
        if not sa:
            logger.critical("Failed to create security attributes. IPC server cannot start securely.")
            return # Cannot proceed without security attributes

        while not self._stop_event.is_set():
            self._pipe_handle = None # Reset handle for each connection attempt
            try:
                # Create the named pipe instance
                self._pipe_handle = win32pipe.CreateNamedPipe(
                    self.pipe_name,
                    win32pipe.PIPE_ACCESS_DUPLEX, # Allow read/write
                    win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
                    1, # Max instances = 1 (only one server)
                    PIPE_BUFFER_SIZE, # Out buffer size
                    PIPE_BUFFER_SIZE, # In buffer size
                    0, # Default timeout (NMPWAIT_USE_DEFAULT_WAIT) - use ConnectNamedPipe timeout
                    sa # Security attributes
                )
                logger.debug(f"Pipe {self.pipe_name} created. Waiting for client connection...")

                # Wait for a client to connect. This blocks until a client connects or an error occurs.
                # Add a timeout mechanism using overlapped I/O or check stop event periodically?
                # For simplicity, let's rely on ConnectNamedPipe blocking but check stop event before/after.
                if self._stop_event.is_set(): break # Check before blocking connect

                # This blocks until a client connects
                win32pipe.ConnectNamedPipe(self._pipe_handle, None)

                if self._stop_event.is_set(): break # Check after connect before processing

                logger.info(f"Client connected to pipe {self.pipe_name}.")

                # Process the client request
                self._handle_client_request()

            except pywintypes.error as e:
                # Common errors:
                # 2: ERROR_FILE_NOT_FOUND (shouldn't happen with CreateNamedPipe?)
                # 232: ERROR_NO_DATA (Pipe closing)
                # 536: ERROR_PIPE_CONNECTED (Already connected? Should be handled by loop)
                if e.winerror == 232: # ERROR_NO_DATA (Pipe closing)
                     logger.warning(f"Pipe {self.pipe_name} closing or client disconnected prematurely (Error 232).")
                elif e.winerror == 536: # ERROR_PIPE_CONNECTED
                     logger.debug(f"Pipe {self.pipe_name} already connected, likely race condition. Continuing.")
                     # Ensure handle is closed if needed before next loop iteration
                     self._close_pipe_handle()
                     time.sleep(0.1) # Small delay
                     continue
                else:
                     logger.error(f"Named pipe error (winerror {e.winerror}): {e}", exc_info=True)
                # Break the loop on significant errors? Or just log and retry?
                # Let's retry after a short delay unless it's a critical setup error.
                if self._pipe_handle:
                    self._close_pipe_handle() # Ensure handle is closed
                time.sleep(1) # Wait a bit before recreating the pipe

            except Exception as e:
                logger.error(f"Unexpected error in Named Pipe server loop: {e}", exc_info=True)
                if self._pipe_handle:
                    self._close_pipe_handle()
                time.sleep(1) # Wait before retrying

            finally:
                # Ensure the handle is closed and disconnected after handling a client or error
                if self._pipe_handle:
                    try:
                        win32pipe.DisconnectNamedPipe(self._pipe_handle)
                        logger.debug("Disconnected client.")
                    except pywintypes.error as disc_e:
                         # Error 232 (ERROR_NO_DATA) can happen if client already disconnected
                         if disc_e.winerror != 232:
                              logger.warning(f"Error disconnecting pipe client: {disc_e}")
                    finally:
                         self._close_pipe_handle()

        logger.info("Named Pipe IPC Server thread finished.")

    def _handle_client_request(self):
        """Reads request, authenticates, processes, and sends response."""
        request_data = None
        response = {"status": "error", "message": "Internal server error"} # Default error response

        try:
            # Read the request from the pipe
            logger.debug("Reading data from pipe...")
            hr, data_bytes = win32file.ReadFile(self._pipe_handle, PIPE_BUFFER_SIZE)
            if hr != 0: # Error reading
                 logger.error(f"ReadFile failed with error code: {hr}")
                 response = {"status": "error", "message": f"Pipe read error {hr}"}
                 return

            request_str = data_bytes.decode('utf-8')
            logger.debug(f"Received raw request: {request_str}")
            request_data = json.loads(request_str)

            # --- Authentication ---
            client_secret = request_data.get('secret')
            if not client_secret or client_secret != self.ipc_secret:
                logger.warning("IPC request received with invalid or missing secret.")
                response = {"status": "invalid_secret"}
                return

            # --- Command Processing ---
            command = request_data.get('command')
            if command == 'force_restart':
                logger.info("Received valid 'force_restart' command via IPC.")

                # Check agent state
                current_state = self.agent.get_state() # Assumes Agent has get_state()
                if current_state in [AgentState.UPDATING_STARTING, AgentState.UPDATING_DOWNLOADING,
                                     AgentState.UPDATING_VERIFYING, AgentState.UPDATING_EXTRACTING_UPDATER,
                                     AgentState.UPDATING_PREPARING_SHUTDOWN]:
                    logger.warning(f"Agent is currently updating (State: {current_state.name}). Rejecting force_restart.")
                    response = {"status": "busy_updating"}
                else:
                    logger.info("Acknowledging force_restart command. Initiating shutdown.")
                    response = {"status": "acknowledged"}
                    # Trigger agent shutdown asynchronously (don't block pipe communication)
                    # Use a callback or schedule it in the agent's event loop if available.
                    # For simplicity here, we'll call a method directly, assuming it handles async shutdown.
                    threading.Timer(0.1, self.agent.request_restart).start() # Small delay before triggering

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
            # Keep default error response

        finally:
            # Send the response back to the client
            try:
                response_str = json.dumps(response)
                logger.debug(f"Sending response: {response_str}")
                win32file.WriteFile(self._pipe_handle, response_str.encode('utf-8'))
            except pywintypes.error as e:
                 # Error 232 (ERROR_NO_DATA) can happen if client disconnected after sending request
                 if e.winerror == 232:
                      logger.warning("Client disconnected before response could be sent (Error 232).")
                 else:
                      logger.error(f"Failed to write response to pipe: {e}", exc_info=True)
            except Exception as e:
                 logger.error(f"Unexpected error sending response: {e}", exc_info=True)


    def _close_pipe_handle(self):
        """Safely closes the current pipe handle."""
        if self._pipe_handle:
            try:
                win32file.CloseHandle(self._pipe_handle)
                logger.debug(f"Closed pipe handle for {self.pipe_name}")
            except pywintypes.error as e:
                 logger.warning(f"Error closing pipe handle: {e}")
            finally:
                 self._pipe_handle = None

    def stop(self):
        """Signals the server thread to stop listening and cleans up."""
        logger.info("Stopping Named Pipe IPC Server...")
        self._stop_event.set()

        # To unblock ConnectNamedPipe, we need to connect to the pipe ourselves briefly.
        # This is a common pattern for stopping blocking pipe servers.
        if self.pipe_name:
             # Use a separate thread for the dummy connection to avoid deadlock
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
                       # Keep connection open briefly? Or just close? Close immediately.
                       win32file.CloseHandle(handle)
                       logger.debug("Dummy connection successful and closed.")
                  except pywintypes.error as e:
                       # Error 2 (File not found) is expected if server already shut down pipe
                       # Error 231 (All pipe instances are busy) might happen
                       if e.winerror not in [2, 231]:
                            logger.warning(f"Error during dummy pipe connection: {e}")
                  except Exception as e:
                       logger.warning(f"Unexpected error during dummy pipe connection: {e}")

             # Run dummy connect in a thread so it doesn't block 'stop'
             dummy_thread = threading.Thread(target=dummy_connect, daemon=True)
             dummy_thread.start()
             dummy_thread.join(timeout=1.0) # Wait briefly for it

        # Close the handle if it's still open (e.g., if stop was called while connected)
        self._close_pipe_handle()
        logger.info("Named Pipe IPC Server stop sequence initiated.")

