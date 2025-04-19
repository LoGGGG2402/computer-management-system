"""
Logger setup module for the Computer Management System Agent. (Simplified)
Provides functions to configure logging based on a directory path.
"""
import os
import sys
import logging
import logging.handlers
import datetime
from typing import Optional, Dict, Tuple

DEFAULT_LOG_FORMAT = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
DEFAULT_CONSOLE_LEVEL = logging.INFO
DEFAULT_FILE_LEVEL = logging.DEBUG
DEFAULT_MAX_BYTES = 10 * 1024 * 1024  # 10 MB
DEFAULT_BACKUP_COUNT = 5
LOG_FILENAME_PREFIX = "log_"
LOG_FILENAME_DATE_FORMAT = "%Y-%m-%d"
LOG_FILENAME_SUFFIX = ".log"

_loggers: Dict[str, logging.Logger] = {}


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
        test_file = os.path.join(directory_path, f".log_writetest_{os.getpid()}")
        with open(test_file, 'w') as f:
            f.write('test')
        os.remove(test_file)
        return True, f"Directory {directory_path} is writable"
    except PermissionError as e:
        return False, f"Permission denied writing to directory {directory_path}: {e}"
    except Exception as e:
        return False, f"Error checking write permission for {directory_path}: {e}"

def setup_logger(
    name: str = "agent",
    log_directory_path: Optional[str] = None
) -> Tuple[logging.Logger, bool]:
    """
    Sets up and configures a logger instance simply based on a directory.

    Log filename will be 'log_YYYY-MM-DD.log'.
    If log_directory_path is None or not writable, file logging is disabled.

    :param name: The name for the logger
    :type name: str
    :param log_directory_path: Path to the directory for storing log files.
                               If None or invalid, only console logging is enabled.
    :type log_directory_path: Optional[str]
    :return: Tuple containing the configured logger instance and a boolean
             indicating if file logging was successfully enabled.
    :rtype: Tuple[logging.Logger, bool]
    """
    global _loggers
    file_logging_enabled = False

    if name in _loggers:
        existing_logger = _loggers[name]
        has_file_handler = any(isinstance(h, logging.handlers.RotatingFileHandler) for h in existing_logger.handlers)
        return existing_logger, has_file_handler

    logger = logging.getLogger(name)
    lowest_level = min(DEFAULT_CONSOLE_LEVEL, DEFAULT_FILE_LEVEL)
    logger.setLevel(lowest_level)
    logger.propagate = False

    if logger.hasHandlers():
        logger.handlers.clear()

    formatter = logging.Formatter(DEFAULT_LOG_FORMAT)

    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(DEFAULT_CONSOLE_LEVEL)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

    actual_log_path = None
    if log_directory_path:
        is_writable, msg = _check_directory_writable(log_directory_path)
        if is_writable:
            try:
                current_date = datetime.date.today()
                today_str = current_date.strftime(LOG_FILENAME_DATE_FORMAT)
                filename = f"{LOG_FILENAME_PREFIX}{today_str}{LOG_FILENAME_SUFFIX}"
                actual_log_path = os.path.join(log_directory_path, filename)

                file_handler = logging.handlers.RotatingFileHandler(
                    actual_log_path,
                    maxBytes=DEFAULT_MAX_BYTES,
                    backupCount=DEFAULT_BACKUP_COUNT,
                    encoding='utf-8'
                )
                file_handler.setLevel(DEFAULT_FILE_LEVEL)
                file_handler.setFormatter(formatter)
                logger.addHandler(file_handler)

                file_logging_enabled = True

            except PermissionError as e:
                print(f"ERROR: Permission denied creating log file handler for {actual_log_path}: {e}", file=sys.stderr)
                logger.error(f"Permission denied creating log file handler for {actual_log_path}: {e}")
            except Exception as e:
                print(f"ERROR: Failed to set up file logging to {actual_log_path}: {e}", file=sys.stderr)
                logger.error(f"Failed to set up file logging to {actual_log_path}: {e}", exc_info=True)
        else:
            print(f"ERROR: Provided log directory '{log_directory_path}' is not writable or could not be created: {msg}. File logging disabled.", file=sys.stderr)
            logger.error(f"Provided log directory '{log_directory_path}' is not writable or could not be created: {msg}. File logging disabled.")

    _loggers[name] = logger
    return logger, file_logging_enabled


def get_logger(name: str = "agent") -> logging.Logger:
    """
    Get a configured logger instance by name.

    It's recommended to call `setup_logger` first to ensure proper configuration.

    :param name: The name of the logger to retrieve
    :type name: str
    :return: The logger instance
    :rtype: logging.Logger
    """
    if name not in _loggers:
        return logging.getLogger(name)
    return _loggers[name]