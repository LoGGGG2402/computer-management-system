# -*- coding: utf-8 -*-
"""
Logger setup module for the Computer Management System Agent.
Provides functions to configure logging based on external settings.
"""
import os
import logging
import logging.handlers
from typing import Optional, Dict

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

    console_handler = logging.StreamHandler()
    console_handler.setLevel(console_level)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

    if log_file_path:
        try:
            log_dir = os.path.dirname(log_file_path)
            if log_dir:
                os.makedirs(log_dir, exist_ok=True)

            file_handler = logging.handlers.RotatingFileHandler(
                log_file_path,
                maxBytes=max_bytes,
                backupCount=backup_count,
                encoding='utf-8'
            )
            file_handler.setLevel(file_level)
            file_handler.setFormatter(formatter)
            logger.addHandler(file_handler)

        except PermissionError as e:
             logger.error(f"Permission denied creating log file/directory {log_file_path}: {e}")

        except Exception as e:
            logger.error(f"Failed to set up file logging to {log_file_path}: {e}", exc_info=True)

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
