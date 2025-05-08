"""
Core Agent module for the Computer Management System.
"""
import time
import threading
import os
import sys
from typing import Optional, TYPE_CHECKING, Dict, Any

if TYPE_CHECKING:
    from agent.communication import WSClient, ServerConnector
    from agent.config import StateManager, ConfigManager
    from agent.system import NamedMutexManager
    from agent.core import CommandExecutor
    
from agent.core import AgentState
from agent.core.update_handler import UpdateHandler
from agent.system import (
    create_admin_only_named_event, 
    wait_for_named_event, 
    close_handle
)

from agent.utils import get_logger
from agent.ui import get_or_prompt_room_config

logger = get_logger("agent")


AGENT_SHUTDOWN_EVENT_NAME = "Global\\CMSAgentSystemShutdownRequestEvent"

class Agent:
    """
    The main Agent class orchestrating all components of the Computer Management System.
    
    This class serves as the central controller that coordinates all agent activities including:
    - Authentication with the management server using device identification
    - Establishing and maintaining secure WebSocket connections for real-time communication
    - Sending regular status updates about system resource usage (CPU, RAM, disk)
    - Handling Windows-specific synchronization for single instance and shutdown coordination
    - Managing the agent's lifecycle through various operational states
    - Transmitting detailed system hardware information for inventory purposes
    - Coordinating command execution received from the management server
    
    The Agent implements a robust state management system using thread-safe operations
    and ensures proper resource cleanup during shutdown or restart scenarios.
    It also handles various error conditions including authentication failures,
    network connectivity issues, and unexpected exceptions.
    
    The class uses Windows synchronization primitives (Named Mutex) enforced through
    the Windows API to prevent multiple agent instances from running simultaneously.
    """

    def __init__(self,
                 config_manager: 'ConfigManager',
                 state_manager: 'StateManager',
                 ws_client: 'WSClient',
                 command_executor: 'CommandExecutor',
                 named_mutex_manager: 'NamedMutexManager',
                 server_connector: 'ServerConnector',
                 shutdown_event_name: str):
        """
        Initialize the agent with its dependencies.

        :param config_manager: Configuration manager instance
        :param state_manager: State manager for persistence
        :param ws_client: WebSocket client for real-time communication
        :param command_executor: Command execution component
        :param named_mutex_manager: Windows Named Mutex manager for single instance control
        :param server_connector: Handles server communication logic
        :param shutdown_event_name: Name of the global shutdown event
        :raises: RuntimeError if device ID or room configuration can't be determined
        """
        logger.info("Initializing Agent...")
        self._state = AgentState.STARTING
        self._state_lock = threading.Lock()
        self._set_state(AgentState.STARTING)

        self._running = threading.Event()
        self._status_timer: Optional[threading.Timer] = None
        self._shutdown_event_handle = None
        self._event_listener_thread: Optional[threading.Thread] = None
        self._should_stop_listening = threading.Event()

        self.config = config_manager
        self.state_manager = state_manager
        self.ws_client = ws_client
        self.command_executor = command_executor
        self.named_mutex_manager = named_mutex_manager
        self.server_connector = server_connector
        self.http_client = self.server_connector.http_client
        self.shutdown_event_name = shutdown_event_name

        
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
            if self.named_mutex_manager:
                self.named_mutex_manager.release()
            raise RuntimeError("Could not determine Device ID via StateManager.")

        self.room_config = get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
            self._set_state(AgentState.STOPPED)
            if self.named_mutex_manager:
                self.named_mutex_manager.release()
            raise RuntimeError("Could not determine Room Configuration.")

        
        self.ws_client.register_message_handler(self.command_executor.handle_incoming_command)
        
        
        self.ws_client.register_update_handler(self._handle_new_version_event)

        logger.info(f"Agent initialized (pre-auth). Device ID: {self.device_id}, Room: {self.room_config.get('room', 'N/A')}")

    def start(self):
        """
        Starts the agent's main lifecycle. Uses ServerConnector for communication tasks.
        Also sets up the listener for the shutdown event.
        """
        if self._running.is_set():
            logger.warning("Agent start requested but already running.")
            return

        self._set_state(AgentState.STARTING)

        logger.info("================ Starting Agent ================")
        self._running.set()

        try:
            
            self._setup_shutdown_event_listener()

            
            auth_successful = False
            while not auth_successful and self._running.is_set():
                logger.info("Attempting to authenticate with server...")
                if self.server_connector.authenticate_agent(self.room_config):
                    auth_successful = True
                    logger.info("Authentication successful!")
                else:
                    logger.warning("Authentication failed, retrying in 10 seconds...")
                    time.sleep(10)
            
            if not auth_successful:
                logger.critical("Authentication process aborted. Agent is shutting down.")
                self.graceful_shutdown()
                return
            
            self.command_executor.start_workers()

            
            self._schedule_next_status_report()
            

            self._set_state(AgentState.IDLE)
            logger.info("Agent started successfully. Monitoring for commands and reporting status.")
            self.update_handler.check_for_updates_proactively(
                self.get_state(),
            )  

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
        Stops the agent gracefully, including event threads and mutex release.
        """
        if not self._running.is_set() and self.get_state() in [AgentState.SHUTTING_DOWN, AgentState.STOPPED, AgentState.FORCE_RESTARTING]:
            logger.debug("Graceful shutdown called but agent already stopping/stopped.")
            return

        current_state = self.get_state()
        is_updating = current_state == AgentState.UPDATING_PREPARING_SHUTDOWN

        if not is_updating and current_state != AgentState.FORCE_RESTARTING:
            self._set_state(AgentState.SHUTTING_DOWN)
        elif current_state == AgentState.FORCE_RESTARTING:
            logger.info("Proceeding with shutdown due to force restart request.")
        elif is_updating:
            logger.info("Proceeding with shutdown as part of update process.")

        logger.info("================ Initiating Graceful Shutdown ================")
        self._running.clear()

        
        self._stop_shutdown_event_listener()

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

        if self.named_mutex_manager:
            logger.info("Releasing named mutex...")
            self.named_mutex_manager.release()
            logger.info("Named mutex released.")
        else:
            logger.warning("Mutex manager instance not found, cannot release mutex explicitly.")

        if not is_updating:
            self._set_state(AgentState.STOPPED)
        logger.info("================ Agent Shutdown Complete ================")
        
        
        import logging
        logging.shutdown()
        
        
        is_service_context = 'servicemanager' in sys.modules

        if not is_service_context and not is_updating:
            logger.info("Exiting process (not in service context and not updating).")
            sys.exit(0)
        else:
            logger.info("Graceful shutdown complete. Process will not exit (service context or updating).")

    def _setup_shutdown_event_listener(self):
        """
        Sets up the named event for external shutdown requests and starts a listener thread.
        """
        if not self.shutdown_event_name:
            logger.error("Shutdown event name not configured. Cannot set up listener.")
            return

        try:
            
            self._shutdown_event_handle = create_admin_only_named_event(self.shutdown_event_name)
            
            if not self._shutdown_event_handle:
                logger.warning(f"Failed to create named event '{self.shutdown_event_name}'. External shutdown triggers will not work.")
                return
                
            logger.info(f"Created shutdown event: {self.shutdown_event_name}")
            
            
            self._should_stop_listening.clear()
            
            
            self._event_listener_thread = threading.Thread(
                target=self._event_listener_thread_target,
                name="ShutdownEventListener"
            )
            self._event_listener_thread.daemon = True
            self._event_listener_thread.start()
            
            logger.info("Shutdown event listener thread started")
            
        except Exception as e:
            logger.error(f"Error setting up shutdown event listener: {e}", exc_info=True)
            
            if self._shutdown_event_handle:
                try:
                    close_handle(self._shutdown_event_handle)
                    self._shutdown_event_handle = None
                except Exception:
                    pass

    def _stop_shutdown_event_listener(self):
        """
        Stops the shutdown event listener thread and cleans up resources.
        """
        
        if self._event_listener_thread and self._event_listener_thread.is_alive():
            logger.debug("Stopping shutdown event listener thread...")
            self._should_stop_listening.set()
            
            
            self._event_listener_thread.join(timeout=5.0)
            if self._event_listener_thread.is_alive():
                logger.warning("Shutdown event listener thread did not stop gracefully")
            else:
                logger.debug("Shutdown event listener thread stopped gracefully")
        
        
        if self._shutdown_event_handle:
            try:
                close_handle(self._shutdown_event_handle)
                logger.debug(f"Closed shutdown event handle for '{self.shutdown_event_name}'")
            except Exception as e:
                logger.error(f"Error closing shutdown event handle: {e}")
            finally:
                self._shutdown_event_handle = None

    def _event_listener_thread_target(self):
        """
        Thread that waits for the shutdown event to be signaled.
        """
        logger.debug("Shutdown event listener thread started")
        
        while not self._should_stop_listening.is_set():
            try:
                if not self._shutdown_event_handle:
                    logger.warning("Shutdown event handle is not valid, listener cannot wait.")
                    self._should_stop_listening.set()
                    break

                
                if wait_for_named_event(self._shutdown_event_handle, 5000):  
                    if self._should_stop_listening.is_set():
                        logger.debug("Shutdown event signaled, but listener is stopping. Ignoring.")
                        break
                    self._handle_shutdown_request()
                    break
            except Exception as e:
                logger.error(f"Error in shutdown event listener thread: {e}", exc_info=True)
                time.sleep(5)  
        
        logger.debug("Shutdown event listener thread exiting")

    def _handle_shutdown_request(self):
        """
        Handles an external shutdown request received via the named event.
        """
        logger.info("External shutdown request received. Initiating graceful shutdown.")
        self._set_state(AgentState.FORCE_RESTARTING)
        
        
        threading.Thread(
            target=self.graceful_shutdown,
            name="GracefulShutdownThread"
        ).start()

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
        Sets the agent state thread-safely.
        
        :param new_state: New agent state to set
        :type new_state: AgentState
        :return: True if state changed, False otherwise
        :rtype: bool
        """
        with self._state_lock:
            if self._state != new_state:
                logger.info(f"State transition: {self._state.name} -> {new_state.name}")
                self._state = new_state
                return True
            return False

    def _schedule_next_status_report(self):
        """
        Schedules the next status report to be sent.
        """
        if not self._running.is_set():
            return
            
        try:
            if self._status_timer:
                self._status_timer.cancel()
                
            
            self._status_timer = threading.Timer(
                self.status_report_interval,
                self._send_status_report
            )
            self._status_timer.daemon = True
            self._status_timer.start()
            
        except Exception as e:
            logger.error(f"Error scheduling next status report: {e}", exc_info=True)

    def _send_status_report(self):
        """
        Sends a status report to the server and schedules the next one.
        """
        if not self._running.is_set():
            return
            
        try:
            
            success = self.server_connector.send_status_report()
            
            if not success:
                logger.warning("Failed to send status report")
                
        except Exception as e:
            logger.error(f"Error sending status report: {e}", exc_info=True)
            
        finally:
            
            self._schedule_next_status_report()

    def _handle_new_version_event(self, payload: Dict[str, Any]):
        """
        Handles the 'new_version_available' event from the server.
        
        :param payload: Event payload with update information
        :type payload: Dict[str, Any]
        """
        if not self._running.is_set():
            logger.warning("Received update notification but agent is shutting down. Ignoring.")
            return
            
        try:
            
            self.update_handler.handle_new_version_event(payload, self.get_state())
        except Exception as e:
            logger.error(f"Error handling new version event: {e}", exc_info=True)
