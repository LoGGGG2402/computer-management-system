"""
Windows-specific synchronization utilities for the Computer Management System Agent.

Provides mechanisms for ensuring a single agent instance is running, and for
inter-process communication using Windows synchronization primitives like Mutexes and Events.
"""
import win32event
import win32api
import win32security
import win32con
import pywintypes
from typing import Optional
from agent.utils import get_logger

logger = get_logger(__name__)


class NamedMutexManager:
    """
    Manages a Windows Named Mutex for single instance control.
    
    Uses Windows-specific mutex APIs for safer and more reliable process isolation
    than file-based locking, especially for Windows Service scenarios.
    """

    def __init__(self, mutex_name: str):
        """
        Initializes the NamedMutexManager.
        
        :param mutex_name: Name of the mutex, should include 'Global\\' for system-wide visibility
        :type mutex_name: str
        """
        self.mutex_name = mutex_name
        self._mutex_handle = None
        logger.debug(f"NamedMutexManager initialized with mutex name: {mutex_name}")

    def acquire(self) -> bool:
        """
        Attempts to acquire the named mutex.
        
        :return: True if the mutex was acquired successfully, False otherwise
        :rtype: bool
        """
        if self._mutex_handle is not None:
            logger.warning("acquire() called when mutex is already held")
            return True

        try:
            
            
            security_attributes = None  
            initial_owner = False
            self._mutex_handle = win32event.CreateMutex(
                security_attributes,
                initial_owner,
                self.mutex_name
            )
            
            if self._mutex_handle is None:
                logger.error(f"Failed to create mutex '{self.mutex_name}': Null handle returned")
                return False
                
            
            wait_result = win32event.WaitForSingleObject(self._mutex_handle, 0)
            
            if wait_result == win32event.WAIT_OBJECT_0:
                logger.info(f"Successfully acquired mutex '{self.mutex_name}'")
                return True
            elif wait_result == win32event.WAIT_ABANDONED:
                
                
                logger.warning(f"Acquired previously abandoned mutex '{self.mutex_name}'")
                return True
            elif wait_result == win32event.WAIT_TIMEOUT:
                
                logger.critical(f"Could not acquire mutex '{self.mutex_name}': Already owned by another process")
                self._close_handle()
                return False
            else:
                logger.critical(f"Unexpected result ({wait_result}) from WaitForSingleObject for mutex '{self.mutex_name}'")
                self._close_handle()
                return False
                
        except pywintypes.error as e:
            logger.critical(f"Windows API error acquiring mutex '{self.mutex_name}': {e}")
            self._close_handle()
            return False
        except Exception as e:
            logger.critical(f"Unexpected error acquiring mutex '{self.mutex_name}': {e}", exc_info=True)
            self._close_handle()
            return False

    def release(self):
        """
        Releases the named mutex if it was previously acquired.
        Safe to call even if the mutex wasn't acquired or already released.
        """
        if self._mutex_handle is None:
            logger.debug("release() called but no mutex handle exists")
            return
            
        try:
            
            win32event.ReleaseMutex(self._mutex_handle)
            logger.info(f"Released mutex '{self.mutex_name}'")
        except pywintypes.error as e:
            
            
            logger.error(f"Error releasing mutex '{self.mutex_name}': {e}")
        except Exception as e:
            logger.error(f"Unexpected error releasing mutex '{self.mutex_name}': {e}", exc_info=True)
        finally:
            self._close_handle()
            
    def _close_handle(self):
        """Closes the mutex handle safely."""
        if self._mutex_handle is not None:
            try:
                win32api.CloseHandle(self._mutex_handle)
                logger.debug(f"Closed handle for mutex '{self.mutex_name}'")
            except Exception as e:
                logger.error(f"Error closing handle for mutex '{self.mutex_name}': {e}")
            finally:
                self._mutex_handle = None


