"""
Token manager module for the Computer Management System Agent.
This module provides functionality for securely storing and retrieving agent tokens.
"""
import os
import json
from typing import Optional

# Optional keyring support for more secure token storage
try:
    import keyring
    KEYRING_AVAILABLE = True
except ImportError:
    KEYRING_AVAILABLE = False

from src.utils.logger import get_logger

# Get logger for this module
logger = get_logger(__name__)

# Constants
SERVICE_NAME = "ComputerManagementSystem"
TOKEN_FILENAME = "agent_token.json"

def save_token(agent_id: str, token: str, storage_path: str) -> bool:
    """
    Save the agent token securely.
    
    Args:
        agent_id (str): The unique agent ID
        token (str): The token to save
        storage_path (str): Path to store the token file
        
    Returns:
        bool: True if saved successfully, False otherwise
    """
    try:
        if KEYRING_AVAILABLE:
            keyring.set_password(SERVICE_NAME, agent_id, token)
            logger.info(f"Token saved securely for agent_id: {agent_id}")
        else:
            token_data = {agent_id: token}
            token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
            with open(token_file_path, 'w') as token_file:
                json.dump(token_data, token_file)
            logger.info(f"Token saved to file for agent_id: {agent_id}")
        return True
    except Exception as e:
        logger.error(f"Failed to save token for agent_id: {agent_id}. Error: {e}")
        return False

def load_token(agent_id: str, storage_path: str) -> Optional[str]:
    """
    Load the agent token.
    
    Args:
        agent_id (str): The unique agent ID
        storage_path (str): Path where the token file is stored
        
    Returns:
        Optional[str]: The token if found, None otherwise
    """
    try:
        if KEYRING_AVAILABLE:
            token = keyring.get_password(SERVICE_NAME, agent_id)
            delete_token(agent_id, storage_path)  # Remove from keyring after loading
            if token:
                logger.info(f"Token loaded securely for agent_id: {agent_id}")
                return token
        else:
            token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
            if os.path.exists(token_file_path):
                print(f"Token file found at: {token_file_path}")
                with open(token_file_path, 'r') as token_file:
                    token_data = json.load(token_file)
                    token = token_data.get(agent_id)
                    if token:
                        logger.info(f"Token loaded from file for agent_id: {agent_id}")
                        return token
        logger.warning(f"Token not found for agent_id: {agent_id}")
        return None
    except Exception as e:
        logger.error(f"Failed to load token for agent_id: {agent_id}. Error: {e}")
        return None

def delete_token(agent_id: str, storage_path: str) -> bool:
    """
    Delete the stored agent token.
    
    Args:
        agent_id (str): The unique agent ID
        storage_path (str): Path where the token file is stored
        
    Returns:
        bool: True if deleted successfully, False otherwise
    """
    try:
        if KEYRING_AVAILABLE:
            keyring.delete_password(SERVICE_NAME, agent_id)
            logger.info(f"Token deleted securely for agent_id: {agent_id}")
        else:
            token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
            if os.path.exists(token_file_path):
                with open(token_file_path, 'r') as token_file:
                    token_data = json.load(token_file)
                if agent_id in token_data:
                    del token_data[agent_id]
                    with open(token_file_path, 'w') as token_file:
                        json.dump(token_data, token_file)
                    logger.info(f"Token deleted from file for agent_id: {agent_id}")
        return True
    except Exception as e:
        logger.error(f"Failed to delete token for agent_id: {agent_id}. Error: {e}")
        return False