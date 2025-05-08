"""
Computer Management System Agent - Source Package

This package contains the core logic, communication, monitoring, configuration,
and utility modules for the agent application.

Main components:
- Agent: The main agent class orchestrating all components
- AgentState: Enumeration of possible agent operational states
- CommandExecutor: Handles command processing and execution
- ConfigManager: Manages agent configuration
- StateManager: Manages agent persistent state
- WSClient: WebSocket client for real-time communication
- ServerConnector: Handles server API interactions
- SystemMonitor: Monitors system resources
"""


from .version import __version__, __app_name__


from .core import Agent
from .core import AgentState
from .core import CommandExecutor


from .config import ConfigManager
from .config import StateManager


from .communication import ServerConnector, WSClient, HttpClient


from .monitoring import SystemMonitor

__all__ = [
    
    '__version__',
    '__app_name__',
    
    
    'Agent',
    'AgentState',
    'CommandExecutor',
    
    
    'ConfigManager',
    'StateManager',
    
    
    'WSClient',
    'ServerConnector',
    'HttpClient',
    
    
    'SystemMonitor'
]
