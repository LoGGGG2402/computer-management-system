"""
HTTP client module for the Computer Management System Agent.
This module provides functions to make API calls to the backend server.
"""
import json
import logging
import requests
from typing import Dict, Any, Optional, Tuple

logger = logging.getLogger(__name__)

class HttpClient:
    """HTTP client for making API calls to the backend server."""
    
    def __init__(self, base_url: str):
        """
        Initialize the HTTP client.
        
        Args:
            base_url (str): Base URL of the backend server
        """
        self.base_url = base_url
        logger.debug(f"HTTP client initialized with base URL: {base_url}")
    
    def identify_agent(self, unique_agent_id: str) -> Tuple[bool, Dict[str, Any]]:
        """
        Identify the agent with the backend server.
        
        Args:
            unique_agent_id (str): Unique identifier for this agent
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        url = f"{self.base_url}/api/agent/identify"
        payload = {
            "unique_agent_id": unique_agent_id
        }
        
        try:
            logger.info(f"Sending identify request for agent: {unique_agent_id}")
            response = requests.post(url, json=payload, timeout=10)
            
            if response.status_code == 200:
                data = response.json()
                logger.info(f"Identification response: {data}")
                return True, data
            else:
                logger.error(f"Identification failed with status {response.status_code}: {response.text}")
                return False, {"status": "error", "message": f"HTTP error: {response.status_code}"}
                
        except requests.RequestException as e:
            logger.error(f"Error identifying agent: {e}")
            return False, {"status": "error", "message": f"Connection error: {str(e)}"}
    
    def verify_mfa(self, unique_agent_id: str, mfa_code: str) -> Tuple[bool, Dict[str, Any]]:
        """
        Verify MFA code with the backend server.
        
        Args:
            unique_agent_id (str): Unique identifier for this agent
            mfa_code (str): MFA code to verify
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        url = f"{self.base_url}/api/agent/verify-mfa"
        payload = {
            "unique_agent_id": unique_agent_id,
            "mfaCode": mfa_code
        }
        
        try:
            logger.info(f"Sending MFA verification for agent: {unique_agent_id}")
            response = requests.post(url, json=payload, timeout=10)
            
            if response.status_code == 200:
                data = response.json()
                logger.info("MFA verification successful")
                return True, data
            else:
                logger.error(f"MFA verification failed with status {response.status_code}: {response.text}")
                return False, {"status": "error", "message": f"HTTP error: {response.status_code}"}
                
        except requests.RequestException as e:
            logger.error(f"Error verifying MFA: {e}")
            return False, {"status": "error", "message": f"Connection error: {str(e)}"}
    
    def update_status(self, agent_token: str, unique_agent_id: str, status_data: Dict[str, Any]) -> Tuple[bool, Dict[str, Any]]:
        """
        Update agent status with the backend server.
        
        Args:
            agent_token (str): Authentication token for the agent
            unique_agent_id (str): Unique identifier for this agent
            status_data (Dict): Status data to send
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        url = f"{self.base_url}/api/agent/status"
        headers = {
            "Authorization": f"Bearer {agent_token}",
            "X-Agent-ID": unique_agent_id
        }
        
        try:
            logger.debug("Sending status update")
            response = requests.put(url, json=status_data, headers=headers, timeout=10)
            
            if response.status_code == 200:
                data = response.json()
                return True, data
            else:
                logger.error(f"Status update failed with status {response.status_code}: {response.text}")
                return False, {"status": "error", "message": f"HTTP error: {response.status_code}"}
                
        except requests.RequestException as e:
            logger.error(f"Error updating status: {e}")
            return False, {"status": "error", "message": f"Connection error: {str(e)}"}
    
    def send_command_result(self, agent_token: str, unique_agent_id: str, command_id: str, 
                          result: Dict[str, Any]) -> Tuple[bool, Dict[str, Any]]:
        """
        Send command execution result to the backend server.
        
        Args:
            agent_token (str): Authentication token for the agent
            unique_agent_id (str): Unique identifier for this agent
            command_id (str): ID of the command that was executed
            result (Dict): Result of the command execution
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        url = f"{self.base_url}/api/agent/command-result"
        headers = {
            "Authorization": f"Bearer {agent_token}",
            "X-Agent-ID": unique_agent_id
        }
        
        payload = {
            "command_id": command_id,
            **result
        }
        
        try:
            logger.info(f"Sending command result for command: {command_id}")
            response = requests.post(url, json=payload, headers=headers, timeout=10)
            
            if response.status_code == 200:
                data = response.json()
                return True, data
            else:
                logger.error(f"Sending command result failed with status {response.status_code}: {response.text}")
                return False, {"status": "error", "message": f"HTTP error: {response.status_code}"}
                
        except requests.RequestException as e:
            logger.error(f"Error sending command result: {e}")
            return False, {"status": "error", "message": f"Connection error: {str(e)}"}