"""
Core Agent module for the Computer Management System.
This module contains the main Agent class that manages the agent functionality.
"""
import os
import json
import time
import uuid
import socket
import platform
import threading
from typing import Dict, Any, Optional

# Import modules from the new directory structure
from src.config.config_manager import ConfigManager
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient
from src.monitoring.system_monitor import SystemMonitor
from src.core.command_executor import CommandExecutor
from src.auth.token_manager import load_token, save_token
from src.auth.mfa_handler import prompt_for_mfa, display_registration_success
from src.utils.utils import run_command, get_room_config, prompt_room_config, save_room_config
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
        
        # Command executor will be initialized after authentication
        self.command_executor = None
        
        # Set up command handler
        self.ws_client.register_command_handler(self.handle_command)
        # Set up command result handler for acknowledgements
        self.ws_client.register_command_result_handler(self.handle_command_result)
        
        logger.info(f"Agent initialized for device: {self.device_id} in room: {self.room_config['room']}")
    
    def _get_device_id(self) -> str:
        """
        Generate or retrieve a unique device ID.
        
        Returns:
            str: Unique device identifier
        """
        try:
            # Try to get device ID from storage
            storage_path = self.config_manager.get('storage_path')
            device_id_file = os.path.join(storage_path, 'device_id')
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
        if save_room_config(storage_path, room, pos_x, pos_y):
            room_config = {
                'room': room,
                'position': {'x': pos_x, 'y': pos_y}
            }
            logger.info(f"Saved new room configuration: {room_config}")
            return room_config
        else:
            raise RuntimeError("Failed to save room configuration")
    
    def _collect_system_info(self) -> Dict[str, Any]:
        """
        Collect basic system information.
        
        Returns:
            Dict[str, Any]: System information
        """
        return {
            "hostname": socket.gethostname(),
            "platform": platform.system(),
            "platform_release": platform.release(),
            "platform_version": platform.version(),
            "architecture": platform.machine(),
            "processor": platform.processor(),
            "device_id": self.device_id
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
            return False
            
        # Kiểm tra nếu server trả về token trực tiếp (bỏ qua MFA)
        if response.get('status') == 'success' and response.get('agentToken'):
            logger.info("Received token directly from server, skipping MFA")
            self.agent_token = response.get('agentToken')
            save_token(self.device_id, self.agent_token, storage_path)
            display_registration_success()
            return True
            
        # Kiểm tra nếu server yêu cầu authentication
        if response.get('status') == 'authentication_required':
            logger.info("Server requires authentication but no token found, requesting new token")
            # Yêu cầu server cấp mới token bằng cách thêm forceRenewToken=True
            success, renew_response = self.http_client.identify_agent(self.device_id, self.room_config, force_renew=True)
            
            if success and renew_response.get('status') == 'success' and renew_response.get('agentToken'):
                logger.info("Successfully renewed token")
                self.agent_token = renew_response.get('agentToken')
                save_token(self.device_id, self.agent_token, storage_path)
                display_registration_success()
                return True
            else:
                logger.error("Failed to renew token")
                return False
            
        # Kiểm tra nếu phản hồi có chứa thông báo lỗi về vị trí
        if response.get('status') == 'position_error':
            error_message = response.get('message', 'Vị trí không hợp lệ')
            logger.error(f"Position error: {error_message}")
            
            # Yêu cầu người dùng nhập lại thông tin phòng
            logger.info("Requesting new room configuration from user")
            room, pos_x, pos_y = prompt_room_config()
            
            # Cập nhật cấu hình phòng
            self.room_config = {
                'room': room,
                'position': {'x': pos_x, 'y': pos_y}
            }
            
            # Lưu cấu hình mới
            if save_room_config(storage_path, room, pos_x, pos_y):
                logger.info(f"Saved new room configuration: {self.room_config}")
            else:
                logger.error("Failed to save new room configuration")
                return False
                
            # Thử lại xác thực với thông tin phòng mới
            success, response = self.http_client.identify_agent(self.device_id, self.room_config)
            if not success:
                logger.error("Failed to identify agent with server after room configuration update")
                return False
                
            # Kiểm tra lại nếu server trả về token trực tiếp
            if response.get('status') == 'success' and response.get('agentToken'):
                logger.info("Received token directly from server after position update, skipping MFA")
                self.agent_token = response.get('agentToken')
                save_token(self.device_id, self.agent_token, storage_path)
                display_registration_success()
                return True
        
        # Handle MFA if required
        mfa_code = prompt_for_mfa()
        success, response = self.http_client.verify_mfa(self.device_id, mfa_code, self.room_config)
        if not success:
            logger.error("MFA verification failed")
            return False
            
        # Save the token
        self.agent_token = response.get('agentToken')
        if not self.agent_token:
            logger.error("No token received from server")
            return False
            
        save_token(self.device_id, self.agent_token, storage_path)
        display_registration_success()
        return True
    
    def handle_command(self, command_data: Dict[str, Any]):
        """
        Handle a command received from the server.
        
        Args:
            command_data: Command data from server
        """
        try:
            logger.info(f"Received command: {command_data}")
            
            # For command:execute event
            if 'commandId' in command_data and 'command' in command_data:
                command_id = command_data['commandId']
                command = command_data['command']
                
                # Initialize command executor if not already done
                if not self.command_executor:
                    self.command_executor = CommandExecutor(
                        self.http_client,
                        self.device_id,
                        self.agent_token,
                        self.ws_client  # Truyền WebSocket client vào đây
                    )
                
                # Execute the command
                logger.info(f"Executing command: {command} (ID: {command_id})")
                self.command_executor.run_command(command, command_id)
                return
                
            # Legacy command handling (if any)
            command_id = command_data.get('id')
            command_type = command_data.get('type')
            command_params = command_data.get('params', {})
            
            if not command_id or not command_type:
                logger.error("Invalid command format")
                return
                
            logger.info(f"Handling legacy command: {command_type} (ID: {command_id})")
            
            result = None
            if command_type == 'shell':
                command = command_params.get('command')
                if command:
                    result = run_command(command)
            elif command_type == 'status':
                result = self.collect_system_stats()
            else:
                logger.warning(f"Unknown command type: {command_type}")
                result = {"error": "Unknown command type"}
                
            # Gửi kết quả qua WebSocket nếu có thể
            if self.ws_client and self.ws_client.connected:
                self.ws_client.send_command_result(command_id, result or {"error": "Command failed"})
            else:
                # Fallback to HTTP
                self.http_client.send_command_result(
                    self.agent_token,
                    self.device_id,
                    command_id,
                    result or {"error": "Command failed"}
                )
            
        except Exception as e:
            logger.error(f"Error handling command: {e}", exc_info=True)
    
    def handle_command_result(self, result_data: Dict[str, Any]):
        """
        Handle a command result acknowledgment received from the server.
        
        Args:
            result_data: Command result data from server
        """
        try:
            command_id = result_data.get('commandId')
            status = result_data.get('status')
            logger.info(f"Received command result acknowledgment: {command_id} ({status})")
            
            # Additional handling for command results can be added here
            # For example, resending if the server reports an error
            
            if status == 'error':
                error_message = result_data.get('message', 'Unknown error')
                logger.warning(f"Server reported error for command {command_id}: {error_message}")
                
                # Optional: implement retry logic if needed
                
        except Exception as e:
            logger.error(f"Error handling command result acknowledgment: {e}", exc_info=True)
    
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
        """
        Start a thread to periodically report system status to the server.
        Uses threading.Timer for periodic execution.
        """
        if not self.running:
            return
            
        # Get monitoring interval from config (default 30 seconds)
        interval = 30  # Fixed interval as per requirement
        
        logger.debug(f"Starting status reporting thread with interval: {interval}s")
        
        # Send initial status update
        self._send_status_update()
        
        # Schedule the next update
        self.status_timer = threading.Timer(interval, self._start_status_reporting_thread)
        self.status_timer.daemon = True  # Allow the program to exit even if the timer is running
        self.status_timer.start()
        
    def _send_status_update(self):
        """
        Send system status update to the server.
        Gets current CPU and RAM usage and sends it.
        """
        try:
            # Get basic system stats (CPU and RAM usage)
            stats = self.system_monitor.get_stats()
            
            logger.debug(f"Sending status update: {stats}")
            
            # Send the update to the server
            success, response = self.http_client.update_status(
                self.agent_token,
                self.device_id,
                stats
            )
            
            if not success:
                logger.warning(f"Failed to send status update: {response.get('error', 'Unknown error')}")
            else:
                logger.debug("Status update sent successfully")
                
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
            
        # Start status reporting thread
        self.running = True
        self._start_status_reporting_thread()
        
        # Main agent loop (can be used for other periodic tasks)
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
        
        # Cancel the status reporting timer if it exists
        if hasattr(self, 'status_timer') and self.status_timer:
            self.status_timer.cancel()
            
        # Disconnect from WebSocket
        self.ws_client.disconnect()