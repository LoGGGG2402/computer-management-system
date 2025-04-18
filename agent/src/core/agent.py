# -*- coding: utf-8 -*-
"""
Core Agent module for the Computer Management System.
Manages the agent's lifecycle, authentication, communication, monitoring,
and command execution coordination. Uses Dependency Injection.

**Change:** Removed periodic WebSocket connection check (_check_ws_connection).
Relies solely on WSClient's auto-reconnect mechanism.
"""
import time
import sys
import threading
import logging  # Added import for logging
from typing import Dict, Any, Optional

# Configuration
from src.config.config_manager import ConfigManager
# Communication
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient
# Monitoring
from src.monitoring.system_monitor import SystemMonitor
# Command Execution
from src.core.command_executor import CommandExecutor
# State Management
from src.config.state_manager import StateManager
# Console UI Interactions
from src.ui import ui_console
# Added for Phase 1
from src.core.agent_state import AgentState  # Import the enum
from src.system.lock_manager import LockManager  # Import for type hint
# IPC
from src.ipc.named_pipe_server import NamedPipeIPCServer, WINDOWS_PIPE_SUPPORT  # Added for IPC
# Use the centralized logger
from src.utils.logger import get_logger

try:
    import pywintypes  # Added import
except ImportError:
    pywintypes = None  # Define as None if import fails

# Get a properly configured logger instance
logger = get_logger("agent")

