
"""
Standalone Agent Updater Script (Windows Only).

This script handles the process of updating an agent application by working
with executable file paths while managing their containing directories.

It performs the following steps:
1. Waits for the currently running agent process to terminate.
2. Backs up the directory containing the current agent executable.
3. Deploys the directory containing the new agent executable, replacing the old one.
4. Starts the new agent process using its specific executable file path.
5. Cleans up the backup directory on successful update.
6. Performs rollback (restores backup directory and restarts old agent executable) if any step fails.
7. Logs actions and errors to a file and console.
8. Saves detailed error reports in JSON format for later analysis.

Requires the 'psutil' library for reliable process management on Windows.
"""

import argparse
import logging
import os
import shutil
import subprocess
import sys
import time
import json
import datetime
import traceback
from pathlib import Path
from typing import Optional, Dict, Any
import psutil


BACKUP_SUFFIX = ".bak" #: Suffix appended to the backup directory name.
PROCESS_WAIT_TIMEOUT = 60  #: Max seconds to wait for the agent process to terminate.
PROCESS_START_WAIT = 5     #: Seconds to wait after starting the new agent before checking its status.
ERROR_REPORT_DIR = "error_reports" #: Subdirectory name for storing JSON error reports.
LOG_DIR = "logs" #: Subdirectory name for storing log files.


def setup_updater_logging(log_dir: Path):
    """Sets up logging for the updater process.

    Configures logging to output messages to both a file ('updater.log'
    within the specified log directory) and the console (stdout).
    Creates the log directory if it doesn't exist.

    :param log_dir: The directory path where log files should be stored.
    :type log_dir: pathlib.Path
    """
    log_file = log_dir / "updater.log"
    try:
        log_dir.mkdir(parents=True, exist_ok=True)
        log_format = '%(asctime)s - %(levelname)s - %(message)s'
        logging.basicConfig(
            level=logging.INFO,
            format=log_format,
            handlers=[
                logging.FileHandler(log_file, encoding='utf-8'),
                logging.StreamHandler(sys.stdout)
            ]
        )
        logging.info("="*10 + " Updater Logging Started " + "="*10)
    except Exception as e:
        
        print(f"CRITICAL: Failed to set up file logging to {log_file}: {e}", file=sys.stderr)
        logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
        logging.error("Using basic console logging due to setup error.")


def save_error_report(error_type: str, error_message: str, details: Optional[Dict[str, Any]], error_dir: Path) -> bool:
    """Saves a simplified error report to a JSON file.

    Creates the error directory if needed. Generates a unique filename
    based on timestamp and error type. Logs the error message and the
    path to the saved report.

    :param error_type: A string categorizing the error (e.g., 'BACKUP_FAILED').
    :type error_type: str
    :param error_message: A descriptive message explaining the error.
    :type error_message: str
    :param details: An optional dictionary containing additional context or data related to the error.
    :type details: Optional[Dict[str, Any]]
    :param error_dir: The directory path where the error report file should be saved.
    :type error_dir: pathlib.Path
    :return: True if the report was saved successfully, False otherwise.
    :rtype: bool
    """
    try:
        error_dir.mkdir(parents=True, exist_ok=True)
        timestamp_str = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
        filename = f"error_{timestamp_str}_{error_type.lower()}.json"
        file_path = error_dir / filename

        
        serializable_details = {}
        if details:
            for k, v in details.items():
                serializable_details[k] = str(v) if isinstance(v, Path) else v

        error_data = {
            "error_type": error_type,
            "error_message": error_message,
            "details": serializable_details,
            "timestamp": datetime.datetime.now().isoformat(),
            "stack_trace": traceback.format_exc() 
        }

        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(error_data, f, indent=2)

        logging.error(f"Error report saved to {file_path} - Type: {error_type}, Message: {error_message}")
        return True
    except Exception as e:
        logging.critical(f"Failed to save error report: {e}", exc_info=True)
        return False


