"""
System utilities for the Computer Management System Agent.
"""
from agent.system.lock_manager import LockManager
from agent.system.windows_utils import (
    is_running_as_admin,
    register_autostart,
    unregister_autostart
)
from agent.system.directory_utils import (
    setup_directory_structure,
    determine_storage_path
)

__all__ = [
    'LockManager',
    'is_running_as_admin',
    'register_autostart',
    'unregister_autostart',
    'setup_directory_structure',
    'determine_storage_path'
]