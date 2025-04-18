# -*- coding: utf-8 -*-
"""
Command execution module for the agent.
"""
import subprocess
import threading
import time
from typing import Dict, Any, Tuple, List, Optional
from queue import Queue, Empty, Full

from typing import TYPE_CHECKING
from src.config.config_manager import ConfigManager
if TYPE_CHECKING:
    from src.communication.ws_client import WSClient

from src.utils.logger import get_logger

logger = get_logger(__name__)

class CommandExecutor:
    """
    Executes shell commands received via WebSocket and manages concurrency.
    """

    def __init__(self, ws_client: 'WSClient', config: ConfigManager):
        """
        Initialize the command executor.

        :param ws_client: WebSocket client instance for sending results
        :type ws_client: WSClient
        :param config: Configuration manager instance
        :type config: ConfigManager
        :raises: ValueError if ws_client is None
        """
        if not ws_client:
             raise ValueError("WSClient instance is required for CommandExecutor.")
        if not config:
             raise ValueError("ConfigManager instance is required for CommandExecutor.")

        self.ws_client = ws_client
        self.config = config

        self.max_parallel_commands = self.config.get('command_executor.max_parallel_commands', 2)
        self.command_timeout = self.config.get('command_executor.default_timeout_sec', 300)
        logger.info(f"CommandExecutor Config: Max Parallel={self.max_parallel_commands}, Timeout={self.command_timeout}s")

        self._command_queue: Queue[Tuple[str, str, str]] = Queue(maxsize=self.max_parallel_commands * 5)
        self._stop_event = threading.Event()
        self._worker_threads: List[threading.Thread] = []

        logger.info(f"CommandExecutor initialized")

    def start_workers(self):
        """
        Starts worker threads to process the command queue.
        """
        if self._worker_threads:
             logger.warning("Worker threads already started.")
             return

        logger.info(f"Starting {self.max_parallel_commands} command execution worker threads...")
        self._stop_event.clear()
        for i in range(self.max_parallel_commands):
            thread = threading.Thread(target=self._worker_loop, name=f"CmdExecWorker-{i}", daemon=True)
            thread.start()
            self._worker_threads.append(thread)

    def _worker_loop(self):
        """
        The main loop for each worker thread.
        """
        thread_name = threading.current_thread().name
        logger.debug(f"Worker thread {thread_name} started.")
        while not self._stop_event.is_set():
            command_info: Optional[Tuple[str, str, str]] = None
            try:
                command_info = self._command_queue.get(block=True, timeout=1.0)
                command, command_id, command_type = command_info

                logger.info(f"Worker {thread_name} processing command: {command_id} (Type: {command_type})")
                self._execute_and_send_result(command, command_id, command_type)
                self._command_queue.task_done()

            except Empty:
                continue
            except Exception as e:
                 logger.error(f"Unexpected error in worker thread {thread_name}: {e}", exc_info=True)
                 if command_info:
                      _, cmd_id_err, _ = command_info
                      self._send_result(cmd_id_err, {
                           "type": "unknown",
                           "success": False,
                           "result": {
                               "stdout": "",
                               "stderr": f"Agent internal error in worker thread: {e}",
                               "exitCode": -1
                           }
                      })
                      self._command_queue.task_done()
                 time.sleep(1)

        logger.debug(f"Worker thread {thread_name} stopping.")

    def _execute_and_send_result(self, command: str, command_id: str, command_type: str):
        """
        Executes a single command and sends the result back via WebSocket.
        
        :param command: The command to execute
        :type command: str
        :param command_id: The ID of the command
        :type command_id: str
        :param command_type: The type of command
        :type command_type: str
        """
        logger.info(f"Executing command: '{command}' (ID: {command_id}, Type: {command_type})")
        
        result = {
            "type": command_type,
            "success": False,
            "result": {
                "stdout": "", 
                "stderr": "", 
                "exitCode": -1
            }
        }

        if command_type == 'console':
            self._execute_console_command(command, command_id, result)
        else:
            logger.warning(f"Command type '{command_type}' is not yet implemented")
            result["result"]["stderr"] = f"Lỗi: Loại lệnh '{command_type}' chưa được hỗ trợ."
            result["result"]["exitCode"] = -2
        
        self._send_result(command_id, result)

    def _execute_console_command(self, command: str, command_id: str, result: Dict[str, Any]):
        """
        Execute a console command using shell and update the result dict.
        
        :param command: Command to execute
        :type command: str
        :param command_id: ID of the command
        :type command_id: str
        :param result: Dictionary to update with result
        :type result: Dict[str, Any]
        """
        use_shell = True
        if use_shell:
            logger.warning(f"Executing command '{command_id}' with shell=True. Ensure the command source is trusted and sanitized.")

        output_encoding = 'cp1252'
        
        try:
            process = subprocess.run(
                command,
                shell=use_shell,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding=output_encoding,
                errors='replace',
                timeout=self.command_timeout,
                check=False,
                creationflags=subprocess.CREATE_NO_WINDOW
            )

            result["result"] = {
                "stdout": process.stdout.strip() if process.stdout else "",
                "stderr": process.stderr.strip() if process.stderr else "",
                "exitCode": process.returncode
            }
            result["success"] = process.returncode == 0
            
            log_level = logging.INFO if process.returncode == 0 else logging.WARNING
            logger.log(log_level, f"Command '{command_id}' completed. Exit Code: {process.returncode}")
            
            if result["result"]["stdout"]:
                logger.debug(f"Command '{command_id}' STDOUT:\n{result['result']['stdout']}")
            if result["result"]["stderr"]:
                logger.warning(f"Command '{command_id}' STDERR:\n{result['result']['stderr']}")

        except subprocess.TimeoutExpired:
            logger.error(f"Command '{command_id}' timed out after {self.command_timeout} seconds: {command}")
            result["result"] = {
                "stdout": "",
                "stderr": f"Lỗi: Lệnh hết hạn sau {self.command_timeout} giây.",
                "exitCode": 124
            }
        except FileNotFoundError:
            cmd_part = command.split()[0] if command else 'N/A'
            logger.error(f"Command '{command_id}' not found: '{cmd_part}'")
            result["result"] = {
                "stdout": "",
                "stderr": f"Lỗi: Lệnh không tìm thấy: '{cmd_part}'. Đảm bảo lệnh đã được cài đặt và nằm trong PATH của hệ thống.",
                "exitCode": 127
            }
        except PermissionError as e:
            logger.error(f"Permission denied executing command '{command_id}': {e}", exc_info=True)
            result["result"] = {
                "stdout": "",
                "stderr": f"Lỗi: Không có quyền thực thi lệnh: {e}",
                "exitCode": 126
            }
        except OSError as e:
            logger.error(f"OS error executing command '{command_id}': {e}", exc_info=True)
            result["result"] = {
                "stdout": "",
                "stderr": f"Lỗi hệ điều hành khi thực thi lệnh: {e}",
                "exitCode": e.errno if hasattr(e, 'errno') else 1
            }
        except Exception as e:
            logger.critical(f"Unexpected error executing command '{command_id}': {e}", exc_info=True)
            result["result"] = {
                "stdout": "",
                "stderr": f"Lỗi không mong muốn khi thực thi lệnh: {str(e)}",
                "exitCode": 1
            }

    def _send_result(self, command_id: str, result: Dict[str, Any]):
        """
        Sends the command result to the server using the WebSocket client.
        
        :param command_id: ID of the command
        :type command_id: str
        :param result: Result data to send
        :type result: Dict[str, Any]
        """
        logger.debug(f"Attempting to send result for command ID: {command_id}")
        if not self.ws_client:
            logger.error(f"Cannot send result for {command_id}: WSClient is not available.")
            return

        success = self.ws_client.send_command_result(command_id, result)
        if success:
            logger.debug(f"Command result sent successfully for {command_id}")
        else:
            logger.error(f"Failed to send command result via WebSocket for {command_id}. Check WSClient logs.")

    def handle_incoming_command(self, command_data: Dict[str, Any]):
        """
        Callback method registered with WSClient to queue incoming commands.
        
        :param command_data: Command data from WebSocket
        :type command_data: Dict[str, Any]
        """
        try:
            command_id = command_data.get('commandId') or command_data.get('id')
            command = command_data.get('command')
            command_type = command_data.get('commandType', command_data.get('type', 'console'))

            if not command_id:
                logger.error(f"Received command message missing required 'commandId' (or 'id'): {command_data}")
                return
            if command is None:
                logger.error(f"Received command message missing required 'command' field for ID {command_id}: {command_data}")
                self._send_result(command_id, {
                    "type": "unknown",
                    "success": False,
                    "result": {
                        "stdout": "",
                        "stderr": "Lỗi Agent: Thiếu trường 'command' trong dữ liệu lệnh.",
                        "exitCode": -1
                    }
                })
                return
            if not isinstance(command, str):
                 logger.error(f"Received command with non-string 'command' field for ID {command_id}: {type(command)}")
                 self._send_result(command_id, {
                    "type": "unknown",
                    "success": False,
                    "result": {
                        "stdout": "",
                        "stderr": "Lỗi Agent: Trường 'command' phải là một chuỗi.",
                        "exitCode": -1
                    }
                 })
                 return

            try:
                 self._command_queue.put((command, command_id, command_type), block=False)
                 logger.info(f"Command queued: {command_id} (Type: {command_type}, Queue size: {self._command_queue.qsize()})")
            except Full:
                 logger.error(f"Command queue is full (max={self._command_queue.maxsize}). Cannot queue command: {command_id}")
                 self._send_result(command_id, {
                    "type": command_type,
                    "success": False,
                    "result": {
                        "stdout": "",
                        "stderr": "Lỗi Agent: Hàng đợi lệnh đã đầy. Vui lòng thử lại sau.",
                        "exitCode": -1
                    }
                 })

        except Exception as e:
            cmd_id_err = command_data.get('commandId', command_data.get('id', 'N/A'))
            logger.error(f"Error handling/queuing command data for ID {cmd_id_err}: {e}", exc_info=True)
            if cmd_id_err != 'N/A':
                 self._send_result(cmd_id_err, {
                    "type": "unknown",
                    "success": False,
                    "result": {
                        "stdout": "",
                        "stderr": f"Lỗi nội bộ Agent khi xử lý lệnh: {e}",
                        "exitCode": -1
                    }
                 })

    def stop(self):
        """
        Signals worker threads to stop and waits for the queue to be processed.
        """
        logger.info("Stopping CommandExecutor...")
        self._stop_event.set()

        logger.debug("Waiting for worker threads to terminate...")
        for thread in self._worker_threads:
            thread.join(timeout=self.command_timeout + 5)
            if thread.is_alive():
                 logger.warning(f"Worker thread {thread.name} did not terminate gracefully.")

        self._worker_threads = []

        self.clear_command_queue()

        logger.info("CommandExecutor stopped.")

    def clear_command_queue(self) -> int:
        """
        Clears all pending commands from the execution queue.
        
        :return: Number of cleared commands
        :rtype: int
        """
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
