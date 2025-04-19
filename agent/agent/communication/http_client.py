"""
HTTP client module for communication with the backend server's Agent API.
"""
import json
import requests
from typing import Dict, Any, Tuple, Optional

from agent.config.config_manager import ConfigManager
from agent.utils.logger import get_logger

logger = get_logger(__name__)

class HttpClient:
    """
    HTTP client for making API calls to the backend server.
    """

    def __init__(self, config: ConfigManager):
        """
        Initialize the HTTP client.
        
        :param config: The configuration manager instance
        :type config: ConfigManager
        :raises: ValueError if server_url is not configured
        """
        self.config = config
        base_url = self.config.get('server_url')
        if not base_url:
            raise ValueError("Base URL (server_url) not found in configuration.")

        self.base_url = f"{base_url.rstrip('/')}/api/agent"

        self.timeout = self.config.get('http_client.request_timeout_sec', 15)

        logger.info(f"HTTP client initialized. Base API URL: {self.base_url}, Timeout: {self.timeout}s")

    def _make_request(self, method: str, endpoint: str, **kwargs) -> Tuple[Optional[Dict[str, Any]], Optional[str]]:
        """
        Internal helper to make HTTP requests and handle common errors.
        
        :param method: HTTP method (e.g., 'POST', 'GET')
        :type method: str
        :param endpoint: API endpoint path (e.g., '/identify')
        :type endpoint: str
        :param kwargs: Additional arguments passed to requests.request
        :return: Tuple containing response data and error message
        :rtype: Tuple[Optional[Dict[str, Any]], Optional[str]]
        """
        url = f"{self.base_url}{endpoint}"
        request_timeout = kwargs.pop('timeout', self.timeout)
        headers = kwargs.get('headers', {})
        headers.setdefault('User-Agent', 'ComputerManagementAgent/1.0')

        try:
            logger.debug(f"Making HTTP request: {method} {url} (Timeout: {request_timeout}s)")
            response = requests.request(method, url, timeout=request_timeout, **kwargs)
            response.raise_for_status()

            if response.status_code == 204:
                 logger.debug(f"Request successful (204 No Content): {method} {url}")
                 return {}, None
            try:
                response_json = response.json()
                logger.debug(f"Request successful ({response.status_code}): {method} {url}")
                return response_json, None
            except json.JSONDecodeError:
                logger.error(f"Failed to decode JSON response from {method} {url} (Status: {response.status_code}). Response text: {response.text[:200]}...")
                return None, "Invalid JSON response from server despite success status."

        except requests.exceptions.Timeout:
            logger.error(f"Request timed out after {request_timeout}s: {method} {url}")
            return None, f"Request timed out after {request_timeout} seconds."
        except requests.exceptions.ConnectionError as e:
            logger.error(f"Connection error: {method} {url} - {e}")
            return None, f"Unable to connect to the server at {self.base_url}."
        except requests.exceptions.HTTPError as e:
            status_code = e.response.status_code
            try:
                error_data = e.response.json()
                error_message = error_data.get('message', json.dumps(error_data))
                logger.error(f"HTTP error {status_code}: {method} {url}. Server response: {error_message}")
                return error_data, f"Server error {status_code}: {error_message}"
            except json.JSONDecodeError:
                 error_text = e.response.text[:200]
                 logger.error(f"HTTP error {status_code}: {method} {url}. Response: {error_text}...")
                 return None, f"Server returned error {status_code} (non-JSON response)."
        except requests.exceptions.RequestException as e:
            logger.error(f"An unexpected request error occurred: {method} {url} - {e}", exc_info=True)
            return None, f"Unexpected network error: {e}"
        except Exception as e:
             logger.critical(f"An unexpected internal error occurred during HTTP request: {method} {url} - {e}", exc_info=True)
             return None, f"Unexpected internal error while processing request: {e}"

    def identify_agent(self, unique_agent_id: str, room_config: Optional[Dict[str, Any]] = None, force_renew: bool = False) -> Tuple[bool, Dict[str, Any]]:
        """
        Identifies the agent with the backend, potentially triggering registration or MFA.
        
        :param unique_agent_id: Unique identifier for this agent
        :type unique_agent_id: str
        :param room_config: Room configuration (roomName, posX, posY)
        :type room_config: Optional[Dict[str, Any]]
        :param force_renew: Force token renewal even if already registered
        :type force_renew: bool
        :return: Tuple containing success status and response data
        :rtype: Tuple[bool, Dict[str, Any]]
        """
        endpoint = "/identify"
        payload: Dict[str, Any] = {"unique_agent_id": unique_agent_id}

        if force_renew:
            payload["forceRenewToken"] = True

        if room_config:
            payload["positionInfo"] = {
                "roomName": room_config.get('room'),
                "posX": room_config.get('position', {}).get('x'),
                "posY": room_config.get('position', {}).get('y')
            }

        logger.info(f"Identifying agent {unique_agent_id}...")
        response_json, error_message = self._make_request('POST', endpoint, json=payload)

        if error_message is None:
            return True, response_json if response_json is not None else {}
        else:
            error_response = response_json if isinstance(response_json, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            if 'message' not in error_response: error_response['message'] = error_message

            return False, error_response

    def verify_mfa(self, unique_agent_id: str, mfa_code: str) -> Tuple[bool, Dict[str, Any]]:
        """
        Verifies the MFA code with the backend server.
        
        :param unique_agent_id: Unique identifier for this agent
        :type unique_agent_id: str
        :param mfa_code: MFA code entered by the user
        :type mfa_code: str
        :return: Tuple containing success status and response data
        :rtype: Tuple[bool, Dict[str, Any]]
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
        
        :param agent_token: Agent authentication token
        :type agent_token: str
        :param unique_agent_id: Unique identifier for this agent
        :type unique_agent_id: str
        :param hardware_data: Dictionary containing hardware information
        :type hardware_data: Dict[str, Any]
        :return: Tuple containing success status and response data
        :rtype: Tuple[bool, Dict[str, Any]]
        """
        endpoint = "/hardware-info"
        headers = {
            "Authorization": f"Bearer {agent_token}",
            "X-Agent-Id": unique_agent_id,
            "Content-Type": "application/json"
        }

        logger.info(f"Sending hardware info for agent {unique_agent_id}...")
        logger.debug(f"Hardware payload: {json.dumps(hardware_data)}")

        response_json, error_message = self._make_request('POST', endpoint, json=hardware_data, headers=headers)

        if error_message is None:
            return True, response_json if response_json is not None else {}
        else:
            error_response = response_json if isinstance(response_json, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            if 'message' not in error_response: error_response['message'] = error_message

            return False, error_response
