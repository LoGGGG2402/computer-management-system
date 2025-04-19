"""
System utilities for operating system interactions.
"""
from .lock_manager import LockManager
from .windows_utils import (
    is_running_as_admin,
    register_autostart,
    unregister_autostart
)
from .directory_utils import (
    determine_storage_path,
    setup_directory_structure
)

__all__ = [
    'LockManager',
    'is_running_as_admin',
    'register_autostart',
    'unregister_autostart',
    'determine_storage_path',
    'setup_directory_structure'
]