"""
Utility functions for the Computer Management System Agent.
"""
import datetime
import os
import hashlib
import json
import tarfile
import zipfile
import shutil
import traceback
import uuid
from typing import Dict, Any, Optional, List, Tuple

from agent.utils import get_logger

logger = get_logger(__name__)

def save_json(data: Any, file_path: str) -> bool:
    """
    Save data to a JSON file.
    
    :param data: Data to save
    :type data: Any
    :param file_path: Path to save the JSON file
    :type file_path: str
    :return: True if save succeeded, False otherwise
    :rtype: bool
    """
    if not file_path:
        logger.error("Cannot save JSON: File path is empty")
        return False
        
    try:
        directory = os.path.dirname(file_path)
        if directory and not os.path.exists(directory):
            os.makedirs(directory, exist_ok=True)
            
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
        logger.debug(f"Successfully saved JSON data to: {file_path}")
        return True
    except (IOError, OSError) as e:
        logger.error(f"Failed to write file {file_path}: {e}")
        return False
    except Exception as e:
        logger.error(f"Unexpected error writing {file_path}: {e}", exc_info=True)
        return False

def load_json(file_path: str) -> Any:
    """
    Load data from a JSON file.
    
    :param file_path: Path to the JSON file
    :type file_path: str
    :return: Loaded data or empty dict on error
    :rtype: Any
    """
    if not file_path or not os.path.exists(file_path):
        logger.debug(f"JSON file does not exist: {file_path}")
        return {}
        
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        logger.debug(f"Successfully loaded JSON data from: {file_path}")
        return data
    except json.JSONDecodeError as e:
        logger.error(f"Failed to parse JSON from {file_path}: {e}")
        return {}
    except (IOError, OSError) as e:
        logger.error(f"Failed to read file {file_path}: {e}")
        return {}
    except Exception as e:
        logger.error(f"Unexpected error reading {file_path}: {e}", exc_info=True)
        return {}

def calculate_sha256(filepath: str) -> Tuple[bool, str]:
    """
    Calculate SHA256 checksum for a file.
    
    :param filepath: Path to the file
    :type filepath: str
    :return: Tuple (success_flag, checksum_or_error_message)
    :rtype: Tuple[bool, str]
    """
    if not os.path.exists(filepath):
        return False, f"File not found: {filepath}"
        
    try:
        hash_sha256 = hashlib.sha256()
        
        with open(filepath, "rb") as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hash_sha256.update(chunk)
                
        return True, hash_sha256.hexdigest()
    except IOError as e:
        return False, f"I/O error calculating checksum: {e}"
    except Exception as e:
        return False, f"Unexpected error calculating checksum: {e}"

def extract_package(package_path: str, destination_path: str, extract_only_updater: bool = False) -> Tuple[bool, str]:
    """
    Extract an archive file to the destination path.
    
    :param package_path: Path to the archive file
    :type package_path: str
    :param destination_path: Path to extract to
    :type destination_path: str
    :param extract_only_updater: If True, only extract updater.exe or updater directory
    :type extract_only_updater: bool
    :return: Tuple (success, error_message)
    :rtype: Tuple[bool, str]
    """
    if not os.path.exists(package_path):
        error_msg = f"Archive file not found: {package_path}"
        logger.error(error_msg)
        return False, error_msg
    
    logger.info(f"Extracting package from '{package_path}' to '{destination_path}'")
    
    try:
        os.makedirs(destination_path, exist_ok=True)
        
        
        if package_path.endswith('.zip'):
            try:
                with zipfile.ZipFile(package_path, 'r') as zip_ref:
                    if extract_only_updater:
                        
                        updater_files = [f for f in zip_ref.namelist() if 
                                        'updater.exe' in f or 
                                        'updater/' in f or 
                                        'updater\\' in f]
                        
                        if not updater_files:
                            logger.info("No updater files found in the package")
                            return False, "No updater files found in the package"
                        
                        
                        for file in updater_files:
                            zip_ref.extract(file, destination_path)
                    else:
                        
                        zip_ref.extractall(destination_path)
                        
            except zipfile.BadZipFile as e:
                error_msg = f"Invalid ZIP file: {e}"
                logger.error(error_msg)
                return False, error_msg
                
        elif package_path.endswith(('.tar.gz', '.tgz')):
            try:
                with tarfile.open(package_path, 'r:gz') as tar_ref:
                    if extract_only_updater:
                        
                        updater_files = [f for f in tar_ref.getnames() if 
                                        'updater.exe' in f or 
                                        'updater/' in f or 
                                        'updater\\' in f]
                        
                        if not updater_files:
                            logger.warning("No updater files found in the package")
                            return False, "No updater files found in the package"
                        
                        
                        for file in updater_files:
                            tar_ref.extract(file, destination_path)
                    else:
                        
                        tar_ref.extractall(destination_path)
                        
            except tarfile.ReadError as e:
                error_msg = f"Invalid TAR file: {e}"
                logger.error(error_msg)
                return False, error_msg
                
        else:
            error_msg = f"Unsupported archive format: {package_path}"
            logger.error(error_msg)
            return False, error_msg
        
        logger.info("Package extraction completed successfully")
        return True, ""
        
    except Exception as e:
        error_msg = f"Failed during extraction: {e}\n{traceback.format_exc()}"
        logger.error(error_msg)
        return False, error_msg

