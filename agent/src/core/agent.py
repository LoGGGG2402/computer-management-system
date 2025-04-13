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
import logging
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

logger = logging.getLogger("agent")

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
                 command_executor: CommandExecutor):
        """
        Initialize the agent with its dependencies.
        """
        logger.info("Initializing Agent...")
        self._running = threading.Event()
        self._status_timer: Optional[threading.Timer] = None
        # --- REMOVED: WebSocket connection check timer ---
        # self._ws_connection_check_timer: Optional[threading.Timer] = None
        # -------------------------------------------------

        # Store Injected Dependencies
        self.config = config_manager
        self.state_manager = state_manager
        self.http_client = http_client
        self.ws_client = ws_client
        self.system_monitor = system_monitor
        self.command_executor = command_executor

        # Essential Configuration Values
        self.status_report_interval = self.config.get('agent.status_report_interval_sec', 30)
        logger.info(f"Agent Config: Status Interval={self.status_report_interval}s")

        # Agent Identification & State
        self.device_id = self.state_manager.get_device_id()
        if not self.device_id:
             raise RuntimeError("Could not determine Device ID.")

        self.room_config = ui_console.get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
             raise RuntimeError("Could not determine Room Configuration.")

        self.agent_token: Optional[str] = None

        # Register WS message handler
        self.ws_client.register_message_handler(self.command_executor.handle_incoming_command)

        logger.info(f"Agent initialized. Device ID: {self.device_id}, Room: {self.room_config.get('room', 'N/A')}")


    # --- Authentication Flow ---
    # _handle_mfa_verification and _process_identification_response remain unchanged
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
            # TODO: Optionally verify token validity with server here
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
            # Schedule next report even if disconnected, to retry later
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
                pass # Error logged by ws_client
        except Exception as e:
            logger.error(f"Error during status update collection or sending: {e}", exc_info=True)
        finally:
            # Schedule the next report regardless of success/failure of this one
            if self._running.is_set():
                 self._schedule_next_status_report()

    def _schedule_next_status_report(self):
        """Schedules the next status report using a Timer thread."""
        if not self._running.is_set(): return
        # Cancel existing timer if it exists
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()
        # Schedule the next call
        self._status_timer = threading.Timer(self.status_report_interval, self._send_status_update)
        self._status_timer.daemon = True
        self._status_timer.start()
        logger.debug(f"Next status report scheduled in {self.status_report_interval} seconds.")

    # --- REMOVED: _check_ws_connection method ---
    # def _check_ws_connection(self): ...
    # --------------------------------------------

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

        logger.info("================ Starting Agent ================")
        self._running.set()

        # 1. Authenticate (HTTP)
        if not self.authenticate():
            logger.critical("Authentication failed. Agent cannot start.")
            self.stop()
            return

        # 2. Send initial hardware info
        self._send_hardware_info()

        # 3. Connect WebSocket
        if not self._connect_websocket():
             logger.warning("Failed to establish and authenticate initial WebSocket connection. Will rely on auto-reconnect.")
        else:
             logger.info("Initial WebSocket connection and authentication successful.")

        # 4. Start Command Executor Workers
        self.command_executor.start_workers()

        # 5. Start Periodic Tasks
        self._send_status_update() # Send first status immediately (if authenticated)
        # --- REMOVED: Call to start WS connection check ---
        # self._check_ws_connection()
        # -------------------------------------------------

        logger.info("Agent started successfully. Monitoring for commands and reporting status.")
        logger.info("Nhấn Ctrl+C để dừng agent.")

        try:
            while self._running.is_set():
                time.sleep(5)
        except KeyboardInterrupt:
            logger.info("Keyboard interrupt received (Ctrl+C). Stopping agent...")
        except Exception as e:
            logger.critical(f"Critical error in agent main loop: {e}", exc_info=True)
        finally:
            self.stop()

    def stop(self):
        """Stops the agent gracefully."""
        if not self._running.is_set():
             return

        logger.info("================ Stopping Agent ================")
        self._running.clear()

        # Cancel Timers
        logger.debug("Cancelling timers...")
        if self._status_timer and self._status_timer.is_alive():
             self._status_timer.cancel()
             logger.debug("Status timer cancelled.")
        # --- REMOVED: Cancelling WS connection check timer ---
        # if self._ws_connection_check_timer and self._ws_connection_check_timer.is_alive():
        #      self._ws_connection_check_timer.cancel()
        #      logger.debug("WS connection check timer cancelled.")
        # ----------------------------------------------------

        # Stop Command Executor
        if self.command_executor:
            logger.debug("Stopping command executor...")
            self.command_executor.stop()
            logger.debug("Command executor stopped.")

        # Disconnect WebSocket
        if self.ws_client:
            logger.debug("Disconnecting WebSocket client...")
            self.ws_client.disconnect()
            logger.debug("WebSocket client disconnected.")

        time.sleep(0.5)
        logger.info("Agent stopped.")