def wait_for_process_termination(pid: int, timeout: int = PROCESS_WAIT_TIMEOUT) -> bool:
    """Waits for the process with the given PID to terminate.

    Uses `psutil` to monitor the process. If the process does not
    terminate within the specified timeout, it attempts to kill the process.

    :param pid: The Process ID (PID) of the process to wait for.
    :type pid: int
    :param timeout: Maximum time in seconds to wait for termination before attempting to kill.
                    Defaults to PROCESS_WAIT_TIMEOUT.
    :type timeout: int
    :return: True if the process terminated (or was already gone), False if it timed out and could not be killed.
    :rtype: bool
    """
    logging.info(f"Waiting for process PID: {pid} to terminate (timeout: {timeout}s)...")
    try:
        process = psutil.Process(pid)
        process.wait(timeout=timeout)
        logging.info(f"Process {pid} terminated.")
        return True
    except psutil.NoSuchProcess:
        logging.info(f"Process {pid} already terminated or does not exist.")
        return True
    except psutil.TimeoutExpired:
        logging.error(f"Process {pid} did not terminate within {timeout}s. Attempting to kill.")
        try:
            process.kill()
            process.wait(timeout=5) 
            if not process.is_running():
                logging.info(f"Process {pid} killed successfully.")
                return True
            else:
                logging.error(f"Failed to kill process {pid} even after attempt.")
                return False
        except Exception as kill_err:
            logging.error(f"Error killing process {pid}: {kill_err}", exc_info=True)
            return False
    except Exception as e:
        logging.error(f"Error waiting for process {pid}: {e}", exc_info=True)
        return False

def start_agent(agent_file_path: Path) -> bool:
    """Starts the agent application using its specific executable file path.

    Executes the provided agent file path using the same Python interpreter
    that the updater script is running with (if it's a .py file) or directly
    (if it's an .exe).
    Starts the agent as a detached process on Windows. The working directory
    is set to the parent directory of the agent file.
    Waits briefly and checks if the process started successfully using `psutil`.

    :param agent_file_path: The full path to the agent's main executable script (e.g., '.../main.py' or '.../agent.exe').
    :type agent_file_path: pathlib.Path
    :return: True if the agent process was started successfully and appears to be running, False otherwise.
    :rtype: bool
    """
    python_exe = sys.executable
    working_dir = agent_file_path.parent.parent

    logging.info(f"Attempting to start agent executable: {agent_file_path}")
    if not agent_file_path.exists():
        logging.error(f"Agent executable not found: {agent_file_path}")
        return False
    if not agent_file_path.is_file():
        logging.error(f"Agent path is not a file: {agent_file_path}")
        return False

    
    cmd = []
    if agent_file_path.suffix.lower() == '.py':
        cmd = [python_exe, "-m", "agent.main"]
    elif agent_file_path.suffix.lower() == '.exe':
        cmd = [str(agent_file_path)]
    else:
        logging.error(f"Unsupported agent file type: {agent_file_path.suffix}. Only .py and .exe are supported.")
        return False

    logging.info(f"Executing command: {' '.join(cmd)} in CWD: {working_dir}")

    try:
        
        
        creationflags = subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
        process = subprocess.Popen(
            cmd,                                
            cwd=str(working_dir),               
            close_fds=True,                     
            creationflags=creationflags
        )
        logging.info(f"Launched agent process with PID: {process.pid}. Checking status in {PROCESS_START_WAIT}s...")
        time.sleep(PROCESS_START_WAIT)

        
        try:
            launched_process = psutil.Process(process.pid)
            if launched_process.is_running() and launched_process.status() != psutil.STATUS_ZOMBIE:
                logging.info(f"Agent process (PID: {process.pid}) started successfully and is running.")
                return True
            else:
                status = launched_process.status() if launched_process.is_running() else "terminated"
                logging.error(f"Agent process (PID: {process.pid}) is not running correctly. Status: {status}")
                return False
        except psutil.NoSuchProcess:
            logging.error(f"Agent process (PID: {process.pid}) terminated unexpectedly after launch.")
            return False

    except Exception as e:
        logging.error(f"Failed to start agent process from {agent_file_path}: {e}", exc_info=True)
        return False


