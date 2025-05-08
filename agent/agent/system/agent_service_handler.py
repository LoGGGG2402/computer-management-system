"""
Windows Service Handler for the CMS Agent.

This module defines the Windows service class for the agent, handling service
lifecycle events like start, stop, and the main service execution loop.
It integrates with the core Agent logic, manages a named mutex for single
instance control, and sets up logging, including to the Windows Event Log.
"""
import os
import sys
import servicemanager  
import win32event
import win32service
import win32serviceutil
import subprocess
import ctypes



current_script_path = os.path.abspath(__file__)
system_dir = os.path.dirname(current_script_path) 
agent_dir_for_service = os.path.dirname(system_dir) 
project_root_for_service = os.path.dirname(agent_dir_for_service) 

if project_root_for_service not in sys.path:
    sys.path.insert(0, project_root_for_service)
if agent_dir_for_service not in sys.path:
    sys.path.insert(0, agent_dir_for_service)


from agent.config import ConfigManager, StateManager
from agent.communication import HttpClient, ServerConnector, WSClient
from agent.core import CommandExecutor, Agent
from agent.system.windows_sync import NamedMutexManager, close_handle, create_admin_only_named_event
from agent.system.directory_utils import determine_storage_path, setup_directory_structure
from agent.system.windows_utils import get_executable_path
from agent.utils.logger import setup_logger, get_logger


AGENT_SERVICE_NAME = "CMSAgentService"
AGENT_SERVICE_DISPLAY_NAME = "Computer Management System Agent"
AGENT_SERVICE_DESCRIPTION = "Monitors the computer and communicates with the CMS server."
AGENT_MUTEX_NAME = "Global\\CMSAgentSingletonMutex"
AGENT_SHUTDOWN_EVENT_NAME = "Global\\CMSAgentSystemShutdownRequestEvent" 
AGENT_CONFIG_FILENAME = "agent_config.json"
PROGRAMDATA_LOG_DIR_NAME = "logs"


logger = get_logger("agent_service")

def get_service_config_file_path() -> str:
    """
    Determines the path to agent_config.json when running as a service.
    Assumes config is in a 'config' subdirectory relative to the service executable's directory.
    e.g., C:\Program Files\CMSAgent\agent\config\agent_config.json
    if agent.exe is in C:\Program Files\CMSAgent\agent\agent.exe
    """
    exe_dir = os.path.dirname(get_executable_path()) 
    config_path = os.path.join(exe_dir, "config", AGENT_CONFIG_FILENAME)
    return config_path

