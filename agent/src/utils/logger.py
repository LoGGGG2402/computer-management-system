# -*- coding: utf-8 -*-
"""
Logger setup module for the Computer Management System Agent.
Provides functions to configure logging based on external settings.
"""
import os
import sys
import logging
import logging.handlers
from typing import Optional, Dict

# Default log levels and format (can be overridden by config)
DEFAULT_CONSOLE_LEVEL_NAME = 'INFO'
DEFAULT_FILE_LEVEL_NAME = 'DEBUG'
DEFAULT_LOG_FORMAT = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'

# Global logger dictionary to prevent duplicate handlers if called multiple times for the same name
_loggers: Dict[str, logging.Logger] = {}

def _get_log_level(level_name: str, default_level: int = logging.INFO) -> int:
    """Convert log level string (e.g., 'DEBUG') to logging level constant."""
    level_name_upper = str(level_name).upper() # Ensure it's a string and uppercase
    level = logging.getLevelName(level_name_upper)
    if isinstance(level, int):
        return level
    else:
        # Log a warning if the level name is invalid and return default
        logging.warning(f"Invalid log level name '{level_name}'. Using default level {logging.getLevelName(default_level)}.")
        return default_level


def setup_logger(
    name: str = "agent",
    log_format: str = DEFAULT_LOG_FORMAT,
    console_level_name: str = DEFAULT_CONSOLE_LEVEL_NAME,
    file_level_name: str = DEFAULT_FILE_LEVEL_NAME,
    log_file_path: Optional[str] = None,
    max_bytes: int = 10 * 1024 * 1024, # 10 MB
    backup_count: int = 5
) -> logging.Logger:
    """
    Sets up and configures a logger instance.

    If a logger with the same name already exists, it returns the existing one
    to avoid adding duplicate handlers.

    Args:
        name (str): The name for the logger (e.g., 'agent', 'agent.core').
        log_format (str): The format string for log messages.
        console_level_name (str): Logging level for console output (e.g., 'INFO', 'DEBUG').
        file_level_name (str): Logging level for file output (e.g., 'DEBUG').
        log_file_path (Optional[str]): Path to the log file. If None, file logging is disabled.
        max_bytes (int): Maximum size of the log file before rotation.
        backup_count (int): Number of backup log files to keep.

    Returns:
        logging.Logger: The configured logger instance.
    """
    # Check if logger already configured
    if name in _loggers:
        # Optionally check if configuration needs update (e.g., level change)
        # For simplicity, we return the existing one here.
        # If dynamic level changes are needed, this logic would need adjustment.
        return _loggers[name]

    # Create logger
    logger = logging.getLogger(name)
    # Set logger level to the lowest level among handlers to capture all messages
    console_level = _get_log_level(console_level_name, logging.INFO)
    file_level = _get_log_level(file_level_name, logging.DEBUG)
    lowest_level = min(console_level, file_level) if log_file_path else console_level
    logger.setLevel(lowest_level)

    # Prevent messages from propagating to the root logger if it has handlers
    # logger.propagate = False # Only set if root logger configuration is complex

    # Clear existing handlers for this logger name, in case of re-configuration attempts
    if logger.hasHandlers():
        # print(f"Logger '{name}' already had handlers. Clearing them before re-configuring.")
        logger.handlers.clear()


    # Create formatter
    formatter = logging.Formatter(log_format)

    # --- Console Handler ---
    console_handler = logging.StreamHandler()
    console_handler.setLevel(console_level)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)
    # print(f"Logger '{name}' Console Handler Level: {logging.getLevelName(console_level)}") # Debug print

    # --- File Handler (Optional) ---
    if log_file_path:
        try:
            # Ensure log directory exists
            log_dir = os.path.dirname(log_file_path)
            if log_dir: # Create directory only if path includes one
                os.makedirs(log_dir, exist_ok=True)

            # Create rotating file handler
            file_handler = logging.handlers.RotatingFileHandler(
                log_file_path,
                maxBytes=max_bytes,
                backupCount=backup_count,
                encoding='utf-8'
            )
            file_handler.setLevel(file_level)
            file_handler.setFormatter(formatter)
            logger.addHandler(file_handler)
            # print(f"Logging to file: {log_file_path} (Level: {logging.getLevelName(file_level)})") # Inform user
        except PermissionError as e:
             # Log permission error specifically
             logger.error(f"Permission denied creating log file/directory {log_file_path}: {e}")
             print(f"Lỗi quyền truy cập: Không thể tạo file log tại {log_file_path}. File logging bị vô hiệu hóa.", file=sys.stderr)
        except Exception as e:
            # Log other errors to console if file logging setup fails
            logger.error(f"Failed to set up file logging to {log_file_path}: {e}", exc_info=True)
            print(f"Lỗi: Không thể thiết lập file log tại {log_file_path}. File logging bị vô hiệu hóa.", file=sys.stderr)


    # Store the configured logger
    _loggers[name] = logger
    # print(f"Logger '{name}' configured. Overall Level: {logging.getLevelName(logger.level)}") # Inform user
    return logger

def get_logger(name: str = "agent") -> logging.Logger:
    """
    Get a configured logger instance by name.
    
    If a logger with this name has not been set up through setup_logger() yet,
    this will automatically set it up with default settings.
    
    Args:
        name (str): The name of the logger to retrieve.
        
    Returns:
        logging.Logger: The configured logger instance.
    """
    # If logger not already configured, set it up with defaults
    if name not in _loggers:
        return setup_logger(name)
    
    # Return the existing logger
    return _loggers[name]
