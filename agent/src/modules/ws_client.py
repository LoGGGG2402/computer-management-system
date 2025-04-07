"""
WebSocket client module for the Computer Management System Agent.
This module provides functionality for real-time communication with the backend server.
"""
import logging
import socketio
from typing import Dict, Any, Optional, Callable

logger = logging.getLogger(__name__)

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
        
        self._setup_default_events()
        logger.debug(f"WebSocket client initialized with server URL: {server_url}")
    
    def _setup_default_events(self):
        """Set up default socket.io event handlers."""
        
        @self.sio.event
        def connect():
            self.connected = True
            logger.info("Connected to WebSocket server")
        
        @self.sio.event
        def connect_error(data):
            self.connected = False
            logger.error(f"Connection to WebSocket server failed: {data}")
        
        @self.sio.event
        def disconnect():
            self.connected = False
            logger.info("Disconnected from WebSocket server")
    
    def register_command_handler(self, callback: Callable[[Dict[str, Any]], None]):
        """
        Register a callback function to handle incoming commands.
        
        Args:
            callback: Function to call when a command is received
        """
        self.command_callback = callback
        
        @self.sio.on('command:execute')
        def on_command(data):
            logger.info(f"Received command execution request: {data}")
            if self.command_callback:
                try:
                    self.command_callback(data)
                except Exception as e:
                    logger.error(f"Error handling command: {e}")
    
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
            # If already connected, disconnect first
            if self.connected:
                self.disconnect()
            
            # Connect to the server
            logger.info(f"Connecting to WebSocket server: {self.server_url}")
            self.sio.connect(self.server_url)
            
            if not self.connected:
                logger.error("Failed to connect to WebSocket server")
                return False
            
            # Send authentication event
            logger.info("Sending WebSocket authentication")
            auth_payload = {
                "agentId": agent_id,
                "token": token
            }
            
            auth_response = {}
            auth_received = False
            
            # Setup authentication response handler
            @self.sio.on('agent:ws_auth_success')
            def on_auth_success(data):
                nonlocal auth_response, auth_received
                auth_response = data
                auth_response['success'] = True
                auth_received = True
                logger.info("WebSocket authentication successful")
            
            @self.sio.on('agent:ws_auth_failed')
            def on_auth_failed(data):
                nonlocal auth_response, auth_received
                auth_response = data
                auth_response['success'] = False
                auth_received = True
                logger.error(f"WebSocket authentication failed: {data}")
            
            # Emit authentication event
            self.sio.emit('agent:authenticate_ws', auth_payload)
            
            # Wait for authentication response with timeout
            self.sio.sleep(5)
            
            if not auth_received:
                logger.error("WebSocket authentication timed out")
                self.disconnect()
                return False
            
            if not auth_response.get('success', False):
                logger.error("WebSocket authentication rejected by server")
                self.disconnect()
                return False
            
            logger.info("WebSocket connection established and authenticated")
            return True
            
        except Exception as e:
            logger.error(f"Error connecting to WebSocket server: {e}")
            self.connected = False
            return False
    
    def disconnect(self):
        """Disconnect from the WebSocket server."""
        if self.connected:
            try:
                self.sio.disconnect()
                logger.info("Disconnected from WebSocket server")
            except Exception as e:
                logger.error(f"Error disconnecting from WebSocket server: {e}")
        
        self.connected = False
    
    def send_status_update(self, status_data: Dict[str, Any]) -> bool:
        """
        Send a status update to the server.
        
        Args:
            status_data: Status data to send
            
        Returns:
            bool: True if sent successfully
        """
        if not self.connected:
            logger.warning("Cannot send status update: Not connected to WebSocket server")
            return False
        
        try:
            self.sio.emit('agent:status_update', status_data)
            logger.debug("Status update sent via WebSocket")
            return True
        except Exception as e:
            logger.error(f"Error sending status update via WebSocket: {e}")
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
        if not self.connected:
            logger.warning("Cannot send command result: Not connected to WebSocket server")
            return False
        
        try:
            payload = {
                'commandId': command_id,
                **result
            }
            self.sio.emit('agent:command_result', payload)
            logger.info(f"Command result sent for command ID: {command_id}")
            return True
        except Exception as e:
            logger.error(f"Error sending command result via WebSocket: {e}")
            return False