class AgentService(win32serviceutil.ServiceFramework):
    _svc_name_ = AGENT_SERVICE_NAME
    _svc_display_name_ = AGENT_SERVICE_DISPLAY_NAME
    _svc_description_ = AGENT_SERVICE_DESCRIPTION

    def __init__(self, args):
        win32serviceutil.ServiceFramework.__init__(self, args)
        self.hWaitStop = win32event.CreateEvent(None, 0, 0, None)
        self.agent_instance: Agent | None = None
        self.named_mutex_manager: NamedMutexManager | None = None
        self._shutdown_event_handle = None 

    def SvcStop(self):
        logger.info(f"{self._svc_name_} - Stop signal received.")
        self.ReportServiceStatus(win32service.SERVICE_STOP_PENDING)
        
        if self.agent_instance:
            logger.info(f"{self._svc_name_} - Requesting agent core to shutdown.")
            
            
            
            self.agent_instance.graceful_shutdown() 
        
        win32event.SetEvent(self.hWaitStop) 
        logger.info(f"{self._svc_name_} - Stop signal processed for SvcDoRun.")
        

    def SvcDoRun(self):
        servicemanager.LogMsg(
            servicemanager.EVENTLOG_INFORMATION_TYPE,
            servicemanager.PYS_SERVICE_STARTED,
            (self._svc_name_, '')
        )
        self.ReportServiceStatus(win32service.SERVICE_START_PENDING)

        
        log_dir_base = determine_storage_path() 
        log_dir = os.path.join(log_dir_base, PROGRAMDATA_LOG_DIR_NAME)
        try:
            os.makedirs(log_dir, exist_ok=True)
            
            
            global logger 
            logger, file_log_ok = setup_logger(
                name="agent_service", 
                log_directory_path=log_dir,
                service_name=self._svc_name_ 
            )
            if not file_log_ok:
                servicemanager.LogErrorMsg(f"{self._svc_name_} - Failed to initialize file logging at {log_dir}. Event log should still work.")
            logger.info(f"{self._svc_name_} - Logging initialized. Log directory: {log_dir}")
        except Exception as e:
            servicemanager.LogErrorMsg(f"{self._svc_name_} - CRITICAL: Failed to set up logging: {e}")
            self.ReportServiceStatus(win32service.SERVICE_STOPPED)
            return 

        logger.info(f"{self._svc_name_} - Service execution started (SvcDoRun).")

        
        try:
            setup_directory_structure() 
            logger.info(f"{self._svc_name_} - Ensured base directory structure at {log_dir_base}.")
        except Exception as e:
            logger.error(f"{self._svc_name_} - Failed to setup directory structure: {e}", exc_info=True)
            self.ReportServiceStatus(win32service.SERVICE_STOPPED)
            return

        
        self.named_mutex_manager = NamedMutexManager(AGENT_MUTEX_NAME)
        if not self.named_mutex_manager.acquire():
            logger.error(f"{self._svc_name_} - Failed to acquire mutex '{AGENT_MUTEX_NAME}'. Another instance might be running.")
            servicemanager.LogErrorMsg(f"{self._svc_name_} - Mutex acquisition failed. Shutting down.")
            self.ReportServiceStatus(win32service.SERVICE_STOPPED)
            if self.named_mutex_manager: 
                self.named_mutex_manager.release()
            return
        logger.info(f"{self._svc_name_} - Mutex '{AGENT_MUTEX_NAME}' acquired successfully.")

        
        
        
        try:
            self._shutdown_event_handle = create_admin_only_named_event(AGENT_SHUTDOWN_EVENT_NAME)
            if not self._shutdown_event_handle:
                
                raise RuntimeError(f"Failed to create or open the global shutdown event: {AGENT_SHUTDOWN_EVENT_NAME}")
            logger.info(f"{self._svc_name_} - Successfully created/opened global shutdown event: {AGENT_SHUTDOWN_EVENT_NAME}")
        except Exception as e:
            logger.critical(f"{self._svc_name_} - Failed to create/initialize global shutdown event '{AGENT_SHUTDOWN_EVENT_NAME}': {e}", exc_info=True)
            if self.named_mutex_manager: self.named_mutex_manager.release()
            self.ReportServiceStatus(win32service.SERVICE_STOPPED)
            return

        try:
            self.ReportServiceStatus(win32service.SERVICE_RUNNING)
            logger.info(f"{self._svc_name_} - Service reported as RUNNING.")

            
            logger.info(f"{self._svc_name_} - Initializing agent components...")
            config_file_path = get_service_config_file_path()
            logger.info(f"{self._svc_name_} - Using config file path: {config_file_path}")

            config_manager = ConfigManager(config_file_path=config_file_path)
            state_manager = StateManager() 

            
            
            if not state_manager.get_device_id() or not state_manager.get_token() or not state_manager.get_room_config():
                logger.warning(f"{self._svc_name_} - Agent is not fully configured (missing device_id, token, or room_config).")
                logger.warning(f"{self._svc_name_} - The agent will not be able to authenticate or connect to the server.")
                
                

            http_client = HttpClient(config_manager, state_manager)
            server_connector = ServerConnector(config_manager, state_manager, http_client)
            ws_client = WSClient(config_manager, state_manager, server_connector) 
            command_executor = CommandExecutor(config_manager, state_manager, ws_client, server_connector)

            self.agent_instance = Agent(
                config_manager=config_manager,
                state_manager=state_manager,
                ws_client=ws_client,
                command_executor=command_executor,
                named_mutex_manager=self.named_mutex_manager, 
                server_connector=server_connector,
                shutdown_event_name=AGENT_SHUTDOWN_EVENT_NAME 
            )
            logger.info(f"{self._svc_name_} - Agent components initialized.")

            
            logger.info(f"{self._svc_name_} - Starting agent core logic...")
            self.agent_instance.start() 

            logger.info(f"{self._svc_name_} - Agent core logic has completed (likely shutdown).")

        except Exception as e:
            logger.critical(f"{self._svc_name_} - Unhandled exception in SvcDoRun: {e}", exc_info=True)
            servicemanager.LogErrorMsg(f"{self._svc_name_} - Runtime error: {e}")
            
            if self.agent_instance:
                try:
                    self.agent_instance.graceful_shutdown()
                except Exception as shutdown_exc:
                    logger.error(f"{self._svc_name_} - Exception during forced shutdown after error: {shutdown_exc}", exc_info=True)
        finally:
            logger.info(f"{self._svc_name_} - SvcDoRun cleanup sequence started.")
            
            if self._shutdown_event_handle:
                try:
                    close_handle(self._shutdown_event_handle)
                    logger.info(f"{self._svc_name_} - Closed global shutdown event handle: {AGENT_SHUTDOWN_EVENT_NAME}")
                except Exception as e_close:
                    logger.error(f"{self._svc_name_} - Error closing global shutdown event handle: {e_close}", exc_info=True)
                self._shutdown_event_handle = None

            
            if self.named_mutex_manager and self.named_mutex_manager.is_acquired():
                logger.info(f"{self._svc_name_} - Releasing mutex '{AGENT_MUTEX_NAME}' in SvcDoRun finally block.")
                self.named_mutex_manager.release()
            
            self.ReportServiceStatus(win32service.SERVICE_STOPPED)
            logger.info(f"{self._svc_name_} - Service reported as STOPPED.")
            servicemanager.LogMsg(
                servicemanager.EVENTLOG_INFORMATION_TYPE,
                servicemanager.PYS_SERVICE_STOPPED,
                (self._svc_name_, '')
            )

