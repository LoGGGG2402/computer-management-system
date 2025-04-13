# -*- coding: utf-8 -*-
"""
Core Agent module for the Computer Management System.
Manages the agent's lifecycle, authentication, communication, monitoring,
and command execution coordination. Uses Dependency Injection.
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
# Console UI Interactions (Imported from the new ui module)
from src.ui import ui_console

logger = logging.getLogger("agent") # Use root agent logger name

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

        Args:
            config_manager (ConfigManager): Instance for configuration access.
            state_manager (StateManager): Instance for managing persistent state.
            http_client (HttpClient): Instance for HTTP communication.
            ws_client (WSClient): Instance for WebSocket communication.
            system_monitor (SystemMonitor): Instance for system monitoring.
            command_executor (CommandExecutor): Instance for executing commands.

        Raises:
            RuntimeError: If agent state (like room config or device ID) cannot be determined.
        """
        logger.info("Initializing Agent...")
        self._running = threading.Event()
        # Initialize timer attributes to None
        self._status_timer: Optional[threading.Timer] = None
        self._ws_connection_check_timer: Optional[threading.Timer] = None

        # --- Store Injected Dependencies ---
        self.config = config_manager
        self.state_manager = state_manager
        self.http_client = http_client
        self.ws_client = ws_client
        self.system_monitor = system_monitor
        self.command_executor = command_executor
        # Note: ui_console functions are called directly, not stored as instance var

        # --- Essential Configuration Values ---
        self.status_report_interval = self.config.get('agent.status_report_interval_sec', 30)
        logger.info(f"Agent Config: Status Interval={self.status_report_interval}s")

        # --- Agent Identification & State ---
        # Device ID is ensured by StateManager during its initialization (called in main.py)
        self.device_id = self.state_manager.get_device_id()
        if not self.device_id:
             # This should not happen if StateManager init was successful
             logger.critical("Device ID not available from StateManager after initialization.")
             raise RuntimeError("Could not determine Device ID.")

        # Get initial room config, prompt if missing/invalid using the UI function
        # Pass the state_manager instance to the function
        self.room_config = ui_console.get_or_prompt_room_config(self.state_manager)
        if not self.room_config:
             # Prompt was cancelled or failed validation after prompt
             logger.critical("Failed to obtain valid Room Configuration during initialization.")
             raise RuntimeError("Could not determine Room Configuration.")

        self.agent_token: Optional[str] = None # Loaded/obtained during authentication

        # --- Register WS message handler ---
        # Do this after CommandExecutor is initialized
        self.ws_client.register_message_handler(self.command_executor.handle_incoming_command)

        logger.info(f"Agent initialized. Device ID: {self.device_id}, Room: {self.room_config.get('room', 'N/A')}")


    # --- Authentication Flow ---

    def _handle_mfa_verification(self) -> bool:
        """Handles the MFA prompt and verification process."""
        logger.info("MFA required for registration.")
        # Use the imported UI function
        mfa_code = ui_console.prompt_for_mfa()
        if not mfa_code:
            logger.warning("MFA prompt cancelled by user.")
            return False # User cancelled

        # Call HTTP client to verify
        api_call_success, response = self.http_client.verify_mfa(self.device_id, mfa_code)

        if not api_call_success:
            # Network/Server error during verification
            error_msg = response.get('message', 'Lỗi kết nối hoặc lỗi máy chủ không xác định')
            logger.error(f"MFA verification API call failed. Error: {error_msg}")
            print(f"\nLỗi khi xác thực MFA với máy chủ: {error_msg}\n")
            return False

        # API call succeeded (2xx), check logical status from server response
        if response.get('status') == 'success' and 'agentToken' in response:
            self.agent_token = response['agentToken']
            # Use StateManager to save the newly acquired token
            if self.state_manager.save_token(self.device_id, self.agent_token):
                logger.info("MFA verification successful. Agent registered, token saved.")
                # Use the imported UI function
                ui_console.display_registration_success()
                return True
            else:
                # CRITICAL: MFA succeeded but token couldn't be saved locally!
                # The error is already logged by state_manager.save_token if using file fallback
                logger.critical("MFA successful but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                print("\nCẢNH BÁO NGHIÊM TRỌNG: Xác thực MFA thành công nhưng không thể lưu token cục bộ. Agent có thể cần đăng ký lại sau khi khởi động lại.", file=sys.stderr)
                # Use the imported UI function
                ui_console.display_registration_success() # Still show success for this session
                return True # Allow session to continue, but state is inconsistent
        else:
            # Server reported MFA failure (e.g., wrong code, expired)
            mfa_error = response.get('message', 'Mã MFA không hợp lệ hoặc đã hết hạn.')
            logger.error(f"MFA verification failed: {mfa_error}")
            print(f"\nXác thực MFA thất bại: {mfa_error}\n")
            return False

    def _process_identification_response(self, response: Dict[str, Any]) -> bool:
        """
        Processes the logical response from a successful /identify API call.

        Args:
            response (Dict[str, Any]): The JSON response data from the server.

        Returns:
            bool: True if authentication flow should proceed successfully, False otherwise.
        """
        status = response.get('status')
        message = response.get('message', 'No message from server.')

        if status == 'success':
            # Identification successful, check if new token provided or if already registered
            if 'agentToken' in response:
                # Server provided a new token (e.g., first registration, forced renewal)
                self.agent_token = response['agentToken']
                # Use StateManager to save the new token
                if self.state_manager.save_token(self.device_id, self.agent_token):
                    logger.info("Agent registered/identified successfully, new token saved.")
                    return True
                else:
                    # CRITICAL: Identified but couldn't save token!
                    logger.critical("Agent identified but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                    print("\nCẢNH BÁO NGHIÊM TRỌNG: Xác định agent thành công nhưng không thể lưu token cục bộ. Agent có thể cần đăng ký lại sau khi khởi động lại.", file=sys.stderr)
                    return True # Allow session to continue
            else:
                # Server confirms agent is known, but didn't issue a new token
                logger.info(f"Agent already registered on server: {message}. Attempting to load existing token.")
                # Use StateManager to load the existing token
                self.agent_token = self.state_manager.load_token(self.device_id)
                if self.agent_token:
                    logger.info("Successfully loaded existing token after server confirmation.")
                    return True
                else:
                    # Problem: Server says registered, but we have no local token.
                    logger.error("Server indicates agent is registered, but no local token found via StateManager. Authentication failed.")
                    print("\nLỗi: Máy chủ báo agent đã đăng ký nhưng không tìm thấy token cục bộ. Vui lòng thử xóa file trạng thái hoặc liên hệ quản trị viên.", file=sys.stderr)
                    return False

        elif status == 'mfa_required':
            # MFA is needed, trigger the MFA flow
            return self._handle_mfa_verification()

        elif status == 'position_error':
            # Server rejected registration due to position conflict
            logger.error(f"Server rejected agent due to position conflict: {message}")
            print(f"\nLỗi đăng ký: Xung đột vị trí tại máy chủ.")
            # Ensure room_config and its nested keys exist before accessing
            pos_x = self.room_config.get('position', {}).get('x', 'N/A')
            pos_y = self.room_config.get('position', {}).get('y', 'N/A')
            room_name = self.room_config.get('room', 'N/A')
            print(f"Vị trí ({pos_x}, {pos_y}) trong phòng '{room_name}' có thể đã được sử dụng.")
            print(f"Vui lòng kiểm tra cấu hình phòng hoặc liên hệ quản trị viên.\nChi tiết từ máy chủ: {message}\n")
            return False

        else: # Handle other unexpected statuses from the server
            logger.error(f"Failed to identify/register agent. Unknown server status: '{status}', Message: '{message}'")
            print(f"\nLỗi đăng ký không xác định từ máy chủ. Status: {status}, Message: {message}\n")
            return False

    def authenticate(self) -> bool:
        """
        Authenticates the agent with the backend server.
        Loads existing token or performs identification/MFA flow.

        Returns:
            bool: True if authentication is successful, False otherwise.
        """
        logger.info("--- Starting Authentication Process ---")

        # 1. Try loading existing token first
        self.agent_token = self.state_manager.load_token(self.device_id)
        if self.agent_token:
            logger.info("Found existing agent token via StateManager. Assuming authenticated for now.")
            # TODO: Optionally add a step here to *verify* the token with the server?
            #       (e.g., a simple GET /api/agent/status endpoint requiring auth)
            #       This would ensure the token is still valid on the server side.
            #       For now, we assume loaded token is valid.
            logger.info("--- Authentication Successful (Used Existing Token) ---")
            return True

        # 2. No existing token, proceed with identification
        logger.info("No existing token found. Attempting identification with server...")
        try:
            # Room config should be valid at this point due to checks in __init__
            api_call_success, response = self.http_client.identify_agent(
                self.device_id,
                self.room_config, # Pass the validated room config
                force_renew=False # Don't force renewal unless necessary
            )

            if not api_call_success:
                # Network or server error during the API call itself
                error_msg = response.get('message', 'Lỗi kết nối hoặc lỗi máy chủ không xác định')
                logger.error(f"Agent identification API call failed. Error: {error_msg}")
                print(f"\nKhông thể kết nối hoặc xác định agent với máy chủ: {error_msg}\n")
                logger.info("--- Authentication Failed (API Call Error) ---")
                return False

            # API call was successful (2xx), process the logical response
            auth_logic_success = self._process_identification_response(response)

            if auth_logic_success:
                 logger.info("--- Authentication Successful ---")
            else:
                 # Reason for failure already logged by _process_identification_response or _handle_mfa_verification
                 logger.info("--- Authentication Failed (Server Logic/MFA/Error) ---")
            return auth_logic_success

        except Exception as e:
            # Catch unexpected errors during the whole authentication attempt
            logger.critical(f"An unexpected error occurred during the authentication process: {e}", exc_info=True)
            print(f"\nĐã xảy ra lỗi không mong muốn trong quá trình xác thực: {e}\n", file=sys.stderr)
            logger.info("--- Authentication Failed (Unexpected Error) ---")
            return False

    # --- WebSocket Communication ---

    def _connect_websocket(self) -> bool:
        """Establishes and authenticates the WebSocket connection."""
        if not self.agent_token:
            logger.error("Cannot connect WebSocket: Agent token is missing (Authentication likely failed).")
            return False

        logger.info("Attempting to connect to WebSocket server...")
        if not self.ws_client.connect_and_authenticate(self.device_id, self.agent_token):
             # Error initiating connection (logged by ws_client)
             return False

        # Wait for connection confirmation
        if not self.ws_client.wait_for_connection(timeout=15.0): # Use a reasonable timeout
            logger.error("WebSocket connection attempt timed out.")
            # Optionally try to disconnect cleanly, though it might already be disconnected
            self.ws_client.disconnect()
            return False

        logger.info("WebSocket connection established and authenticated.")
        return True

    # --- Periodic Tasks ---

    def _send_status_update(self):
        """Fetches system stats and sends them via WebSocket."""
        if not self._running.is_set(): return # Stop if agent is stopping

        if not self.ws_client.connected:
            logger.warning("Cannot send status update: WebSocket not connected.")
            # No need to schedule next report if disconnected, _check_ws_connection handles reconnect logic
            return

        try:
            stats = self.system_monitor.get_usage_stats()
            status_data = {
                "cpuUsage": stats.get("cpu", 0.0),
                "ramUsage": stats.get("ram", 0.0),
                "diskUsage": stats.get("disk", 0.0),
                # Add timestamp?
                # "timestamp": time.time()
            }
            logger.debug(f"Sending status update: {status_data}")
            if not self.ws_client.send_status_update(status_data):
                # Error logged by ws_client
                pass
        except Exception as e:
            logger.error(f"Error during status update collection or sending: {e}", exc_info=True)
        finally:
            # Schedule the next report regardless of success/failure of this one,
            # as long as the agent is running.
            if self._running.is_set():
                 self._schedule_next_status_report()

    def _schedule_next_status_report(self):
        """Schedules the next status report using a Timer thread."""
        # This function is now only responsible for scheduling the *next* call.
        # The actual sending is done in _send_status_update.
        # The timer is started initially in start() and then rescheduled within _send_status_update.
        if not self._running.is_set(): return

        # Cancel existing timer if it exists (e.g., if called manually)
        # *** FIX: Use self._status_timer ***
        if self._status_timer and self._status_timer.is_alive():
            self._status_timer.cancel()

        # Schedule the next call to _send_status_update
        # *** FIX: Use self._status_timer ***
        self._status_timer = threading.Timer(self.status_report_interval, self._send_status_update)
        self._status_timer.daemon = True # Allow program exit even if timer is waiting
        self._status_timer.start()
        logger.debug(f"Next status report scheduled in {self.status_report_interval} seconds.")

    def _check_ws_connection(self):
         """Periodically checks WebSocket connection and attempts reconnect if needed."""
         if not self._running.is_set(): return

         check_interval = 60 # Check every 60 seconds (make configurable?)

         if not self.ws_client.connected:
              logger.warning("WebSocket connection check: Currently disconnected. WSClient auto-reconnect is active.")
              # WSClient handles reconnection attempts automatically based on its config.
              # No explicit reconnect attempt needed here unless WSClient's mechanism fails.
         else:
             logger.debug("WebSocket connection check: Currently connected.")
             # If connected, ensure command executor workers are running (they might have stopped on error)
             # Note: CommandExecutor doesn't currently have a way to check/restart workers easily.
             # This might require adding a health check to CommandExecutor.
             # For now, assume workers started by start() are running if WS is connected.

         # Schedule the next check
         if self._running.is_set():
             # *** FIX: Use self._ws_connection_check_timer ***
             self._ws_connection_check_timer = threading.Timer(check_interval, self._check_ws_connection)
             self._ws_connection_check_timer.daemon = True
             self._ws_connection_check_timer.start()
             logger.debug(f"Next WebSocket connection check scheduled in {check_interval} seconds.")


    # --- Hardware Information ---

    def _send_hardware_info(self):
        """Collects and sends detailed hardware information via HTTP."""
        if not self.agent_token:
            logger.error("Cannot send hardware info: Agent token is missing.")
            return False # Indicate failure

        logger.info("Collecting and sending hardware information...")
        try:
            hardware_info = self.system_monitor.get_hardware_info()
            api_call_success, response = self.http_client.send_hardware_info(
                self.agent_token,
                self.device_id,
                hardware_info
            )

            if api_call_success:
                # Server responded with 2xx
                logger.info("Hardware information sent successfully.")
                return True
            else:
                # Network/Server error
                error_msg = response.get('message', 'Lỗi không xác định từ máy chủ')
                logger.error(f"Failed to send hardware information. Server response: {error_msg}")
                return False # Indicate failure

        except Exception as e:
            logger.error(f"Error collecting or sending hardware info: {e}", exc_info=True)
            return False # Indicate failure

    # --- Agent Lifecycle ---

    def start(self):
        """Starts the agent's main lifecycle."""
        if self._running.is_set():
             logger.warning("Agent start requested but already running.")
             return

        logger.info("================ Starting Agent ================")
        self._running.set()

        # 1. Authenticate
        if not self.authenticate():
            logger.critical("Authentication failed. Agent cannot start.")
            self.stop() # Ensure cleanup if auth fails
            return # Exit start method

        # 2. Send initial hardware info (after successful authentication)
        self._send_hardware_info() # Log errors internally, continue even if it fails initially

        # 3. Connect WebSocket
        if not self._connect_websocket():
             logger.warning("Failed to establish initial WebSocket connection. Will rely on auto-reconnect.")
             # Agent will still run, relying on WSClient's reconnect attempts
        else:
             logger.info("Initial WebSocket connection successful.")

        # 4. Start Command Executor Workers (after WS connection attempt)
        self.command_executor.start_workers()

        # 5. Start Periodic Tasks
        self._send_status_update() # Send first status immediately
        # _schedule_next_status_report() is called within _send_status_update
        self._check_ws_connection() # Start connection checker loop

        logger.info("Agent started successfully. Monitoring for commands and reporting status.")
        logger.info("Nhấn Ctrl+C để dừng agent.")

        # Keep main thread alive while background threads run
        try:
            while self._running.is_set():
                # Keep main thread alive, maybe sleep longer
                time.sleep(5) # Check running flag every 5 seconds
        except KeyboardInterrupt:
            logger.info("Keyboard interrupt received (Ctrl+C). Stopping agent...")
        except Exception as e:
            # Catch unexpected errors in the main loop (should be rare)
            logger.critical(f"Critical error in agent main loop: {e}", exc_info=True)
        finally:
            self.stop()

    def stop(self):
        """Stops the agent gracefully."""
        if not self._running.is_set():
             # logger.info("Agent stop requested but already stopped or stopping.")
             return # Already stopped

        logger.info("================ Stopping Agent ================")
        self._running.clear() # Signal all loops/threads to stop

        # 1. Cancel Timers
        logger.debug("Cancelling timers...")
        # *** FIX: Use self._status_timer ***
        if self._status_timer and self._status_timer.is_alive():
             self._status_timer.cancel()
             logger.debug("Status timer cancelled.")
        # *** FIX: Use self._ws_connection_check_timer ***
        if self._ws_connection_check_timer and self._ws_connection_check_timer.is_alive():
             self._ws_connection_check_timer.cancel()
             logger.debug("WS connection check timer cancelled.")

        # 2. Stop Command Executor (waits for workers)
        if self.command_executor:
            logger.debug("Stopping command executor...")
            self.command_executor.stop()
            logger.debug("Command executor stopped.")

        # 3. Disconnect WebSocket
        if self.ws_client:
            logger.debug("Disconnecting WebSocket client...")
            self.ws_client.disconnect() # Intentional disconnect
            logger.debug("WebSocket client disconnected.")

        # Allow some time for threads to potentially finish logging etc.
        time.sleep(0.5)
        logger.info("Agent stopped.")