def move_path(src: Path, dst: Path, max_retries=3, delay=1) -> bool:
    """Moves a file or directory with a retry mechanism.

    Uses `shutil.move`. Retries the operation up to `max_retries` times
    with a `delay` between attempts if an `OSError` occurs.

    :param src: The source path (file or directory) to move.
    :type src: pathlib.Path
    :param dst: The destination path.
    :type dst: pathlib.Path
    :param max_retries: Maximum number of move attempts. Defaults to 3.
    :type max_retries: int
    :param delay: Delay in seconds between retry attempts. Defaults to 1.
    :type delay: int
    :return: True if the move was successful, False otherwise.
    :rtype: bool
    """
    src  = src.parent if src.is_file() else src 
    dst  = dst.parent if dst.is_file() else dst 
    for attempt in range(max_retries):
        try:
            shutil.move(str(src), str(dst))
            logging.info(f"Moved '{src}' to '{dst}'")
            return True
        except OSError as e:
            logging.warning(f"Move attempt {attempt + 1}/{max_retries} failed for '{src}' -> '{dst}': {e}")
            if attempt < max_retries - 1:
                time.sleep(delay)
            else:
                logging.error(f"Failed to move '{src}' after {max_retries} attempts.")
                return False
    return False 

def remove_path(path: Path, max_retries=3, delay=1) -> bool:
    """Removes a file or directory with a retry mechanism.

    Uses `shutil.rmtree` for directories and `os.remove` for files.
    Retries the operation up to `max_retries` times with a `delay`
    between attempts if an `OSError` occurs. Skips removal if the
    path doesn't exist initially.

    :param path: The path (file or directory) to remove.
    :type path: pathlib.Path
    :param max_retries: Maximum number of removal attempts. Defaults to 3.
    :type max_retries: int
    :param delay: Delay in seconds between retry attempts. Defaults to 1.
    :type delay: int
    :return: True if the removal was successful or the path didn't exist, False otherwise.
    :rtype: bool
    """
    if not path.exists():
        logging.info(f"Path '{path}' does not exist, skipping removal.")
        return True

    for attempt in range(max_retries):
        try:
            if path.is_dir():
                shutil.rmtree(path)
                logging.info(f"Removed directory '{path}'")
            elif path.is_file():
                os.remove(path)
                logging.info(f"Removed file '{path}'")
            else: 
                logging.warning(f"Path '{path}' exists but is neither file nor directory. Attempting os.remove.")
                os.remove(path)
                logging.info(f"Removed '{path}' (was not file/dir).")
            return True 
        except OSError as e:
            logging.warning(f"Remove attempt {attempt + 1}/{max_retries} failed for '{path}': {e}")
            if attempt < max_retries - 1:
                time.sleep(delay)
            else:
                logging.error(f"Failed to remove '{path}' after {max_retries} attempts.")
                return False
    return False 


