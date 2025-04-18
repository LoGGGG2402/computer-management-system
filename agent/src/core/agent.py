"""
Core Agent module for the Computer Management System.
"""
import time
import threading
import logging
from typing import Dict, Any, Optional

from src.config.config_manager import ConfigManager
from src.communication.ws_client import WSClient
from src.communication.server_connector import ServerConnector
from src.core.command_executor import CommandExecutor
from src.config.state_manager import StateManager
from src.ui import ui_console
from src.core.agent_state import AgentState
from src.system.lock_manager import LockManager
from src.ipc.named_pipe_server import NamedPipeIPCServer, WINDOWS_PIPE_SUPPORT
from src.utils.logger import get_logger

try:
    import pywintypes
except ImportError:
    pywintypes = None

logger = get_logger("agent")

class Agent:
    """
    The main Agent class orchestrating all components of the Computer Management System.
    
    This class serves as the central controller that coordinates all agent activities including:
    - Authentication with the management server using device identification
    - Establishing and maintaining secure WebSocket connections for real-time communication
    - Sending regular status updates about system resource usage (CPU, RAM, disk)
    - Handling IPC for agent restart commands and inter-process coordination
    - Managing the agent's lifecycle through various operational states
    - Transmitting detailed system hardware information for inventory purposes
    - Coordinating command execution received from the management server
    
    The Agent implements a robust state management system using thread-safe operations
    and ensures proper resource cleanup during shutdown or restart scenarios.
    It also handles various error conditions including authentication failures,
    network connectivity issues, and unexpected exceptions.
    
    The class uses a singleton pattern enforced through file locking to prevent
    multiple agent instances from running simultaneously on the same machine.
    """

    def __init__(self,
                 config_manager: ConfigManager,
                 state_manager: StateManager,
                 ws_client: WSClient,
                 command_executor: CommandExecutor,
                 lock_manager: LockManager,
                 server_connector: ServerConnector,
                 is_admin: bool):
        """
        Initialize the agent with its dependencies.

        :param config_manager: Configuration manager instance
        :param state_manager: State manager for persistence
        :param ws_client: WebSocket client for real-time communication
        :param command_executor: Command execution component
        :param lock_manager: Lock manager for single instance control
        :param server_connector: Handles server communication logic
        :param is_admin: Whether agent is running with admin privileges
        :raises: RuntimeError if device ID or room configuration can't be determined
        """
        logger.info("Initializing Agent...")
        self._state = AgentState.STARTING
        self._state_lock = threading.Lock()
        self._set_state(AgentState.STARTING)

        self._running = threading.Event()
        self._status_timer: Optional[threading.Timer] = None
        self._ipc_server: Optional[NamedPipeIPCServer] = None

        self.config = config_manager
        self.state_manager = state_manager
        self.ws_client = ws_client
        self.command_executor = command_executor
        self.lock_manager = lock_manager
        self.server_connector = server_connector
        self.is_admin = is_admin

        self.status_report_interval = self.config.get('agent.status_report_interval_sec', 30)
        logger.info(f"Agent Config: Status Interval={self.status_report_interval}s")

        self.device_id = self.state_manager.get_device_id()
        if not self.device_id:
            self._set_state(AgentState.STOPPED)
            raise RuntimeError("Could not determine Device ID via StateManager.")

        self.room_config = ui_console.get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
            self._set_state(AgentState.STOPPED)
            raise RuntimeError("Could not determine Room Configuration.")

        self.ws_client.register_message_handler(self.command_executor.handle_incoming_command)

        logger.info(f"Agent initialized (pre-auth). Device ID: {self.device_id}, Room: {self.room_config.get('room', 'N/A')}")

    def _set_state(self, new_state: AgentState):
        """
        Sets the agent's state thread-safely and logs the transition.
        
        :param new_state: New state to set
        :type new_state: AgentState
        """
        if not isinstance(new_state, AgentState):
            logger.warning(f"Attempted to set invalid state type: {type(new_state)}")
            return

        with self._state_lock:
            if self._state != new_state:
                logger.info(f"State transition: {self._state.name} -> {new_state.name}")
                self._state = new_state

    def get_state(self) -> AgentState:
        """
        Gets the current agent state thread-safely.
        
        :return: Current agent state
        :rtype: AgentState
        """
        with self._state_lock:
            return self._state

    def request_restart(self):
        """
        Called by the IPC server when a valid force_restart command is received.
        """
        logger.info("Restart requested via IPC. Initiating graceful shutdown.")
        self._set_state(AgentState.FORCE_RESTARTING)
        threading.Thread(target=self.graceful_shutdown, name="GracefulShutdownThread").start()

    def _send_status_update(self):
        """
        Sends a status update using the ServerConnector. Schedules the next one.
        """
        if not self._running.is_set(): return

        logger.debug("Requesting ServerConnector to send status update.")
        if not self.server_connector.send_status_update():
            pass

        if self._running.is_set():
            self._schedule_next_status_report()

    def _schedule_next_status_report(self):
        """
        Schedules the next status report using a Timer thread.
        The timer calls _send_status_update which uses the ServerConnector.
        """
        if not self._running.is_set(): return
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
        self._status_timer = threading.Timer(self.status_report_interval, self._send_status_update)
        self._status_timer.daemon = True
        self._status_timer.start()
        logger.debug(f"Next status report scheduled in {self.status_report_interval} seconds.")

    def start(self):
        """
        Starts the agent's main lifecycle. Uses ServerConnector for communication tasks.
        """
        if self._running.is_set():
            logger.warning("Agent start requested but already running.")
            return

        self._set_state(AgentState.STARTING)

        logger.info("================ Starting Agent ================")
        self._running.set()

        try:
            # --- IPC Server Setup ---
            # Use default token "123" for initial IPC server setup
            default_token = "123"
            if WINDOWS_PIPE_SUPPORT:
                logger.info("Initializing Named Pipe IPC Server with default token...")
                try:
                    # Initialize IPC server with default token first
                    self._ipc_server = NamedPipeIPCServer(self, self.is_admin, default_token)
                    logger.info("Starting Named Pipe IPC Server...")
                    self._ipc_server.start()
                except (ImportError, ValueError, pywintypes.error if pywintypes else Exception) as e:
                    logger.error(f"Failed to initialize or start Named Pipe IPC Server: {e}", exc_info=True)
                    self._ipc_server = None # Ensure it's None if setup fails
            else: # Not WINDOWS_PIPE_SUPPORT
                logger.warning("IPC Server not supported or win32 modules missing.")
            # --- IPC Server End ---

            # --- Authentication with Retry Logic ---
            auth_successful = False
            while not auth_successful and self._running.is_set():
                logger.info("Attempting to authenticate with server...")
                if self.server_connector.authenticate_agent(self.room_config):
                    auth_successful = True
                    logger.info("Authentication successful!")
                    
                    # Get token after successful authentication
                    agent_token_for_ipc = self.server_connector.get_agent_token()
                    
                    # Update IPC server with real token
                    if self._ipc_server and agent_token_for_ipc:
                        logger.info("Updating IPC Server with real authentication token...")
                        self._ipc_server.update_token(agent_token_for_ipc)
                        logger.info("IPC Server token updated successfully.")
                else:
                    logger.warning("Authentication failed, retrying in 10 seconds...")
                    time.sleep(10)
            
            if not auth_successful:
                # This would happen if _running was set to False during auth attempts
                logger.critical("Authentication process aborted. Agent is shutting down.")
                self.graceful_shutdown()
                return
            # --- Authentication End ---

            # Start command executor workers (still managed by Agent)
            self.command_executor.start_workers()

            # --- Start Status Reporting ---
            # Schedule the first status report (which uses the connector)
            self._send_status_update()
            # --- Status Reporting End ---

            self._set_state(AgentState.IDLE)
            logger.info("Agent started successfully. Monitoring for commands and reporting status.")

            # Main loop
            while self._running.is_set():
                # Check WebSocket connection periodically? WSClient handles reconnects.
                # Maybe add a check here: if not self.ws_client.connected: logger.warning("WS disconnected...")
                time.sleep(5) # Main loop sleep

        except KeyboardInterrupt:
            logger.info("Keyboard interrupt received (Ctrl+C). Stopping agent...")
        except Exception as e:
            logger.critical(f"Critical error in agent main loop: {e}", exc_info=True)
            self._set_state(AgentState.STOPPED) # Ensure state is updated on critical failure
        finally:
            self.graceful_shutdown() # Ensure cleanup happens

    def graceful_shutdown(self):
        """
        Stops the agent gracefully, including IPC server and lock release.
        """
        if not self._running.is_set() and self.get_state() in [AgentState.SHUTTING_DOWN, AgentState.STOPPED, AgentState.FORCE_RESTARTING]:
            logger.debug("Graceful shutdown called but agent already stopping/stopped.")
            return

        current_state = self.get_state()
        if current_state != AgentState.FORCE_RESTARTING:
            self._set_state(AgentState.SHUTTING_DOWN)
        else:
            logger.info("Proceeding with shutdown due to force restart request.")

        logger.info("================ Initiating Graceful Shutdown ================")
        self._running.clear()

        if self._ipc_server and self._ipc_server.is_alive():
            logger.debug("Stopping Named Pipe IPC server...")
            self._ipc_server.stop()

        logger.debug("Cancelling timers...")
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
            logger.debug("Status timer cancelled.")

        if self.command_executor:
            logger.debug("Stopping command executor workers...")
            self.command_executor.stop()
            logger.debug("Command executor workers stopped.")

        if self.ws_client:
            logger.debug("Disconnecting WebSocket client...")
            self.ws_client.disconnect()
            logger.debug("WebSocket client disconnected.")

        if self._ipc_server and self._ipc_server.is_alive():
            logger.debug("Waiting for IPC server thread to join...")
            self._ipc_server.join(timeout=5.0)
            if self._ipc_server.is_alive():
                logger.warning("IPC server thread did not join within timeout.")
            else:
                logger.debug("IPC server thread joined.")

        try:
            logging.shutdown()
            logger.debug("Logging shutdown requested.")
        except Exception as log_e:
            logger.error(f"Error during logging shutdown: {log_e}", exc_info=True)

        if self.lock_manager:
            logger.info("Releasing agent lock file...")
            self.lock_manager.release()
            logger.info("Agent lock file released.")
        else:
            logger.warning("Lock manager instance not found, cannot release lock explicitly.")

        self._set_state(AgentState.STOPPED)
        logger.info("================ Agent Shutdown Complete ================")

