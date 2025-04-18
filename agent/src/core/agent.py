"""
Core Agent module for the Computer Management System.
"""
import time
import threading
import logging
from typing import Dict, Any, Optional

from src.config.config_manager import ConfigManager
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient
from src.monitoring.system_monitor import SystemMonitor
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
                 http_client: HttpClient,
                 ws_client: WSClient,
                 system_monitor: SystemMonitor,
                 command_executor: CommandExecutor,
                 lock_manager: LockManager,
                 is_admin: bool):
        """
        Initialize the agent with its dependencies.

        :param config_manager: Configuration manager instance
        :type config_manager: ConfigManager
        :param state_manager: State manager for persistence
        :type state_manager: StateManager
        :param http_client: HTTP client for API calls
        :type http_client: HttpClient
        :param ws_client: WebSocket client for real-time communication
        :type ws_client: WSClient
        :param system_monitor: System monitoring component
        :type system_monitor: SystemMonitor
        :param command_executor: Command execution component
        :type command_executor: CommandExecutor
        :param lock_manager: Lock manager for single instance control
        :type lock_manager: LockManager
        :param is_admin: Whether agent is running with admin privileges
        :type is_admin: bool
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
        self.http_client = http_client
        self.ws_client = ws_client
        self.system_monitor = system_monitor
        self.command_executor = command_executor
        self.lock_manager = lock_manager
        self.is_admin = is_admin

        self.status_report_interval = self.config.get('agent.status_report_interval_sec', 30)
        logger.info(f"Agent Config: Status Interval={self.status_report_interval}s")

        self.device_id = self.state_manager.get_device_id()
        if not self.device_id:
            self._set_state(AgentState.STOPPED)
            raise RuntimeError("Could not determine Device ID.")

        self.room_config = ui_console.get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
            self._set_state(AgentState.STOPPED)
            raise RuntimeError("Could not determine Room Configuration.")

        self.agent_token: Optional[str] = None

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

    def _handle_mfa_verification(self) -> bool:
        """
        Handles the MFA prompt and verification process.
        
        :return: True if MFA verification successful, False otherwise
        :rtype: bool
        """
        logger.info("MFA required for registration.")
        mfa_code = ui_console.prompt_for_mfa()
        if not mfa_code:
            logger.warning("MFA prompt cancelled by user.")
            return False

        api_call_success, response = self.http_client.verify_mfa(self.device_id, mfa_code)

        if not api_call_success:
            error_msg = response.get('message', 'Lỗi kết nối hoặc lỗi máy chủ không xác định')
            logger.error(f"MFA verification API call failed. Error: {error_msg}")
            return False

        if response.get('status') == 'success' and 'agentToken' in response:
            self.agent_token = response['agentToken']
            if self.state_manager.save_token(self.device_id, self.agent_token):
                logger.info("MFA verification successful. Agent registered, token saved.")
                ui_console.display_registration_success()
                return True
            else:
                logger.critical("MFA successful but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                ui_console.display_registration_success()
                return True
        else:
            mfa_error = response.get('message', 'Mã MFA không hợp lệ hoặc đã hết hạn.')
            logger.error(f"MFA verification failed: {mfa_error}")
            return False

    def _process_identification_response(self, response: Dict[str, Any]) -> bool:
        """
        Processes the logical response from a successful /identify API call.
        
        :param response: Response data from API
        :type response: Dict[str, Any]
        :return: True if identification successful, False otherwise
        :rtype: bool
        """
        status = response.get('status')
        message = response.get('message', 'No message from server.')

        if status == 'success':
            if 'agentToken' in response:
                self.agent_token = response['agentToken']
                if self.state_manager.save_token(self.device_id, self.agent_token):
                    logger.info("Agent registered/identified successfully, new token saved.")
                    return True
                else:
                    logger.critical("Agent identified but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                    return True
            else:
                logger.info(f"Agent already registered on server: {message}. Attempting to load existing token.")
                self.agent_token = self.state_manager.load_token(self.device_id)
                if self.agent_token:
                    logger.info("Successfully loaded existing token after server confirmation.")
                    return True
                else:
                    logger.error("Server indicates agent is registered, but no local token found via StateManager. Authentication failed.")
                    return False

        elif status == 'mfa_required':
            return self._handle_mfa_verification()

        elif status == 'position_error':
            logger.error(f"Server rejected agent due to position conflict: {message}")
            pos_x = self.room_config.get('position', {}).get('x', 'N/A')
            pos_y = self.room_config.get('position', {}).get('y', 'N/A')
            room_name = self.room_config.get('room', 'N/A')
            return False

        else:
            logger.error(f"Failed to identify/register agent. Unknown server status: '{status}', Message: '{message}'")
            return False

    def authenticate(self) -> bool:
        """
        Authenticates the agent with the backend server.
        
        :return: True if authentication successful, False otherwise
        :rtype: bool
        """
        logger.info("--- Starting Authentication Process ---")
        self.agent_token = self.state_manager.load_token(self.device_id)
        if self.agent_token:
            logger.info("Found existing agent token. Assuming authenticated.")
            logger.info("--- Authentication Successful (Used Existing Token) ---")
            return True

        logger.info("No existing token found. Attempting identification with server...")
        try:
            api_call_success, response = self.http_client.identify_agent(
                self.device_id,
                self.room_config,
                force_renew=False
            )
            if not api_call_success:
                error_msg = response.get('message', 'Lỗi kết nối hoặc lỗi máy chủ không xác định')
                logger.error(f"Agent identification API call failed. Error: {error_msg}")
                logger.info("--- Authentication Failed (API Call Error) ---")
                return False

            auth_logic_success = self._process_identification_response(response)
            if auth_logic_success:
                logger.info("--- Authentication Successful ---")
            else:
                logger.info("--- Authentication Failed (Server Logic/MFA/Error) ---")
            return auth_logic_success
        except Exception as e:
            logger.critical(f"An unexpected error occurred during the authentication process: {e}", exc_info=True)
            logger.info("--- Authentication Failed (Unexpected Error) ---")
            return False

    def _connect_websocket(self) -> bool:
        """
        Establishes and waits for authenticated WebSocket connection.
        
        :return: True if connection successful, False otherwise
        :rtype: bool
        """
        if not self.agent_token:
            logger.error("Cannot connect WebSocket: Agent token is missing.")
            return False

        logger.info("Attempting to connect and authenticate WebSocket...")
        if not self.ws_client.connect_and_authenticate(self.device_id, self.agent_token):
            return False

        if not self.ws_client.wait_for_authentication(timeout=20.0):
            logger.error("WebSocket connection and authentication attempt timed out or failed.")
            self.ws_client.disconnect()
            return False

        logger.info("WebSocket connection established and authenticated.")
        return True

    def _send_status_update(self):
        """
        Fetches system stats and sends them via WebSocket.
        """
        if not self._running.is_set(): return

        if not self.ws_client.connected:
            logger.warning("Cannot send status update: WebSocket not connected/authenticated.")
            if self._running.is_set():
                self._schedule_next_status_report()
            return

        try:
            stats = self.system_monitor.get_usage_stats()
            status_data = {
                "cpuUsage": stats.get("cpu", 0.0),
                "ramUsage": stats.get("ram", 0.0),
                "diskUsage": stats.get("disk", 0.0),
            }
            logger.debug(f"Sending status update: {status_data}")
            if not self.ws_client.send_status_update(status_data):
                pass
        except Exception as e:
            logger.error(f"Error during status update collection or sending: {e}", exc_info=True)
        finally:
            if self._running.is_set():
                self._schedule_next_status_report()

    def _schedule_next_status_report(self):
        """
        Schedules the next status report using a Timer thread.
        """
        if not self._running.is_set(): return
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
        self._status_timer = threading.Timer(self.status_report_interval, self._send_status_update)
        self._status_timer.daemon = True
        self._status_timer.start()
        logger.debug(f"Next status report scheduled in {self.status_report_interval} seconds.")

    def _send_hardware_info(self):
        """
        Collects and sends detailed hardware information via HTTP.
        
        :return: True if hardware info sent successfully, False otherwise
        :rtype: bool
        """
        if not self.agent_token:
            logger.error("Cannot send hardware info: Agent token is missing.")
            return False

        logger.info("Collecting and sending hardware information...")
        try:
            hardware_info = self.system_monitor.get_hardware_info()
            api_call_success, response = self.http_client.send_hardware_info(
                self.agent_token,
                self.device_id,
                hardware_info
            )
            if api_call_success:
                logger.info("Hardware information sent successfully.")
                return True
            else:
                error_msg = response.get('message', 'Lỗi không xác định từ máy chủ')
                logger.error(f"Failed to send hardware information. Server response: {error_msg}")
                return False
        except Exception as e:
            logger.error(f"Error collecting or sending hardware info: {e}", exc_info=True)
            return False

    def start(self):
        """
        Starts the agent's main lifecycle.
        """
        if self._running.is_set():
            logger.warning("Agent start requested but already running.")
            return

        self._set_state(AgentState.STARTING)

        logger.info("================ Starting Agent ================")
        self._running.set()

        try:
            if not self.authenticate():
                logger.critical("Authentication failed. Agent cannot start.")
                self.graceful_shutdown()
                return

            if WINDOWS_PIPE_SUPPORT and self.agent_token:
                logger.info("Initializing Named Pipe IPC Server...")
                try:
                    self._ipc_server = NamedPipeIPCServer(self, self.is_admin, self.agent_token)
                    logger.info("Starting Named Pipe IPC Server...")
                    self._ipc_server.start()
                except (ImportError, ValueError, pywintypes.error if pywintypes else Exception) as e:
                    logger.error(f"Failed to initialize or start Named Pipe IPC Server: {e}", exc_info=True)
                    self._ipc_server = None
            elif not self.agent_token:
                logger.warning("Cannot start IPC Server: Agent token is missing after authentication.")
            else:
                logger.warning("IPC Server not supported or win32 modules missing.")

            self._send_hardware_info()

            if not self._connect_websocket():
                logger.warning("Failed to establish and authenticate initial WebSocket connection. Will rely on auto-reconnect.")
            else:
                logger.info("Initial WebSocket connection and authentication successful.")

            self.command_executor.start_workers()

            self._send_status_update()

            self._set_state(AgentState.IDLE)

            logger.info("Agent started successfully. Monitoring for commands and reporting status.")
            logger.info("Nhấn Ctrl+C để dừng agent.")

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

