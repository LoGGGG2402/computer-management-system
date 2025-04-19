"""
System utilities for operating system interactions.
"""
from .lock_manager import LockManager
from .windows_utils import (
    is_running_as_admin,
    register_autostart,
    unregister_autostart
)

__all__ = [
    'LockManager',
    'is_running_as_admin',
    'register_autostart',
    'unregister_autostart'
]