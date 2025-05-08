"""
Command Executor module for processing and executing incoming commands.
"""
import threading
import time
import queue
from typing import Dict, Any, List, Optional, Tuple, Type, cast, TYPE_CHECKING

from agent.utils import get_logger

from agent.command_handlers import BaseCommandHandler, ConsoleCommandHandler, SystemCommandHandler
if TYPE_CHECKING:
    from agent.communication import WSClient
    from agent.config import ConfigManager

logger = get_logger("command.executor")

EXECUTOR_ERROR_TYPE = "ExecutorError"
INPUT_ERROR_TYPE = "InputError"
QUEUE_ERROR_TYPE = "QueueError"
HANDLER_ERROR_TYPE = "HandlerError"
UNKNOWN_ERROR_TYPE = "UnknownError"

CommandInfo = Tuple[str, str, str]


class CommandExecutor:
    """
    Receives commands via WebSocket, queues them, and executes them using
    registered handlers based on command type. Manages concurrent execution.
    (Optimized for readability and maintainability)
    """

    def __init__(self, ws_client: 'WSClient', config: 'ConfigManager'):
        """
        Initialize the command executor.

        :param ws_client: WebSocket client instance for sending results.
        :param config: Configuration manager instance.
        :raises: ValueError if ws_client or config is None.
        """
        if not ws_client:
             raise ValueError("WSClient instance is required for CommandExecutor.")
        if not config:
             raise ValueError("ConfigManager instance is required for CommandExecutor.")

        self.ws_client = ws_client
        self.config = config

        self.max_parallel_commands = self.config.get('command_executor.max_parallel_commands', 2)
        self.command_timeout = self.config.get('command_executor.default_timeout_sec', 300)
        self.max_queue_size = self.config.get('command_executor.max_queue_size', self.max_parallel_commands * 10)

        logger.info(f"CommandExecutor Config: Max Parallel={self.max_parallel_commands}, "
                    f"Default Timeout={self.command_timeout}s, Max Queue Size={self.max_queue_size}")

        self._command_queue: queue.Queue[CommandInfo] = queue.Queue(maxsize=self.max_queue_size)
        self._stop_event = threading.Event()
        self._worker_threads: List[threading.Thread] = []
        self._handlers: Dict[str, BaseCommandHandler] = {}

        self._register_handlers()
        logger.info(f"CommandExecutor initialized with handlers for types: {', '.join(self._handlers.keys())}")

    def _register_handlers(self):
        """Initializes and registers command handlers."""
        handlers_to_register: Dict[str, Type[BaseCommandHandler]] = {
            'console': ConsoleCommandHandler,
            'system': SystemCommandHandler
        }

        for handler_type, handler_class in handlers_to_register.items():
            try:
                self._handlers[handler_type] = handler_class(self.config)
                logger.info(f"Registered handler '{handler_type}' using {handler_class.__name__}")
            except Exception as e:
                logger.error(f"Failed to initialize or register handler '{handler_type}' ({handler_class.__name__}): {e}", exc_info=True)

    # === PUBLIC METHODS ===
    
    def start_workers(self):
        """
        Starts worker threads to process the command queue.
        Idempotent: Does nothing if workers are already running.
        """
        if self._worker_threads:
             logger.warning("Worker threads already started.")
             return
        if not self._handlers:
             logger.error("Cannot start workers: No command handlers registered.")
             return

        logger.info(f"Starting {self.max_parallel_commands} command execution worker threads...")
        self._stop_event.clear()
        for i in range(self.max_parallel_commands):
            thread = threading.Thread(target=self._worker_loop, name=f"CmdExecWorker-{i}", daemon=True)
            thread.start()
            self._worker_threads.append(thread)
        logger.info("Worker threads started.")
        
    def handle_incoming_command(self, command_data: Dict[str, Any]):
        """
        Callback method to validate and queue incoming commands.
        Uses helper methods for validation and queuing.
        """
        is_valid, validation_error_msg = self._validate_incoming_command(command_data)

        command_id = command_data.get('commandId') or command_data.get('id')
        command_type = command_data.get('commandType', command_data.get('type', 'console')).lower()

        if not is_valid:
            logger.error(f"Invalid command data received (ID: {command_id or 'N/A'}): {validation_error_msg}")
            if command_id:
                error_result = self._create_error_result(
                    command_id, command_type, INPUT_ERROR_TYPE, validation_error_msg or "Invalid command data."
                )
                self._send_result(command_id, error_result)
            return

        try:
            command = cast(str, command_data.get('command'))

            self._queue_command(command, command_id, command_type)

        except Exception as e:
            err_id = command_id or 'N/A'
            logger.error(f"Error queuing command data for ID '{err_id}': {e}", exc_info=True)
            if err_id != 'N/A':
                 error_result = self._create_error_result(
                    err_id, command_type, EXECUTOR_ERROR_TYPE,
                    f"Agent internal error while queuing command: {e}"
                 )
                 self._send_result(err_id, error_result)
    
    def stop(self, graceful: bool = True, timeout_factor: float = 1.1):
        """
        Signals worker threads to stop and waits for them to finish.
        Incorporates logic previously in _join_worker_threads and clear_command_queue.

        :param graceful: If True, waits for the queue to be processed. If False, clears queue first.
        :param timeout_factor: Multiplier for command timeout to wait for threads.
        """
        logger.info(f"Stopping CommandExecutor (graceful={graceful})...")

        if not graceful:
            logger.warning("Performing non-graceful shutdown. Clearing command queue.")
            cleared_count = 0
            while True:
                try:
                    self._command_queue.get_nowait()
                    self._command_queue.task_done()
                    cleared_count += 1
                except queue.Empty:
                    break
                except Exception as e:
                    logger.error(f"Unexpected error clearing command queue item during non-graceful stop: {e}", exc_info=True)
                    time.sleep(0.1)
            if cleared_count > 0:
                 logger.warning(f"Cleared {cleared_count} pending commands.")

        self._stop_event.set()

        if graceful and not self._command_queue.empty():
             logger.info(f"Waiting for remaining {self._command_queue.qsize()} commands to be processed...")
             try:
                  self._command_queue.join()
                  logger.info("Command queue processed.")
             except Exception as e:
                  logger.error(f"Error waiting for command queue to join: {e}")

        logger.debug("Waiting for worker threads to terminate...")
        join_timeout = (self.command_timeout * timeout_factor) if self.command_timeout else 10.0
        start_join_time = time.time()
        threads_to_join = list(self._worker_threads)

        for thread in threads_to_join:
             remaining_time = max(0, join_timeout - (time.time() - start_join_time))
             thread.join(timeout=remaining_time)
             if thread.is_alive():
                 logger.warning(f"Worker thread {thread.name} did not terminate gracefully within timeout ({join_timeout}s).")

        self._worker_threads = []

        final_cleared_count = 0
        while True:
            try:
                self._command_queue.get_nowait()
                self._command_queue.task_done()
                final_cleared_count += 1
            except queue.Empty:
                break
            except Exception as e:
                 logger.error(f"Unexpected error clearing command queue item during final cleanup: {e}", exc_info=True)
                 time.sleep(0.1)

        if final_cleared_count > 0:
             logger.warning(f"Cleared {final_cleared_count} commands during final cleanup after worker termination.")

        logger.info("CommandExecutor stopped.")

    # === COMMAND VALIDATION AND QUEUEING ===
    
    def _validate_incoming_command(self, command_data: Dict[str, Any]) -> Tuple[bool, Optional[str]]:
        """
        Validates the raw command data dictionary. Logs errors internally.

        :return: Tuple (is_valid: bool, error_message: Optional[str])
        """
        command_id = command_data.get('commandId') or command_data.get('id')
        command = command_data.get('command')

        error_message: Optional[str] = None

        if not command_id:
            error_message = "Received command message missing required 'commandId' or 'id'."
            logger.error(error_message + f" Data: {command_data}")
            return False, error_message
        if command is None:
            error_message = "Required 'command' field is missing."
            logger.error(f"{error_message} (ID: {command_id}) Data: {command_data}")
            return False, error_message
        if not isinstance(command, str):
             error_message = f"'command' field must be a string (received {type(command).__name__})."
             logger.error(f"{error_message} (ID: {command_id}) Data: {command_data}")
             return False, error_message
        if not command.strip():
             error_message = "'command' field cannot be empty or whitespace."
             logger.error(f"{error_message} (ID: {command_id}) Data: {command_data}")
             return False, error_message

        return True, None

    def _queue_command(self, command: str, command_id: str, command_type: str):
        """
        Attempts to put the command onto the queue. Sends error result if full.
        Does not raise exceptions anymore. Returns True on success, False on failure (queue full).
        """
        try:
             self._command_queue.put((command, command_id, command_type), block=False)
             logger.info(f"Command queued: {command_id} (Type: {command_type}, Queue size: {self._command_queue.qsize()}/{self.max_queue_size})")
        except queue.Full:
             logger.error(f"Command queue is full (max={self.max_queue_size}). Cannot queue command: {command_id}")
             error_result = self._create_error_result(
                command_id, command_type, QUEUE_ERROR_TYPE,
                "Agent command queue is full. Please try again later."
             )
             self._send_result(command_id, error_result)
    
    # === WORKER AND EXECUTION LOGIC ===
    
    def _worker_loop(self):
        """
        The main loop for each worker thread. Fetches commands from the queue
        and delegates execution.
        """
        thread_name = threading.current_thread().name
        logger.debug(f"Worker thread {thread_name} started.")

        while not self._stop_event.is_set():
            command_info: Optional[CommandInfo] = None
            try:
                command_info = self._command_queue.get(block=True, timeout=1.0)
                command, command_id, command_type = command_info

                logger.info(f"Worker {thread_name} processing command: {command_id} (Type: {command_type})")
                self._process_command(command, command_id, command_type)

            except queue.Empty:
                continue
            except Exception as e:
                cmd_id = command_info[1] if command_info else "N/A"
                cmd_type = command_info[2] if command_info else "unknown"
                logger.error(f"Unexpected error in worker {thread_name} processing command '{cmd_id}': {e}", exc_info=True)
                if cmd_id != "N/A":
                    self._handle_execution_error(
                        e, cmd_id, cmd_type, EXECUTOR_ERROR_TYPE, "Agent internal error in worker thread"
                    )
                time.sleep(1)
            finally:
                 if command_info:
                     try:
                         self._command_queue.task_done()
                         logger.debug(f"Command {command_info[1]} marked as done.")
                     except ValueError:
                         logger.warning(f"task_done() called inappropriately for command {command_info[1]}.")
                     except Exception as td_err:
                         logger.error(f"Unexpected error calling task_done for {command_info[1]}: {td_err}", exc_info=True)

        logger.debug(f"Worker thread {thread_name} stopping.")

    def _process_command(self, command: str, command_id: str, command_type: str):
        """
        Handles the execution of a single command and sending the result.
        Errors are handled by modifying result_data directly.
        """
        result_data = {
            "type": command_type,
            "success": False,
            "result": None
        }
        handler = self._handlers.get(command_type)

        try:
            if not handler:
                logger.error(f"Handler not found for command '{command_id}' (Type: {command_type})")
                result_data['success'] = False
                result_data['result'] = self._create_error_payload(
                    HANDLER_ERROR_TYPE,
                    f"Command type '{command_type}' is not supported by this agent."
                )
            else:
                self._execute_via_handler(handler, command, command_id, result_data)

                if result_data.get('success', False):
                    self._validate_handler_result(result_data, command_id, command_type)

                logger.debug(f"Command '{command_id}' processing finished. Final Success: {result_data.get('success')}")

        except Exception as e:
             logger.error(f"Unexpected error processing command '{command_id}': {e}", exc_info=True)
             result_data['success'] = False
             result_data['result'] = self._create_error_payload(
                 EXECUTOR_ERROR_TYPE, f"Unexpected agent error processing command: {e}", type(e).__name__
             )

        self._send_result(command_id, result_data)

    def _execute_via_handler(self, handler: BaseCommandHandler, command: str, command_id: str, result_data: Dict[str, Any]):
        """
        Executes the command using the provided handler.
        Updates result_data directly on handler exception.
        """
        try:
            handler.execute_command(command, command_id, result_data)
        except Exception as e:
            logger.error(f"Handler '{result_data.get('type')}' raised an exception executing command '{command_id}': {e}", exc_info=True)
            result_data['success'] = False
            result_data['result'] = self._create_error_payload(
                HANDLER_ERROR_TYPE,
                f"Handler error: {e}",
                exception_type=type(e).__name__
            )

    def _validate_handler_result(self, result_data: Dict[str, Any], command_id: str, command_type: str):
        """
        Checks if the handler populated the result dictionary correctly.
        Updates result_data directly on validation failure.
        """
        if 'success' not in result_data or not isinstance(result_data['success'], bool):
             error_msg = f"Handler '{command_type}' for command '{command_id}' did not correctly set 'success' boolean."
             logger.error(error_msg)
             result_data['success'] = False
             result_data['result'] = self._create_error_payload(HANDLER_ERROR_TYPE, error_msg)
             return

        if result_data['success'] and result_data.get('result') is None:
             logger.warning(f"Handler '{command_type}' for command '{command_id}' succeeded but did not set 'result' field. Sending null.")
    
    # === RESULT AND ERROR HANDLING ===
    
    def _send_result(self, command_id: str, result_data: Dict[str, Any]):
        """Sends the final result dictionary back via the WebSocket client."""
        if not self.ws_client:
            logger.error(f"Cannot send result for {command_id}: WSClient is not available.")
            return

        logger.debug(f"Attempting to send result for command ID: {command_id}")
        try:
            success = self.ws_client.send_command_result(command_id, result_data)
            if not success:
                logger.error(f"WSClient reported failure sending result for {command_id}. Check WSClient logs.")
        except Exception as e:
             logger.error(f"Exception during ws_client.send_command_result for {command_id}: {e}", exc_info=True)

    def _handle_execution_error(self, error: Exception, command_id: str, command_type: str, error_type: str, context_msg: str):
         """Handles errors occurring during command execution (e.g., worker errors)."""
         error_result = self._create_error_result(
              command_id, command_type, error_type, f"{context_msg}: {error}"
         )
         self._send_result(command_id, error_result)

    def _create_error_result(self, command_id: str, command_type: str,
                             error_type: str, message: str) -> Dict[str, Any]:
        """Helper to create a standardized error result dictionary for sending."""
        logger.debug(f"Creating error result for {command_id}: Type={error_type}, Msg={message}")
        return {
            "type": command_type,
            "success": False,
            "result": self._create_error_payload(error_type, message)
        }

    def _create_error_payload(self, error_type: str, message: str, exception_type: Optional[str] = None) -> Dict[str, Any]:
        """Helper to create the 'result' payload for various errors."""
        payload = {
            "error_type": error_type,
            "message": message
        }
        if exception_type:
             payload["exception"] = exception_type
        return payload

