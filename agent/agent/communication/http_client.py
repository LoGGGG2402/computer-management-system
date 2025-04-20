"""
HTTP client module for communication with the backend server's Agent API.
"""
import json
import requests
import os
import time
import tempfile
from urllib.parse import urljoin
from typing import Dict, Any, Tuple, Optional, TYPE_CHECKING, List

if TYPE_CHECKING:
    from ..config import ConfigManager

from ..utils import get_logger

logger = get_logger(__name__)

class HttpClient:
    """
    HTTP client for making API calls to the backend server.
    """

    def __init__(self, config: 'ConfigManager'):
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
        
        # Agent identification info
        self._agent_id = None
        self._agent_token = None
        
        logger.info(f"HTTP client initialized. Base API URL: {self.base_url}, Timeout: {self.timeout}s")

    def set_auth_info(self, agent_id: str, agent_token: str):
        """
        Set the agent ID and token for authenticated requests.
        
        :param agent_id: Agent ID for authentication
        :type agent_id: str
        :param agent_token: Agent authentication token
        :type agent_token: str
        """
        self._agent_id = agent_id
        self._agent_token = agent_token
        logger.debug(f"Auth info set for agent ID: {agent_id}")

    def _get_auth_headers(self) -> Dict[str, str]:
        """
        Get authentication headers for API requests.
        
        :return: Dictionary of headers
        :rtype: Dict[str, str]
        """
        headers = {
            'User-Agent': 'ComputerManagementAgent/1.0',
            'Content-Type': 'application/json'
        }
        
        if self._agent_id and self._agent_token:
            headers['X-Agent-Id'] = self._agent_id
            headers['Authorization'] = f"Bearer {self._agent_token}"
        
        return headers

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

    def send_hardware_info(self, hardware_data: Dict[str, Any]) -> Tuple[bool, Dict[str, Any]]:
        """
        Sends hardware information to the backend server. Requires authentication.
        
        :param hardware_data: Dictionary containing hardware information
        :type hardware_data: Dict[str, Any]
        :return: Tuple containing success status and response data
        :rtype: Tuple[bool, Dict[str, Any]]
        """
        endpoint = "/hardware-info"
        headers = {
            "Authorization": f"Bearer {self._agent_token}",
            "X-Agent-Id": self._agent_id,
            "Content-Type": "application/json"
        }

        logger.debug(f"Hardware payload: {json.dumps(hardware_data)}")

        response_json, error_message = self._make_request('POST', endpoint, json=hardware_data, headers=headers)

        if error_message is None:
            return True, response_json if response_json is not None else {}
        else:
            error_response = response_json if isinstance(response_json, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            if 'message' not in error_response: error_response['message'] = error_message

            return False, error_response
            
    def check_for_update(self, current_version: str) -> Tuple[bool, Optional[Dict[str, Any]]]:
        """
        Check for agent updates from the server.
        
        :param current_version: Current agent version
        :type current_version: str
        :return: Tuple (success_flag, update_info_or_none)
        :rtype: Tuple[bool, Optional[Dict[str, Any]]]
        """
        if not self._agent_id or not self._agent_token:
            logger.error("Cannot check for updates: Agent ID or token not set")
            return False, None
            
        url = urljoin(self.base_url, "/check_update")
        headers = self._get_auth_headers()
        params = {"current_version": current_version}
        
        try:
            logger.info(f"Checking for updates (current version: {current_version})...")
            response = requests.get(url, headers=headers, params=params, timeout=self.timeout)
            
            if response.status_code == 204:
                # No updates available
                logger.info("No updates available")
                return True, None
                
            if response.status_code == 200:
                # Update available
                update_info = response.json()
                logger.info(f"Update available: {update_info.get('version', 'Unknown')}")
                return True, update_info
                
            logger.error(f"Server returned unexpected status code during update check: {response.status_code}")
            return False, None
            
        except requests.RequestException as e:
            logger.error(f"Error checking for updates: {e}")
            return False, None
        except json.JSONDecodeError as e:
            logger.error(f"Error parsing update check response: {e}")
            return False, None
        except Exception as e:
            logger.error(f"Unexpected error during update check: {e}", exc_info=True)
            return False, None

    def download_file(self, url: str, save_path: str) -> Tuple[bool, str]:
        """
        Download a file from the server with authentication.
        
        :param url: URL to download from (can be relative to base URL)
        :type url: str
        :param save_path: Path to save the downloaded file
        :type save_path: str
        :return: Tuple (success_flag, error_message_or_empty_string)
        :rtype: Tuple[bool, str]
        """
        if not self._agent_id or not self._agent_token:
            logger.error("Cannot download file: Agent ID or token not set")
            return False, "Missing authentication credentials"
            
        # Handle both absolute and relative URLs
        if not url.startswith("http"):
            url = urljoin(self.base_url, url)
            
        headers = self._get_auth_headers()
        
        try:
            logger.info(f"Downloading file from {url} to {save_path}...")
            
            # Ensure target directory exists
            os.makedirs(os.path.dirname(os.path.abspath(save_path)), exist_ok=True)
            
            # Download with streaming to handle large files
            with requests.get(url, headers=headers, stream=True, timeout=self.timeout) as response:
                if response.status_code != 200:
                    error_msg = f"Server returned status {response.status_code} when downloading file"
                    logger.error(error_msg)
                    return False, error_msg
                    
                # Save to a temporary file first, then move to final destination
                with tempfile.NamedTemporaryFile(delete=False) as temp_file:
                    temp_path = temp_file.name
                    
                    # Download with progress tracking for large files
                    total_size = int(response.headers.get('content-length', 0))
                    downloaded = 0
                    last_log_time = time.time()
                    
                    for chunk in response.iter_content(chunk_size=8192):
                        if chunk:
                            temp_file.write(chunk)
                            downloaded += len(chunk)
                            
                            # Log progress periodically (every 3 seconds)
                            current_time = time.time()
                            if current_time - last_log_time > 3:
                                if total_size > 0:
                                    progress = (downloaded / total_size) * 100
                                    logger.debug(f"Download progress: {progress:.1f}% ({downloaded}/{total_size} bytes)")
                                else:
                                    logger.debug(f"Download progress: {downloaded} bytes")
                                last_log_time = current_time
                    
                # Move temp file to final destination
                import shutil
                shutil.move(temp_path, save_path)
                
            logger.info(f"File downloaded successfully to {save_path}")
            return True, ""
            
        except requests.RequestException as e:
            error_msg = f"Network error downloading file: {e}"
            logger.error(error_msg)
            return False, error_msg
        except (IOError, OSError) as e:
            error_msg = f"I/O error saving file: {e}"
            logger.error(error_msg)
            return False, error_msg
        except Exception as e:
            error_msg = f"Unexpected error downloading file: {e}"
            logger.error(error_msg, exc_info=True)
            return False, error_msg

    def report_error(self, error_data: Dict[str, Any]) -> bool:
        """
        Report an error to the server.
        
        :param error_data: Error data to report
        :type error_data: Dict[str, Any]
        :return: True if sent successfully, False otherwise
        :rtype: bool
        """
        if not self._agent_id or not self._agent_token:
            logger.error("Cannot report error: Agent ID or token not set")
            return False
            
        url = urljoin(self.base_url, "/report-error")
        headers = self._get_auth_headers()
        headers["Content-Type"] = "application/json"
        
        try:
            logger.info(f"Reporting error to server: {error_data.get('error_type', 'Unknown')}")
            response = requests.post(url, headers=headers, json=error_data, timeout=self.timeout)
            
            if response.status_code == 204:  # No content, success
                logger.info("Error report sent successfully")
                return True
                
            logger.error(f"Server returned unexpected status code for error report: {response.status_code}")
            return False
            
        except requests.RequestException as e:
            logger.error(f"Network error reporting error: {e}")
            return False
        except Exception as e:
            logger.error(f"Unexpected error sending error report: {e}", exc_info=True)
            return False