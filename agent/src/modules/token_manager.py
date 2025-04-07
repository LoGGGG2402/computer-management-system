"""
Token manager module for the Computer Management System Agent.
This module provides functionality for securely storing and retrieving agent tokens.
"""
import os
import json
import logging
from typing import Optional

# Optional keyring support for more secure token storage
try:
    import keyring
    KEYRING_AVAILABLE = True
except ImportError:
    KEYRING_AVAILABLE = False

logger = logging.getLogger(__name__)

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
    # Try to use keyring if available
    if KEYRING_AVAILABLE:
        try:
            keyring.set_password(SERVICE_NAME, agent_id, token)
            logger.info(f"Token saved securely using keyring for agent: {agent_id}")
            
            # Create an empty file to indicate keyring is used
            token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
            os.makedirs(os.path.dirname(token_file_path), exist_ok=True)
            
            with open(token_file_path, 'w') as f:
                json.dump({"use_keyring": True, "agent_id": agent_id}, f)
                
            return True
        except Exception as e:
            logger.warning(f"Failed to save token using keyring: {e}")
            # Fall back to file-based storage
    
    # File-based storage as fallback
    try:
        token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
        os.makedirs(os.path.dirname(token_file_path), exist_ok=True)
        
        with open(token_file_path, 'w') as f:
            json.dump({
                "use_keyring": False,
                "agent_id": agent_id,
                "token": token
            }, f)
            
        logger.info(f"Token saved to file for agent: {agent_id}")
        return True
    except Exception as e:
        logger.error(f"Failed to save token to file: {e}")
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
    token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
    
    # Check if token file exists
    if not os.path.exists(token_file_path):
        logger.debug(f"No token file found at: {token_file_path}")
        return None
    
    try:
        # Read the token file
        with open(token_file_path, 'r') as f:
            data = json.load(f)
            
        # If keyring was used
        if data.get("use_keyring", False) and KEYRING_AVAILABLE:
            stored_agent_id = data.get("agent_id")
            
            # Verify the agent ID matches
            if stored_agent_id != agent_id:
                logger.warning(f"Agent ID mismatch: {stored_agent_id} vs {agent_id}")
                return None
                
            # Retrieve from keyring
            token = keyring.get_password(SERVICE_NAME, agent_id)
            if token:
                logger.info(f"Token loaded from keyring for agent: {agent_id}")
                return token
            else:
                logger.warning(f"No token found in keyring for agent: {agent_id}")
                return None
        
        # If file-based storage was used
        elif not data.get("use_keyring", False):
            stored_agent_id = data.get("agent_id")
            stored_token = data.get("token")
            
            # Verify the agent ID matches
            if stored_agent_id != agent_id:
                logger.warning(f"Agent ID mismatch: {stored_agent_id} vs {agent_id}")
                return None
                
            if stored_token:
                logger.info(f"Token loaded from file for agent: {agent_id}")
                return stored_token
            else:
                logger.warning("Token file exists but contains no token")
                return None
        
        else:
            logger.warning("Token file format is invalid")
            return None
            
    except Exception as e:
        logger.error(f"Error loading token: {e}")
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
    token_file_path = os.path.join(storage_path, TOKEN_FILENAME)
    
    # If file doesn't exist, nothing to delete
    if not os.path.exists(token_file_path):
        return True
    
    try:
        # Check if keyring was used
        with open(token_file_path, 'r') as f:
            data = json.load(f)
            
        # Delete from keyring if it was used
        if data.get("use_keyring", False) and KEYRING_AVAILABLE:
            try:
                keyring.delete_password(SERVICE_NAME, agent_id)
                logger.info(f"Token deleted from keyring for agent: {agent_id}")
            except Exception as ke:
                logger.warning(f"Error deleting token from keyring: {ke}")
        
        # Delete the token file
        os.remove(token_file_path)
        logger.info(f"Token file deleted for agent: {agent_id}")
        return True
        
    except Exception as e:
        logger.error(f"Error deleting token: {e}")
        return False