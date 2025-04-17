# -*- coding: utf-8 -*-
"""
Manages the agent's lock file to ensure only one instance runs.
Phase 1: Basic existence check and PID writing.
"""
import os
import logging
import sys
import time # For potential future timestamp checks

logger = logging.getLogger(__name__)

class LockManager:
    """Manages the agent.lock file for single instance control."""

    def __init__(self, storage_path: str):
        """
        Initializes the LockManager.

        Args:
            storage_path (str): The absolute path to the agent's storage directory.
        """
        if not storage_path or not os.path.isdir(storage_path):
             # This should ideally be validated before LockManager is created
             raise ValueError(f"Invalid storage path provided to LockManager: {storage_path}")
        self.lock_file_path = os.path.join(storage_path, "agent.lock")
        self._lock_fd = None # File descriptor
        logger.debug(f"LockManager initialized. Lock file path: {self.lock_file_path}")

    def acquire(self) -> bool:
        """
        Attempts to acquire the lock by creating the lock file exclusively.
        Writes the current PID to the file.

        Returns:
            bool: True if the lock was acquired successfully, False otherwise.
        """
        if self._lock_fd is not None:
             logger.warning("Acquire called when lock is already held.")
             return True # Already acquired

        try:
            # O_CREAT: Create the file if it doesn't exist.
            # O_EXCL: Error if the file already exists (atomic check).
            # O_RDWR: Open for reading and writing.
            # Use low-level os.open for atomic creation check
            fd = os.open(self.lock_file_path, os.O_CREAT | os.O_EXCL | os.O_RDWR)
            self._lock_fd = fd
            logger.info(f"Successfully acquired lock file: {self.lock_file_path}")

            # Write current PID to the lock file
            pid = str(os.getpid()).encode('utf-8')
            try:
                 os.write(self._lock_fd, pid)
                 os.fsync(self._lock_fd) # Ensure it's written to disk
                 logger.debug(f"Wrote PID {os.getpid()} to lock file.")
            except OSError as e:
                 logger.error(f"Failed to write PID to lock file {self.lock_file_path}: {e}")
                 # Release the lock if PID write fails, as it's inconsistent
                 self.release()
                 return False

            return True

        except FileExistsError:
            logger.critical(f"Lock file {self.lock_file_path} already exists. Another instance may be running.")
            # In Phase 1, we don't check PID/staleness yet. Just fail.
            # TODO Phase 2: Add stale lock check (PID exists? Timestamp?)
            print(f"ERROR: Lock file {self.lock_file_path} exists. Is another agent instance running?", file=sys.stderr)
            self._lock_fd = None # Ensure fd is None if acquire fails
            return False
        except PermissionError as e:
            logger.critical(f"Permission denied creating lock file {self.lock_file_path}: {e}")
            print(f"FATAL: Permission denied for lock file at {self.lock_file_path}. Check permissions.", file=sys.stderr)
            self._lock_fd = None
            return False
        except OSError as e:
            logger.critical(f"Failed to create or open lock file {self.lock_file_path}: {e}")
            print(f"FATAL: Could not create lock file {self.lock_file_path}. Error: {e}", file=sys.stderr)
            self._lock_fd = None
            return False
        except Exception as e: # Catch any other unexpected errors
            logger.critical(f"Unexpected error acquiring lock file {self.lock_file_path}: {e}", exc_info=True)
            print(f"FATAL: Unexpected error acquiring lock file {self.lock_file_path}. Error: {e}", file=sys.stderr)
            self._lock_fd = None
            return False

    def release(self):
        """
        Releases the lock by closing the file descriptor and deleting the lock file.
        Safe to call even if the lock wasn't acquired or already released.
        """
        if self._lock_fd is not None:
            try:
                os.close(self._lock_fd)
                logger.debug(f"Closed lock file descriptor for {self.lock_file_path}.")
            except OSError as e:
                logger.error(f"Error closing lock file descriptor {self.lock_file_path}: {e}")
            finally:
                 self._lock_fd = None # Mark as closed even if close failed

        # Attempt to remove the lock file if it exists
        try:
            if os.path.exists(self.lock_file_path):
                os.remove(self.lock_file_path)
                logger.info(f"Removed lock file: {self.lock_file_path}")
        except PermissionError as e:
             logger.error(f"Permission denied removing lock file {self.lock_file_path}: {e}")
        except OSError as e:
            logger.error(f"Error removing lock file {self.lock_file_path}: {e}")
        except Exception as e:
             logger.error(f"Unexpected error removing lock file {self.lock_file_path}: {e}", exc_info=True)

    def __del__(self):
        """Ensure release is called when the object is garbage collected."""
        self.release()

