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

# Version information
from .version import __version__, __app_name__

# Core components
from .core import Agent
from .core import AgentState
from .core import CommandExecutor

# Configuration components
from .config import ConfigManager
from .config import StateManager

# Communication components
from .communication import ServerConnector, WSClient, HttpClient

# System monitoring
from .monitoring import SystemMonitor

__all__ = [
    # Version
    '__version__',
    '__app_name__',
    
    # Core
    'Agent',
    'AgentState',
    'CommandExecutor',
    
    # Configuration
    'ConfigManager',
    'StateManager',
    
    # Communication
    'WSClient',
    'ServerConnector',
    'HttpClient',
    
    # Monitoring
    'SystemMonitor'
]