def check_disk_space(path: str, required_bytes: int) -> Tuple[bool, str]:
    """
    Check if there is enough disk space at the specified path.
    
    :param path: Path to check for available disk space
    :type path: str
    :param required_bytes: Required space in bytes
    :type required_bytes: int
    :return: Tuple containing (success flag, error message if any)
    :rtype: Tuple[bool, str]
    """
    try:
        if not os.path.exists(path):
            os.makedirs(path, exist_ok=True)
            
        total, used, free = shutil.disk_usage(path)
        if free < required_bytes:
            error_msg = f"Not enough disk space: required {required_bytes/(1024*1024):.2f} MB, available {free/(1024*1024):.2f} MB"
            logger.error(error_msg)
            return False, error_msg
            
        return True, ""
    except PermissionError as e:
        error_msg = f"Permission denied checking disk space at {path}: {e}"
        logger.error(error_msg)
        return False, error_msg
    except (IOError, OSError) as e:
        error_msg = f"I/O error checking disk space at {path}: {e}"
        logger.error(error_msg, exc_info=True)
        return False, error_msg
    except Exception as e:
        error_msg = f"Unexpected error checking disk space at {path}: {e}"
        logger.error(error_msg, exc_info=True)
        return False, error_msg

def save_error_report(error_data: Dict[str, Any], error_dir: str) -> Tuple[bool, str]:
    """
    Save error report to a separate file for later submission.
    
    :param error_data: Error report data with standardized format
    :type error_data: Dict[str, Any]
    :param error_dir: Path to the error directory
    :type error_dir: str
    :return: Tuple (success_flag, file_path or error_message)
    :rtype: Tuple[bool, str]
    """
    try:
        
        if 'error_type' not in error_data:
            error_data['error_type'] = 'UNKNOWN_ERROR'
            logger.warning("Error report missing required 'error_type' field, defaulting to 'UNKNOWN_ERROR'")
            
        if 'error_message' not in error_data:
            error_data['error_message'] = 'No error message provided'
            logger.warning("Error report missing required 'error_message' field")
            
        if 'error_details' not in error_data:
            error_data['error_details'] = {}
        
        
        if 'timestamp' not in error_data:
            error_data['timestamp'] = datetime.datetime.now().isoformat()
            
        
        if not os.path.exists(error_dir):
            os.makedirs(error_dir, exist_ok=True)
            
        
        timestamp_str = datetime.datetime.fromisoformat(error_data['timestamp']).strftime('%Y%m%d_%H%M%S')
        error_id = str(uuid.uuid4())[:8]
        error_type = error_data['error_type'].lower().replace(' ', '_')
        filename = f"{timestamp_str}_{error_type}_{error_id}.json"
        
        
        file_path = os.path.join(error_dir, filename)
        
        
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(error_data, f, indent=2)
            
        logger.debug(f"Error report saved to {file_path}")
        return True, file_path
        
    except (IOError, OSError) as e:
        error_msg = f"Failed to write error report: {e}"
        logger.error(error_msg)
        return False, error_msg
    except Exception as e:
        error_msg = f"Unexpected error saving error report: {e}"
        logger.error(error_msg, exc_info=True)
        return False, error_msg

def read_buffered_error_reports(error_dir: str) -> List[Dict[str, Any]]:
    """
    Read all error reports from the error directory.
    
    :param error_dir: Path to the error directory
    :type error_dir: str
    :return: List of error reports with their file paths
    :rtype: List[Dict[str, Any]]
    """
    reports = []
    
    if not os.path.exists(error_dir):
        logger.debug(f"Error directory does not exist: {error_dir}")
        return reports
        
    try:
        
        error_files = [f for f in os.listdir(error_dir) if f.endswith('.json')]
        
        for filename in error_files:
            file_path = os.path.join(error_dir, filename)
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    report = json.load(f)
                    
                    report['_file_path'] = file_path
                    reports.append(report)
            except json.JSONDecodeError as e:
                logger.warning(f"Skipping invalid JSON file {file_path}: {e}")
            except (IOError, OSError) as e:
                logger.warning(f"Could not read error file {file_path}: {e}")
                        
        logger.debug(f"Loaded {len(reports)} error reports from {error_dir}")
        return reports
        
    except (IOError, OSError) as e:
        logger.error(f"Failed to read error reports from directory {error_dir}: {e}")
        return reports
    except Exception as e:
        logger.error(f"Unexpected error reading error reports: {e}", exc_info=True)
        return reports
