"""
Command execution module for the Computer Management System Agent.
This module provides functionality to run shell commands and capture their output.
"""
import subprocess
import time
from typing import Dict, Any

from src.utils.logger import get_logger
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient

# Get logger for this module
logger = get_logger(__name__)

class CommandExecutor:
    """Class responsible for executing shell commands."""
    
    def __init__(self, http_client: HttpClient, agent_id: str, agent_token: str, ws_client: WSClient = None):
        """
        Initialize the command executor.
        
        Args:
            http_client: HTTP client instance for sending results
            agent_id: The unique agent ID
            agent_token: The agent authentication token
            ws_client: WebSocket client instance (required for sending results)
        """
        self.http_client = http_client
        self.agent_id = agent_id
        self.agent_token = agent_token
        self.ws_client = ws_client
        logger.debug("CommandExecutor initialized")
    
    def run_command(self, command: str, command_id: str):
        """
        Run a shell command and send the result back to the server.
        
        Args:
            command: The command to execute
            command_id: The ID of the command
        """
        logger.info(f"Executing command: {command} (ID: {command_id})")
        
        try:
            # Use subprocess to run the command
            process = subprocess.run(
                command,
                shell=True,  # Use shell to allow complex commands
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,  # Return strings instead of bytes
                timeout=60  # Timeout after 1 minute (reduced from 5 minutes)
            )
            
            # Prepare result
            result = {
                "stdout": process.stdout,
                "stderr": process.stderr,
                "exitCode": process.returncode
            }
            
            logger.debug(f"Command completed with exit code: {process.returncode}")
            
            # Send result back to server
            self._send_result(command_id, result)
            
        except subprocess.TimeoutExpired:
            logger.error(f"Command timed out: {command}")
            self._send_result(command_id, {
                "stdout": "",
                "stderr": "Command timed out after 1 minute",
                "exitCode": 124  # Standard Linux timeout exit code
            })
        except Exception as e:
            logger.error(f"Error executing command: {e}", exc_info=True)
            self._send_result(command_id, {
                "stdout": "",
                "stderr": f"Error executing command: {str(e)}",
                "exitCode": 1
            })
    
    def _send_result(self, command_id: str, result: Dict[str, Any]):
        """
        Send command result to the server using WebSocket.
        
        Args:
            command_id: The ID of the command
            result: The command execution result
        """
        try:
            logger.debug(f"Sending command result for ID: {command_id}")
            
            # Check WebSocket connection and try to send result
            if not self.ws_client:
                logger.error("WebSocket client not initialized")
                return
                
            if not self.ws_client.connected:
                logger.warning("WebSocket not connected, attempting to reconnect")
                # Try to reconnect before sending
                max_attempts = 3
                for attempt in range(max_attempts):
                    logger.debug(f"Reconnection attempt {attempt + 1}/{max_attempts}")
                    reconnected = self.ws_client.connect_and_authenticate(self.agent_id, self.agent_token)
                    if reconnected:
                        logger.info("Successfully reconnected to WebSocket server")
                        break
                    
                    if attempt < max_attempts - 1:
                        wait_time = 2 * (attempt + 1)  # Exponential backoff
                        logger.debug(f"Waiting {wait_time} seconds before next attempt")
                        time.sleep(wait_time)
                        
            # Try to send the command result via WebSocket
            if self.ws_client.connected:
                success = self.ws_client.send_command_result(command_id, result)
                if success:
                    logger.debug("Command result sent successfully via WebSocket")
                else:
                    logger.error("Failed to send command result via WebSocket")
            else:
                logger.error("Cannot send command result: WebSocket not connected")
                
        except Exception as e:
            logger.error(f"Error sending command result: {e}", exc_info=True)
            
    def handle_legacy_command(self, command_data: Dict[str, Any]):
        """
        Handle a legacy format command.
        
        Args:
            command_data: Command data with legacy format
        """
        try:
            command_id = command_data.get('id', 'unknown')
            command_type = command_data.get('type')
            
            if command_type == 'shell':
                # Extract command from data
                command = command_data.get('command', '')
                if command:
                    self.run_command(command, command_id)
                else:
                    logger.error("Legacy shell command missing 'command' field")
                    self._send_result(command_id, {
                        "stdout": "",
                        "stderr": "Invalid command format - missing command field",
                        "exitCode": 1
                    })
            else:
                logger.warning(f"Unsupported legacy command type: {command_type}")
                self._send_result(command_id, {
                    "stdout": "",
                    "stderr": f"Unsupported command type: {command_type}",
                    "exitCode": 1
                })
        except Exception as e:
            logger.error(f"Error handling legacy command: {e}", exc_info=True)