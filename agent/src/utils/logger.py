"""
Logger module for the Computer Management System Agent.
This module provides centralized logging functionality for the entire application.
"""
import os
import logging
import logging.handlers
from typing import Optional, Dict, Any

# Default log levels
DEFAULT_CONSOLE_LEVEL = logging.INFO
DEFAULT_FILE_LEVEL = logging.DEBUG

# Default log format
DEFAULT_LOG_FORMAT = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'

# Global logger dictionary to maintain references
loggers = {}

LOG_FILE = None
LOG_LEVEL = logging.DEBUG


def setup_logger(
    name: str = "agent", 
    log_file: Optional[str] = None,
    console_level: int = DEFAULT_CONSOLE_LEVEL,
    file_level: int = DEFAULT_FILE_LEVEL,
    log_format: str = DEFAULT_LOG_FORMAT
) -> logging.Logger:
    """
    Set up and configure a logger with the specified parameters.
    
    Args:
        name: Logger name
        log_file: Path to log file (None for no file logging)
        console_level: Logging level for console output
        file_level: Logging level for file output
        log_format: Format string for log messages
        
    Returns:
        Configured logger instance
    """
    # Check if logger already exists
    if name in loggers:
        return loggers[name]
    
    # Create logger
    logger = logging.getLogger(name)
    logger.setLevel(logging.DEBUG)  # Set to lowest level, handlers will filter
    
    # Create formatter
    formatter = logging.Formatter(log_format)
    
    # Create console handler
    console_handler = logging.StreamHandler()
    console_handler.setLevel(console_level)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)
    
    # Create file handler if log_file is specified
    if log_file:
        # Ensure log directory exists
        os.makedirs(os.path.dirname(log_file), exist_ok=True)
        
        # Create rotating file handler (10 MB max size, keep 5 backup files)
        file_handler = logging.handlers.RotatingFileHandler(
            log_file,
            maxBytes=10*1024*1024,  # 10 MB
            backupCount=5,
            encoding='utf-8'
        )
        file_handler.setLevel(file_level)
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)
    
    # Store logger in dictionary
    loggers[name] = logger
    
    return logger

def get_logger(name: str = "agent") -> logging.Logger:
    """
    Get an existing logger or create a new one with default settings.
    
    Args:
        name: Logger name
        
    Returns:
        Logger instance
    """

    global LOG_FILE
    if LOG_FILE is None:
        LOG_FILE = os.path.join(os.path.dirname(__file__), 'logs', 'agent.log')
    
    if name in loggers:
        return loggers[name]
    else:
        # Return a new logger with default settings (no file logging)
        return setup_logger(name=name, log_file=LOG_FILE,
                            console_level=LOG_LEVEL,
                            file_level=LOG_LEVEL)

