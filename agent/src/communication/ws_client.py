# -*- coding: utf-8 -*-
"""
WebSocket client module for real-time communication with the backend server.
Handles connection, authentication, receiving commands, and sending updates/results.
Accepts ConfigManager instance for configuration parameters like reconnect delays.
"""
import socketio
import logging
import threading
import time
from typing import Dict, Any, Callable, Optional

# Configuration
# from src.config.config_manager import config_manager # No longer using global instance
from src.config.config_manager import ConfigManager # Import class for type hinting

logger = logging.getLogger(__name__)

class WSClient:
    """
    Manages the WebSocket connection and communication logic.
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
            # This should be caught by ConfigManager validation, but double-check
            raise ValueError("Server URL (server_url) not found in configuration.")

        self.server_url = server_url # Store for logging/debugging

        # --- Read connection parameters from config ---
        reconnect_delay_initial = self.config.get('websocket.reconnect_delay_initial_sec', 5)
        reconnect_delay_max = self.config.get('websocket.reconnect_delay_max_sec', 60)
        # Handle null/None for infinite attempts, ensure it's an int or None
        reconnect_attempts_config = self.config.get('websocket.reconnect_attempts_max', None)
        reconnect_attempts_max = None
        if reconnect_attempts_config is not None:
            try:
                # Allow float conversion first, then int, handle negative as infinite
                attempts = int(float(reconnect_attempts_config))
                reconnect_attempts_max = attempts if attempts >= 0 else None
            except (ValueError, TypeError):
                logger.warning(f"Invalid value for websocket.reconnect_attempts_max: '{reconnect_attempts_config}'. Using infinite attempts.")
                reconnect_attempts_max = None # Default to infinite if invalid

        logger.info(f"WebSocket Config: URL={self.server_url}, Initial Delay={reconnect_delay_initial}s, Max Delay={reconnect_delay_max}s, Max Attempts={reconnect_attempts_max or 'Infinite'}")
        # ---------------------------------------------

        # Configure Socket.IO client with reconnection settings from config
        self.sio = socketio.Client(
            reconnection=True,
            reconnection_attempts=reconnect_attempts_max, # Pass None for infinite
            reconnection_delay=reconnect_delay_initial,
            reconnection_delay_max=reconnect_delay_max,
            # Add randomization factor to avoid thundering herd on reconnect
            randomization_factor=0.5,
            # Set logger=True for socketio internal logs, engineio_logger for engine.io logs
            logger=False, # Set to True for detailed Socket.IO debugging
            engineio_logger=False # Set to True for detailed Engine.IO debugging
        )
        self._connected_event = threading.Event() # Use Event for thread-safe connection status check
        self._message_handler: Optional[Callable[[Dict[str, Any]], None]] = None
        self._agent_id: Optional[str] = None
        self._agent_token: Optional[str] = None
        self._connection_lock = threading.Lock() # Lock for connect/disconnect operations
        self._is_intentionally_disconnected = False # Flag to prevent auto-reconnect after explicit disconnect

        self._setup_event_handlers()
        logger.info(f"WebSocket client initialized for server: {self.server_url}")

    @property
    def connected(self) -> bool:
        """Return the current connection status based on the event."""
        # Also check sio.connected for immediate status, though event is better for waiting
        return self._connected_event.is_set() and self.sio.connected

    def _setup_event_handlers(self):
        """Register handlers for standard Socket.IO and custom agent events."""
        self.sio.on('connect', self._on_connect)
        self.sio.on('disconnect', self._on_disconnect)
        self.sio.on('connect_error', self._on_connect_error)
        self.sio.on('reconnect', self._on_reconnect) # Handle successful reconnection
        self.sio.on('reconnecting', self._on_reconnecting) # Log reconnection attempts

        # Command execution event from server
        # Listen to the specific event name used by the server
        self.sio.on('command:execute', self._on_command_message)
        # Optional: Keep legacy 'command' event if server might still use it
        # self.sio.on('command', self._on_command_message)

        # Server confirmation of WebSocket authentication (useful for debugging)
        self.sio.on('agent:ws_auth_success', self._on_auth_success)
        self.sio.on('agent:ws_auth_failed', self._on_auth_failed)

        logger.debug("Standard WebSocket event handlers registered.")

    def _on_connect(self):
        """Callback executed upon successful WebSocket connection."""
        logger.info(f"Successfully connected to WebSocket server. SID: {self.sio.sid}")
        self._is_intentionally_disconnected = False # Reset flag on successful connect
        self._connected_event.set() # Signal connection is up
        # Authentication is handled by the headers/auth dict during connect_and_authenticate
        # No need to re-emit auth here usually, unless the server requires it after connect

    def _on_disconnect(self):
        """Callback executed upon WebSocket disconnection."""
        self._connected_event.clear() # Signal connection is down
        if self._is_intentionally_disconnected:
            logger.info("Disconnected from WebSocket server (intentional).")
        else:
            logger.warning("Disconnected from WebSocket server unexpectedly. Auto-reconnect mechanism active.")
            # Reconnection attempts are handled automatically by the client library

    def _on_connect_error(self, data):
        """Callback executed when a connection attempt fails."""
        # This often includes initial connection errors or auth failures during handshake
        logger.error(f"WebSocket connection failed: {data}")
        self._connected_event.clear()
        # Automatic reconnection attempts will continue based on client config unless max attempts reached

    def _on_reconnect(self):
        """Callback executed upon successful reconnection."""
        logger.info(f"Successfully reconnected to WebSocket server. SID: {self.sio.sid}")
        self._connected_event.set() # Signal connection is back up
        # Server might require re-authentication or state sync after reconnect

    def _on_reconnecting(self, attempt_number: int):
        """Callback executed when the client is attempting to reconnect."""
        logger.warning(f"Attempting to reconnect to WebSocket server... (Attempt {attempt_number})")

    def _on_command_message(self, data: Any):
        """
        Handles incoming 'command:execute' messages from the server.
        Validates data and routes it to the registered message handler.
        """
        if not isinstance(data, dict):
            logger.warning(f"Received non-dictionary command message: {data}. Ignoring.")
            return

        # Prioritize specific 'commandId', fallback to generic 'id'
        command_id = data.get('commandId') or data.get('id')

        if not command_id:
             logger.error(f"Received command message without 'commandId' or 'id': {data}. Ignoring.")
             return

        # Prioritize specific 'command', fallback to generic 'data' or 'payload' if needed
        command_payload = data.get('command') # Adjust if server uses different key

        if command_payload is None: # Check for None explicitly
             logger.error(f"Received command message missing 'command' payload for ID {command_id}: {data}. Ignoring.")
             # Optionally send an error back to the server
             # self.send_command_result(command_id, {"stderr": "Agent Error: Missing command payload", "exitCode": -1})
             return

        if self._message_handler:
            # Determine command type (optional, depends on server)
            command_type = data.get('commandType', 'unknown') # Default if not provided
            logger.debug(f"Received command (ID: {command_id}, Type: {command_type}), routing to handler.")
            try:
                # Pass the entire data dictionary to the handler, let it extract details
                self._message_handler(data)
            except Exception as e:
                logger.error(f"Error in message handler while processing command {command_id}: {e}", exc_info=True)
                # Send an error result back to the server
                self.send_command_result(command_id, {
                    "stdout": "",
                    "stderr": f"Agent internal error processing command: {e}",
                    "exitCode": -1 # Indicate agent-side error
                })
        else:
            logger.warning(f"No message handler registered. Ignoring command: {command_id}")
            # Optionally send a result indicating no handler
            # self.send_command_result(command_id, {"stderr": "Agent Error: No handler registered for command", "exitCode": -1})

    def _on_auth_success(self, data: Any):
        """Callback for server confirming successful WebSocket authentication."""
        # This might be sent after the initial 'connect' if auth happens separately
        logger.info(f"WebSocket authentication confirmed by server. Data: {data}")

    def _on_auth_failed(self, data: Any):
        """Callback for server indicating WebSocket authentication failed."""
        error_message = data.get('message', 'No reason provided') if isinstance(data, dict) else str(data)
        logger.error(f"WebSocket authentication failed (reported by server): {error_message}")
        # This might lead to disconnection or prevent commands from being processed

    def register_message_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """
        Registers a callback function to handle incoming command messages.

        Args:
            callback: The function to call when a 'command:execute' message is received.
                      It must accept one argument: the command data dictionary.
        """
        if not callable(callback):
            raise TypeError("Handler must be a callable function.")
        self._message_handler = callback
        logger.info("Command message handler registered successfully.")

    def connect_and_authenticate(self, agent_id: str, token: str) -> bool:
        """
        Attempts to connect to the WebSocket server and authenticate using provided credentials.
        Authentication happens primarily during the connection handshake via headers/auth data.

        Args:
            agent_id (str): Unique agent ID.
            token (str): Agent authentication token.

        Returns:
            bool: True if the connection attempt is initiated successfully, False otherwise.
                  Actual connection success/failure is asynchronous. Use `wait_for_connection()`.
        """
        if not agent_id or not token:
            logger.error("Connection attempt failed: Agent ID and Token are required.")
            return False

        with self._connection_lock: # Prevent concurrent connect/disconnect attempts
            if self.connected:
                logger.warning("Connection attempt skipped: Already connected.")
                return True # Already connected, consider it a success

            self._agent_id = agent_id
            self._agent_token = token
            self._is_intentionally_disconnected = False # Allow auto-reconnect

            # Headers for the *initial* HTTP handshake part of the WebSocket connection
            headers = {
                "Agent-ID": self._agent_id,
                "Authorization": f"Bearer {self._agent_token}",
                "X-Client-Type": "agent" # Use standard X- prefix if possible
            }

            # Authentication data passed to Socket.IO for its internal mechanisms (e.g., during connect event on server)
            auth_payload = {"token": self._agent_token, "agentId": self._agent_id}

            logger.info(f"Attempting to connect to WebSocket server at {self.server_url} for agent {self._agent_id}")
            try:
                self.sio.connect(
                    url=self.server_url,
                    headers=headers,
                    auth=auth_payload,
                    transports=["websocket"], # Prefer WebSocket transport explicitly
                    wait=False, # Connect asynchronously
                    wait_timeout=10, # Timeout for the connection attempt itself
                    namespaces=["/"] # Connect to default namespace, adjust if needed
                )
                logger.debug("WebSocket connection attempt initiated asynchronously.")
                return True # Indicate attempt was started
            except socketio.exceptions.ConnectionError as e:
                logger.error(f"Failed to initiate WebSocket connection: {e}")
                self._connected_event.clear()
                return False
            except ValueError as e: # Catch potential errors from invalid URL etc.
                logger.error(f"WebSocket connection configuration error: {e}")
                return False
            except Exception as e: # Catch other unexpected errors during connect initiation
                logger.critical(f"An unexpected error occurred during WebSocket connection initiation: {e}", exc_info=True)
                self._connected_event.clear()
                return False

    def wait_for_connection(self, timeout: Optional[float] = 10.0) -> bool:
        """
        Blocks until the WebSocket connection is established (connected_event is set)
        or a timeout occurs.

        Args:
            timeout (Optional[float]): Maximum time to wait in seconds. None waits indefinitely.

        Returns:
            bool: True if connected within the timeout, False otherwise.
        """
        logger.debug(f"Waiting for WebSocket connection (timeout: {timeout}s)...")
        # Use the event for waiting
        connected = self._connected_event.wait(timeout=timeout)
        if connected:
            # Double check sio.connected in case the event was set but disconnected immediately after
            if self.sio.connected:
                 logger.debug("WebSocket connection established.")
                 return True
            else:
                 logger.warning("WebSocket connected event was set, but sio is no longer connected.")
                 self._connected_event.clear() # Ensure event reflects reality
                 return False
        else:
            logger.warning(f"WebSocket connection timed out after {timeout}s.")
            return False

    def disconnect(self):
        """Disconnects from the WebSocket server intentionally."""
        with self._connection_lock:
            if not self.sio.connected:
                logger.info("WebSocket already disconnected.")
                self._connected_event.clear() # Ensure event is clear
                return

            logger.info("Disconnecting from WebSocket server intentionally...")
            self._is_intentionally_disconnected = True # Prevent auto-reconnect attempts
            self._connected_event.clear() # Clear event before disconnecting
            try:
                self.sio.disconnect()
                # Wait a moment to allow disconnect to process
                time.sleep(0.5)
                logger.info("WebSocket disconnection request sent.")
            except Exception as e:
                logger.error(f"An error occurred during WebSocket disconnection: {e}", exc_info=True)
            finally:
                 # Ensure event is clear even if disconnect throws error
                 self._connected_event.clear()

    def _emit_message(self, event_name: str, data: Dict[str, Any]) -> bool:
        """Internal helper to emit messages, checking connection status first."""
        if not self.connected: # Check property which uses the event
            logger.warning(f"Cannot emit '{event_name}': WebSocket not connected.")
            return False
        if not isinstance(data, dict):
            logger.error(f"Cannot emit '{event_name}': Data must be a dictionary. Got: {type(data)}")
            return False

        try:
            logger.debug(f"Emitting WebSocket event '{event_name}'") # Log before emit
            self.sio.emit(event_name, data)
            # Avoid logging potentially large data frequently unless debugging level is high
            # logger.debug(f"Successfully emitted event '{event_name}' with data: {json.dumps(data)}")
            return True
        except socketio.exceptions.BadNamespaceError as e:
             logger.error(f"Failed to emit '{event_name}': Bad namespace - {e}.")
             return False
        # socketio client might raise other exceptions if disconnected during emit
        except Exception as e:
            logger.error(f"Error emitting event '{event_name}': {e}", exc_info=True)
            # Check connection status again after error
            if not self.sio.connected:
                logger.warning(f"WebSocket appears disconnected after failing to emit '{event_name}'.")
                self._connected_event.clear()
            return False

    def send_status_update(self, status_data: Dict[str, Any]) -> bool:
        """Sends a system status update to the server."""
        # Add agent ID to the status update for server-side identification
        if self._agent_id:
            status_data['agentId'] = self._agent_id
        else:
            logger.warning("Cannot add agentId to status update: agent_id not set.")

        return self._emit_message('agent:status_update', status_data)

    def send_command_result(self, command_id: str, result: Dict[str, Any]) -> bool:
        """Sends the result of an executed command back to the server."""
        if not command_id:
            logger.error("Cannot send command result: command_id is missing.")
            return False

        # Ensure result is a dictionary
        if not isinstance(result, dict):
             logger.error(f"Cannot send command result for {command_id}: result is not a dictionary ({type(result)}).")
             result = {"stderr": "Agent Error: Invalid result format", "exitCode": -1}


        # Standardize result format before sending
        formatted_result = {
            'commandId': command_id,
            # Ensure keys exist and provide defaults
            'stdout': result.get('stdout', ''),
            'stderr': result.get('stderr', ''),
            'exitCode': result.get('exitCode', -1) # Use -1 for unknown/error exit code
        }
        # Add agent ID for clarity on server side
        if self._agent_id:
            formatted_result['agentId'] = self._agent_id

        return self._emit_message('agent:command_result', formatted_result)