def main():
    """Parses arguments and executes the main agent update workflow."""
    parser = argparse.ArgumentParser(
        description="Optimized Agent Updater using executable paths (Windows Only)",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    parser.add_argument("--pid", type=int, required=True, help="PID of the main agent process to wait for.")
    parser.add_argument("--new_agent", type=str, required=True, help="Path to the new agent executable file (.py or .exe). Assumes the entire containing directory should be deployed.")
    parser.add_argument("--current_agent", type=str, required=True, help="Path to the currently running agent executable file (.py or .exe). Its containing directory will be backed up and replaced.")
    parser.add_argument("--storage_dir", type=str, required=True, help="Base directory for updater storage (logs, error reports).")
    args = parser.parse_args()

    
    storage_dir = Path(args.storage_dir).resolve()
    log_dir = storage_dir / LOG_DIR
    error_dir = storage_dir / ERROR_REPORT_DIR

    try:
        current_agent_path = Path(args.current_agent).resolve(strict=True) 
        new_agent_path = Path(args.new_agent).resolve(strict=True) 
    except FileNotFoundError as e:
        print(f"ERROR: Agent executable path not found: {e}", file=sys.stderr)
        logging.error(f"Agent executable path not found: {e}")

    backup_agent_path = current_agent_path.parent.with_name(current_agent_path.parent.name + BACKUP_SUFFIX) 

    setup_updater_logging(log_dir)
    logging.info("Updater starting...")
    logging.info(f"  PID to wait: {args.pid}")
    logging.info(f"  New agent executable: {new_agent_path}")
    logging.info(f"  Current agent executable: {current_agent_path}")
    logging.info(f"  Storage: {storage_dir}")
    logging.info(f"  Backup directory target: {backup_agent_path}")


    final_exit_code = 1 
    backup_created = False

    try:
        
        if not wait_for_process_termination(args.pid):
            save_error_report("TERMINATION_FAILED", f"Agent process {args.pid} did not terminate.", {"pid": args.pid}, error_dir)
            raise SystemExit(1) 

        
        logging.info(f"Backing up current agent file '{current_agent_path}' to '{backup_agent_path}'...")
        if current_agent_path.exists():
            
            if backup_agent_path.exists():
                logging.warning(f"Removing existing backup directory: {backup_agent_path}")
                if not remove_path(backup_agent_path):
                    save_error_report("BACKUP_CLEANUP_FAILED", f"Failed to remove old backup {backup_agent_path}", {"backup_dir": backup_agent_path}, error_dir)
                    
                    
            
            if move_path(current_agent_path, backup_agent_path):
                backup_created = True
                logging.info(f"Backup of directory '{backup_agent_path}' created at '{backup_agent_path}'")
            else:
                save_error_report("BACKUP_FAILED", f"Failed to move {current_agent_path} to {backup_agent_path}", {"src": current_agent_path, "dst": backup_agent_path}, error_dir)
                raise SystemExit(1) 
        else:
            
            logging.warning(f"Current agent directory '{current_agent_path}' does not exist, cannot back up. Skipping backup.")


        
        logging.info(f"Deploying new agent from '{new_agent_path}' to '{current_agent_path}'...")
        if not new_agent_path.exists():
             
             save_error_report("DEPLOY_SOURCE_DIR_MISSING", f"New agent source not found: {new_agent_path}", {"source_dir": new_agent_path}, error_dir)
             raise SystemExit(1) 

        
        if move_path(new_agent_path, current_agent_path):
            logging.info("New agent directory deployed successfully.")
        else:
            save_error_report("DEPLOY_FAILED", f"Failed to move {new_agent_path} to {current_agent_path}", {"src": new_agent_path, "dst": current_agent_path}, error_dir)
            raise SystemExit(1) 

        
        
        
        deployed_agent_executable = current_agent_path 
        logging.info(f"Starting new agent executable: {deployed_agent_executable}...")
        if start_agent(deployed_agent_executable):
            logging.info("Update successful! New agent started.")
            final_exit_code = 0 
            
            if backup_created and backup_agent_path.exists():
                logging.info(f"Removing successful backup directory: {backup_agent_path}")
                if not remove_path(backup_agent_path):
                    logging.warning(f"Could not remove backup directory {backup_agent_path} after successful update.")
                    
        else:
            save_error_report("START_FAILED", "Failed to start the newly deployed agent.", {"agent_executable": deployed_agent_executable}, error_dir)
            logging.error("Failed to start new agent. Initiating rollback...")
            raise SystemExit(1) 

    except SystemExit as e:
        
        final_exit_code = e.code if isinstance(e.code, int) else 1
        if final_exit_code != 0: 
             logging.error("Update process failed. Attempting rollback if possible.")
             
             if backup_created and backup_agent_path.exists():
                 logging.info(f"Restoring agent directory from backup: {backup_agent_path} to {current_agent_path}...")
                 
                 
                 if current_agent_path.exists():
                     if not remove_path(current_agent_path):
                         
                         logging.critical(f"ROLLBACK FAILED: Could not remove failed deployment directory at {current_agent_path}. Manual intervention needed.")
                         save_error_report("ROLLBACK_CLEANUP_FAILED", f"Could not remove {current_agent_path} during rollback", {"target_dir": current_agent_path}, error_dir)
                         final_exit_code = 2 
                     
                     elif move_path(backup_agent_path, current_agent_path):
                         logging.info("Rollback successful. Attempting to restart old agent...")
                         
                         old_agent_executable = current_agent_path
                         if start_agent(old_agent_executable):
                             logging.info("Old agent restarted successfully after rollback.")
                         else:
                             
                             logging.error("ROLLBACK WARNING: Failed to restart the old agent after restoring backup.")
                             save_error_report("ROLLBACK_RESTART_FAILED", "Failed to restart old agent after rollback", {"agent_executable": old_agent_executable}, error_dir)
                             
                     else:
                         
                         logging.critical(f"ROLLBACK FAILED: Could not move backup {backup_agent_path} to {current_agent_path}. Manual intervention needed.")
                         save_error_report("ROLLBACK_MOVE_FAILED", f"Could not restore backup from {backup_agent_path}", {"backup_dir": backup_agent_path, "target_dir": current_agent_path}, error_dir)
                         final_exit_code = 2 
                 else:
                     
                     if move_path(backup_agent_path, current_agent_path):
                          logging.info("Rollback successful (restored backup directory). Attempting to restart old agent...")
                          old_agent_executable = current_agent_path / current_agent_path.name
                          if start_agent(old_agent_executable):
                              logging.info("Old agent restarted successfully after rollback.")
                          else:
                              logging.error("ROLLBACK WARNING: Failed to restart the old agent after restoring backup.")
                              save_error_report("ROLLBACK_RESTART_FAILED", "Failed to restart old agent after rollback", {"agent_executable": old_agent_executable}, error_dir)
                     else:
                          logging.critical(f"ROLLBACK FAILED: Could not move backup {backup_agent_path} to {current_agent_path}. Manual intervention needed.")
                          save_error_report("ROLLBACK_MOVE_FAILED", f"Could not restore backup from {backup_agent_path}", {"backup_dir": backup_agent_path, "target_dir": current_agent_path}, error_dir)
                          final_exit_code = 2

             else:
                 
                 logging.error("Rollback impossible: No backup was created or backup path not found.")
                 save_error_report("ROLLBACK_NO_BACKUP", "Cannot rollback because backup is missing.", {"backup_dir": backup_agent_path}, error_dir)
                 final_exit_code = 2 

    except Exception as e:
        
        logging.critical(f"Unexpected critical error during update: {e}", exc_info=True)
        save_error_report("CRITICAL_ERROR", f"An unexpected error occurred: {e}", {}, error_dir)
        
        if backup_created and backup_agent_path.exists():
             logging.warning("Attempting emergency rollback due to critical error...")
             if current_agent_path.exists(): remove_path(current_agent_path) 
             if move_path(backup_agent_path, current_agent_path):
                 logging.info("Emergency rollback successful. Attempting restart.")
                 old_agent_executable = current_agent_path / current_agent_path.name
                 start_agent(old_agent_executable) 
             else:
                 logging.critical("EMERGENCY ROLLBACK FAILED.")
                 save_error_report("EMERGENCY_ROLLBACK_FAILED", "Emergency rollback failed", {"backup_dir": backup_agent_path, "target_dir": current_agent_path}, error_dir)
        final_exit_code = 2 

    finally:
        
        logging.info(f"Updater process finished with exit code {final_exit_code}.")
        sys.exit(final_exit_code)

if __name__ == "__main__":
    
    if psutil is None:
        
        print("Error: psutil library is required. Please install it (`pip install psutil`)", file=sys.stderr)
        sys.exit(3) 
    main()
