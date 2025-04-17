# -*- coding: utf-8 -*-
"""
Windows specific utility functions for the agent.
"""
import ctypes
import os
import logging

logger = logging.getLogger(__name__)

def is_running_as_admin() -> bool:
    """
    Checks if the current process is running with administrative privileges.

    Returns:
        bool: True if running as admin, False otherwise.
    """
    try:
        is_admin = (os.getuid() == 0) # Linux/macOS check (will fail on Windows)
    except AttributeError:
        # Windows check
        try:
            is_admin = ctypes.windll.shell32.IsUserAnAdmin() != 0
        except AttributeError:
            logger.warning("Could not determine admin status using ctypes. Assuming not admin.")
            is_admin = False
        except Exception as e:
             logger.error(f"Unexpected error checking admin status: {e}", exc_info=True)
             is_admin = False # Fail safe
    logger.debug(f"Admin check result: {is_admin}")
    return is_admin

# Placeholder for future functions from maintain_agent.md
# def register_autostart(exe_path, is_admin): pass
# def unregister_autostart(is_admin): pass
# def set_directory_acls(path): pass
# def manage_ipc_secret(action='get', context='USER'): pass
# def get_user_sid_string(): pass
