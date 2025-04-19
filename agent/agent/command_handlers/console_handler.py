"""
Console command handler for executing console/shell commands using subprocess.
"""
import subprocess
import platform
from typing import Dict, Any
from base_handler import BaseCommandHandler 
from agent.config import ConfigManager
from agent.utils import get_logger

logger = get_logger(__name__)

class ConsoleCommandHandler(BaseCommandHandler):
    """
    Handler responsible for executing commands in the underlying operating system's
    shell (e.g., bash, cmd.exe) via the `subprocess` module.
    """

    def __init__(self, config: ConfigManager):
        """
        Initialize the console command handler.

        :param config: The configuration manager instance
        :type config: ConfigManager
        """
        super().__init__(config)
        self.command_timeout: int = self.config.get('command_executor.default_timeout_sec', 300)
        default_encoding = 'utf-8' if platform.system() != 'Windows' else 'cp1252'
        self.output_encoding: str = self.config.get('command_executor.console_encoding', default_encoding)

        logger.info(f"ConsoleCommandHandler initialized with timeout={self.command_timeout}s, encoding='{self.output_encoding}'")

    def execute_command(self, command: str, command_id: str,
                       result: Dict[str, Any], **kwargs) -> None:
        """
        Execute a console/shell command using `subprocess.run` and update the
        result dictionary **in-place**.

        :param command: The command string to execute in the shell
        :type command: str
        :param command_id: The unique ID for this command execution instance
        :type command_id: str
        :param result: The dictionary to update with execution results
        :type result: Dict[str, Any]
        :param kwargs: Optional keyword arguments
        
        :returns: None
        """
        command_result_payload = {
            "stdout": "",
            "stderr": "",
            "exitCode": -1
        }
        result['success'] = False
        result['result'] = command_result_payload

        use_shell = True
        if use_shell:
            logger.warning(f"Executing command '{command_id}' with shell=True. Ensure command source is trusted.")

        creationflags = 0
        if platform.system() == 'Windows':
            creationflags = subprocess.CREATE_NO_WINDOW

        try:
            process = subprocess.run(
                command,
                shell=use_shell,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding=self.output_encoding,
                errors='replace',
                timeout=self.command_timeout,
                check=False,
                creationflags=creationflags
            )

            command_result_payload["stdout"] = process.stdout.strip() if process.stdout else ""
            command_result_payload["stderr"] = process.stderr.strip() if process.stderr else ""
            command_result_payload["exitCode"] = process.returncode
            result["success"] = (process.returncode == 0)

            logger.info(f"Command '{command_id}' executed. ExitCode={process.returncode}, Success={result['success']}")

            if command_result_payload["stderr"] and not result["success"]:
                 logger.warning(f"Command '{command_id}' STDERR:\n{command_result_payload['stderr']}")

        except subprocess.TimeoutExpired:
            error_msg = f"Error: Command timed out after {self.command_timeout} seconds."
            logger.error(f"Command '{command_id}' timed out: {command}")
            command_result_payload["stderr"] = error_msg
            command_result_payload["exitCode"] = 124
            result["success"] = False

        except FileNotFoundError:
            cmd_part = command.split()[0] if command else 'N/A'
            error_msg = f"Error: Command not found: '{cmd_part}'. Ensure it's installed and in the system PATH."
            logger.error(f"Command '{command_id}' not found: '{cmd_part}'")
            command_result_payload["stderr"] = error_msg
            command_result_payload["exitCode"] = 127
            result["success"] = False

        except PermissionError as e:
            error_msg = f"Error: Permission denied to execute command: {e}"
            logger.error(f"Permission denied executing command '{command_id}': {e}", exc_info=True)
            command_result_payload["stderr"] = error_msg
            command_result_payload["exitCode"] = 126
            result["success"] = False

        except OSError as e:
            error_msg = f"Operating system error while executing command: {e}"
            logger.error(f"OS error executing command '{command_id}': {e}", exc_info=True)
            command_result_payload["stderr"] = error_msg
            command_result_payload["exitCode"] = e.errno if hasattr(e, 'errno') else 1
            result["success"] = False

        except Exception as e:
            error_msg = f"Unexpected error while executing command: {str(e)}"
            logger.critical(f"Unexpected error executing command '{command_id}': {e}", exc_info=True)
            command_result_payload["stderr"] = error_msg
            command_result_payload["exitCode"] = 1
            result["success"] = False
