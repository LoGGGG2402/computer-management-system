"""
Core Agent module for the Computer Management System.
"""
import time
import threading
import pywintypes
import os
import sys
from typing import Optional, TYPE_CHECKING, Dict, Any

if TYPE_CHECKING:
    from agent.communication import WSClient, ServerConnector
    from agent.config import StateManager, ConfigManager
    from agent.system import LockManager
    from agent.core import CommandExecutor
    
from agent.core import AgentState
from agent.core.update_handler import UpdateHandler
from agent.ipc import NamedPipeIPCServer

from agent.utils import get_logger
from agent.ui import get_or_prompt_room_config

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
                 config_manager: 'ConfigManager',
                 state_manager: 'StateManager',
                 ws_client: 'WSClient',
                 command_executor: 'CommandExecutor',
                 lock_manager: 'LockManager',
                 server_connector: 'ServerConnector',
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
        self._error_reporting_thread: Optional[threading.Thread] = None
        self._error_reporting_complete = threading.Event()

        self.config = config_manager
        self.state_manager = state_manager
        self.ws_client = ws_client
        self.command_executor = command_executor
        self.lock_manager = lock_manager
        self.server_connector = server_connector
        self.is_admin = is_admin
        self.http_client = self.server_connector.http_client

        # Initialize update handler
        self.update_handler = UpdateHandler(
            state_manager=self.state_manager,
            http_client=self.http_client,
            server_connector=self.server_connector,
            set_state_callback=self._set_state,
            shutdown_callback=self.graceful_shutdown
        )

        self.status_report_interval = self.config.get('agent.status_report_interval_sec', 30)
        logger.info(f"Agent Config: Status Interval={self.status_report_interval}s")

        self.device_id = self.state_manager.get_device_id()
        if not self.device_id:
            self._set_state(AgentState.STOPPED)
            raise RuntimeError("Could not determine Device ID via StateManager.")

        self.room_config = get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
            self._set_state(AgentState.STOPPED)
            raise RuntimeError("Could not determine Room Configuration.")

        # Register handlers for commands and update notifications
        self.ws_client.register_message_handler(self.command_executor.handle_incoming_command)
        
        # Register update handler - only register with WebSocket client
        # Server connector will get updates through WebSocket events
        self.ws_client.register_update_handler(self._handle_new_version_event)

        logger.info(f"Agent initialized (pre-auth). Device ID: {self.device_id}, Room: {self.room_config.get('room', 'N/A')}")

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
            default_token = "123"
            logger.info("Initializing Named Pipe IPC Server with default token...")
            try:
                self._ipc_server = NamedPipeIPCServer(self, self.is_admin, default_token)
                logger.info("Starting Named Pipe IPC Server...")
                self._ipc_server.start()
            except (ImportError, ValueError, pywintypes.error) as e:
                logger.error(f"Failed to initialize or start Named Pipe IPC Server: {e}", exc_info=True)
                self._ipc_server = None            # --- IPC Server End ---

            # --- Authentication with Retry Logic ---
            auth_successful = False
            while not auth_successful and self._running.is_set():
                logger.info("Attempting to authenticate with server...")
                if self.server_connector.authenticate_agent(self.room_config):
                    auth_successful = True
                    logger.info("Authentication successful!")
                    
                    agent_token_for_ipc = self.server_connector.get_agent_token()
                    
                    if self._ipc_server and agent_token_for_ipc:
                        logger.info("Updating IPC Server with real authentication token...")
                        self._ipc_server.update_token(agent_token_for_ipc)
                        logger.info("IPC Server token updated successfully.")
                    
                    # Start error reporting thread after successful authentication
                    self._start_error_reporting_thread()
                else:
                    logger.warning("Authentication failed, retrying in 10 seconds...")
                    time.sleep(10)
            
            if not auth_successful:
                logger.critical("Authentication process aborted. Agent is shutting down.")
                self.graceful_shutdown()
                return
            # --- Authentication End ---
            self.command_executor.start_workers()

            # --- Start Status Reporting ---
            self._schedule_next_status_report()
            # --- Status Reporting End ---

            self._set_state(AgentState.IDLE)
            logger.info("Agent started successfully. Monitoring for commands and reporting status.")
            self.update_handler.check_for_updates_proactively(
                self.get_state(),
            )  # --- Update Check End ---

            while self._running.is_set():
                time.sleep(5)

        except KeyboardInterrupt:
            logger.info("Keyboard interrupt received (Ctrl+C). Stopping agent...")
        except Exception as e:
            logger.critical(f"Critical error in agent main loop: {e}", exc_info=True)
            self._set_state(AgentState.STOPPED)        
        finally:
            self.graceful_shutdown()

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

        # Wait for error reporting thread to complete if it's running
        if self._error_reporting_thread and self._error_reporting_thread.is_alive():
            logger.debug("Waiting for error reporting thread to complete...")
            self._error_reporting_complete.wait(timeout=10.0)
            if not self._error_reporting_complete.is_set():
                logger.warning("Error reporting thread did not complete within timeout")

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

        if self.lock_manager:
            logger.info("Releasing agent lock file...")
            self.lock_manager.release()
            logger.info("Agent lock file released.")
        else:
            logger.warning("Lock manager instance not found, cannot release lock explicitly.")

        self._set_state(AgentState.STOPPED)
        logger.info("================ Agent Shutdown Complete ================")
        
        # Flush all log handlers to ensure logs are written to disk
        import logging
        logging.shutdown()
        
        # Exit the process if this is part of a complete shutdown (not an update preparation)
        if current_state != AgentState.UPDATING_PREPARING_SHUTDOWN:
            sys.exit(0)

    def request_restart(self):
        """
        Called by the IPC server when a valid force_restart command is received.
        """
        logger.info("Restart requested via IPC. Initiating graceful shutdown.")
        self._set_state(AgentState.FORCE_RESTARTING)
        threading.Thread(target=self.graceful_shutdown, name="GracefulShutdownThread").start()

    def get_state(self) -> AgentState:
        """
        Gets the current agent state thread-safely.
        
        :return: Current agent state
        :rtype: AgentState
        """
        with self._state_lock:
            return self._state

    def _set_state(self, new_state: AgentState):
        """
        Sets the agent's state thread-safely and logs the transition.
        
        :param new_state: New state to set
        :type new_state: AgentState
        :return: True if state was changed, False if the transition is not allowed
        :rtype: bool
        """
        if not isinstance(new_state, AgentState):
            logger.warning(f"Attempted to set invalid state type: {type(new_state)}")
            return False

        with self._state_lock:
            current_state = self._state
            
            # Special case: don't allow transitions from update-related states back to IDLE
            # unless coming from UPDATING_STARTING (allows rollback if update initialization fails)
            if (current_state.name.startswith('UPDATING_') and 
                current_state != AgentState.UPDATING_STARTING and 
                new_state == AgentState.IDLE):
                logger.warning(f"Blocked state transition: {current_state.name} -> {new_state.name} (not allowed during update)")
                return False
                
            if self._state != new_state:
                logger.info(f"State transition: {self._state.name} -> {new_state.name}")
                self._state = new_state
                return True
            return False

    def _schedule_next_status_report(self):
        """
        Schedules the next status report using a Timer thread.
        The timer calls server_connector.send_status_update directly.
        """
        if not self._running.is_set(): return
        
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
            
        def status_update_callback():
            if not self._running.is_set(): return
            
            logger.debug("Sending status update via ServerConnector.")
            self.server_connector.send_status_update()
            
            if self._running.is_set():
                self._schedule_next_status_report()
                
        self._status_timer = threading.Timer(self.status_report_interval, status_update_callback)
        self._status_timer.daemon = True
        self._status_timer.start()
        logger.debug(f"Next status report scheduled in {self.status_report_interval} seconds.")

    def _handle_new_version_event(self, payload: Dict[str, Any]):
        """
        Handles new version notifications by delegating to the update handler.
        
        :param payload: New version event payload
        :type payload: Dict[str, Any]
        """
        # Delegate to update handler
        self.update_handler.handle_new_version_event(payload, self.get_state())

    def _start_error_reporting_thread(self):
        """
        Starts a thread to read and report any buffered error files from previous runs.
        """
        if self._error_reporting_thread and self._error_reporting_thread.is_alive():
            logger.warning("Error reporting thread is already running")
            return

        self._error_reporting_complete.clear()
        
        # Get the error directory from the state manager
        error_dir = os.path.join(self.state_manager.storage_path, "error_reports")
        if not error_dir:
            logger.warning("Cannot determine error directory path, skipping error reporting")
            self._error_reporting_complete.set()
            return
            
        # Create and start the thread
        self._error_reporting_thread = threading.Thread(
            target=self.server_connector.process_error_reports_thread,
            args=(error_dir, self._error_reporting_complete.set),
            name="ErrorReportingThread", 
            daemon=True
        )
        
        logger.info("Starting error reporting thread")
        self._error_reporting_thread.start()
