"""
System utilities for the Computer Management System Agent.
"""
from agent.system.windows_sync import (
    NamedMutexManager,
    create_admin_only_named_event,
    open_named_event,
    set_named_event,
    wait_for_named_event,
    close_handle
)
from agent.system.windows_utils import (
    is_running_as_system,
    get_executable_path,
    get_logged_in_users,
    get_windows_version
)
from agent.system.directory_utils import (
    setup_directory_structure,
    determine_storage_path,
    set_directory_permissions_for_system
)
from agent.system.agent_service_handler import (
    AgentService,
    AGENT_SERVICE_NAME,
    AGENT_DISPLAY_NAME,
    AGENT_DESCRIPTION,
    AGENT_MUTEX_NAME,
    AGENT_SHUTDOWN_EVENT_NAME,
    run_service
)

__all__ = [
    
    'NamedMutexManager',
    'create_admin_only_named_event',
    'open_named_event',
    'set_named_event',
    'wait_for_named_event',
    'close_handle',
    
    
    'is_running_as_system',
    'get_executable_path',
    'get_logged_in_users',
    'get_windows_version',
    
    
    'setup_directory_structure',
    'determine_storage_path',
    'set_directory_permissions_for_system',

    
    'AgentService',
    'AGENT_SERVICE_NAME',
    'AGENT_DISPLAY_NAME',
    'AGENT_DESCRIPTION',
    'AGENT_MUTEX_NAME',
    'AGENT_SHUTDOWN_EVENT_NAME',
    'run_service'
]