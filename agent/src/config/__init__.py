"""
Configuration module for the Computer Management System Agent.
This module provides centralized configuration management.
"""
from src.config.config_manager import config_manager

# Export the global config manager instance
__all__ = ['config_manager']