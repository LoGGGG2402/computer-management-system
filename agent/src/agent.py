#!/usr/bin/env python3
"""
Agent module for the Computer Management System.
This module contains the main Agent class that handles system monitoring
and communication with the backend server.
"""
import os
import json
import time
import uuid
import logging
import socket
import platform
import psutil
from typing import Dict, Any, Optional, Tuple

# Import agent modules
from modules.http_client import HttpClient
from modules.mfa_handler import prompt_for_mfa, display_registration_success
from modules.token_manager import load_token, save_token, delete_token
from modules.ws_client import WSClient
from modules.system_monitor import SystemMonitor
from utils.utils import run_command

logger = logging.getLogger(__name__)

class Agent:
    """
    Agent class responsible for monitoring system resources
    and communicating with the backend server.
    """
    
    def __init__(self, config_path: str):
        """
        Initialize the agent with configuration.
        
        Args:
            config_path (str): Path to the configuration file
        """
        self.config = self._load_config(config_path)
        self.running = False
        
        # Device identification
        self.device_id = self._get_device_id()
        
        # Initialize required services
        self.http_client = HttpClient(self.config.get('server_url'))
        self.ws_client = WSClient(self.config.get('server_url'))
        self.system_monitor = SystemMonitor()
        self.agent_token = None
        
        # Set up command handler
        self.ws_client.register_command_handler(self.handle_command)
        
        logger.info(f"Agent initialized for device: {self.device_id}")
        
    def _load_config(self, config_path: str) -> Dict[str, Any]:
        """Load configuration from file."""
        default_config = {
            "server_url": "http://localhost:3000",
            "api_endpoint": "/api/agent",
            "monitoring_interval": 60,  # seconds
            "reconnect_delay": 5,  # seconds
            "storage_path": "../storage"
        }
        
        try:
            if os.path.exists(config_path):
                with open(config_path, 'r') as f:
                    loaded_config = json.load(f)
                    default_config.update(loaded_config)
                    logger.info(f"Loaded configuration from {config_path}")
            else:
                logger.warning(f"Config file {config_path} not found, using defaults")
                
                # Create default config file
                os.makedirs(os.path.dirname(config_path), exist_ok=True)
                with open(config_path, 'w') as f:
                    json.dump(default_config, f, indent=4)
                logger.info(f"Created default configuration at {config_path}")
                
        except Exception as e:
            logger.error(f"Error loading configuration: {e}")
            
        return default_config
    
    def _get_device_id(self) -> str:
        """Generate or retrieve a unique device ID."""
        storage_path = self.config.get("storage_path")
        device_id_file = os.path.join(storage_path, "device_id.txt")
        
        try:
            os.makedirs(storage_path, exist_ok=True)
            
            if os.path.exists(device_id_file):
                with open(device_id_file, 'r') as f:
                    device_id = f.read().strip()
                    logger.debug(f"Loaded existing device ID: {device_id}")
                    return device_id
            
            # Generate new device ID based on hostname and MAC address
            hostname = socket.gethostname()
            mac = ':'.join(['{:02x}'.format((uuid.getnode() >> elements) & 0xff) 
                          for elements in range(0, 48, 8)][::-1])
            device_id = f"{hostname}-{mac}"
            
            with open(device_id_file, 'w') as f:
                f.write(device_id)
            
            logger.info(f"Generated new device ID: {device_id}")
            return device_id
            
        except Exception as e:
            logger.error(f"Error getting device ID: {e}")
            return f"unknown-{socket.gethostname()}"
    
    def _collect_system_info(self) -> Dict[str, Any]:
        """Collect basic system information."""
        return self.system_monitor.get_system_info()
        
    def authenticate(self) -> bool:
        """
        Authenticate the agent with the backend server.
        
        Returns:
            bool: True if authenticated successfully
        """
        logger.info("Starting agent authentication process")
        
        # Try to load existing token
        storage_path = self.config.get("storage_path")
        token = load_token(self.device_id, storage_path)
        
        if token:
            logger.info("Agent token found, using existing token")
            self.agent_token = token
            return True
        
        # No token found, start identification process
        logger.info("No agent token found, starting identification process")
        success, identify_response = self.http_client.identify_agent(self.device_id)
        
        if not success:
            logger.error("Failed to identify agent with backend server")
            return False
        
        # Check the identification response
        if identify_response.get("status") == "mfa_required":
            logger.info("MFA required for agent registration")
            
            try:
                # Prompt user for MFA code
                mfa_code = prompt_for_mfa()
                
                # Verify MFA code
                success, verify_response = self.http_client.verify_mfa(self.device_id, mfa_code)
                
                if not success or "agentToken" not in verify_response:
                    logger.error("MFA verification failed")
                    return False
                
                # Get and save the agent token
                self.agent_token = verify_response["agentToken"]
                saved = save_token(self.device_id, self.agent_token, storage_path)
                
                if not saved:
                    logger.warning("Failed to save agent token")
                
                # Display success message
                display_registration_success()
                
                logger.info("Agent registered and authenticated successfully")
                return True
                
            except KeyboardInterrupt:
                logger.info("Authentication process interrupted by user")
                return False
                
            except Exception as e:
                logger.error(f"Error during authentication: {e}")
                return False
        
        elif identify_response.get("status") == "authentication_required":
            logger.error("Agent is registered but token is missing")
            return False
        
        else:
            logger.error(f"Unexpected response from server: {identify_response}")
            return False
    
    def handle_command(self, command_data: Dict[str, Any]):
        """
        Handle a command received from the server.
        
        Args:
            command_data: Command data from server
        """
        command_id = command_data.get("commandId")
        command = command_data.get("command")
        
        if not command_id or not command:
            logger.error("Received invalid command data")
            return
        
        logger.info(f"Executing command: {command}")
        
        try:
            # Execute the command
            command_args = command.split()
            success, stdout, stderr = run_command(command_args)
            
            # Prepare the result
            result = {
                "stdout": stdout,
                "stderr": stderr,
                "exitCode": 0 if success else 1
            }
            
            # Send the result via WebSocket
            self.ws_client.send_command_result(command_id, result)
            
            # Also try to send via HTTP (fallback)
            self.http_client.send_command_result(
                self.agent_token,
                self.device_id,
                command_id,
                result
            )
        
        except Exception as e:
            logger.error(f"Error executing command: {e}")
            error_result = {
                "stdout": "",
                "stderr": str(e),
                "exitCode": 1
            }
            self.ws_client.send_command_result(command_id, error_result)
    
    def collect_system_stats(self) -> Dict[str, Any]:
        """Collect current system statistics."""
        cpu_info = self.system_monitor.get_cpu_info()
        memory_info = self.system_monitor.get_memory_info()
        
        stats = {
            'timestamp': time.time(),
            'cpu': {
                'percent': cpu_info["cpu_percent"],
                'per_cpu': cpu_info["cpu_percent_per_core"]
            },
            'memory': {
                'total': memory_info["total"],
                'available': memory_info["available"],
                'percent': memory_info["percent"]
            }
        }
        
        return stats
    
    def connect_to_server(self) -> bool:
        """
        Connect to the backend server via WebSocket.
        
        Returns:
            bool: True if connected successfully
        """
        if not self.agent_token:
            logger.error("Cannot connect to server: No agent token available")
            return False
        
        return self.ws_client.connect_and_authenticate(self.device_id, self.agent_token)
    
    def start_monitoring(self):
        """Start the monitoring loop."""
        interval = self.config.get('monitoring_interval')
        reconnect_delay = self.config.get('reconnect_delay')
        
        logger.info(f"Starting monitoring loop with interval: {interval}s")
        
        while self.running:
            try:
                # Collect system stats
                stats = self.collect_system_stats()
                
                # Send stats to server via WebSocket if connected
                if self.ws_client.connected:
                    self.ws_client.send_status_update(stats)
                else:
                    logger.warning("Not connected to WebSocket server, attempting to reconnect")
                    self.connect_to_server()
                    time.sleep(reconnect_delay)
                    continue
                
                # Also send stats via HTTP (if needed)
                # self.http_client.update_status(self.agent_token, self.device_id, stats)
                
            except Exception as e:
                logger.error(f"Error in monitoring loop: {e}")
                
            # Sleep for the configured interval
            time.sleep(interval)
    
    def start(self):
        """Start the agent."""
        try:
            logger.info("Starting agent")
            
            # Authenticate with the server
            if not self.authenticate():
                logger.error("Authentication failed, cannot start agent")
                return
            
            # Connect to the server via WebSocket
            if not self.connect_to_server():
                logger.error("WebSocket connection failed, will retry in monitoring loop")
            
            # Start the monitoring loop
            self.running = True
            self.start_monitoring()
            
        except KeyboardInterrupt:
            logger.info("Agent stopped by user")
            self.stop()
        except Exception as e:
            logger.error(f"Error starting agent: {e}")
            self.stop()
    
    def stop(self):
        """Stop the agent."""
        logger.info("Stopping agent")
        self.running = False
        
        if self.ws_client:
            self.ws_client.disconnect()