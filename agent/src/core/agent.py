"""
Core Agent module for the Computer Management System.
This module contains the main Agent class that manages the agent functionality.
"""
import os
import time
import uuid
import socket
import platform
import threading
from typing import Dict, Any

# Import modules from the new directory structure
from src.config.config_manager import ConfigManager
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient
from src.monitoring.system_monitor import SystemMonitor
from src.core.command_executor import CommandExecutor
from src.auth.token_manager import load_token, save_token
from src.auth.mfa_handler import prompt_for_mfa, display_registration_success
from src.utils.utils import get_room_config, prompt_room_config, save_room_config
from src.utils.logger import get_logger

# Get logger for this module
logger = get_logger(__name__)

class Agent:
    """
    Agent class responsible for monitoring system resources
    and communicating with the backend server.
    """
    
    def __init__(self, config_manager: ConfigManager):
        """
        Initialize the agent with configuration.
        
        Args:
            config_manager (ConfigManager): The configuration manager instance
        """
        self.config_manager = config_manager
        self.running = False
        
        # Device identification
        self.device_id = self._get_device_id()
        
        # Room configuration
        self.room_config = self._get_or_create_room_config()
        
        # Initialize required services
        self.http_client = HttpClient(self.config_manager.get('server_url'))
        self.ws_client = WSClient(self.config_manager.get('server_url'))
        self.system_monitor = SystemMonitor()
        self.agent_token = None
        self.command_executor = None
        
        # Set up WebSocket handlers
        self.ws_client.register_command_handler(self.handle_command)
        self.ws_client.register_command_result_handler(self.handle_command_result)
        
        logger.info(f"Agent initialized for device: {self.device_id} in room: {self.room_config['room']}")
    
    def _get_device_id(self) -> str:
        """
        Generate or retrieve a unique device ID.
        
        Returns:
            str: Unique device identifier
        """
        storage_path = self.config_manager.get('storage_path')
        device_id_file = os.path.join(storage_path, 'device_id')
        
        try:
            # Try to get device ID from storage
            if os.path.exists(device_id_file):
                with open(device_id_file, 'r') as f:
                    return f.read().strip()
            
            # Generate new device ID if not found
            hostname = socket.gethostname()
            mac = uuid.getnode()
            device_id = f"ANM-{hostname}-{mac}"
            
            # Save device ID
            with open(device_id_file, 'w') as f:
                f.write(device_id)
            
            return device_id
            
        except Exception as e:
            logger.error(f"Error getting device ID: {e}")
            # Fallback to a random UUID if everything fails
            return str(uuid.uuid4())
    
    def _get_or_create_room_config(self) -> Dict[str, Any]:
        """
        Get existing room configuration or create new one.
        
        Returns:
            Dict[str, Any]: Room configuration
        """
        storage_path = self.config_manager.get('storage_path')
        room_config = get_room_config(storage_path)
        
        if room_config:
            logger.info(f"Loaded existing room configuration: {room_config}")
            return room_config
            
        # Prompt for room configuration on first run
        logger.info("No room configuration found. Prompting user for input.")
        room, pos_x, pos_y = prompt_room_config()
        
        # Save the configuration
        if not save_room_config(storage_path, room, pos_x, pos_y):
            raise RuntimeError("Failed to save room configuration")
            
        return {
            'room': room,
            'position': {'x': pos_x, 'y': pos_y}
        }
    
    def authenticate(self) -> bool:
        """
        Authenticate the agent with the backend server.
        
        Returns:
            bool: True if authenticated successfully
        """
        # Check for existing token
        storage_path = self.config_manager.get('storage_path')
        self.agent_token = load_token(self.device_id, storage_path)
        if self.agent_token:
            logger.info("Using existing agent token")
            return True
            
        # Start authentication process
        success, response = self.http_client.identify_agent(self.device_id, self.room_config)
        if not success:
            logger.error("Failed to identify agent with server")
            logger.error(f"Server response: {response}")
            return False
        
        if response.get('status') == 'mfa_required':
            # MFA verification
            mfa_code = prompt_for_mfa()
            success, response = self.http_client.verify_mfa(self.device_id, mfa_code, self.room_config)
            if not success:
                logger.error(f"MFA verification failed {response}")
                return False
                
            self.agent_token = response.get('agentToken')
            if not self.agent_token:
                logger.error("No token received from server")
                return False
                
            save_token(self.device_id, self.agent_token, storage_path)
            display_registration_success()
            return True
        
        if 'agentToken' in response:
            self.agent_token = response['agentToken']
            save_token(self.device_id, self.agent_token, storage_path)
            logger.info("Agent registered successfully")
            return True
        
        logger.error("Failed to register agent with server")
        logger.error(f"Server response: {response}")
        return False
    
    def handle_command(self, command_data: Dict[str, Any]):
        """
        Handle a command received from the server.
        
        Args:
            command_data: Command data from server
        """
        try:
            logger.info(f"Received command: {command_data}")
            
            # Initialize command executor if needed
            if not self.command_executor:
                self.command_executor = CommandExecutor(
                    self.device_id,
                    self.agent_token,
                    self.ws_client
                )
            
            # Modern command format
            if 'commandId' in command_data and 'command' in command_data:
                command_id = command_data['commandId']
                command = command_data['command']
                logger.info(f"Executing command: {command} (ID: {command_id})")
                self.command_executor.run_command(command, command_id)
                return
                
            # Legacy command handling
            command_id = command_data.get('id')
            command_type = command_data.get('type')
            if not command_id or not command_type:
                logger.error("Invalid command format")
                return
                
            self.command_executor.handle_legacy_command(command_data)
            
        except Exception as e:
            logger.error(f"Error handling command: {e}", exc_info=True)
    
    def handle_command_result(self, result_data: Dict[str, Any]):
        """
        Handle a command result acknowledgment from the server.
        
        Args:
            result_data: Command result data from server
        """
        try:
            command_id = result_data.get('commandId')
            status = result_data.get('status')
            logger.info(f"Received command result acknowledgment: {command_id} ({status})")
            
            if status == 'error':
                error_message = result_data.get('message', 'Unknown error')
                logger.warning(f"Server reported error for command {command_id}: {error_message}")
                
        except Exception as e:
            logger.error(f"Error handling command result: {e}", exc_info=True)
    
    def collect_system_stats(self) -> Dict[str, Any]:
        """
        Collect current system statistics.
        
        Returns:
            Dict[str, Any]: Current system statistics
        """
        try:
            stats = self.system_monitor.get_all_stats()
            # Add room configuration to stats
            stats.update({
                'room': self.room_config['room'],
                'position': self.room_config['position']
            })
            return stats
        except Exception as e:
            logger.error(f"Error collecting system stats: {e}")
            return {"error": str(e)}
    
    def connect_to_server(self) -> bool:
        """
        Connect to the backend server via WebSocket.
        
        Returns:
            bool: True if connected successfully
        """
        try:
            return self.ws_client.connect_and_authenticate(self.device_id, self.agent_token)
        except Exception as e:
            logger.error(f"Error connecting to server: {e}")
            return False
    
    def _start_status_reporting_thread(self):
        """Start a thread to periodically report system status to the server."""
        if not self.running:
            return
            
        # Send initial status update
        self._send_status_update()
        
        # Schedule next update
        interval = 30  # Fixed interval as per requirement
        self.status_timer = threading.Timer(interval, self._start_status_reporting_thread)
        self.status_timer.daemon = True
        self.status_timer.start()
        
    def _send_status_update(self):
        """Send system status update to the server using WebSocket."""
        try:
            # Get system statistics
            stats = self.system_monitor.get_stats()
            logger.debug(f"Sending status update via WebSocket: {stats}")
            
            # Format data for WebSocket
            status_data = {
                "cpuUsage": stats.get("cpu", 0),
                "ramUsage": stats.get("ram", 0),
                "diskUsage": stats.get("disk", 0),
            }
            
            # Send via WebSocket
            if self.ws_client.connected:
                success = self.ws_client.send_status_update(status_data)
                if not success:
                    logger.warning("Failed to send status update via WebSocket")
            else:
                logger.warning("Cannot send status update: WebSocket not connected")
                
                # Try to reconnect if WebSocket is not connected
                reconnected = self.connect_to_server()
                if reconnected:
                    logger.info("Reconnected to WebSocket server")
                    # Try sending again after reconnecting
                    self.ws_client.send_status_update(status_data)
                else:
                    logger.error("Failed to reconnect to WebSocket server")
            
        except Exception as e:
            logger.error(f"Error sending status update: {e}", exc_info=True)
    
    def start(self):
        """Start the agent."""
        logger.info("Starting agent...")
        
        # Authenticate with the server
        if not self.authenticate():
            logger.error("Authentication failed")
            return
            
        # Connect to WebSocket server
        if not self.connect_to_server():
            logger.error("Failed to connect to server")
            return
            
        # Start status reporting
        self.running = True
        self._start_status_reporting_thread()
        
        # Main agent loop
        try:
            while self.running:
                time.sleep(1)
        except KeyboardInterrupt:
            logger.info("Agent interrupted by user")
            self.stop()
        except Exception as e:
            logger.error(f"Error in agent main loop: {e}", exc_info=True)
            self.stop()
    
    def stop(self):
        """Stop the agent."""
        logger.info("Stopping agent...")
        self.running = False
        
        # Cancel the status reporting timer
        if hasattr(self, 'status_timer') and self.status_timer:
            self.status_timer.cancel()
            
        # Disconnect from WebSocket
        self.ws_client.disconnect()