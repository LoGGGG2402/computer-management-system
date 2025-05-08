"""
Base command handler class providing common functionality for all command handlers.
"""
from typing import Dict, Any, TYPE_CHECKING
from abc import ABC, abstractmethod

from agent.utils import get_logger

if TYPE_CHECKING:
    from agent.config import ConfigManager

logger = get_logger(__name__)

class BaseCommandHandler(ABC):
    """
    Abstract base class for all command handlers.

    Command handlers are responsible for executing a specific type of command
    and populating a result dictionary. All specific command handlers (e.g.,
    ConsoleCommandHandler, SystemCommandHandler) should inherit from this class.
    """

    def __init__(self, config: 'ConfigManager'):
        """
        Initialize the base command handler.
        
        :param config: The configuration manager instance, providing access to
                      shared configuration settings
        :type config: ConfigManager
        :raises ValueError: If the config parameter is None
        """
        if not config:
             raise ValueError("ConfigManager instance is required for BaseCommandHandler.")
        self.config = config
        logger.debug(f"{self.__class__.__name__} initialized.")

    @abstractmethod
    def execute_command(self, command: str, command_id: str,
                        result: Dict[str, Any], **kwargs) -> None:
        """
        Execute a command and update the result dictionary **in-place**.

        Implementations of this method MUST modify the passed `result`
        dictionary and set the following keys:

        :param command: The command string or identifier to execute
        :type command: str
        :param command_id: The unique ID associated with this specific command
                          execution instance
        :type command_id: str
        :param result: The dictionary object to populate with execution results.
                      It is passed in by the CommandExecutor, typically
                      initialized like: ``{'type': command_type, 'success': False, 'result': None}``.
                      This dictionary MUST be modified directly
        :type result: Dict[str, Any]
        :param kwargs: Optional additional keyword arguments that might be
                      needed for specific handler implementations
        :raises: Any exceptions raised during execution *should ideally* be caught
                within the implementation and reflected in the `result` dictionary
                (e.g., setting `success=False` and providing error details in
                `result['result']`). If an exception is not caught here, it will
                be caught by the CommandExecutor's worker loop, which will
                generate a generic handler error message
        :returns: None
        """
        pass
