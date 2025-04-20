"""
Server connector module for handling authentication and communication with the server.
"""
from typing import Dict, Any, Optional, TYPE_CHECKING, Callable, Tuple
import threading
import datetime

if TYPE_CHECKING:
    from ..config import ConfigManager, StateManager
    from . import HttpClient, WSClient
    from ..monitoring import SystemMonitor

from ..ui import prompt_for_mfa, display_registration_success
from ..utils import get_logger, utils

logger = get_logger(__name__)

class ServerConnector:
    """
    Handles communication tasks like authentication, status updates, and hardware info.
    """

    def __init__(self,
                 config_manager: 'ConfigManager',
                 state_manager: 'StateManager',
                 http_client: 'HttpClient',
                 ws_client: 'WSClient',
                 system_monitor: 'SystemMonitor'):
        """
        Initialize the ServerConnector.

        :param config_manager: Configuration manager instance
        :param state_manager: State manager for persistence
        :param http_client: HTTP client for API calls
        :param ws_client: WebSocket client for real-time communication
        :param system_monitor: System monitoring component
        """
        self.config = config_manager
        self.state_manager = state_manager
        self.http_client = http_client
        self.ws_client = ws_client
        self.system_monitor = system_monitor
        self.agent_token: Optional[str] = None
        self.device_id: Optional[str] = self.state_manager.get_device_id() 

        logger.info("ServerConnector initialized.")

    def _handle_mfa_verification(self) -> bool:
        """
        Handles the MFA prompt and verification process.

        :return: True if MFA verification successful, False otherwise
        :rtype: bool
        """
        if not self.device_id:
            logger.error("Cannot handle MFA: Device ID is not set.")
            return False

        logger.info("MFA required for registration.")
        mfa_code = prompt_for_mfa()
        if not mfa_code:
            logger.warning("MFA prompt cancelled by user.")
            return False

        api_call_success, response = self.http_client.verify_mfa(self.device_id, mfa_code)

        if not api_call_success:
            error_msg = response.get('message', 'Connection error or unknown server error')
            logger.error(f"MFA verification API call failed. Error: {error_msg}")
            return False

        if response.get('status') == 'success' and 'agentToken' in response:
            self.agent_token = response['agentToken']
            if self.state_manager.save_token(self.device_id, self.agent_token):
                logger.info("MFA verification successful. Agent registered, token saved.")
                display_registration_success()
                return True
            else:
                
                logger.critical("MFA successful but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                display_registration_success() 
                return True
        else:
            mfa_error = response.get('message', 'Invalid MFA code or it has expired.')
            logger.error(f"MFA verification failed: {mfa_error}")
            
            return False

    def _process_identification_response(self, response: Dict[str, Any], room_config: Dict[str, Any]) -> bool:
        """
        Processes the logical response from a successful /identify API call.

        :param response: Response data from API
        :type response: Dict[str, Any]
        :param room_config: The room configuration used for identification
        :type room_config: Dict[str, Any]
        :return: True if identification successful, False otherwise
        :rtype: bool
        """
        status = response.get('status')
        message = response.get('message', 'No message from server.')

        if not self.device_id: 
             logger.error("Cannot process identification: Device ID is missing.")
             return False

        if status == 'success':
            if 'agentToken' in response:
                self.agent_token = response['agentToken']
                if self.state_manager.save_token(self.device_id, self.agent_token):
                    logger.info("Agent registered/identified successfully, new token saved.")
                    return True
                else:
                    
                    logger.critical("Agent identified but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                    return True 
            else:
                
                logger.info(f"Agent already registered on server: {message}. Attempting to load existing token.")
                self.agent_token = self.state_manager.load_token(self.device_id)
                if self.agent_token:
                    logger.info("Successfully loaded existing token after server confirmation.")
                    return True
                else:
                    
                    
                    logger.error("Server indicates agent is registered, but no local token found via StateManager. Authentication failed.")
                    
                    
                    return False

        elif status == 'mfa_required':
            return self._handle_mfa_verification()

        elif status == 'position_error':
            logger.error(f"Server rejected agent due to position conflict: {message}")
            pos_x = room_config.get('position', {}).get('x', 'N/A')
            pos_y = room_config.get('position', {}).get('y', 'N/A')
            room_name = room_config.get('room', 'N/A')
            
            logger.error(f"Position conflict details: Room='{room_name}', X={pos_x}, Y={pos_y}")
            return False

        else: 
            logger.error(f"Failed to identify/register agent. Server status: '{status}', Message: '{message}'")
            return False

    def authenticate_agent(self, room_config: Dict[str, Any]) -> bool:
        """
        Authenticates the agent with the backend server. Loads or obtains a token,
        sends initial hardware info, and establishes the WebSocket connection.

        :param room_config: The room configuration for this agent.
        :type room_config: Dict[str, Any]
        :return: True if authentication, hardware info sending, and WebSocket connection are all successful, False otherwise
        :rtype: bool
        """
        logger.info("--- Starting Full Authentication Process (via ServerConnector) ---")
        if not self.device_id:
            logger.critical("Authentication cannot proceed: Device ID is missing.")
            return False

        
        logger.debug("Step 1: Core Authentication (Token Acquisition)")
        core_auth_success = False
        self.agent_token = self.state_manager.load_token(self.device_id)
        if self.agent_token:
            
            
            logger.info("Found existing agent token. Assuming core authentication successful.")
            core_auth_success = True
        else:
            logger.info("No existing token found. Attempting identification with server...")
            try:
                api_call_success, response = self.http_client.identify_agent(
                    self.device_id,
                    room_config,
                    force_renew=False 
                )

                if not api_call_success:
                    logger.error(f"Agent identification API call failed.")
                    logger.info("--- Authentication Failed (API Call Error) ---")
                    return False

                
                core_auth_success = self._process_identification_response(response, room_config)

                if not core_auth_success:
                    
                    logger.info("--- Authentication Failed (Server Logic/MFA/Error) ---")
                    return False
                else:
                     logger.info("Core authentication successful (new token obtained/verified).")

            except Exception as e:
                logger.critical(f"An unexpected error occurred during the core authentication process: {e}", exc_info=True)
                logger.info("--- Authentication Failed (Unexpected Error during Core Auth) ---")
                return False

        if not core_auth_success or not self.agent_token:
             logger.error("Core authentication step failed or did not result in a token.")
             logger.info("--- Authentication Failed (No Token) ---")
             return False
        
        self.http_client.set_auth_info(self.device_id, self.agent_token)
        
        logger.debug("Step 2: Sending Initial Hardware Information")
        if not self.send_hardware_info():
            
            logger.error("Failed to send initial hardware information.")
            logger.info("--- Authentication Failed (Hardware Info Send Failed) ---")
            
            return False
        logger.info("Initial hardware information sent successfully.")
        
        logger.debug("Step 3: Establishing WebSocket Connection")
        if not self.connect_websocket():
            
            logger.error("Failed to establish initial WebSocket connection.")
            logger.info("--- Authentication Failed (WebSocket Connect Failed) ---")
            
            return False
        logger.info("Initial WebSocket connection established and authenticated successfully.")
        

        
        logger.info("--- Full Authentication Successful (Token + Hardware Info + WebSocket) ---")
        return True

    def connect_websocket(self) -> bool:
        """
        Establishes and waits for authenticated WebSocket connection.
        Requires self.agent_token to be set.

        :return: True if connection successful, False otherwise
        :rtype: bool
        """
        if not self.agent_token:
            logger.error("Cannot connect WebSocket: Agent token is missing.")
            return False
        if not self.device_id:
            logger.error("Cannot connect WebSocket: Device ID is missing.")
            return False

        logger.info("Attempting to connect and authenticate WebSocket...")
        if not self.ws_client.connect_and_authenticate(self.device_id, self.agent_token):
            
            return False

        
        if not self.ws_client.wait_for_authentication(timeout=20.0): 
            logger.error("WebSocket connection and authentication attempt timed out or failed.")
            self.ws_client.disconnect() 
            return False

        logger.info("WebSocket connection established and authenticated.")
        return True

    def send_status_update(self) -> bool:
        """
        Fetches system stats and sends them via WebSocket.

        :return: True if status sent successfully, False otherwise
        :rtype: bool
        """
        if not self.ws_client.connected:
            logger.warning("Cannot send status update: WebSocket not connected/authenticated.")
            return False

        try:
            stats = self.system_monitor.get_usage_stats()
            status_data = {
                "cpuUsage": stats.get("cpu", 0.0),
                "ramUsage": stats.get("ram", 0.0),
                "diskUsage": stats.get("disk", 0.0),
                
            }
            logger.debug(f"Sending status update: {status_data}")
            if self.ws_client.send_status_update(status_data):
                return True
            else:
                
                return False
        except Exception as e:
            logger.error(f"Error during status update collection or sending: {e}", exc_info=True)
            return False

    def send_hardware_info(self) -> bool:
        """
        Collects and sends detailed hardware information via HTTP.
        Requires self.agent_token to be set.

        :return: True if hardware info sent successfully, False otherwise
        :rtype: bool
        """
        logger.info("Collecting and sending hardware information...")
        try:
            hardware_info = self.system_monitor.get_hardware_info()
            api_call_success, response = self.http_client.send_hardware_info(
                hardware_info
            )
            if api_call_success:
                logger.info("Hardware information sent successfully.")
                return True
            else:
                
                logger.error(f"Failed to send hardware information.")
                return False
        except Exception as e:
            logger.error(f"Error collecting or sending hardware info: {e}", exc_info=True)
            return False

    def get_agent_token(self) -> Optional[str]:
        """Returns the current agent token held by the connector."""
        return self.agent_token
        
    def report_error_to_backend(self, error_type: str, message: str, details: Dict[str, Any] = None, stack_trace: str = None):
        """
        Reports an error to the backend server.
        
        :param error_type: Type of error from standardized list
        :type error_type: str
        :param message: Error message
        :type message: str
        :param details: Additional error details
        :type details: Dict[str, Any], optional
        :param stack_trace: Stack trace if available
        :type stack_trace: str, optional
        """
        from ..version import __version__ as current_version
        
        if details is None:
            details = {}
            
        if stack_trace is None:
            import traceback
            stack_trace = ''.join(traceback.format_stack()[:-1])
            
        # Create standardized error data
        error_details = details.copy()
        error_details['stack_trace'] = stack_trace
        error_details['agent_version'] = current_version
            
        error_data = {
            "error_type": error_type,
            "error_message": message,
            "error_details": error_details,
            "timestamp": datetime.datetime.now().isoformat()
        }
        
        logger.error(f"Reporting error to backend: {error_type} - {message}")
        
        # Try to report error directly
        if not self.http_client.report_error(error_data):
            # If direct reporting fails, save to file for later reporting
            from ..utils.utils import save_error_report
            error_dir = self.state_manager.get_error_directory()
            if error_dir:
                success, file_path = save_error_report(error_data, error_dir)
                if success:
                    logger.info(f"Error saved to file for later reporting: {file_path}")
                else:
                    logger.error(f"Failed to save error report to file: {file_path}")
            else:
                logger.error("Could not save error report: Error directory path unavailable")

    def process_error_reports(self, error_dir: str, max_retries: int = 3) -> Tuple[int, int]:
        """
        Process and report all error files from the error directory.
        Reports and deletes each file individually after successful reporting.
        
        :param error_dir: Path to the error directory
        :type error_dir: str
        :param max_retries: Maximum number of retries for each error report
        :type max_retries: int
        :return: Tuple (successfully_reported_count, total_error_files_count)
        :rtype: Tuple[int, int]
        """
        from ..utils.utils import read_buffered_error_reports
        
        # Read all buffered error reports
        error_reports = read_buffered_error_reports(error_dir)
        if not error_reports:
            logger.info("No buffered error reports found to send")
            return 0, 0
            
        logger.info(f"Found {len(error_reports)} buffered error reports to process")
        
        successfully_reported = 0
        
        for report in error_reports:
            # Get the file path and remove it from the report data before sending
            file_path = report.pop('_file_path', None)
            if not file_path:
                logger.warning("Error report missing file path, skipping")
                continue
                
            # Try to send the error report with retries
            success = False
            for attempt in range(max_retries):
                if self.http_client.report_error(report):
                    # Successfully reported, delete the file
                    try:
                        import os
                        if os.path.exists(file_path):
                            os.remove(file_path)
                            logger.debug(f"Successfully deleted error file after reporting: {file_path}")
                            successfully_reported += 1
                            success = True
                            break
                        else:
                            logger.warning(f"Error file not found (already removed?): {file_path}")
                            success = True  # Consider it successful if file is already gone
                            break
                    except Exception as e:
                        logger.error(f"Failed to delete error file {file_path} after successful reporting: {e}")
                        # Consider it a partial success even if file deletion fails
                        successfully_reported += 1
                        success = True
                        break
                else:
                    logger.warning(f"Failed to report error (attempt {attempt + 1}/{max_retries}): {file_path}")
                    import time
                    time.sleep(2)  # Wait before retry
            
            if not success:
                logger.error(f"Failed to report error after {max_retries} attempts: {file_path}")
                
        logger.info(f"Completed error reporting: {successfully_reported}/{len(error_reports)} reports sent successfully")
        return successfully_reported, len(error_reports)

    def process_error_reports_thread(self, error_dir: str, completion_callback=None):
        """
        Thread function to process error reports. Designed to be run in a separate thread.
        
        :param error_dir: Path to the error directory
        :type error_dir: str
        :param completion_callback: Callback function to be called when processing is complete
        :type completion_callback: callable, optional
        """
        from ..utils.utils import read_buffered_error_reports
        
        try:
            logger.info(f"Checking for stored error reports in {error_dir}")
            
            # Report buffered errors
            successfully_reported, total_reports = self.process_error_reports(error_dir)
            
            if successfully_reported > 0:
                logger.info(f"Successfully reported {successfully_reported} error files")
            
            # Check if there are any remaining error files
            remaining_reports = read_buffered_error_reports(error_dir)
            
            if remaining_reports:
                logger.warning(f"{len(remaining_reports)} error reports remain unprocessed")
            else:
                logger.info("All error reports have been processed successfully")
                
        except Exception as e:
            logger.error(f"Error in error reporting thread: {e}", exc_info=True)
        finally:
            logger.info("Error reporting thread finished")
            if completion_callback:
                completion_callback()