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
import socketio
import requests
from typing import Dict, Any, Optional

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
        self.sio = socketio.Client()
        self.running = False
        self.setup_socket_events()
        
        # Device identification
        self.device_id = self._get_device_id()
        self.device_info = self._collect_system_info()
        
        logger.info(f"Agent initialized for device: {self.device_id}")
        
    def _load_config(self, config_path: str) -> Dict[str, Any]:
        """Load configuration from file."""
        default_config = {
            "server_url": "http://localhost:3000",
            "api_endpoint": "/api/v1",
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
        info = {
            "hostname": socket.gethostname(),
            "platform": platform.system(),
            "platform_release": platform.release(),
            "platform_version": platform.version(),
            "architecture": platform.machine(),
            "processor": platform.processor(),
            "physical_cores": psutil.cpu_count(logical=False),
            "total_cores": psutil.cpu_count(logical=True),
            "total_memory": psutil.virtual_memory().total,
            "python_version": platform.python_version()
        }
        
        logger.debug(f"Collected system information: {info}")
        return info
    
    def setup_socket_events(self):
        """Set up socket.io event handlers."""
        @self.sio.event
        def connect():
            logger.info("Connected to server")
            # Register device with server
            self.sio.emit('register_device', {
                'device_id': self.device_id,
                'device_info': self.device_info
            })
            
        @self.sio.event
        def disconnect():
            logger.info("Disconnected from server")
            
        @self.sio.on('command')
        def on_command(data):
            command = data.get('command')
            args = data.get('args', {})
            command_id = data.get('command_id')
            
            logger.info(f"Received command: {command} with ID: {command_id}")
            
            # Process the command and send back the result
            result = self.process_command(command, args)
            
            self.sio.emit('command_result', {
                'command_id': command_id,
                'device_id': self.device_id,
                'result': result
            })
    
    def process_command(self, command: str, args: Dict[str, Any]) -> Dict[str, Any]:
        """
        Process a command received from the server.
        
        Args:
            command (str): Command to execute
            args (Dict): Command arguments
            
        Returns:
            Dict with the result of the command
        """
        logger.debug(f"Processing command: {command} with args: {args}")
        
        result = {
            'success': False,
            'data': None,
            'error': None
        }
        
        try:
            if command == 'ping':
                result['data'] = 'pong'
                result['success'] = True
                
            elif command == 'get_system_stats':
                stats = self.collect_system_stats()
                result['data'] = stats
                result['success'] = True
                
            # Add more command handlers as needed
                
            else:
                result['error'] = f"Unknown command: {command}"
                
        except Exception as e:
            logger.exception(f"Error processing command {command}")
            result['error'] = str(e)
            
        return result
    
    def collect_system_stats(self) -> Dict[str, Any]:
        """Collect current system statistics."""
        stats = {
            'timestamp': time.time(),
            'cpu': {
                'percent': psutil.cpu_percent(interval=1),
                'per_cpu': psutil.cpu_percent(interval=1, percpu=True)
            },
            'memory': {
                'total': psutil.virtual_memory().total,
                'available': psutil.virtual_memory().available,
                'percent': psutil.virtual_memory().percent
            },
            'disk': {
                'total': psutil.disk_usage('/').total,
                'used': psutil.disk_usage('/').used,
                'free': psutil.disk_usage('/').free,
                'percent': psutil.disk_usage('/').percent
            },
            'network': {
                'bytes_sent': psutil.net_io_counters().bytes_sent,
                'bytes_recv': psutil.net_io_counters().bytes_recv
            },
            'boot_time': psutil.boot_time()
        }
        
        return stats
    
    def connect_to_server(self):
        """Connect to the backend server."""
        server_url = self.config.get('server_url')
        
        try:
            logger.info(f"Connecting to server: {server_url}")
            self.sio.connect(server_url)
        except Exception as e:
            logger.error(f"Failed to connect to server: {e}")
            
    def start_monitoring(self):
        """Start the monitoring loop."""
        interval = self.config.get('monitoring_interval')
        
        logger.info(f"Starting monitoring loop with interval: {interval}s")
        
        while self.running:
            try:
                stats = self.collect_system_stats()
                
                # Send stats to server if connected
                if self.sio.connected:
                    self.sio.emit('system_stats', {
                        'device_id': self.device_id,
                        'stats': stats
                    })
                    logger.debug("Sent system stats to server")
                else:
                    logger.warning("Not connected to server, couldn't send stats")
                    # Attempt to reconnect
                    self.connect_to_server()
                
            except Exception as e:
                logger.error(f"Error in monitoring loop: {e}")
                
            # Sleep for the configured interval
            time.sleep(interval)
    
    def start(self):
        """Start the agent."""
        self.running = True
        
        # Connect to the server
        self.connect_to_server()
        
        # Start the monitoring loop
        self.start_monitoring()
    
    def stop(self):
        """Stop the agent."""
        logger.info("Stopping agent")
        self.running = False
        
        if self.sio.connected:
            self.sio.disconnect()