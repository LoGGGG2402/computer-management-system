
"""
Defines the possible operational states of the agent.
"""
from enum import Enum, auto

class AgentState(Enum):
    """
    Enumeration of agent operational states.
    
    This enum defines all possible states the agent may be in during operation,
    which helps with tracking the agent's lifecycle and handling state-specific
    behaviors. The state machine approach allows for clear transitions between
    different operational modes and provides context for logging and debugging.
    
    States:
        STARTING: Initial state during agent startup and initialization
        IDLE: Normal operation state, monitoring and waiting for commands
        FORCE_RESTARTING: Agent is restarting due to external restart command via IPC
        UPDATING_STARTING: Beginning of the software update process
        UPDATING_DOWNLOADING: Currently downloading update package from server
        UPDATING_VERIFYING: Verifying integrity and authenticity of downloaded update
        UPDATING_EXTRACTING_UPDATER: Extracting update executable or script
        UPDATING_PREPARING_SHUTDOWN: Final preparations before shutting down for update
        SHUTTING_DOWN: Agent is in the process of shutting down normally
        STOPPED: Agent has been fully stopped and resources released
    """
    STARTING = auto()
    IDLE = auto()
    FORCE_RESTARTING = auto()
    UPDATING_STARTING = auto()
    UPDATING_DOWNLOADING = auto()
    UPDATING_VERIFYING = auto()
    UPDATING_EXTRACTING_UPDATER = auto()
    UPDATING_PREPARING_SHUTDOWN = auto()
    SHUTTING_DOWN = auto()
    STOPPED = auto()
