# -*- coding: utf-8 -*-
"""
General utility functions for the Computer Management System Agent.
"""
import json
import os
from typing import Dict, Any, Optional
from src.utils.logger import get_logger

# Use get_logger to get a properly configured logger
logger = get_logger("agent.utils.utils")

def save_json(data: Dict[str, Any], filepath: str) -> bool:
    """
    Saves dictionary data to a JSON file with UTF-8 encoding.
    Creates the directory if it doesn't exist.

    Args:
        data (Dict[str, Any]): The dictionary data to save.
        filepath (str): The full path to the output JSON file.

    Returns:
        bool: True if saving was successful, False otherwise.
    """
    try:
        # Ensure the directory exists
        dirpath = os.path.dirname(filepath)
        if dirpath: # Only create if there's a directory part
            os.makedirs(dirpath, exist_ok=True)

        # Write the JSON data
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4, ensure_ascii=False)
        logger.debug(f"Successfully saved JSON data to: {filepath}")
        return True
    except PermissionError as e:
         logger.error(f"Permission denied saving JSON to {filepath}: {e}")
         return False
    except (IOError, OSError, TypeError) as e:
        logger.error(f"Error saving JSON to {filepath}: {e}", exc_info=True)
        return False
    except Exception as e: # Catch any other unexpected errors
        logger.error(f"Unexpected error saving JSON to {filepath}: {e}", exc_info=True)
        return False

def load_json(filepath: str) -> Optional[Dict[str, Any]]:
    """
    Loads dictionary data from a JSON file with UTF-8 encoding.

    Args:
        filepath (str): The full path to the JSON file.

    Returns:
        Optional[Dict[str, Any]]: The loaded dictionary, or None if the file
                                   doesn't exist, is empty, has permission issues,
                                   or contains invalid JSON.
    """
    if not os.path.exists(filepath):
        # This is not an error, just the file doesn't exist yet.
        logger.debug(f"JSON file not found (this is okay if it's the first run): {filepath}")
        # Return {} instead of None when file not found, so the caller can easily add data.
        return {}
    # Check if it's actually a file
    if not os.path.isfile(filepath):
        logger.error(f"Path exists but is not a file: {filepath}")
        return None

    # Check file size *after* confirming it exists and is a file
    try:
        if os.path.getsize(filepath) == 0:
            logger.warning(f"JSON file is empty: {filepath}")
            # Return an empty dict for an empty file.
            return {}
    except OSError as e:
        logger.error(f"Could not get size of file {filepath}: {e}")
        return None # Can't proceed if we can't check size

    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            data = json.load(f)
        if not isinstance(data, dict):
             logger.warning(f"JSON file does not contain a dictionary object: {filepath}")
             return None # Or raise TypeError depending on desired strictness
        logger.debug(f"Successfully loaded JSON data from: {filepath}")
        return data
    except json.JSONDecodeError as e:
        # Log the specific decoding error
        logger.error(f"Error decoding JSON from {filepath}: {e}", exc_info=False) # exc_info=False usually sufficient
        return None
    except PermissionError as e:
         # Log permission error specifically
         logger.error(f"Permission denied reading JSON file {filepath}: {e}")
         return None
    except (IOError, OSError) as e:
        # Log other I/O errors
        logger.error(f"I/O error reading JSON file {filepath}: {e}", exc_info=True)
        return None
    except Exception as e: # Catch any other unexpected errors
        logger.error(f"Unexpected error loading JSON from {filepath}: {e}", exc_info=True)
        return None
