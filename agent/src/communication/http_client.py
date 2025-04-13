# -*- coding: utf-8 -*-
"""
HTTP client module for communication with the backend server's Agent API.
Handles agent identification, MFA verification, and hardware info submission.
Accepts ConfigManager instance for configuration.
"""
import json
import requests
import logging
from typing import Dict, Any, Tuple, Optional

# Configuration
# from src.config.config_manager import config_manager # No longer using global instance
from src.config.config_manager import ConfigManager # Import class for type hinting

logger = logging.getLogger(__name__)

class HttpClient:
    """HTTP client for making API calls to the backend server."""

    def __init__(self, config: ConfigManager):
        """
        Initialize the HTTP client.

        Args:
            config (ConfigManager): The configuration manager instance.

        Raises:
            ValueError: If server_url is not configured.
        """
        self.config = config
        base_url = self.config.get('server_url')
        if not base_url:
            # This should be caught by ConfigManager validation, but double-check
            raise ValueError("Base URL (server_url) not found in configuration.")

        # Ensure base_url ends correctly for joining API path
        self.base_url = f"{base_url.rstrip('/')}/api/agent" # Ensure no double slashes

        # Read timeout from config, provide a default fallback
        self.timeout = self.config.get('http_client.request_timeout_sec', 15) # Default 15 seconds

        logger.info(f"HTTP client initialized. Base API URL: {self.base_url}, Timeout: {self.timeout}s")

    def _make_request(self, method: str, endpoint: str, **kwargs) -> Tuple[Optional[Dict[str, Any]], Optional[str]]:
        """
        Internal helper to make HTTP requests and handle common errors.

        Args:
            method (str): HTTP method (e.g., 'POST', 'GET').
            endpoint (str): API endpoint path (e.g., '/identify').
            **kwargs: Additional arguments passed to requests.request (e.g., json, headers).

        Returns:
            Tuple[Optional[Dict[str, Any]], Optional[str]]:
                (response_json, error_message)
                response_json: Parsed JSON response if successful and content exists,
                               or parsed JSON error body if server returns error with JSON.
                error_message: A description of the error if the request failed at network level
                               or if the server response was not JSON or empty on success.
        """
        url = f"{self.base_url}{endpoint}"
        # Use the configured timeout for the request
        request_timeout = kwargs.pop('timeout', self.timeout)
        headers = kwargs.get('headers', {})
        headers.setdefault('User-Agent', 'ComputerManagementAgent/1.0') # Add a user agent

        try:
            logger.debug(f"Making HTTP request: {method} {url} (Timeout: {request_timeout}s)")
            response = requests.request(method, url, timeout=request_timeout, **kwargs)
            response.raise_for_status() # Raise HTTPError for bad responses (4xx or 5xx)

            # Handle successful responses
            if response.status_code == 204: # No Content
                 logger.debug(f"Request successful (204 No Content): {method} {url}")
                 return {}, None # Return empty dict for consistency, no error
            try:
                response_json = response.json()
                logger.debug(f"Request successful ({response.status_code}): {method} {url}")
                return response_json, None
            except json.JSONDecodeError:
                logger.error(f"Failed to decode JSON response from {method} {url} (Status: {response.status_code}). Response text: {response.text[:200]}...")
                # Success status code but invalid JSON body
                return None, "Invalid JSON response from server despite success status."

        except requests.exceptions.Timeout:
            logger.error(f"Request timed out after {request_timeout}s: {method} {url}")
            return None, f"Yêu cầu hết hạn sau {request_timeout} giây."
        except requests.exceptions.ConnectionError as e:
            logger.error(f"Connection error: {method} {url} - {e}")
            return None, f"Không thể kết nối đến máy chủ tại {self.base_url}."
        except requests.exceptions.HTTPError as e:
            status_code = e.response.status_code
            try:
                # Try to get error details from JSON response body
                error_data = e.response.json()
                error_message = error_data.get('message', json.dumps(error_data)) # Use message or full JSON
                logger.error(f"HTTP error {status_code}: {method} {url}. Server response: {error_message}")
                # Return the server's error structure if possible, along with a generic message
                return error_data, f"Lỗi máy chủ {status_code}: {error_message}"
            except json.JSONDecodeError:
                 # If response is not JSON
                 error_text = e.response.text[:200] # Limit error text length
                 logger.error(f"HTTP error {status_code}: {method} {url}. Response: {error_text}...")
                 return None, f"Máy chủ trả về lỗi {status_code} (không phải JSON)."
        except requests.exceptions.RequestException as e:
            logger.error(f"An unexpected request error occurred: {method} {url} - {e}", exc_info=True)
            return None, f"Lỗi mạng không mong muốn: {e}"
        except Exception as e: # Catch any other unexpected errors
             logger.critical(f"An unexpected internal error occurred during HTTP request: {method} {url} - {e}", exc_info=True)
             return None, f"Lỗi nội bộ không mong muốn khi thực hiện yêu cầu: {e}"


    def identify_agent(self, unique_agent_id: str, room_config: Optional[Dict[str, Any]] = None, force_renew: bool = False) -> Tuple[bool, Dict[str, Any]]:
        """
        Identifies the agent with the backend, potentially triggering registration or MFA.

        Args:
            unique_agent_id (str): Unique identifier for this agent.
            room_config (dict, optional): Room configuration (roomName, posX, posY).
                                          Must be validated *before* calling this method.
            force_renew (bool, optional): Force token renewal even if already registered.

        Returns:
            Tuple[bool, Dict[str, Any]]: (api_call_success, response_data)
                api_call_success: True if the API call itself was successful (status 2xx), False on network/timeout errors or 4xx/5xx.
                response_data: The JSON response from the server (for 2xx) or an error structure.
                               For network/timeout errors, response_data contains a generic error message.
                               For 4xx/5xx errors, response_data contains the parsed error body if JSON, or a generic error message.
        """
        endpoint = "/identify"
        payload: Dict[str, Any] = {"unique_agent_id": unique_agent_id}

        if force_renew:
            payload["forceRenewToken"] = True

        # Assume room_config is validated by the caller (Agent class)
        if room_config:
            payload["positionInfo"] = {
                "roomName": room_config.get('room'),
                "posX": room_config.get('position', {}).get('x'), # Safe access
                "posY": room_config.get('position', {}).get('y')  # Safe access
            }
        # else: # No need to log warning here, caller handles validation
        #      logger.debug("No valid room config provided for identification.")


        logger.info(f"Identifying agent {unique_agent_id}...")
        response_json, error_message = self._make_request('POST', endpoint, json=payload)

        if error_message is None: # Indicates a 2xx response was received
            # API call itself was successful, return the server's response (which could still indicate logical failure like MFA needed)
            return True, response_json if response_json is not None else {} # Handle 204 No Content case
        else:
            # API call failed (network, timeout, server 4xx/5xx error)
            # response_json might contain the parsed error body from _make_request
            error_response = response_json if isinstance(response_json, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error' # Ensure status field
            if 'message' not in error_response: error_response['message'] = error_message # Add network/generic error message

            return False, error_response

    def verify_mfa(self, unique_agent_id: str, mfa_code: str) -> Tuple[bool, Dict[str, Any]]:
        """
        Verifies the MFA code with the backend server.

        Args:
            unique_agent_id (str): Unique identifier for this agent.
            mfa_code (str): MFA code entered by the user.

        Returns:
            Tuple[bool, Dict[str, Any]]: (api_call_success, response_data) - See identify_agent for details.
        """
        endpoint = "/verify-mfa"
        payload = {"unique_agent_id": unique_agent_id, "mfaCode": mfa_code}

        logger.info(f"Verifying MFA code for agent {unique_agent_id}...")
        response_json, error_message = self._make_request('POST', endpoint, json=payload)

        if error_message is None:
            return True, response_json if response_json is not None else {}
        else:
            error_response = response_json if isinstance(response_json, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            if 'message' not in error_response: error_response['message'] = error_message
            return False, error_response

    def send_hardware_info(self, agent_token: str, unique_agent_id: str, hardware_data: Dict[str, Any]) -> Tuple[bool, Dict[str, Any]]:
        """
        Sends hardware information to the backend server. Requires authentication.

        Args:
            agent_token (str): Agent authentication token.
            unique_agent_id (str): Unique identifier for this agent.
            hardware_data (Dict[str, Any]): Dictionary containing hardware information
                                           (e.g., cpu_info, gpu_info, total_ram, etc.).

        Returns:
            Tuple[bool, Dict[str, Any]]: (api_call_success, response_data) - See identify_agent for details.
                                           Success usually means a 204 No Content or 200 OK.
        """
        endpoint = "/hardware-info"
        headers = {
            "Authorization": f"Bearer {agent_token}",
            "X-Agent-Id": unique_agent_id, # Standard header for agent ID
            "Content-Type": "application/json"
        }

        logger.info(f"Sending hardware info for agent {unique_agent_id}...")
        logger.debug(f"Hardware payload: {json.dumps(hardware_data)}") # Log the payload being sent

        # Note: hardware_data is sent directly as the JSON payload
        response_json, error_message = self._make_request('POST', endpoint, json=hardware_data, headers=headers)

        if error_message is None: # Indicates success (2xx status code)
             # response_json will be {} for 204, or parsed JSON for 200/201 etc.
            return True, response_json if response_json is not None else {}
        else:
            # Return the error structure provided by _make_request or a generic one
            error_response = response_json if isinstance(response_json, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            if 'message' not in error_response: error_response['message'] = error_message

            return False, error_response
