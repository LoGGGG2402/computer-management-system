"""
Utility functions for the Computer Management System Agent.
"""
import json
import logging
import os
import subprocess
from typing import Dict, Any, Optional, List, Tuple

logger = logging.getLogger(__name__)

def run_command(command: List[str]) -> Tuple[bool, str, str]:
    """
    Run a system command and return the result.
    
    Args:
        command (List[str]): Command as a list of arguments
        
    Returns:
        Tuple of (success, stdout, stderr)
    """
    try:
        process = subprocess.Popen(
            command,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            universal_newlines=True
        )
        stdout, stderr = process.communicate()
        success = process.returncode == 0
        
        if not success:
            logger.error(f"Command {' '.join(command)} failed with code {process.returncode}")
            logger.error(f"Error: {stderr}")
        
        return success, stdout, stderr
    except Exception as e:
        logger.exception(f"Error running command {' '.join(command)}")
        return False, "", str(e)

def save_json(data: Dict[str, Any], filepath: str) -> bool:
    """
    Save data to a JSON file.
    
    Args:
        data (Dict): Data to save
        filepath (str): Path to save the file
        
    Returns:
        bool: Success or failure
    """
    try:
        os.makedirs(os.path.dirname(filepath), exist_ok=True)
        
        with open(filepath, 'w') as f:
            json.dump(data, f, indent=4)
            
        return True
    except Exception as e:
        logger.error(f"Error saving JSON to {filepath}: {e}")
        return False

def load_json(filepath: str) -> Optional[Dict[str, Any]]:
    """
    Load data from a JSON file.
    
    Args:
        filepath (str): Path to the JSON file
        
    Returns:
        Dict or None if file doesn't exist or is invalid
    """
    if not os.path.exists(filepath):
        logger.debug(f"File {filepath} does not exist")
        return None
        
    try:
        with open(filepath, 'r') as f:
            data = json.load(f)
        return data
    except Exception as e:
        logger.error(f"Error loading JSON from {filepath}: {e}")
        return None

def format_bytes(bytes: int, decimals: int = 2) -> str:
    """
    Format bytes to a human-readable string.
    
    Args:
        bytes (int): Bytes to format
        decimals (int): Number of decimal places
        
    Returns:
        str: Formatted string
    """
    if bytes == 0:
        return "0B"
        
    size_names = ("B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB")
    i = 0
    
    while bytes >= 1024 and i < len(size_names) - 1:
        bytes /= 1024
        i += 1
        
    return f"{bytes:.{decimals}f} {size_names[i]}"