# -*- coding: utf-8 -*-
"""
WebSocket client module for real-time communication with the backend server.
Handles connection, authentication, receiving commands, and sending updates/results.
Accepts ConfigManager instance for configuration parameters like reconnect delays.

**Change:** Ensures authentication is confirmed by the server before allowing emits.
"""
import socketio
import logging
import threading
import time
import json # Added for logging data in debug
from typing import Dict, Any, Callable, Optional

# Configuration
from src.config.config_manager import ConfigManager # Import class for type hinting

logger = logging.getLogger(__name__)

class WSClient:
    """
    Manages the WebSocket connection and communication logic.
    Ensures authentication is confirmed before emitting messages.
    """

    def __init__(self, config: ConfigManager):
        """
        Initialize the WebSocket client.

        Args:
            config (ConfigManager): The configuration manager instance.

        Raises:
            ValueError: If server_url is not configured.
        """
        self.config = config
        server_url = self.config.get('server_url')
        if not server_url:
            raise ValueError("Server URL (server_url) not found in configuration.")

        self.server_url = server_url

        # --- Read connection parameters from config ---
        reconnect_delay_initial = self.config.get('websocket.reconnect_delay_initial_sec', 5)
        reconnect_delay_max = self.config.get('websocket.reconnect_delay_max_sec', 60)
        reconnect_attempts_config = self.config.get('websocket.reconnect_attempts_max', None)
        reconnect_attempts_max = None
        if reconnect_attempts_config is not None:
            try:
                attempts = int(float(reconnect_attempts_config))
                reconnect_attempts_max = attempts if attempts >= 0 else None
            except (ValueError, TypeError):
                logger.warning(f"Invalid value for websocket.reconnect_attempts_max: '{reconnect_attempts_config}'. Using infinite attempts.")
                reconnect_attempts_max = None

        logger.info(f"WebSocket Config: URL={self.server_url}, Initial Delay={reconnect_delay_initial}s, Max Delay={reconnect_delay_max}s, Max Attempts={reconnect_attempts_max or 'Infinite'}")
        # ---------------------------------------------

        self.sio = socketio.Client(
            reconnection=True,
            reconnection_attempts=reconnect_attempts_max,
            reconnection_delay=reconnect_delay_initial,
            reconnection_delay_max=reconnect_delay_max,
            randomization_factor=0.5,
            logger=False,
            engineio_logger=False
        )
        # --- MODIFIED: Use authenticated_event instead of connected_event ---
        self._authenticated_event = threading.Event() # Signals successful authentication confirmation
        # --------------------------------------------------------------------
        self._message_handler: Optional[Callable[[Dict[str, Any]], None]] = None
        self._agent_id: Optional[str] = None
        self._agent_token: Optional[str] = None
        self._connection_lock = threading.Lock()
        self._is_intentionally_disconnected = False

        self._setup_event_handlers()
        logger.info(f"WebSocket client initialized for server: {self.server_url}")

    @property
    def connected(self) -> bool:
        """
        Return the current connection status.
        True only if the underlying socket is connected AND authentication is confirmed.
        """
        # --- MODIFIED: Check sio connection AND authentication event ---
        return self.sio.connected and self._authenticated_event.is_set()
        # ---------------------------------------------------------------

    def _setup_event_handlers(self):
        """Register handlers for standard Socket.IO and custom agent events."""
        self.sio.on('connect', self._on_connect)
        self.sio.on('disconnect', self._on_disconnect)
        self.sio.on('connect_error', self._on_connect_error)
        self.sio.on('reconnect', self._on_reconnect)
        self.sio.on('reconnecting', self._on_reconnecting)

        # Command execution event from server
        self.sio.on('command:execute', self._on_command_message)

        # Server confirmation of WebSocket authentication
        # --- IMPORTANT: These handlers now control the _authenticated_event ---
        self.sio.on('agent:ws_auth_success', self._on_auth_success)
        self.sio.on('agent:ws_auth_failed', self._on_auth_failed)
        # ----------------------------------------------------------------------

        logger.debug("Standard WebSocket event handlers registered.")

    def _on_connect(self):
        """Callback executed upon successful WebSocket connection (transport layer)."""
        logger.info(f"WebSocket transport connected. SID: {self.sio.sid}. Waiting for authentication confirmation...")
        self._is_intentionally_disconnected = False
        # --- MODIFIED: Do NOT set the event here. Wait for _on_auth_success ---
        # self._authenticated_event.set() # REMOVED
        # ----------------------------------------------------------------------
        # Authentication data was sent during the handshake via connect_and_authenticate.
        # Now we wait for the server's response ('agent:ws_auth_success' or 'agent:ws_auth_failed').

    def _on_disconnect(self):
        """Callback executed upon WebSocket disconnection."""
        # --- MODIFIED: Clear authenticated_event ---
        was_authenticated = self._authenticated_event.is_set()
        self._authenticated_event.clear() # Signal authentication is no longer valid
        # -----------------------------------------
        if self._is_intentionally_disconnected:
            logger.info("Disconnected from WebSocket server (intentional).")
        else:
            if was_authenticated:
                 logger.warning("Authenticated WebSocket connection lost unexpectedly. Auto-reconnect mechanism active.")
            else:
                 logger.warning("WebSocket connection lost before authentication completed. Auto-reconnect mechanism active.")
            # Reconnection attempts handled automatically

    def _on_connect_error(self, data):
        """Callback executed when a connection attempt fails."""
        logger.error(f"WebSocket connection failed: {data}")
        # --- MODIFIED: Clear authenticated_event ---
        self._authenticated_event.clear()
        # -----------------------------------------

    def _on_reconnect(self):
        """Callback executed upon successful reconnection (transport layer)."""
        # Note: This is similar to _on_connect. Authentication needs confirmation again.
        logger.info(f"WebSocket transport reconnected. SID: {self.sio.sid}. Waiting for authentication confirmation...")
        # --- MODIFIED: Do NOT set the event here. Wait for _on_auth_success ---
        # self._authenticated_event.set() # REMOVED
        # ----------------------------------------------------------------------
        # The library likely resends auth headers/payload on reconnect if configured.
        # We still need the server to send 'agent:ws_auth_success' again.

    def _on_reconnecting(self, attempt_number: int):
        """Callback executed when the client is attempting to reconnect."""
        logger.warning(f"Attempting to reconnect to WebSocket server... (Attempt {attempt_number})")

    def _on_command_message(self, data: Any):
        """Handles incoming 'command:execute' messages from the server."""
        # --- MODIFIED: Check authentication status early ---
        if not self._authenticated_event.is_set():
             logger.warning(f"Ignoring command message: WebSocket is connected but not authenticated. Data: {data}")
             # Optionally inform the server? Depends on protocol.
             return
        # -------------------------------------------------

        if not isinstance(data, dict):
            logger.warning(f"Received non-dictionary command message: {data}. Ignoring.")
            return

        command_id = data.get('commandId') or data.get('id')
        if not command_id:
             logger.error(f"Received command message without 'commandId' or 'id': {data}. Ignoring.")
             return

        command_payload = data.get('command')
        if command_payload is None:
             logger.error(f"Received command message missing 'command' payload for ID {command_id}: {data}. Ignoring.")
             self.send_command_result(command_id, {"stderr": "Agent Error: Missing command payload", "exitCode": -1})
             return

        if self._message_handler:
            command_type = data.get('commandType', 'unknown')
            logger.debug(f"Received command (ID: {command_id}, Type: {command_type}), routing to handler.")
            try:
                self._message_handler(data)
            except Exception as e:
                logger.error(f"Error in message handler while processing command {command_id}: {e}", exc_info=True)
                self.send_command_result(command_id, {
                    "stdout": "",
                    "stderr": f"Agent internal error processing command: {e}",
                    "exitCode": -1
                })
        else:
            logger.warning(f"No message handler registered. Ignoring command: {command_id}")
            self.send_command_result(command_id, {"stderr": "Agent Error: No handler registered for command", "exitCode": -1})

    def _on_auth_success(self, data: Any):
        """Callback for server confirming successful WebSocket authentication."""
        logger.info(f"WebSocket authentication confirmed by server. Agent is ready. Data: {data}")
        # --- MODIFIED: Set authenticated_event ---
        self._authenticated_event.set()
        # -----------------------------------------

    def _on_auth_failed(self, data: Any):
        """Callback for server indicating WebSocket authentication failed."""
        error_message = data.get('message', 'No reason provided') if isinstance(data, dict) else str(data)
        logger.error(f"WebSocket authentication failed (reported by server): {error_message}")
        # --- MODIFIED: Ensure authenticated_event is clear ---
        self._authenticated_event.clear()
        # ---------------------------------------------------
        # Consider disconnecting if auth fails definitively?
        # self.disconnect() # Optional: Force disconnect on auth failure

    def register_message_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """Registers a callback function to handle incoming command messages."""
        if not callable(callback):
            raise TypeError("Handler must be a callable function.")
        self._message_handler = callback
        logger.info("Command message handler registered successfully.")

    def connect_and_authenticate(self, agent_id: str, token: str) -> bool:
        """
        Initiates connection to the WebSocket server with authentication credentials.
        Actual success is asynchronous and signaled by _authenticated_event.
        """
        if not agent_id or not token:
            logger.error("Connection attempt failed: Agent ID and Token are required.")
            return False

        with self._connection_lock:
            # --- MODIFIED: Check sio.connected directly, not self.connected property ---
            if self.sio.connected:
                logger.warning("Connection attempt skipped: Already connected or connecting.")
                # If already authenticated, return True. If connecting, let it proceed.
                return self._authenticated_event.is_set()
            # ---------------------------------------------------------------------------

            # Clear authentication status before attempting connection
            self._authenticated_event.clear()
            self._agent_id = agent_id
            self._agent_token = token
            self._is_intentionally_disconnected = False

            headers = {
                "Agent-ID": self._agent_id,
                "Authorization": f"Bearer {self._agent_token}",
                "X-Client-Type": "agent"
            }
            auth_payload = {"token": self._agent_token, "agentId": self._agent_id}

            logger.info(f"Attempting to connect WebSocket and authenticate agent {self._agent_id}")
            try:
                self.sio.connect(
                    url=self.server_url,
                    headers=headers,
                    auth=auth_payload,
                    transports=["websocket"],
                    wait=False, # Connect asynchronously
                    wait_timeout=10,
                    namespaces=["/"]
                )
                logger.debug("WebSocket connection attempt initiated asynchronously.")
                return True # Indicate attempt was started
            except socketio.exceptions.ConnectionError as e:
                logger.error(f"Failed to initiate WebSocket connection: {e}")
                self._authenticated_event.clear()
                return False
            except ValueError as e:
                logger.error(f"WebSocket connection configuration error: {e}")
                return False
            except Exception as e:
                logger.critical(f"An unexpected error occurred during WebSocket connection initiation: {e}", exc_info=True)
                self._authenticated_event.clear()
                return False

    # --- REMOVED wait_for_connection ---
    # def wait_for_connection(self, timeout: Optional[float] = 10.0) -> bool: ...

    # --- ADDED wait_for_authentication ---
    def wait_for_authentication(self, timeout: Optional[float] = 15.0) -> bool:
        """
        Blocks until the WebSocket connection is established AND authentication
        is confirmed by the server (_authenticated_event is set) or a timeout occurs.

        Args:
            timeout (Optional[float]): Maximum time to wait in seconds. None waits indefinitely.

        Returns:
            bool: True if connected and authenticated within the timeout, False otherwise.
        """
        if self._authenticated_event.is_set():
            logger.debug("WebSocket already authenticated.")
            return True

        logger.debug(f"Waiting for WebSocket authentication confirmation (timeout: {timeout}s)...")
        # Wait for the authentication event specifically
        authenticated = self._authenticated_event.wait(timeout=timeout)

        if authenticated:
            # Double check sio.connected in case disconnect happened immediately after auth
            if self.sio.connected:
                 logger.info("WebSocket connection established and authenticated.")
                 return True
            else:
                 logger.warning("WebSocket authenticated event was set, but sio is no longer connected.")
                 self._authenticated_event.clear() # Ensure event reflects reality
                 return False
        else:
            logger.error(f"WebSocket authentication confirmation timed out after {timeout}s.")
            # Check if transport connected but auth failed/didn't happen
            if self.sio.connected:
                 logger.warning("WebSocket transport is connected, but authentication was not confirmed by the server.")
            else:
                 logger.warning("WebSocket transport connection also failed or timed out.")
            return False
    # ------------------------------------

    def disconnect(self):
        """Disconnects from the WebSocket server intentionally."""
        with self._connection_lock:
            if not self.sio.connected:
                logger.info("WebSocket already disconnected.")
                self._authenticated_event.clear() # Ensure event is clear
                return

            logger.info("Disconnecting from WebSocket server intentionally...")
            self._is_intentionally_disconnected = True
            self._authenticated_event.clear() # Clear authentication status first
            try:
                self.sio.disconnect()
                time.sleep(0.5)
                logger.info("WebSocket disconnection request sent.")
            except Exception as e:
                logger.error(f"An error occurred during WebSocket disconnection: {e}", exc_info=True)
            finally:
                 # Ensure event is clear even if disconnect throws error
                 self._authenticated_event.clear()

    def _emit_message(self, event_name: str, data: Dict[str, Any]) -> bool:
        """Internal helper to emit messages, checking authentication status first."""
        # --- MODIFIED: Check _authenticated_event ---
        if not self._authenticated_event.is_set():
            logger.warning(f"Cannot emit '{event_name}': WebSocket not authenticated. Data: {json.dumps(data)}")
            return False
        # -------------------------------------------
        # Also check underlying connection just in case
        if not self.sio.connected:
            logger.warning(f"Cannot emit '{event_name}': WebSocket underlying connection is down (despite auth event?). Data: {json.dumps(data)}")
            self._authenticated_event.clear() # Correct the state
            return False

        if not isinstance(data, dict):
            logger.error(f"Cannot emit '{event_name}': Data must be a dictionary. Got: {type(data)}")
            return False

        try:
            logger.debug(f"Emitting WebSocket event '{event_name}'")
            self.sio.emit(event_name, data)
            # logger.debug(f"Successfully emitted event '{event_name}' with data: {json.dumps(data)}") # Optional: Log data
            return True
        except socketio.exceptions.BadNamespaceError as e:
             logger.error(f"Failed to emit '{event_name}': Bad namespace - {e}.")
             return False
        except Exception as e:
            logger.error(f"Error emitting event '{event_name}': {e}", exc_info=True)
            if not self.sio.connected:
                logger.warning(f"WebSocket appears disconnected after failing to emit '{event_name}'.")
                self._authenticated_event.clear() # Clear auth status on disconnect
            return False

    def send_status_update(self, status_data: Dict[str, Any]) -> bool:
        """Sends a system status update to the server."""
        if self._agent_id:
            status_data['agentId'] = self._agent_id
        else:
            logger.warning("Cannot add agentId to status update: agent_id not set.")

        # _emit_message now checks for authentication
        return self._emit_message('agent:status_update', status_data)

    def send_command_result(self, command_id: str, result: Dict[str, Any]) -> bool:
        """Sends the result of an executed command back to the server."""
        if not command_id:
            logger.error("Cannot send command result: command_id is missing.")
            return False

        if not isinstance(result, dict):
             logger.error(f"Cannot send command result for {command_id}: result is not a dictionary ({type(result)}).")
             result = {"stderr": "Agent Error: Invalid result format", "exitCode": -1}

        formatted_result = {
            'commandId': command_id,
            'stdout': result.get('stdout', ''),
            'stderr': result.get('stderr', ''),
            'exitCode': result.get('exitCode', -1)
        }
        if self._agent_id:
            formatted_result['agentId'] = self._agent_id

        # _emit_message now checks for authentication
        return self._emit_message('agent:command_result', formatted_result)