class Agent:
    """
    The main Agent class orchestrating all components via dependency injection.
    """

    def __init__(self,
                 config_manager: ConfigManager,
                 state_manager: StateManager,
                 http_client: HttpClient,
                 ws_client: WSClient,
                 system_monitor: SystemMonitor,
                 command_executor: CommandExecutor,
                 lock_manager: LockManager,
                 is_admin: bool):  # Added is_admin
        """
        Initialize the agent with its dependencies.
        """
        logger.info("Initializing Agent...")
        # --- State Management (Phase 1) ---
        self._state = AgentState.STARTING
        self._state_lock = threading.Lock()
        self._set_state(AgentState.STARTING)  # Explicitly set initial state with logging
        # --- End State Management ---

        self._running = threading.Event()
        self._status_timer: Optional[threading.Timer] = None
        self._ipc_server: Optional[NamedPipeIPCServer] = None  # Initialize _ipc_server to None

        # Store Injected Dependencies
        self.config = config_manager
        self.state_manager = state_manager
        self.http_client = http_client
        self.ws_client = ws_client
        self.system_monitor = system_monitor
        self.command_executor = command_executor
        self.lock_manager = lock_manager  # Store lock manager instance
        self.is_admin = is_admin  # Store is_admin

        # Essential Configuration Values
        self.status_report_interval = self.config.get('agent.status_report_interval_sec', 30)
        logger.info(f"Agent Config: Status Interval={self.status_report_interval}s")

        # Agent Identification & State
        self.device_id = self.state_manager.get_device_id()
        if not self.device_id:
            self._set_state(AgentState.STOPPED)  # Set state before raising error
            raise RuntimeError("Could not determine Device ID.")

        self.room_config = ui_console.get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
            self._set_state(AgentState.STOPPED)  # Set state before raising error
            raise RuntimeError("Could not determine Room Configuration.")

        self.agent_token: Optional[str] = None

        # Register WS message handler
        self.ws_client.register_message_handler(self.command_executor.handle_incoming_command)

        logger.info(f"Agent initialized (pre-auth). Device ID: {self.device_id}, Room: {self.room_config.get('room', 'N/A')}")

    # --- State Management Method (Phase 1) ---
    def _set_state(self, new_state: AgentState):
        """Sets the agent's state thread-safely and logs the transition."""
        if not isinstance(new_state, AgentState):
            logger.warning(f"Attempted to set invalid state type: {type(new_state)}")
            return

        with self._state_lock:
            if self._state != new_state:
                logger.info(f"State transition: {self._state.name} -> {new_state.name}")
                self._state = new_state

    def get_state(self) -> AgentState:
        """Gets the current agent state thread-safely."""
        with self._state_lock:
            return self._state
    # --- End State Management Method ---

    # --- IPC Request Handling ---
    def request_restart(self):
        """Called by the IPC server when a valid force_restart command is received."""
        logger.info("Restart requested via IPC. Initiating graceful shutdown.")
        self._set_state(AgentState.FORCE_RESTARTING)  # Set state before shutdown
        # Call graceful_shutdown asynchronously to avoid blocking the IPC server thread
        threading.Thread(target=self.graceful_shutdown, name="GracefulShutdownThread").start()

    # --- Authentication Flow ---
    def _handle_mfa_verification(self) -> bool:
        """Handles the MFA prompt and verification process."""
        logger.info("MFA required for registration.")
        mfa_code = ui_console.prompt_for_mfa()
        if not mfa_code:
            logger.warning("MFA prompt cancelled by user.")
            return False

        api_call_success, response = self.http_client.verify_mfa(self.device_id, mfa_code)

        if not api_call_success:
            error_msg = response.get('message', 'Lỗi kết nối hoặc lỗi máy chủ không xác định')
            logger.error(f"MFA verification API call failed. Error: {error_msg}")
            print(f"\nLỗi khi xác thực MFA với máy chủ: {error_msg}\n")
            return False

        if response.get('status') == 'success' and 'agentToken' in response:
            self.agent_token = response['agentToken']
            if self.state_manager.save_token(self.device_id, self.agent_token):
                logger.info("MFA verification successful. Agent registered, token saved.")
                ui_console.display_registration_success()
                return True
            else:
                logger.critical("MFA successful but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                print("\nCẢNH BÁO NGHIÊM TRỌNG: Xác thực MFA thành công nhưng không thể lưu token cục bộ. Agent có thể cần đăng ký lại sau khi khởi động lại.", file=sys.stderr)
                ui_console.display_registration_success()
                return True
        else:
            mfa_error = response.get('message', 'Mã MFA không hợp lệ hoặc đã hết hạn.')
            logger.error(f"MFA verification failed: {mfa_error}")
            print(f"\nXác thực MFA thất bại: {mfa_error}\n")
            return False

    def _process_identification_response(self, response: Dict[str, Any]) -> bool:
        """Processes the logical response from a successful /identify API call."""
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
                    print("\nCẢNH BÁO NGHIÊM TRỌNG: Xác định agent thành công nhưng không thể lưu token cục bộ. Agent có thể cần đăng ký lại sau khi khởi động lại.", file=sys.stderr)
                    return True
            else:
                logger.info(f"Agent already registered on server: {message}. Attempting to load existing token.")
                self.agent_token = self.state_manager.load_token(self.device_id)
                if self.agent_token:
                    logger.info("Successfully loaded existing token after server confirmation.")
                    return True
                else:
                    logger.error("Server indicates agent is registered, but no local token found via StateManager. Authentication failed.")
                    print("\nLỗi: Máy chủ báo agent đã đăng ký nhưng không tìm thấy token cục bộ. Vui lòng thử xóa file trạng thái hoặc liên hệ quản trị viên.", file=sys.stderr)
                    return False

        elif status == 'mfa_required':
            return self._handle_mfa_verification()

        elif status == 'position_error':
            logger.error(f"Server rejected agent due to position conflict: {message}")
            print(f"\nLỗi đăng ký: Xung đột vị trí tại máy chủ.")
            pos_x = self.room_config.get('position', {}).get('x', 'N/A')
            pos_y = self.room_config.get('position', {}).get('y', 'N/A')
            room_name = self.room_config.get('room', 'N/A')
            print(f"Vị trí ({pos_x}, {pos_y}) trong phòng '{room_name}' có thể đã được sử dụng.")
            print(f"Vui lòng kiểm tra cấu hình phòng hoặc liên hệ quản trị viên.\nChi tiết từ máy chủ: {message}\n")
            return False

        else:
            logger.error(f"Failed to identify/register agent. Unknown server status: '{status}', Message: '{message}'")
            print(f"\nLỗi đăng ký không xác định từ máy chủ. Status: {status}, Message: {message}\n")
            return False

    def authenticate(self) -> bool:
        """Authenticates the agent with the backend server."""
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
                print(f"\nKhông thể kết nối hoặc xác định agent với máy chủ: {error_msg}\n")
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
            print(f"\nĐã xảy ra lỗi không mong muốn trong quá trình xác thực: {e}\n", file=sys.stderr)
            logger.info("--- Authentication Failed (Unexpected Error) ---")
            return False

    # --- WebSocket Communication ---
    def _connect_websocket(self) -> bool:
        """Establishes and waits for authenticated WebSocket connection."""
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

    # --- Periodic Tasks ---
    def _send_status_update(self):
        """Fetches system stats and sends them via WebSocket."""
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
        """Schedules the next status report using a Timer thread."""
        if not self._running.is_set(): return
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
        self._status_timer = threading.Timer(self.status_report_interval, self._send_status_update)
        self._status_timer.daemon = True
        self._status_timer.start()
        logger.debug(f"Next status report scheduled in {self.status_report_interval} seconds.")

    # --- Hardware Information ---
    def _send_hardware_info(self):
        """Collects and sends detailed hardware information via HTTP."""
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

    # --- Agent Lifecycle ---
    def start(self):
        """Starts the agent's main lifecycle."""
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

            # --- Initialize and Start IPC Server (AFTER getting agent_token) ---
            if WINDOWS_PIPE_SUPPORT and self.agent_token:
                logger.info("Initializing Named Pipe IPC Server...")
                try:
                    self._ipc_server = NamedPipeIPCServer(self, self.is_admin, self.agent_token)
                    logger.info("Starting Named Pipe IPC Server...")
                    self._ipc_server.start()
                except (ImportError, ValueError, pywintypes.error if pywintypes else Exception) as e:
                    logger.error(f"Failed to initialize or start Named Pipe IPC Server: {e}", exc_info=True)
                    self._ipc_server = None  # Ensure it's None on failure
            elif not self.agent_token:
                logger.warning("Cannot start IPC Server: Agent token is missing after authentication.")
            else:  # WINDOWS_PIPE_SUPPORT is False
                logger.warning("IPC Server not supported or win32 modules missing.")
            # --- End IPC Server Initialization ---

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
        """Stops the agent gracefully, including IPC server and lock release."""
        if not self._running.is_set() and self.get_state() in [AgentState.SHUTTING_DOWN, AgentState.STOPPED, AgentState.FORCE_RESTARTING]:
            logger.debug("Graceful shutdown called but agent already stopping/stopped.")
            return

        # Set state only if not already force restarting
        current_state = self.get_state()
        if current_state != AgentState.FORCE_RESTARTING:
            self._set_state(AgentState.SHUTTING_DOWN)
        else:
            logger.info("Proceeding with shutdown due to force restart request.")

        logger.info("================ Initiating Graceful Shutdown ================")
        self._running.clear()

        # 1. Stop accepting new commands (Stop IPC Server first)
        if self._ipc_server and self._ipc_server.is_alive():
            logger.debug("Stopping Named Pipe IPC server...")
            self._ipc_server.stop()
            # Don't join immediately, allow other things to shut down concurrently

        # 2. Stop background tasks (timers, command workers)
        logger.debug("Cancelling timers...")
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
            logger.debug("Status timer cancelled.")
        # Add cancellation for other timers (e.g., polling) here if they exist

        if self.command_executor:
            logger.debug("Stopping command executor workers...")
            self.command_executor.stop()
            logger.debug("Command executor workers stopped.")

        # 3. Disconnect communications
        if self.ws_client:
            logger.debug("Disconnecting WebSocket client...")
            self.ws_client.disconnect()
            logger.debug("WebSocket client disconnected.")

        # 4. Wait for IPC server thread to finish
        if self._ipc_server and self._ipc_server.is_alive():
            logger.debug("Waiting for IPC server thread to join...")
            self._ipc_server.join(timeout=5.0)  # Wait with timeout
            if self._ipc_server.is_alive():
                logger.warning("IPC server thread did not join within timeout.")
            else:
                logger.debug("IPC server thread joined.")

        # 5. Flush logs (optional, depends on logging setup)
        try:
            logging.shutdown()
            logger.debug("Logging shutdown requested.")  # This logger might not work after shutdown
        except Exception as log_e:
            print(f"Error during logging shutdown: {log_e}", file=sys.stderr)

        # 6. Release the lock (CRITICAL: Do this *before* exiting)
        if self.lock_manager:
            logger.info("Releasing agent lock file...")
            self.lock_manager.release()  # LockManager handles actual file release
            logger.info("Agent lock file released.")
        else:
            logger.warning("Lock manager instance not found, cannot release lock explicitly.")

        self._set_state(AgentState.STOPPED)
        logger.info("================ Agent Shutdown Complete ================")

        # 7. Exit the process (optional, depends on how main loop is structured)
        # If start() runs in the main thread, this might not be needed,
        # but if start() is threaded, sys.exit() might be required here.
        # For now, assume main loop handles exit after start() returns.
        # sys.exit(0)

