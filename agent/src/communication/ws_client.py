"""
WebSocket client module for the Computer Management System Agent.
This module provides functionality for real-time communication with the backend server.
"""
import socketio
from typing import Dict, Any, Callable

from src.utils.logger import get_logger

# Get logger for this module
logger = get_logger(__name__)

class WSClient:
    """WebSocket client for real-time communication with the backend server."""
    
    def __init__(self, server_url: str):
        """
        Initialize the WebSocket client.
        
        Args:
            server_url (str): WebSocket server URL
        """
        self.server_url = server_url
        self.sio = socketio.Client()
        self.connected = False
        self.command_callback = None
        self.command_result_callback = None
        self.agent_id = None
        self.agent_token = None
        
        self._setup_default_events()
        logger.debug(f"WebSocket client initialized with server URL: {server_url}")
    
    def _setup_default_events(self):
        """Set up default socket.io event handlers."""
        
        self.sio.on('connect', self._on_connect)
        self.sio.on('disconnect', self._on_disconnect)
        self.sio.on('command', self._on_command)
        self.sio.on('command:execute', self._on_command_execute)
        self.sio.on('agent:ws_auth_success', self._on_auth_success)
        self.sio.on('agent:ws_auth_failed', self._on_auth_failed)
        self.sio.on('command:result_received', self._on_command_result_received)
        logger.debug("Default WebSocket event handlers set up.")
    
    def _on_connect(self):
        """Handle WebSocket connection event."""
        self.connected = True
        logger.info("Connected to WebSocket server.")
        
        # Authenticate with server if we have credentials
        if self.agent_id and self.agent_token:
            self._authenticate_ws()
    
    def _on_disconnect(self):
        """Handle WebSocket disconnection event."""
        self.connected = False
        logger.info("Disconnected from WebSocket server.")
    
    def _on_command(self, data: Dict[str, Any]):
        """Handle incoming command event."""
        if self.command_callback:
            self.command_callback(data)
        else:
            logger.warning("No command handler registered.")
    
    def _on_command_execute(self, data: Dict[str, Any]):
        """Handle incoming command execute event."""
        logger.info(f"Received command:execute event: {data}")
        if self.command_callback:
            self.command_callback(data)
        else:
            logger.warning("No command handler registered.")
    
    def _on_auth_success(self, data: Dict[str, Any]):
        """Handle successful WebSocket authentication."""
        logger.info(f"WebSocket authentication successful: {data}")
    
    def _on_auth_failed(self, data: Dict[str, Any]):
        """Handle failed WebSocket authentication."""
        logger.error(f"WebSocket authentication failed: {data}")
        # Try to reconnect after a delay if needed
        # self.sio.sleep(5)
        # self._authenticate_ws()
    
    def _on_command_result_received(self, data: Dict[str, Any]):
        """Handle command result acknowledgement event."""
        logger.info(f"Command result received: {data}")
        if self.command_result_callback:
            self.command_result_callback(data)
        else:
            logger.warning("No command result handler registered.")
    
    def _authenticate_ws(self):
        """Send authentication data to server via WebSocket."""
        if not self.agent_id or not self.agent_token:
            logger.warning("Cannot authenticate WebSocket: Missing agent ID or token")
            return
            
        logger.info(f"Sending WebSocket authentication for agent: {self.agent_id}")
        self.sio.emit('agent:authenticate_ws', {
            'agentId': self.agent_id,
            'token': self.agent_token
        })
    
    def register_command_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """
        Register a callback function to handle incoming commands.
        
        Args:
            callback: Function to call when a command is received
        """
        self.command_callback = callback
        logger.debug("Command handler registered.")
    
    def register_command_result_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """
        Register a callback function to handle command result acknowledgements.
        
        Args:
            callback: Function to call when a command result is received
        """
        self.command_result_callback = callback
        logger.debug("Command result handler registered.")
    
    def connect_and_authenticate(self, agent_id: str, token: str) -> bool:
        """
        Connect to the WebSocket server and authenticate.
        
        Args:
            agent_id (str): Unique agent ID
            token (str): Agent authentication token
            
        Returns:
            bool: True if connected and authenticated successfully
        """
        try:
            # Store credentials for reconnection
            self.agent_id = agent_id
            self.agent_token = token
            
            # Connect to WebSocket server
            self.sio.connect(self.server_url, headers={"Agent-ID": agent_id, "Authorization": f"Bearer {token}"})
            
            # Authentication will happen in _on_connect handler
            logger.info("WebSocket client connected and authenticated.")
            return True
        except Exception as e:
            logger.error(f"Failed to connect and authenticate: {e}")
            return False
    
    def disconnect(self):
        """Disconnect from the WebSocket server."""
        try:
            self.sio.disconnect()
            logger.info("WebSocket client disconnected.")
        except Exception as e:
            logger.error(f"Failed to disconnect: {e}")
    
    def send_status_update(self, status_data: Dict[str, Any]) -> bool:
        """
        Send a status update to the server.
        
        Args:
            status_data: Status data to send
            
        Returns:
            bool: True if sent successfully
        """
        try:
            self.sio.emit('agent:status_update', status_data)
            logger.info("Status update sent.")
            return True
        except Exception as e:
            logger.error(f"Failed to send status update: {e}")
            return False
    
    def send_command_result(self, command_id: str, result: Dict[str, Any]) -> bool:
        """
        Send a command execution result to the server.
        
        Args:
            command_id: ID of the command that was executed
            result: Result data to send
            
        Returns:
            bool: True if sent successfully
        """
        try:
            self.sio.emit('agent:command_result', {
                "commandId": command_id, 
                "stdout": result.get("stdout", ""),
                "stderr": result.get("stderr", ""),
                "exitCode": result.get("exitCode", 1)
            })
            logger.info(f"Command result for {command_id} sent.")
            return True
        except Exception as e:
            logger.error(f"Failed to send command result: {e}")
            return False