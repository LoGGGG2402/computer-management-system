"""
Console User Interface modules.
Handles user interactions via the console.
"""
# Expose key functions for easier import
from .ui_console import (
    prompt_for_mfa,
    display_registration_success,
    is_room_config_valid,
    prompt_room_config_ui,
    get_or_prompt_room_config,
    display_error,
    
)

__all__ = [
    'prompt_for_mfa',
    'display_registration_success',
    'is_room_config_valid',
    'prompt_room_config_ui',
    'get_or_prompt_room_config',
    'display_error'
]
