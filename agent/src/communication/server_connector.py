\
# -*- coding: utf-8 -*-
"""
Server Connector module for handling communication logic with the backend server.
Encapsulates authentication, hardware info submission, and status updates.
"""
import logging
from typing import Dict, Any, Optional, Tuple

from src.config.config_manager import ConfigManager
from src.config.state_manager import StateManager
from src.communication.http_client import HttpClient
from src.communication.ws_client import WSClient
from src.monitoring.system_monitor import SystemMonitor
from src.ui import ui_console # Import the ui_console module
from src.utils.logger import get_logger

logger = get_logger("agent.connector")

class ServerConnector:
    """
    Handles communication tasks like authentication, status updates, and hardware info.
    """

    def __init__(self,
                 config_manager: ConfigManager,
                 state_manager: StateManager,
                 http_client: HttpClient,
                 ws_client: WSClient,
                 system_monitor: SystemMonitor):
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
        self.device_id: Optional[str] = self.state_manager.get_device_id() # Get device ID early

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
        mfa_code = ui_console.prompt_for_mfa()
        if not mfa_code:
            logger.warning("MFA prompt cancelled by user.")
            return False

        api_call_success, response = self.http_client.verify_mfa(self.device_id, mfa_code)

        if not api_call_success:
            error_msg = response.get('message', 'Lỗi kết nối hoặc lỗi máy chủ không xác định')
            logger.error(f"MFA verification API call failed. Error: {error_msg}")
            return False

        if response.get('status') == 'success' and 'agentToken' in response:
            self.agent_token = response['agentToken']
            if self.state_manager.save_token(self.device_id, self.agent_token):
                logger.info("MFA verification successful. Agent registered, token saved.")
                ui_console.display_registration_success()
                return True
            else:
                # Even if saving fails, authentication succeeded for this session
                logger.critical("MFA successful but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                ui_console.display_registration_success() # Still display success
                return True
        else:
            mfa_error = response.get('message', 'Mã MFA không hợp lệ hoặc đã hết hạn.')
            logger.error(f"MFA verification failed: {mfa_error}")
            # Consider adding a UI message here if needed
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

        if not self.device_id: # Should not happen if called correctly
             logger.error("Cannot process identification: Device ID is missing.")
             return False

        if status == 'success':
            if 'agentToken' in response:
                self.agent_token = response['agentToken']
                if self.state_manager.save_token(self.device_id, self.agent_token):
                    logger.info("Agent registered/identified successfully, new token saved.")
                    return True
                else:
                    # Even if saving fails, authentication succeeded for this session
                    logger.critical("Agent identified but FAILED TO SAVE TOKEN LOCALLY! Agent may not work after restart.")
                    return True # Return True as authentication itself was successful
            else:
                # Server says already registered, but we didn't have a token locally
                logger.info(f"Agent already registered on server: {message}. Attempting to load existing token.")
                self.agent_token = self.state_manager.load_token(self.device_id)
                if self.agent_token:
                    logger.info("Successfully loaded existing token after server confirmation.")
                    return True
                else:
                    # This is a problematic state - server thinks we are registered, but we have no token.
                    # Maybe force renew? Or prompt user? For now, fail authentication.
                    logger.error("Server indicates agent is registered, but no local token found via StateManager. Authentication failed.")
                    # Consider attempting force_renew here automatically?
                    # Or prompt user?
                    return False

        elif status == 'mfa_required':
            return self._handle_mfa_verification()

        elif status == 'position_error':
            logger.error(f"Server rejected agent due to position conflict: {message}")
            pos_x = room_config.get('position', {}).get('x', 'N/A')
            pos_y = room_config.get('position', {}).get('y', 'N/A')
            room_name = room_config.get('room', 'N/A')
            # Consider adding a UI message here
            logger.error(f"Position conflict details: Room='{room_name}', X={pos_x}, Y={pos_y}")
            return False

        else: # Includes 'error' status or any other unknown status
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

        # --- Step 1: Core Authentication (Get Token) ---
        logger.debug("Step 1: Core Authentication (Token Acquisition)")
        core_auth_success = False
        self.agent_token = self.state_manager.load_token(self.device_id)
        if self.agent_token:
            # TODO: Optionally add a check here to verify the token with the server?
            # For now, assume loaded token is valid.
            logger.info("Found existing agent token. Assuming core authentication successful.")
            core_auth_success = True
        else:
            logger.info("No existing token found. Attempting identification with server...")
            try:
                api_call_success, response = self.http_client.identify_agent(
                    self.device_id,
                    room_config,
                    force_renew=False # Don't force renew initially
                )

                if not api_call_success:
                    logger.error(f"Agent identification API call failed.")
                    logger.info("--- Authentication Failed (API Call Error) ---")
                    return False

                # Process the response (handles success, MFA, errors)
                core_auth_success = self._process_identification_response(response, room_config)

                if not core_auth_success:
                    # Specific reason logged within _process_identification_response
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
        # --- Core Authentication End ---


        # --- Step 2: Send Hardware Info ---
        logger.debug("Step 2: Sending Initial Hardware Information")
        if not self.send_hardware_info():
            # Error logged within send_hardware_info
            logger.error("Failed to send initial hardware information.")
            logger.info("--- Authentication Failed (Hardware Info Send Failed) ---")
            # Consider if we should proceed without hardware info? For now, fail auth.
            return False
        logger.info("Initial hardware information sent successfully.")
        # --- Hardware Info End ---


        # --- Step 3: Connect WebSocket ---
        logger.debug("Step 3: Establishing WebSocket Connection")
        if not self.connect_websocket():
            # Error logged within connect_websocket
            logger.error("Failed to establish initial WebSocket connection.")
            logger.info("--- Authentication Failed (WebSocket Connect Failed) ---")
            # Consider if we should proceed without WS? For now, fail auth.
            return False
        logger.info("Initial WebSocket connection established and authenticated successfully.")
        # --- WebSocket End ---

        # If all steps succeeded
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
            # connect_and_authenticate logs errors
            return False

        # Wait for the server to confirm authentication via a message or internal state
        if not self.ws_client.wait_for_authentication(timeout=20.0): # Use a reasonable timeout
            logger.error("WebSocket connection and authentication attempt timed out or failed.")
            self.ws_client.disconnect() # Clean up connection attempt
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
                # Add other relevant stats if needed
            }
            logger.debug(f"Sending status update: {status_data}")
            if self.ws_client.send_status_update(status_data):
                return True
            else:
                # ws_client logs the failure
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
        if not self.agent_token:
            logger.error("Cannot send hardware info: Agent token is missing.")
            return False
        if not self.device_id:
            logger.error("Cannot send hardware info: Device ID is missing.")
            return False

        logger.info("Collecting and sending hardware information...")
        try:
            hardware_info = self.system_monitor.get_hardware_info()
            api_call_success, response = self.http_client.send_hardware_info(
                self.agent_token,
                self.device_id,
                hardware_info
            )
            if api_call_success:
                logger.info("Hardware information sent successfully.")
                return True
            else:
                # http_client logs the error details
                logger.error(f"Failed to send hardware information.")
                return False
        except Exception as e:
            logger.error(f"Error collecting or sending hardware info: {e}", exc_info=True)
            return False

    def get_agent_token(self) -> Optional[str]:
        """Returns the current agent token held by the connector."""
        return self.agent_token