def run_service():
    """
    Main entry point for service operations.
    This function is called by agent.main.py when service commands are issued
    (e.g., install, start, debug) or when the SCM starts the service.
    """
    
    

    
    
    if len(sys.argv) > 1 and sys.argv[1] == 'debug':
        
        global logger
        logger, _ = setup_logger(name="agent_service_debug", level="DEBUG", for_cli=True)
        logger.info("Running service in debug mode (console)...")
        
        
        
        
        
        
        
        
        
        
        class DebugAgentService(AgentService):
            def __init__(self, args):
                super().__init__(args)
                
                
            
            def ReportServiceStatus(self, status):
                logger.debug(f"Debug Mode - ReportServiceStatus: {status}")

            def SvcStop(self): 
                logger.info("Debug Mode - SvcStop called (e.g. by signal).")
                super().SvcStop()

        
        service_instance = DebugAgentService(sys.argv)
        try:
            service_instance.SvcDoRun()
        except KeyboardInterrupt:
            logger.info("Debug mode: KeyboardInterrupt received. Stopping service logic...")
            service_instance.SvcStop() 
        except Exception as e:
            logger.critical(f"Debug mode: Unhandled exception: {e}", exc_info=True)
        finally:
            logger.info("Debug mode: Service logic finished.")
            
            if service_instance.named_mutex_manager and service_instance.named_mutex_manager.is_acquired():
                service_instance.named_mutex_manager.release()
                logger.info("Debug mode: Mutex released.")
            if service_instance._shutdown_event_handle:
                close_handle(service_instance._shutdown_event_handle)
                logger.info("Debug mode: Shutdown event handle closed.")

    else:
        
        try:
            win32serviceutil.HandleCommandLine(AgentService)
        except Exception as e:
            
            servicemanager.LogErrorMsg(f"CMSAgentService - Error in HandleCommandLine: {e}")
            
            print(f"Error during service command processing: {e}", file=sys.stderr)

if __name__ == '__main__':
    
    
    
    run_service()