"""
Logger setup module for the Computer Management System Agent.
Provides functions to configure logging based on external settings.
"""
import os
import sys
import logging
import logging.handlers
import tempfile
from typing import Optional, Dict, Tuple

DEFAULT_CONSOLE_LEVEL_NAME = 'INFO'
DEFAULT_FILE_LEVEL_NAME = 'DEBUG'
DEFAULT_LOG_FORMAT = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'

_loggers: Dict[str, logging.Logger] = {}


def _get_log_level(level_name: str, default_level: int = logging.INFO) -> int:
    """
    Convert log level string to logging level constant.

    :param level_name: Name of the log level (e.g., 'DEBUG')
    :type level_name: str
    :param default_level: Default level to use if level_name is invalid
    :type default_level: int
    :return: The corresponding logging level constant
    :rtype: int
    """
    level_name_upper = str(level_name).upper()
    level = logging.getLevelName(level_name_upper)
    if isinstance(level, int):
        return level
    else:
        logging.warning(f"Invalid log level name '{level_name}'. Using default level {logging.getLevelName(default_level)}.")
        return default_level


def _check_directory_writable(directory_path: str) -> Tuple[bool, str]:
    """
    Check if a directory exists and is writable by the current process.
    
    :param directory_path: Path to the directory to check
    :type directory_path: str
    :return: Tuple (is_writable, message)
    :rtype: Tuple[bool, str]
    """
    if not directory_path:
        return False, "Directory path is empty"
        
    if not os.path.exists(directory_path):
        try:
            os.makedirs(directory_path, exist_ok=True)
            if not os.path.exists(directory_path):
                return False, f"Failed to create directory {directory_path}"
        except PermissionError as e:
            return False, f"Permission denied creating directory {directory_path}: {e}"
        except Exception as e:
            return False, f"Error creating directory {directory_path}: {e}"
    
    if not os.path.isdir(directory_path):
        return False, f"{directory_path} exists but is not a directory"
    
    try:
        # Try to create a temporary file to verify write permissions
        test_file = os.path.join(directory_path, f".log_writetest_{os.getpid()}")
        with open(test_file, 'w') as f:
            f.write('test')
        os.remove(test_file)
        return True, f"Directory {directory_path} is writable"
    except PermissionError as e:
        return False, f"Permission denied writing to directory {directory_path}: {e}"
    except Exception as e:
        return False, f"Error checking write permission for {directory_path}: {e}"


def _get_fallback_log_directory() -> str:
    """
    Get a fallback directory for logs that should be writable on most platforms.
    
    :return: Path to a fallback directory for logging
    :rtype: str
    """
    try:
        # First try user's temp directory
        temp_dir = tempfile.gettempdir()
        app_temp_dir = os.path.join(temp_dir, "CMSAgent", "logs")
        
        # Create the app's directory in temp if it doesn't exist
        if not os.path.exists(app_temp_dir):
            os.makedirs(app_temp_dir, exist_ok=True)
            
        return app_temp_dir
    except Exception:
        # Last resort - use current directory
        if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
            # We're running in a PyInstaller bundle
            base_dir = os.path.dirname(sys.executable)
        else:
            # We're running in a normal Python environment
            base_dir = os.getcwd()
            
        fallback_dir = os.path.join(base_dir, "logs")
        if not os.path.exists(fallback_dir):
            try:
                os.makedirs(fallback_dir, exist_ok=True)
            except Exception:
                # If we can't create a logs directory, just use base directory
                fallback_dir = base_dir
                
        return fallback_dir


def get_file_logging_status() -> Dict[str, any]:
    """
    Get information about the current file logging status.
    Useful for initialization diagnostics.
    
    :return: Dictionary with information about file logging configuration
    :rtype: Dict[str, any]
    """
    result = {
        "file_logging_enabled": False,
        "log_files": [],
        "errors": []
    }
    
    for logger_name, logger in _loggers.items():
        for handler in logger.handlers:
            if isinstance(handler, logging.handlers.RotatingFileHandler):
                result["file_logging_enabled"] = True
                try:
                    log_path = handler.baseFilename
                    if os.path.exists(log_path):
                        size = os.path.getsize(log_path)
                        writable = os.access(log_path, os.W_OK)
                    else:
                        size = 0
                        log_dir = os.path.dirname(log_path)
                        writable = os.access(log_dir, os.W_OK) if log_dir else False
                    
                    result["log_files"].append({
                        "path": log_path,
                        "logger": logger_name,
                        "level": logging.getLevelName(handler.level),
                        "exists": os.path.exists(log_path),
                        "size_bytes": size,
                        "writable": writable,
                        "max_bytes": handler.maxBytes,
                        "backup_count": handler.backupCount
                    })
                except Exception as e:
                    result["errors"].append(f"Error checking log file for {logger_name}: {str(e)}")
    
    return result


