# -*- coding: utf-8 -*-
"""
WebSocket client module for real-time communication with the backend server.
"""
import socketio
import threading
import json
from typing import Dict, Any, Callable, Optional, TYPE_CHECKING

if TYPE_CHECKING:
    from ..config import ConfigManager
from ..utils import get_logger

logger = get_logger(__name__)

class WSClient:
    """
    Manages the WebSocket connection and communication logic.
    """

    def __init__(self, config: 'ConfigManager'):
        """
        Initialize the WebSocket client.

        :param config: The configuration manager instance.
        :type config: ConfigManager
        :raises ValueError: If server_url is not configured.
        """
        self.config = config
        server_url = self.config.get('server_url')
        if not server_url:
            raise ValueError("Server URL (server_url) not found in configuration.")

        self.server_url = server_url

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

        self.sio = socketio.Client(
            reconnection=True,
            reconnection_attempts=reconnect_attempts_max,
            reconnection_delay=reconnect_delay_initial,
            reconnection_delay_max=reconnect_delay_max,
            randomization_factor=0.5,
            logger=False,
            engineio_logger=False
        )
        self._authenticated_event = threading.Event()
        self._message_handler: Optional[Callable[[Dict[str, Any]], None]] = None
        self._update_handler: Optional[Callable[[Dict[str, Any]], None]] = None
        self._agent_id: Optional[str] = None
        self._agent_token: Optional[str] = None
        self._connection_lock = threading.Lock()
        self._is_intentionally_disconnected = False

        self._setup_event_handlers()
        logger.info(f"WebSocket client initialized for server: {self.server_url}")

    @property
    def connected(self) -> bool:
        """
        Return the current connection and authentication status.

        :return: True if connected and authenticated, False otherwise.
        :rtype: bool
        """
        return self.sio.connected and self._authenticated_event.is_set()

    def _setup_event_handlers(self):
        """
        Register handlers for standard Socket.IO and custom agent events.
        Uses the `sio.on` decorator mechanism for clear event handling.
        """
        self.sio.on('connect', self._on_connect)
        self.sio.on('disconnect', self._on_disconnect)
        self.sio.on('connect_error', self._on_connect_error)
        self.sio.on('reconnect', self._on_reconnect)

        self.sio.on('command:execute', self._on_command_message)
        self.sio.on('agent:new_version_available', self._on_new_version_available)
        self.sio.on('agent:ws_auth_success', self._on_auth_success)
        self.sio.on('agent:ws_auth_failed', self._on_auth_failed)

        logger.debug("Standard and custom WebSocket event handlers registered.")

    def _on_connect(self):
        """
        Callback executed upon successful WebSocket transport connection.
        Authentication is not yet complete at this stage.
        """
        logger.info(f"WebSocket transport connected. SID: {self.sio.sid}. Waiting for authentication confirmation...")
        self._is_intentionally_disconnected = False

    def _on_disconnect(self):
        """
        Callback executed upon WebSocket disconnection.
        """
        was_authenticated = self._authenticated_event.is_set()
        self._authenticated_event.clear()

        if self._is_intentionally_disconnected:
            logger.info("Disconnected from WebSocket server (intentional).")
        else:
            if was_authenticated:
                 logger.warning("Authenticated WebSocket connection lost unexpectedly. Auto-reconnect mechanism active.")
            else:
                 logger.warning("WebSocket connection lost before authentication completed. Auto-reconnect mechanism active.")

    def _on_connect_error(self, data):
        """
        Callback executed when a connection attempt fails.

        :param data: Error data from the connection attempt.
        :type data: Any
        """
        logger.error(f"WebSocket connection failed: {data}")
        self._authenticated_event.clear()

    def _on_reconnect(self):
        """
        Callback executed upon successful reconnection.
        Agent authentication state needs to be re-confirmed by the server.
        """
        logger.info(f"WebSocket transport reconnected. SID: {self.sio.sid}. Waiting for authentication confirmation...")

    def _on_command_message(self, data: Any):
        """
        Handles incoming 'command:execute' messages from the server.

        :param data: Command data from server.
        :type data: Any
        """
        if not self._authenticated_event.is_set():
             logger.warning(f"Ignoring command message: WebSocket is connected but not authenticated. Data: {data}")
             return

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
             self.send_command_result(command_id, {
                 "type": "console",
                 "success": False,
                 "result": {"stderr": "Agent Error: Missing command payload", "exitCode": -1}
             })
             return

        command_type = data.get('commandType', 'console')

        if self._message_handler:
            logger.debug(f"Received command (ID: {command_id}, Type: {command_type}), routing to handler.")
            try:
                self._message_handler(data)
            except Exception as e:
                logger.error(f"Error in message handler while processing command {command_id}: {e}", exc_info=True)
                self.send_command_result(command_id, {
                    "type": command_type,
                    "success": False,
                    "result": {
                        "stdout": "",
                        "stderr": f"Agent internal error processing command: {e}",
                        "exitCode": -1
                    }
                })
        else:
            logger.warning(f"No message handler registered. Ignoring command: {command_id}")
            self.send_command_result(command_id, {
                "type": command_type,
                "success": False,
                "result": {"stderr": "Agent Error: No handler registered for command", "exitCode": -1}
            })

    def _on_new_version_available(self, data: Any):
        """
        Handles 'agent:new_version_available' event from server.

        :param data: Event data containing information about new version.
        :type data: Any
        """
        if not self._authenticated_event.is_set():
            logger.warning(f"Ignoring new version notification: WebSocket is connected but not authenticated. Data: {data}")
            return

        if not isinstance(data, dict):
            logger.warning(f"Received invalid new_version_available data: {data}. Ignoring.")
            return

        new_version = data.get('new_stable_version')
        if not new_version:
            logger.warning(f"Received new_version_available without version information: {data}. Ignoring.")
            return

        logger.info(f"New agent version available: {new_version}")

        if self._update_handler:
            try:
                logger.debug("Forwarding new version notification to update handler")
                self._update_handler(data)
            except Exception as e:
                logger.error(f"Error in update handler processing new version notification: {e}", exc_info=True)
        else:
            logger.info("No update handler registered. Update notification will be ignored.")

    def _on_auth_success(self, data: Any):
        """
        Callback for server confirming successful WebSocket authentication.

        :param data: Authentication success data from server.
        :type data: Any
        """
        logger.info(f"WebSocket authentication confirmed by server. Agent is ready. Data: {data}")
        self._authenticated_event.set()

    def _on_auth_failed(self, data: Any):
        """
        Callback for server indicating WebSocket authentication failed.

        :param data: Authentication failure data from server.
        :type data: Any
        """
        error_message = data.get('message', 'No reason provided') if isinstance(data, dict) else str(data)
        logger.error(f"WebSocket authentication failed (reported by server): {error_message}")
        self._authenticated_event.clear()

    def register_message_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """
        Registers a callback function to handle incoming command messages.

        :param callback: Function to call for command messages.
        :type callback: Callable[[Dict[str, Any]], None]
        :raises TypeError: If callback is not callable.
        """
        if not callable(callback):
            raise TypeError("Handler must be a callable function.")
        self._message_handler = callback
        logger.info("Command message handler registered successfully.")

    def register_update_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """
        Registers a callback function to handle incoming update notifications.

        :param callback: Function to call for update notifications.
        :type callback: Callable[[Dict[str, Any]], None]
        :raises TypeError: If callback is not callable.
        """
        if not callable(callback):
            raise TypeError("Update handler must be a callable function.")
        self._update_handler = callback
        logger.info("Update notification handler registered successfully.")

    def connect_and_authenticate(self, agent_id: str, token: str) -> bool:
        """
        Initiates connection to the WebSocket server with authentication credentials.

        :param agent_id: Agent ID for authentication.
        :type agent_id: str
        :param token: Authentication token.
        :type token: str
        :return: True if connection attempt started, False otherwise.
        :rtype: bool
        """
        if not agent_id or not token:
            logger.error("Connection attempt failed: Agent ID and Token are required.")
            return False

        with self._connection_lock:
            if self.sio.connected:
                logger.warning("Connection attempt skipped: Already connected or connecting.")
                return self._authenticated_event.is_set()

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
                    wait=False,
                    wait_timeout=10,
                    namespaces=["/"]
                )
                logger.debug("WebSocket connection attempt initiated asynchronously.")
                return True
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

    def wait_for_authentication(self, timeout: Optional[float] = 15.0) -> bool:
        """
        Blocks until WebSocket connection is established and authentication is confirmed.

        :param timeout: Maximum time to wait in seconds (default 15.0).
        :type timeout: Optional[float]
        :return: True if connected and authenticated within timeout, False otherwise.
        :rtype: bool
        """
        if self._authenticated_event.is_set():
            logger.debug("WebSocket already authenticated.")
            return True

        logger.debug(f"Waiting for WebSocket authentication confirmation (timeout: {timeout}s)...")
        authenticated = self._authenticated_event.wait(timeout=timeout)

        if authenticated:
            if self.sio.connected:
                 logger.info("WebSocket connection established and authenticated.")
                 return True
            else:
                 logger.warning("WebSocket authenticated event was set, but sio is no longer connected.")
                 self._authenticated_event.clear()
                 return False
        else:
            logger.error(f"WebSocket authentication confirmation timed out after {timeout}s.")
            if self.sio.connected:
                 logger.warning("WebSocket transport is connected, but authentication was not confirmed by the server.")
            else:
                 logger.warning("WebSocket transport connection also failed or timed out.")
            return False

    def disconnect(self):
        """
        Disconnects from the WebSocket server intentionally.
        """
        with self._connection_lock:
            if not self.sio.connected:
                logger.info("WebSocket already disconnected.")
                self._authenticated_event.clear()
                return

            logger.info("Disconnecting from WebSocket server intentionally...")
            self._is_intentionally_disconnected = True
            self._authenticated_event.clear()
            try:
                self.sio.disconnect()
                logger.info("WebSocket disconnection request sent.")
            except Exception as e:
                logger.error(f"An error occurred during WebSocket disconnection: {e}", exc_info=True)
            finally:
                 self._authenticated_event.clear()

    def _emit_message(self, event_name: str, data: Dict[str, Any]) -> bool:
        """
        Internal helper to emit messages, checking authentication status first.

        :param event_name: The name of the event to emit.
        :type event_name: str
        :param data: Data to send with the event (must be a dict).
        :type data: Dict[str, Any]
        :return: True if emit succeeded, False otherwise.
        :rtype: bool
        """
        if not self._authenticated_event.is_set():
            logger.warning(f"Cannot emit '{event_name}': WebSocket not authenticated. Data: {json.dumps(data)}")
            return False

        if not self.sio.connected:
            logger.warning(f"Cannot emit '{event_name}': WebSocket underlying connection is down (despite auth event?). Data: {json.dumps(data)}")
            self._authenticated_event.clear()
            return False

        if not isinstance(data, dict):
            logger.error(f"Cannot emit '{event_name}': Data must be a dictionary. Got: {type(data)}")
            return False

        try:
            logger.debug(f"Emitting WebSocket event '{event_name}'")
            self.sio.emit(event_name, data)
            return True
        except socketio.exceptions.BadNamespaceError as e:
             logger.error(f"Failed to emit '{event_name}': Bad namespace - {e}.")
             return False
        except Exception as e:
            logger.error(f"Error emitting event '{event_name}': {e}", exc_info=True)
            if not self.sio.connected:
                logger.warning(f"WebSocket appears disconnected after failing to emit '{event_name}'.")
                self._authenticated_event.clear()
            return False

    def send_status_update(self, status_data: Dict[str, Any]) -> bool:
        """
        Sends a system status update to the server.

        :param status_data: Status data to send.
        :type status_data: Dict[str, Any]
        :return: True if status update was sent successfully, False otherwise.
        :rtype: bool
        """
        if self._agent_id:
            status_data['agentId'] = self._agent_id
        else:
            logger.warning("Cannot add agentId to status update: agent_id not set.")

        return self._emit_message('agent:status_update', status_data)

    def send_command_result(self, command_id: str, result: Dict[str, Any]) -> bool:
        """
        Sends the result of an executed command back to the server.
        Assumes `result` adheres to {type: str, success: bool, result: {stdout: str, stderr: str, exitCode: int}}.

        :param command_id: ID of the command.
        :type command_id: str
        :param result: Result data to send (must be a dict).
        :type result: Dict[str, Any]
        :return: True if command result was sent successfully, False otherwise.
        :rtype: bool
        """
        if not command_id:
            logger.error("Cannot send command result: command_id is missing.")
            return False

        if not isinstance(result, dict):
             logger.error(f"Cannot send command result for {command_id}: result is not a dictionary ({type(result)}).")
             result = {
                 "type": "unknown",
                 "success": False,
                 "result": {
                     "stderr": "Agent Error: Invalid result format",
                     "exitCode": -1
                 }
             }

        if not all(k in result for k in ["type", "success", "result"]) or not isinstance(result.get("result"), dict):
             logger.warning(f"Command result format for {command_id} is potentially unexpected: {result}. Will attempt to send.")

        final_payload = result.copy()

        if self._agent_id:
            final_payload['agentId'] = self._agent_id
        final_payload['commandId'] = command_id

        return self._emit_message('agent:command_result', final_payload)
