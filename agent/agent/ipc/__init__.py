"""
IPC components for inter-process communication.
"""
from agent.ipc.named_pipe_client import send_force_command
from agent.ipc.named_pipe_server import NamedPipeIPCServer

__all__ = [
    'send_force_command',
    'NamedPipeIPCServer'
]