def setup_logger(
    name: str = "agent",
    log_format: str = DEFAULT_LOG_FORMAT,
    console_level_name: str = DEFAULT_CONSOLE_LEVEL_NAME,
    file_level_name: str = DEFAULT_FILE_LEVEL_NAME,
    log_file_path: Optional[str] = None,
    max_bytes: int = 10 * 1024 * 1024,
    backup_count: int = 5
) -> logging.Logger:
    """
    Sets up and configures a logger instance.

    If a logger with the same name already exists, it returns the existing one
    to avoid adding duplicate handlers.

    :param name: The name for the logger
    :type name: str
    :param log_format: The format string for log messages
    :type log_format: str
    :param console_level_name: Logging level for console output
    :type console_level_name: str
    :param file_level_name: Logging level for file output
    :type file_level_name: str
    :param log_file_path: Path to the log file. If None, file logging is disabled
    :type log_file_path: Optional[str]
    :param max_bytes: Maximum size of the log file before rotation
    :type max_bytes: int
    :param backup_count: Number of backup log files to keep
    :type backup_count: int
    :return: The configured logger instance
    :rtype: logging.Logger
    """
    if name in _loggers:
        return _loggers[name]

    logger = logging.getLogger(name)
    console_level = _get_log_level(console_level_name, logging.INFO)
    file_level = _get_log_level(file_level_name, logging.DEBUG)
    lowest_level = min(console_level, file_level) if log_file_path else console_level
    logger.setLevel(lowest_level)

    logger.propagate = False

    if logger.hasHandlers():
        logger.handlers.clear()

    formatter = logging.Formatter(log_format)

    # Always add console handler
    console_handler = logging.StreamHandler()
    console_handler.setLevel(console_level)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

    # Add file handler if log_file_path is provided
    file_logging_success = False
    if log_file_path:
        log_dir = os.path.dirname(log_file_path)
        
        if not log_dir:
            logger.warning("No directory provided in log_file_path. Using filename in current directory.")
            log_file_path = os.path.join(os.getcwd(), os.path.basename(log_file_path))
            log_dir = os.getcwd()
        
        # Check if the directory is writable
        is_writable, msg = _check_directory_writable(log_dir)
        if not is_writable:
            original_path = log_file_path
            fallback_dir = _get_fallback_log_directory()
            log_file_path = os.path.join(fallback_dir, os.path.basename(log_file_path))
            logger.warning(f"Cannot use specified log directory: {msg}. Falling back to {log_file_path}")
            
            # Check fallback directory
            is_writable, msg = _check_directory_writable(fallback_dir)
            if not is_writable:
                logger.error(f"Cannot use fallback log directory either: {msg}. File logging will be disabled.")
                log_file_path = None
        
        try:
            if log_file_path:
                file_handler = logging.handlers.RotatingFileHandler(
                    log_file_path,
                    maxBytes=max_bytes,
                    backupCount=backup_count,
                    encoding='utf-8'
                )
                file_handler.setLevel(file_level)
                file_handler.setFormatter(formatter)
                logger.addHandler(file_handler)
                
                logger.info(f"File logging enabled to: {log_file_path}")
                file_logging_success = True

        except PermissionError as e:
            logger.error(f"Permission denied creating log file {log_file_path}: {e}")
        except Exception as e:
            logger.error(f"Failed to set up file logging to {log_file_path}: {e}", exc_info=True)
    
    if log_file_path and not file_logging_success:
        logger.warning("File logging requested but could not be set up. Logging to console only.")

    _loggers[name] = logger
    return logger


def get_logger(name: str = "agent") -> logging.Logger:
    """
    Get a configured logger instance by name.
    
    If a logger with this name has not been set up yet, this will
    automatically set it up with default settings.
    
    :param name: The name of the logger to retrieve
    :type name: str
    :return: The configured logger instance
    :rtype: logging.Logger
    """
    if name not in _loggers:
        return setup_logger(name)
    
    return _loggers[name]
