# -*- coding: utf-8 -*-
"""
Defines the possible operational states of the agent.
"""
from enum import Enum, auto

class AgentState(Enum):
    """Enumeration of agent operational states."""
    STARTING = auto()         # Initial state during startup and initialization.
    IDLE = auto()             # Agent is running normally, connected (or attempting reconnect), waiting for commands.
    FORCE_RESTARTING = auto() # State when handling --force restart via IPC
    UPDATING_STARTING = auto() # Update process initiated
    UPDATING_DOWNLOADING = auto() # Downloading update package
    UPDATING_VERIFYING = auto() # Verifying package integrity/signature
    UPDATING_EXTRACTING_UPDATER = auto() # Extracting updater tool
    UPDATING_PREPARING_SHUTDOWN = auto() # Preparing to hand off to updater
    SHUTTING_DOWN = auto()    # Agent is in the process of stopping gracefully.
    STOPPED = auto()          # Agent has fully stopped (final state, not typically set internally).
