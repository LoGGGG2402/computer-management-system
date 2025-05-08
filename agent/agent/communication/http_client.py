import json
import requests
import os
import time
import tempfile
import shutil
from urllib.parse import urljoin, urlparse
from typing import Dict, Any, Tuple, Optional, TYPE_CHECKING, List, Union

if TYPE_CHECKING:
    from agent.config import ConfigManager

from agent.utils import get_logger

logger = get_logger(__name__)

class HttpClient:
    """
    HTTP client for communication with the backend server's Agent API.

    Handles request construction, authentication, error handling, and response parsing.

    :ivar config: The configuration manager instance.
    :ivar base_url: The base URL for the agent API.
    :ivar timeout: The default request timeout in seconds.
    :ivar _agent_id: The agent ID for authentication (if set).
    :ivar _agent_token: The agent authentication token (if set).
    """

    def __init__(self, config: 'ConfigManager'):
        """
        Initializes the HTTP client.

        :param config: The configuration manager instance.
        :type config: ConfigManager
        :raises ValueError: If `server_url` is not configured or is invalid.
        """
        self.config = config
        base_url_config = self.config.get('server_url')
        if not base_url_config:
            raise ValueError("Base URL (server_url) not found in configuration.")

        parsed_url = urlparse(base_url_config)
        if not parsed_url.scheme or not parsed_url.netloc:
             raise ValueError(f"Invalid server_url configured: {base_url_config}. Must include scheme (e.g., http:// or https://).")

        
        self.base_url = urljoin(f"{parsed_url.scheme}://{parsed_url.netloc}", parsed_url.path.rstrip('/') + "/api/agent/")
        self.timeout = self.config.get('http_client.request_timeout_sec', 15)
        self._agent_id: Optional[str] = None
        self._agent_token: Optional[str] = None
        logger.info(f"HTTP client initialized. Base API URL: {self.base_url}, Timeout: {self.timeout}s")

    def set_auth_info(self, agent_id: str, agent_token: str):
        """
        Sets the agent ID and token for authenticated requests.

        :param agent_id: The agent ID received from the server.
        :type agent_id: str
        :param agent_token: The agent authentication token received from the server.
        :type agent_token: str
        """
        if not agent_id or not agent_token:
            logger.warning("Attempted to set empty agent_id or agent_token.")
            return

        self._agent_id = agent_id
        self._agent_token = agent_token
        logger.debug(f"Auth info set for agent ID: {self._agent_id}")

    def identify_agent(self, unique_agent_id: str, room_config: Optional[Dict[str, Any]] = None, force_renew: bool = False) -> Tuple[bool, Dict[str, Any]]:
        """
        Identifies the agent with the backend, potentially triggering registration or MFA.

        :param unique_agent_id: Unique identifier for this agent instance.
        :type unique_agent_id: str
        :param room_config: Optional dictionary with room configuration (e.g., {'room': 'RoomA', 'position': {'x': 10, 'y': 20}}).
        :type room_config: Optional[Dict[str, Any]]
        :param force_renew: If True, requests a new token even if already registered.
        :type force_renew: bool
        :return: Tuple (success_flag, response_data_or_error_dict).
                 On success: (True, {'agentId': '...', 'token': '...', 'status': 'registered/mfa_required'})
                 On failure: (False, {'status': 'error', 'message': '...', ...})
        :rtype: Tuple[bool, Dict[str, Any]]
        """
        endpoint = "/identify"
        payload: Dict[str, Any] = {"unique_agent_id": unique_agent_id}

        if force_renew:
            payload["forceRenewToken"] = True

        if room_config:
            pos = room_config.get('position', {})
            payload["positionInfo"] = {
                "roomName": room_config.get('room'),
                "posX": pos.get('x'),
                "posY": pos.get('y')
            }

        logger.info(f"Identifying agent {unique_agent_id}...")
        response_data, error_message = self._make_request('POST', endpoint, json=payload)

        if error_message is None and isinstance(response_data, dict):
            logger.info(f"Agent identification successful. Status: {response_data.get('status', 'Unknown')}")
            return True, response_data
        else:
            error_response = response_data if isinstance(response_data, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            error_response['message'] = error_message or "Unknown identification error"
            logger.error(f"Agent identification failed: {error_response['message']}")
            return False, error_response

    def verify_mfa(self, unique_agent_id: str, mfa_code: str) -> Tuple[bool, Dict[str, Any]]:
        """
        Verifies the MFA code with the backend server.

        :param unique_agent_id: Unique identifier for this agent instance.
        :type unique_agent_id: str
        :param mfa_code: MFA code entered by the user.
        :type mfa_code: str
        :return: Tuple (success_flag, response_data_or_error_dict).
                 On success: (True, {'agentId': '...', 'token': '...', 'status': 'registered'})
                 On failure: (False, {'status': 'error', 'message': '...', ...})
        :rtype: Tuple[bool, Dict[str, Any]]
        """
        endpoint = "/verify-mfa"
        payload = {"unique_agent_id": unique_agent_id, "mfaCode": mfa_code}

        logger.info(f"Verifying MFA code for agent {unique_agent_id}...")
        response_data, error_message = self._make_request('POST', endpoint, json=payload)

        if error_message is None and isinstance(response_data, dict):
             logger.info(f"MFA verification successful. Status: {response_data.get('status', 'Unknown')}")
             return True, response_data
        else:
            error_response = response_data if isinstance(response_data, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            error_response['message'] = error_message or "Unknown MFA verification error"
            logger.error(f"MFA verification failed: {error_response['message']}")
            return False, error_response

    def send_hardware_info(self, hardware_data: Dict[str, Any]) -> Tuple[bool, Dict[str, Any]]:
        """
        Sends hardware information to the backend server. Requires prior authentication.

        :param hardware_data: Dictionary containing structured hardware information.
        :type hardware_data: Dict[str, Any]
        :return: Tuple (success_flag, response_data_or_error_dict).
                 On success: (True, server_response_dict)
                 On failure: (False, {'status': 'error', 'message': '...', ...})
        :rtype: Tuple[bool, Dict[str, Any]]
        """
        endpoint = "/hardware-info"
        logger.info(f"Sending hardware info ({len(hardware_data)} top-level keys)...")

        response_data, error_message = self._make_request('POST', endpoint, authenticated=True, json=hardware_data)

        if error_message is None and isinstance(response_data, dict):
            logger.info("Hardware info sent successfully.")
            return True, response_data
        else:
            error_response = response_data if isinstance(response_data, dict) else {}
            if 'status' not in error_response: error_response['status'] = 'error'
            if error_message == "Agent authentication required but not configured.":
                 error_response['message'] = error_message
            else:
                 error_response['message'] = error_message or "Unknown error sending hardware info"
            logger.error(f"Failed to send hardware info: {error_response['message']}")
            return False, error_response

    def check_for_update(self, current_version: str) -> Tuple[bool, Optional[Dict[str, Any]]]:
        """
        Checks for agent updates from the server. Requires prior authentication.

        :param current_version: The current version string of the running agent.
        :type current_version: str
        :return: Tuple (success_flag, update_info_or_none).
                 On success with update: (True, {'version': '...', 'url': '...', ...})
                 On success with no update: (True, None)
                 On failure: (False, None)
        :rtype: Tuple[bool, Optional[Dict[str, Any]]]
        """
        endpoint = "/check-update"
        params = {"current_version": current_version}
        logger.info(f"Checking for updates (current version: {current_version})...")

        response_data, error_message = self._make_request('GET', endpoint, authenticated=True, params=params)

        if error_message is None:
            if isinstance(response_data, dict) and not response_data:
                 logger.info("No updates available (Server returned 204 No Content).")
                 return True, None
            elif isinstance(response_data, dict) and response_data:
                 logger.info(f"Update available: Version {response_data.get('version', 'Unknown')}")
                 return True, response_data
            else:
                 logger.error(f"Received unexpected successful response format during update check: {type(response_data)}")
                 return False, None
        else:
            logger.error(f"Failed to check for updates: {error_message}")
            if error_message == "Agent authentication required but not configured.":
                 logger.error("Update check failed due to missing authentication.")
            return False, None

    def download_file(self, url: str, save_path: str) -> Tuple[bool, str]:
        """
        Downloads a file from the server, handling authentication and large files.

        Uses streaming download and saves to a temporary file before moving.

        :param url: URL to download from (can be relative to agent API base or absolute).
        :type url: str
        :param save_path: The local filesystem path to save the downloaded file.
        :type save_path: str
        :return: Tuple (success_flag, status_message).
                 On success: (True, "File downloaded successfully.")
                 On failure: (False, "Error message describing the failure.")
        :rtype: Tuple[bool, str]
        """
        if not self._agent_id or not self._agent_token:
            msg = "Cannot download file: Agent authentication credentials are not set."
            logger.error(msg)
            return False, msg

        parsed_relative_url = urlparse(url)
        if not parsed_relative_url.scheme and not parsed_relative_url.netloc:
            full_url = urljoin(self.base_url, url.lstrip('/'))
            logger.debug(f"Resolved relative URL '{url}' to '{full_url}'")
        else:
            full_url = url
            logger.debug(f"Using absolute URL for download: '{full_url}'")

        headers = self._get_auth_headers()
        headers.setdefault('User-Agent', 'ComputerManagementAgent/1.0')
        temp_file_path: Optional[str] = None

        try:
            logger.info(f"Attempting to download file from {full_url} to {save_path}...")
            target_dir = os.path.dirname(os.path.abspath(save_path))
            if not os.path.exists(target_dir):
                 logger.info(f"Creating target directory: {target_dir}")
                 os.makedirs(target_dir, exist_ok=True)
            elif not os.path.isdir(target_dir):
                 msg = f"Target path's directory is not a directory: {target_dir}"
                 logger.error(msg)
                 return False, msg

            with requests.get(full_url, headers=headers, stream=True, timeout=self.timeout * 4) as response:
                response.raise_for_status()

                with tempfile.NamedTemporaryFile(dir=target_dir, delete=False) as temp_file:
                    temp_file_path = temp_file.name
                    logger.debug(f"Downloading to temporary file: {temp_file_path}")

                    total_size = int(response.headers.get('content-length', 0))
                    downloaded = 0
                    last_log_time = time.time()
                    chunk_size = 8192

                    for chunk in response.iter_content(chunk_size=chunk_size):
                        if chunk:
                            temp_file.write(chunk)
                            downloaded += len(chunk)
                            current_time = time.time()
                            if current_time - last_log_time > 3:
                                if total_size > 0:
                                    progress = (downloaded / total_size) * 100
                                    logger.debug(f"Download progress: {progress:.1f}% ({downloaded}/{total_size} bytes)")
                                else:
                                    logger.debug(f"Download progress: {downloaded} bytes (total size unknown)")
                                last_log_time = current_time

                logger.debug(f"Moving temporary file {temp_file_path} to {save_path}")
                shutil.move(temp_file_path, save_path)
                temp_file_path = None

                final_size = os.path.getsize(save_path)
                logger.info(f"File downloaded successfully to {save_path} ({final_size} bytes)")
                return True, "File downloaded successfully."

        except requests.exceptions.Timeout:
             msg = f"Download timed out after {self.timeout * 4}s: {full_url}"
             logger.error(msg)
             return False, msg
        except requests.exceptions.ConnectionError as e:
             base_domain = urlparse(self.base_url).netloc
             msg = f"Connection error downloading file from {base_domain}: {e}"
             logger.error(msg)
             return False, msg
        except requests.exceptions.HTTPError as e:
             status_code = e.response.status_code
             msg = f"Server returned HTTP error {status_code} for download URL {full_url}"
             try:
                 error_details = e.response.text[:200]
                 msg += f". Response: {error_details}..."
             except Exception:
                 pass
             logger.error(msg)
             return False, f"Server error {status_code} during download."
        except requests.exceptions.RequestException as e:
            msg = f"Network error downloading file: {e}"
            logger.error(msg, exc_info=True)
            return False, msg
        except (IOError, OSError) as e:
            msg = f"File system error during download/save to {save_path}: {e}"
            logger.error(msg, exc_info=True)
            return False, msg
        except Exception as e:
            msg = f"Unexpected error downloading file: {e}"
            logger.error(msg, exc_info=True)
            return False, msg
        finally:
            if temp_file_path and os.path.exists(temp_file_path):
                logger.warning(f"Cleaning up temporary download file due to error: {temp_file_path}")
                try:
                    os.remove(temp_file_path)
                except OSError as e:
                    logger.error(f"Failed to remove temporary file {temp_file_path}: {e}")

    def report_error(self, error_data: Dict[str, Any]) -> bool:
        """
        Reports an error condition to the backend server. Requires prior authentication.

        :param error_data: Dictionary containing details about the error.
        :type error_data: Dict[str, Any]
        :return: True if the error report was sent successfully (received 2xx), False otherwise.
        :rtype: bool
        """
        endpoint = "/report-error"
        logger.info(f"Reporting error to server: Type '{error_data.get('error_type', 'Unknown')}'...")

        response_data, error_message = self._make_request('POST', endpoint, authenticated=True, json=error_data)

        if error_message is None:
             logger.info("Error report sent successfully to the server.")
             return True
        else:
             logger.error(f"Failed to report error: {error_message}")
             return False

    
    def _make_request(self, method: str, endpoint: str, authenticated: bool = False, **kwargs) -> Tuple[Optional[Union[Dict[str, Any], List[Any]]], Optional[str]]:
        """
        Internal helper method to make HTTP requests and handle common errors.

        :param method: HTTP method (e.g., 'POST', 'GET').
        :type method: str
        :param endpoint: API endpoint path (e.g., '/identify'). Should start with '/'.
        :type endpoint: str
        :param authenticated: If True, include authentication headers. Defaults to False.
        :type authenticated: bool
        :param kwargs: Additional arguments passed to requests.request (e.g., json, params, data, headers).
        :return: Tuple containing (response_data, error_message).
                 response_data is the parsed JSON response (dict or list) or None on error/no content.
                 error_message is a string describing the error, or None on success.
        :rtype: Tuple[Optional[Union[Dict[str, Any], List[Any]]], Optional[str]]
        """
        if not endpoint.startswith('/'):
             logger.warning(f"Endpoint '{endpoint}' does not start with '/'. Prepending '/'.")
             endpoint = '/' + endpoint

        full_url = urljoin(self.base_url, endpoint.lstrip('/'))
        request_timeout = kwargs.pop('timeout', self.timeout)
        headers = kwargs.pop('headers', {})

        headers.setdefault('User-Agent', 'ComputerManagementAgent/1.0')
        if 'json' in kwargs:
             headers.setdefault('Content-Type', 'application/json')

        if authenticated:
            if not self._agent_id or not self._agent_token:
                logger.error(f"Authentication required for {method} {full_url}, but auth info not set.")
                return None, "Agent authentication required but not configured."
            auth_headers = self._get_auth_headers()
            headers.update(auth_headers)

        try:
            logger.debug(f"Making HTTP request: {method} {full_url} (Timeout: {request_timeout}s)")
            if 'json' in kwargs:
                 pass 
            if 'params' in kwargs:
                 logger.debug(f"Params: {kwargs['params']}")

            response = requests.request(method, full_url, headers=headers, timeout=request_timeout, **kwargs)
            response.raise_for_status() 

            if response.status_code == 204: 
                logger.debug(f"Request successful (204 No Content): {method} {full_url}")
                return {}, None 
            try:
                
                response_json = response.json()
                logger.debug(f"Request successful ({response.status_code}): {method} {full_url}")
                return response_json, None
            except json.JSONDecodeError:
                logger.error(f"Failed to decode JSON response from {method} {full_url} (Status: {response.status_code}). Response text: {response.text[:200]}...")
                return None, "Invalid JSON response from server despite success status."

        except requests.exceptions.Timeout:
            logger.error(f"Request timed out after {request_timeout}s: {method} {full_url}")
            return None, f"Request timed out after {request_timeout} seconds."
        except requests.exceptions.ConnectionError as e:
            logger.error(f"Connection error: {method} {full_url} - {e}")
            base_domain = urlparse(self.base_url).netloc
            return None, f"Unable to connect to the server at {base_domain}."
        except requests.exceptions.HTTPError as e:
            status_code = e.response.status_code
            error_message = f"Server error {status_code}"
            error_details = None 
            try:
                
                error_data = e.response.json()
                error_details = error_data.get('message', json.dumps(error_data))
                error_message = f"{error_message}: {error_details}"
                logger.error(f"HTTP error {status_code}: {method} {full_url}. Server response: {error_details}")
                
                return error_data, error_message
            except json.JSONDecodeError:
                
                error_text = e.response.text[:200] 
                error_message = f"{error_message} (non-JSON response)."
                logger.error(f"HTTP error {status_code}: {method} {full_url}. Response: {error_text}...")
                
                return None, error_message
        except requests.exceptions.RequestException as e:
            
            logger.error(f"An unexpected request error occurred: {method} {full_url} - {e}", exc_info=True)
            return None, f"Unexpected network error: {e}"
        except Exception as e:
            
            logger.critical(f"An unexpected internal error occurred during HTTP request: {method} {full_url} - {e}", exc_info=True)
            return None, f"Unexpected internal error while processing request: {e}"

    def _get_auth_headers(self) -> Dict[str, str]:
        """
        Constructs authentication headers if auth info is set.

        :return: Dictionary containing authentication headers, or empty if not authenticated.
        :rtype: Dict[str, str]
        """
        headers = {}
        if self._agent_id and self._agent_token:
            headers['X-Agent-Id'] = self._agent_id
            headers['Authorization'] = f"Bearer {self._agent_token}"
        else:
            
            logger.warning("Attempting to get auth headers, but agent ID or token is not set.")
        return headers