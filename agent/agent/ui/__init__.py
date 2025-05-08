"""
User interface utilities for the Computer Management System Agent.
"""
from agent.ui.ui_console import (
    display_error,
    display_info,
    display_success,
    prompt_for_room_config,
    prompt_for_mfa
)
from agent.config import StateManager
from typing import Dict, Any, Optional


def get_or_prompt_room_config(state_manager: StateManager) -> Optional[Dict[str, Any]]:
    """
    Gets room configuration from state manager or prompts user if none exists.
    
    :param state_manager: The state manager instance
    :type state_manager: StateManager
    :return: Room configuration or None if not found/canceled
    :rtype: Optional[Dict[str, Any]]
    """
    
    room_config = state_manager.get_room_config()
    if room_config:
        return room_config
    
    
    return prompt_for_room_config()


__all__ = [
    'display_error',
    'display_info',
    'display_success',
    'prompt_for_room_config',
    'prompt_for_mfa',
    'get_or_prompt_room_config'
]
