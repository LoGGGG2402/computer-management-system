"""
Inter-Process Communication modules for agent coordination.
"""
from .named_pipe_client import send_force_command
from .named_pipe_server import NamedPipeIPCServer, WINDOWS_PIPE_SUPPORT

__all__ = [
    'send_force_command',
    'NamedPipeIPCServer',
    'WINDOWS_PIPE_SUPPORT'
]