def create_admin_only_named_event(event_name: str) -> Optional[int]:
    """
    Creates a named event with security settings restricting modification rights to Administrators and SYSTEM.
    
    :param event_name: Name of the event, should include 'Global\\' for system-wide visibility
    :type event_name: str
    :return: Handle to the event if successful, None otherwise
    :rtype: Optional[int]
    """
    try:
        
        sd = win32security.SECURITY_DESCRIPTOR()
        
        
        dacl = win32security.ACL()
        
        
        adminSID = win32security.CreateWellKnownSid(win32security.WinBuiltinAdministratorsSid)
        dacl.AddAccessAllowedAce(
            win32security.ACL_REVISION,
            win32con.GENERIC_READ | win32con.SYNCHRONIZE | win32con.EVENT_MODIFY_STATE,
            adminSID
        )
        
        
        systemSID = win32security.CreateWellKnownSid(win32security.WinLocalSystemSid)
        dacl.AddAccessAllowedAce(
            win32security.ACL_REVISION,
            win32con.GENERIC_ALL,
            systemSID
        )
        
        
        sd.SetSecurityDescriptorDacl(1, dacl, 0)
        
        
        sa = win32security.SECURITY_ATTRIBUTES()
        sa.SECURITY_DESCRIPTOR = sd
        
        
        event_handle = win32event.CreateEvent(
            sa,                     
            True,                   
            False,                  
            event_name              
        )
        
        if event_handle:
            logger.info(f"Successfully created named event '{event_name}' with admin-only access")
            return event_handle
        else:
            logger.error(f"Failed to create named event '{event_name}': Null handle returned")
            return None
            
    except pywintypes.error as e:
        logger.error(f"Windows API error creating named event '{event_name}': {e}")
        return None
    except Exception as e:
        logger.error(f"Unexpected error creating named event '{event_name}': {e}", exc_info=True)
        return None


def open_named_event(event_name: str, desired_access: int = win32event.SYNCHRONIZE) -> Optional[int]:
    """
    Opens an existing named event.
    
    :param event_name: Name of the event to open
    :type event_name: str
    :param desired_access: Access rights to request, defaults to SYNCHRONIZE
    :type desired_access: int
    :return: Handle to the event if successful, None otherwise
    :rtype: Optional[int]
    """
    try:
        event_handle = win32event.OpenEvent(
            desired_access,  
            False,           
            event_name       
        )
        
        if event_handle:
            logger.debug(f"Successfully opened named event '{event_name}'")
            return event_handle
        else:
            logger.error(f"Failed to open named event '{event_name}': Null handle returned")
            return None
            
    except pywintypes.error as e:
        logger.error(f"Windows API error opening named event '{event_name}': {e}")
        return None
    except Exception as e:
        logger.error(f"Unexpected error opening named event '{event_name}': {e}", exc_info=True)
        return None


def set_named_event(event_handle: int) -> bool:
    """
    Sets (signals) a named event.
    
    :param event_handle: Handle to the event
    :type event_handle: int
    :return: True if successful, False otherwise
    :rtype: bool
    """
    try:
        result = win32event.SetEvent(event_handle)
        if result:
            logger.debug(f"Successfully set event with handle {event_handle}")
            return True
        else:
            logger.error(f"Failed to set event with handle {event_handle}")
            return False
            
    except pywintypes.error as e:
        logger.error(f"Windows API error setting event with handle {event_handle}: {e}")
        return False
    except Exception as e:
        logger.error(f"Unexpected error setting event with handle {event_handle}: {e}", exc_info=True)
        return False


def wait_for_named_event(event_handle: int, timeout_ms: int = win32event.INFINITE) -> bool:
    """
    Waits for a named event to be signaled.
    
    :param event_handle: Handle to the event
    :type event_handle: int
    :param timeout_ms: Timeout in milliseconds, INFINITE by default
    :type timeout_ms: int
    :return: True if the event was signaled, False if timed out or error
    :rtype: bool
    """
    try:
        wait_result = win32event.WaitForSingleObject(event_handle, timeout_ms)
        
        if wait_result == win32event.WAIT_OBJECT_0:
            logger.debug(f"Event with handle {event_handle} was signaled")
            return True
        elif wait_result == win32event.WAIT_TIMEOUT:
            logger.debug(f"Timed out waiting for event with handle {event_handle}")
            return False
        else:
            logger.error(f"Unexpected result ({wait_result}) from WaitForSingleObject for event with handle {event_handle}")
            return False
            
    except pywintypes.error as e:
        logger.error(f"Windows API error waiting for event with handle {event_handle}: {e}")
        return False
    except Exception as e:
        logger.error(f"Unexpected error waiting for event with handle {event_handle}: {e}", exc_info=True)
        return False


def close_handle(handle: int) -> bool:
    """
    Closes a Windows handle safely.
    
    :param handle: The handle to close
    :type handle: int
    :return: True if successful, False otherwise
    :rtype: bool
    """
    try:
        win32api.CloseHandle(handle)
        logger.debug(f"Closed handle {handle}")
        return True
    except pywintypes.error as e:
        logger.error(f"Windows API error closing handle {handle}: {e}")
        return False
    except Exception as e:
        logger.error(f"Unexpected error closing handle {handle}: {e}", exc_info=True)
        return False