"""
Command execution module for the Computer Management System Agent.
This module provides functionality to run shell commands and capture their output.
"""
import subprocess
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
            ws_client: WebSocket client instance (optional)
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
        Send command result to the server.
        
        Args:
            command_id: The ID of the command
            result: The command execution result
        """
        try:
            logger.debug(f"Sending command result for ID: {command_id}")
            
            # Try WebSocket first if available
            if self.ws_client and self.ws_client.connected:
                success = self.ws_client.send_command_result(command_id, result)
                if success:
                    logger.debug("Command result sent successfully via WebSocket")
                    return
                else:
                    logger.warning("Failed to send via WebSocket, falling back to HTTP")
            
            # Fallback to HTTP if WebSocket not available or failed
            success, response = self.http_client.send_command_result(
                self.agent_token,
                self.agent_id,
                command_id,
                result
            )
            
            if not success:
                logger.error(f"Failed to send command result: {response.get('error', 'Unknown error')}")
            else:
                logger.debug("Command result sent successfully via HTTP")
                
        except Exception as e:
            logger.error(f"Error sending command result: {e}", exc_info=True)