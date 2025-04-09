"""
HTTP client module for the Computer Management System Agent.
This module provides functions to make API calls to the backend server.
"""
import json
import requests
from typing import Dict, Any, Optional, Tuple

from src.utils.logger import get_logger

# Get logger for this module
logger = get_logger(__name__)

class HttpClient:
    """HTTP client for making API calls to the backend server."""
    
    def __init__(self, base_url: str):
        """
        Initialize the HTTP client.
        
        Args:
            base_url (str): Base URL of the backend server
        """
        self.base_url = f"{base_url}/api/agent"
        logger.info(f"HTTP client initialized with base URL: {self.base_url}")
    
    def _extract_error_message(self, response) -> str:
        """
        Extract error message from response.
        
        Args:
            response: Response object from requests
            
        Returns:
            str: Extracted error message
        """
        try:
            data = response.json()
            # Check for standard error format from backend
            if 'message' in data:
                return data['message']
            elif 'error' in data:
                return data['error']
            # Check for status with message format
            elif 'status' in data and data['status'] == 'error' and 'message' in data:
                return data['message']
            else:
                return f"Server returned: {response.status_code} {response.reason}"
        except Exception:
            return f"Server returned: {response.status_code} {response.reason}"

    def identify_agent(self, unique_agent_id: str, room_config: dict = None, force_renew: bool = False) -> Tuple[bool, Dict[str, Any]]:
        """
        Identify the agent with the backend server.
        
        Args:
            unique_agent_id (str): Unique identifier for this agent
            room_config (dict, optional): Room configuration information
            force_renew (bool, optional): Force token renewal even if already registered
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        endpoint = f"{self.base_url}/identify"
        payload = {"unique_agent_id": unique_agent_id}
        
        # Add force renew flag if needed
        if force_renew:
            payload["forceRenewToken"] = True
        
        # Add room information if available
        if room_config:
            payload["positionInfo"] = {
                "room": room_config.get('room'),
                "posX": room_config.get('position', {}).get('x'),
                "posY": room_config.get('position', {}).get('y')
            }
            
        try:
            logger.debug(f"Sending identify request with payload: {payload}")
            response = requests.post(endpoint, json=payload)
            response.raise_for_status()
            logger.debug(f"Agent identified successfully: {response.json()}")
            return True, response.json()
        except requests.RequestException as e:
            logger.error(f"Failed to identify agent: {e}")
            error_message = "Unknown error"
            
            if hasattr(e, 'response') and e.response:
                error_message = self._extract_error_message(e.response)
                logger.error(f"Server error: {error_message}")
                
            return False, {"error": error_message, "status": "error"}
    
    def verify_mfa(self, unique_agent_id: str, mfa_code: str, room_config: dict = None) -> Tuple[bool, Dict[str, Any]]:
        """
        Verify MFA code with the backend server.
        
        Args:
            unique_agent_id (str): Unique identifier for this agent
            mfa_code (str): MFA code to verify
            room_config (dict, optional): Room configuration information
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        endpoint = f"{self.base_url}/verify-mfa"
        payload = {"unique_agent_id": unique_agent_id, "mfaCode": mfa_code}
        
        # Add room information if available
        if room_config:
            payload["positionInfo"] = {
                "room": room_config.get('room'),
                "posX": room_config.get('position', {}).get('x'),
                "posY": room_config.get('position', {}).get('y')
            }
        
        try:
            # Add more detailed logging including the full payload and headers
            logger.debug(f"Sending verify MFA request to {endpoint}")
            logger.debug(f"Verify MFA full payload: {json.dumps(payload)}")
            
            response = requests.post(endpoint, json=payload)
            logger.debug(f"MFA verification response status: {response.status_code}")
            logger.debug(f"MFA verification response body: {response.text}")
            
            response.raise_for_status()
            logger.debug(f"MFA verified successfully: {response.json()}")
            return True, response.json()
        except requests.RequestException as e:
            logger.error(f"Failed to verify MFA: {e}")
            logger.error(f"Error details: {str(e)}")
            error_message = "Unknown error"
            
            if hasattr(e, 'response') and e.response:
                error_message = self._extract_error_message(e.response)
                logger.error(f"Server error: {error_message}")
                logger.error(f"Response status: {e.response.status_code}")
                logger.error(f"Response body: {e.response.text}")
            
            return False, {"error": error_message, "status": "error"}
    
    def update_status(self, agent_token: str, unique_agent_id: str, stats: Dict[str, Any]) -> Tuple[bool, Dict[str, Any]]:
        """
        Update agent status with the backend server.
        
        Args:
            agent_token (str): Agent authentication token
            unique_agent_id (str): Unique identifier for this agent
            stats (Dict[str, Any]): System statistics to update
            
        Returns:
            Tuple[bool, Dict]: (success, response_data)
        """
        logger.warning("update_status via HTTP is deprecated. Use WebSocket instead.")
        endpoint = f"{self.base_url}/status"
        headers = {
            "Authorization": f"Bearer {agent_token}",
            "X-Agent-ID": unique_agent_id
        }
        
        # Format the payload according to the expected backend format
        payload = {
            "cpu": stats.get("cpu", 0),
            "ram": stats.get("ram", 0)
        }
        
        try:
            logger.debug(f"Sending status update with payload: {payload}")
            response = requests.put(endpoint, json=payload, headers=headers)
            
            if response.status_code == 204:  # No Content
                logger.debug("Status updated successfully with 204 response")
                return True, {}
                
            response.raise_for_status()
            logger.debug("Status updated successfully")
            return True, response.json() if response.text else {}
        except requests.RequestException as e:
            logger.error(f"Failed to update status: {e}")
            error_message = "Unknown error"
            
            if hasattr(e, 'response') and e.response:
                error_message = self._extract_error_message(e.response)
                logger.error(f"Server error: {error_message}")
            
            return False, {"error": error_message, "status": "error"}

    # Send command result method has been removed as this is now handled via WebSocket