# -*- coding: utf-8 -*-
"""
Command execution module for the agent.
Receives commands via WebSocket, executes them in separate threads,
manages a queue for concurrency control, and sends results back.
Accepts WSClient and ConfigManager instances.
"""
import subprocess
import threading
import logging
import time
from typing import Dict, Any, Tuple, List, Optional
from queue import Queue, Empty, Full

# Import WSClient type hint and ConfigManager class
from typing import TYPE_CHECKING
from src.config.config_manager import ConfigManager
if TYPE_CHECKING:
    from src.communication.ws_client import WSClient

logger = logging.getLogger(__name__)

class CommandExecutor:
    """
    Executes shell commands received via WebSocket and manages concurrency.
    """

    def __init__(self, ws_client: 'WSClient', config: ConfigManager):
        """
        Initialize the command executor.

        Args:
            ws_client (WSClient): WebSocket client instance for sending results.
            config (ConfigManager): Configuration manager instance.

        Raises:
             ValueError: If ws_client is None.
        """
        if not ws_client:
             raise ValueError("WSClient instance is required for CommandExecutor.")
        if not config:
             raise ValueError("ConfigManager instance is required for CommandExecutor.")


        self.ws_client = ws_client
        self.config = config

        # --- Read configuration ---
        self.max_parallel_commands = self.config.get('command_executor.max_parallel_commands', 2)
        self.command_timeout = self.config.get('command_executor.default_timeout_sec', 300)
        logger.info(f"CommandExecutor Config: Max Parallel={self.max_parallel_commands}, Timeout={self.command_timeout}s")
        # --------------------------

        self._command_queue: Queue[Tuple[str, str, str]] = Queue(maxsize=self.max_parallel_commands * 5) # Limit queue size
        self._stop_event = threading.Event()
        self._worker_threads: List[threading.Thread] = []

        logger.info(f"CommandExecutor initialized")

        # Registration of the handler is now done by the Agent class after initializing both

    def start_workers(self):
        """Starts worker threads to process the command queue."""
        if self._worker_threads:
             logger.warning("Worker threads already started.")
             return

        logger.info(f"Starting {self.max_parallel_commands} command execution worker threads...")
        self._stop_event.clear() # Ensure stop event is clear before starting
        for i in range(self.max_parallel_commands):
            thread = threading.Thread(target=self._worker_loop, name=f"CmdExecWorker-{i}", daemon=True)
            thread.start()
            self._worker_threads.append(thread)

    def _worker_loop(self):
        """The main loop for each worker thread."""
        thread_name = threading.current_thread().name
        logger.debug(f"Worker thread {thread_name} started.")
        while not self._stop_event.is_set():
            command_info: Optional[Tuple[str, str, str]] = None
            try:
                # Wait for a command with a timeout to allow checking stop_event periodically
                command_info = self._command_queue.get(block=True, timeout=1.0)
                command, command_id, command_type = command_info

                logger.info(f"Worker {thread_name} processing command: {command_id} (Type: {command_type})")
                self._execute_and_send_result(command, command_id, command_type)
                self._command_queue.task_done()

            except Empty:
                # Queue is empty, loop continues to check stop_event
                continue
            except Exception as e:
                 # Catch unexpected errors within the loop itself
                 logger.error(f"Unexpected error in worker thread {thread_name}: {e}", exc_info=True)
                 # If a command was dequeued but failed before execution, try to notify server
                 if command_info:
                      _, cmd_id_err, _ = command_info
                      self._send_result(cmd_id_err, {
                           "stdout": "",
                           "stderr": f"Agent internal error in worker thread: {e}",
                           "exitCode": -1
                      })
                      self._command_queue.task_done() # Mark as done even on error
                 # Avoid busy-waiting on continuous errors
                 time.sleep(1)

        logger.debug(f"Worker thread {thread_name} stopping.")

    def _execute_and_send_result(self, command: str, command_id: str, command_type: str):
        """Executes a single command and sends the result back via WebSocket."""
        logger.info(f"Executing command: '{command}' (ID: {command_id}, Type: {command_type})")
        result: Dict[str, Any] = {"stdout": "", "stderr": "", "exitCode": -1}

        # --- Security Note ---
        # Using shell=True is potentially dangerous if 'command' comes from an untrusted source.
        # If possible, avoid shell=True and pass arguments as a list after splitting
        # using shlex.split(command) if appropriate for the command structure.
        # If shell=True is absolutely necessary, the source providing the command
        # (the server) MUST rigorously sanitize it.
        use_shell = True # Keep original behavior for now, but be aware of risks
        if use_shell:
             logger.warning(f"Executing command '{command_id}' with shell=True. Ensure the command source is trusted and sanitized.")

        # Determine appropriate encoding based on platform
        # Use UTF-8 as a general default, but Windows might use others like 'cp1252' or 'cp437'
        # Using 'utf-8' with 'replace' errors is often a safe compromise.
        output_encoding = 'utf-8' #'locale' might be better sometimes but can fail
        # if sys.platform == "win32":
        #     output_encoding = 'cp1252' # Example, adjust based on typical Windows console encoding

        try:
            process = subprocess.run(
                command,
                shell=use_shell, # !!! SECURITY RISK if command is untrusted !!!
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True, # Decode stdout/stderr as text
                encoding=output_encoding,
                errors='replace', # Handle decoding errors gracefully
                timeout=self.command_timeout, # Use configured timeout
                check=False # Don't raise exception for non-zero exit code, handle it manually
            )

            result = {
                "stdout": process.stdout.strip() if process.stdout else "",
                "stderr": process.stderr.strip() if process.stderr else "",
                "exitCode": process.returncode
            }
            log_level = logging.INFO if process.returncode == 0 else logging.WARNING
            logger.log(log_level, f"Command '{command_id}' completed. Exit Code: {process.returncode}")
            # Log output only if it exists
            if result["stdout"]:
                 logger.debug(f"Command '{command_id}' STDOUT:\n{result['stdout']}")
            if result["stderr"]:
                 logger.warning(f"Command '{command_id}' STDERR:\n{result['stderr']}")

        except subprocess.TimeoutExpired:
            logger.error(f"Command '{command_id}' timed out after {self.command_timeout} seconds: {command}")
            result = {
                "stdout": "",
                "stderr": f"Lỗi: Lệnh hết hạn sau {self.command_timeout} giây.",
                "exitCode": 124 # Standard exit code for timeout
            }
        except FileNotFoundError:
            # Extract the likely command name that wasn't found
            cmd_part = command.split()[0] if command else 'N/A'
            logger.error(f"Command '{command_id}' not found: '{cmd_part}'")
            result = {
                "stdout": "",
                "stderr": f"Lỗi: Lệnh không tìm thấy: '{cmd_part}'. Đảm bảo lệnh đã được cài đặt và nằm trong PATH của hệ thống.",
                "exitCode": 127 # Standard exit code for command not found
            }
        except PermissionError as e:
             logger.error(f"Permission denied executing command '{command_id}': {e}", exc_info=True)
             result = {
                "stdout": "",
                "stderr": f"Lỗi: Không có quyền thực thi lệnh: {e}",
                "exitCode": 126 # Standard exit code for permission denied
             }
        except OSError as e:
            # Catch other OS-level errors during process creation/execution
            logger.error(f"OS error executing command '{command_id}': {e}", exc_info=True)
            result = {
                "stdout": "",
                "stderr": f"Lỗi hệ điều hành khi thực thi lệnh: {e}",
                "exitCode": e.errno if hasattr(e, 'errno') else 1 # Use errno if available
            }
        except Exception as e:
            # Catch any other unexpected errors
            logger.critical(f"Unexpected error executing command '{command_id}': {e}", exc_info=True)
            result = {
                "stdout": "",
                "stderr": f"Lỗi không mong muốn khi thực thi lệnh: {str(e)}",
                "exitCode": 1 # Generic error code
            }
        finally:
            # Always attempt to send a result back
            self._send_result(command_id, result)

    def _send_result(self, command_id: str, result: Dict[str, Any]):
        """Sends the command result to the server using the WebSocket client."""
        logger.debug(f"Attempting to send result for command ID: {command_id}")
        if not self.ws_client:
             logger.error(f"Cannot send result for {command_id}: WSClient is not available.")
             return

        success = self.ws_client.send_command_result(command_id, result)
        if success:
            logger.debug(f"Command result sent successfully for {command_id}")
        else:
            # Error is already logged by ws_client._emit_message
            logger.error(f"Failed to send command result via WebSocket for {command_id}. Check WSClient logs.")

    def handle_incoming_command(self, command_data: Dict[str, Any]):
        """
        Callback method registered with WSClient to queue incoming commands.
        Should be called by the Agent or WSClient event handler.
        """
        try:
            # Prioritize specific 'commandId', fallback to generic 'id'
            command_id = command_data.get('commandId') or command_data.get('id')
            command = command_data.get('command')
            # Get command type, default to 'console' or 'unknown'
            command_type = command_data.get('commandType', command_data.get('type', 'console'))

            if not command_id:
                logger.error(f"Received command message missing required 'commandId' (or 'id'): {command_data}")
                # Cannot send result back without ID
                return
            if command is None: # Check specifically for None
                logger.error(f"Received command message missing required 'command' field for ID {command_id}: {command_data}")
                self._send_result(command_id, {
                    "stdout": "",
                    "stderr": "Lỗi Agent: Thiếu trường 'command' trong dữ liệu lệnh.",
                    "exitCode": -1 # Indicate agent-side error
                })
                return
            if not isinstance(command, str):
                 logger.error(f"Received command with non-string 'command' field for ID {command_id}: {type(command)}")
                 self._send_result(command_id, {
                    "stdout": "",
                    "stderr": "Lỗi Agent: Trường 'command' phải là một chuỗi.",
                    "exitCode": -1
                 })
                 return


            # Try putting into queue, handle Queue Full
            try:
                 self._command_queue.put((command, command_id, command_type), block=False) # Don't block if full
                 logger.info(f"Command queued: {command_id} (Type: {command_type}, Queue size: {self._command_queue.qsize()})")
            except Full:
                 logger.error(f"Command queue is full (max={self._command_queue.maxsize}). Cannot queue command: {command_id}")
                 self._send_result(command_id, {
                    "stdout": "",
                    "stderr": "Lỗi Agent: Hàng đợi lệnh đã đầy. Vui lòng thử lại sau.",
                    "exitCode": -1
                 })


        except Exception as e:
            # Catch errors during handling/queuing
            cmd_id_err = command_data.get('commandId', command_data.get('id', 'N/A'))
            logger.error(f"Error handling/queuing command data for ID {cmd_id_err}: {e}", exc_info=True)
            # Try to send error back if possible
            if cmd_id_err != 'N/A':
                 self._send_result(cmd_id_err, {
                    "stdout": "",
                    "stderr": f"Lỗi nội bộ Agent khi xử lý lệnh: {e}",
                    "exitCode": -1
                 })

    def stop(self):
        """Signals worker threads to stop and waits for the queue to be processed."""
        logger.info("Stopping CommandExecutor...")
        self._stop_event.set()

        # Wait for threads to finish processing current items and exit loop
        logger.debug("Waiting for worker threads to terminate...")
        for thread in self._worker_threads:
            thread.join(timeout=self.command_timeout + 5) # Wait slightly longer than command timeout
            if thread.is_alive():
                 logger.warning(f"Worker thread {thread.name} did not terminate gracefully.")

        # Clear the list of threads
        self._worker_threads = []

        # Clear any remaining items in the queue (should be empty if workers finished)
        self.clear_command_queue()

        logger.info("CommandExecutor stopped.")

    def clear_command_queue(self) -> int:
        """Clears all pending commands from the execution queue."""
        count = 0
        while not self._command_queue.empty():
            try:
                self._command_queue.get_nowait()
                self._command_queue.task_done()
                count += 1
            except Empty:
                break
        if count > 0:
             logger.warning(f"Cleared {count} commands from the execution queue during stop.")
        return count
