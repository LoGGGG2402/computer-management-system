"""
General utility functions for the Computer Management System Agent.
"""
import json
import os
from typing import Dict, Any, Optional
from agent.utils import get_logger

logger = get_logger("agent.utils.utils")

def save_json(data: Dict[str, Any], filepath: str) -> bool:
    """
    Saves dictionary data to a JSON file with UTF-8 encoding.
    Creates the directory if it doesn't exist.

    :param data: The dictionary data to save
    :type data: Dict[str, Any]
    :param filepath: The full path to the output JSON file
    :type filepath: str
    :return: True if saving was successful, False otherwise
    :rtype: bool
    """
    try:
        dirpath = os.path.dirname(filepath)
        if dirpath:
            os.makedirs(dirpath, exist_ok=True)

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
    except Exception as e:
        logger.error(f"Unexpected error saving JSON to {filepath}: {e}", exc_info=True)
        return False

def load_json(filepath: str) -> Optional[Dict[str, Any]]:
    """
    Loads dictionary data from a JSON file with UTF-8 encoding.

    :param filepath: The full path to the JSON file
    :type filepath: str
    :return: The loaded dictionary, or None if the file doesn't exist, 
             is empty, has permission issues, or contains invalid JSON
    :rtype: Optional[Dict[str, Any]]
    """
    if not os.path.exists(filepath):
        logger.debug(f"JSON file not found (this is okay if it's the first run): {filepath}")
        return {}
    
    if not os.path.isfile(filepath):
        logger.error(f"Path exists but is not a file: {filepath}")
        return None

    try:
        if os.path.getsize(filepath) == 0:
            logger.warning(f"JSON file is empty: {filepath}")
            return {}
    except OSError as e:
        logger.error(f"Could not get size of file {filepath}: {e}")
        return None

    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            data = json.load(f)
        if not isinstance(data, dict):
             logger.warning(f"JSON file does not contain a dictionary object: {filepath}")
             return None
        logger.debug(f"Successfully loaded JSON data from: {filepath}")
        return data
    except json.JSONDecodeError as e:
        logger.error(f"Error decoding JSON from {filepath}: {e}", exc_info=False)
        return None
    except PermissionError as e:
         logger.error(f"Permission denied reading JSON file {filepath}: {e}")
         return None
    except (IOError, OSError) as e:
        logger.error(f"I/O error reading JSON file {filepath}: {e}", exc_info=True)
        return None
    except Exception as e:
        logger.error(f"Unexpected error loading JSON from {filepath}: {e}", exc_info=True)
        return None
