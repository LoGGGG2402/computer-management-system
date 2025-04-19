"""
Manages the agent's lock file to ensure only one instance runs.
Phase 2: Uses file locking, PID, and timestamp checks.
"""
import os
import atexit
import msvcrt
import threading
import psutil
from datetime import datetime, timezone, timedelta
from typing import Optional, Tuple
from agent.utils import get_logger

logger = get_logger(__name__)

LOCK_STALE_TIMEOUT_SECONDS = 120


class LockManager:
    """
    Manages the agent.lock file for single instance control using file locking,
    PID, and timestamp checks. Includes stale lock detection and timestamp updates.
    """

    def __init__(self, storage_path: str):
        """
        Initializes the LockManager.
        
        :param storage_path: Directory path for storing the lock file
        :type storage_path: str
        :raises ValueError: If storage_path is invalid
        """
        if not storage_path or not os.path.isdir(storage_path):
            raise ValueError(f"Invalid storage path provided to LockManager: {storage_path}")
        if not psutil:
            logger.warning("psutil library not found. Stale lock detection based on PID will be skipped.")

        self.lock_file_path = os.path.join(storage_path, "agent.lock")
        self._lock_fd = None
        self._updater_thread: Optional[threading.Thread] = None
        self._stop_event = threading.Event()
        logger.debug(f"LockManager initialized. Lock file path: {self.lock_file_path}")

    def _read_lock_content(self, fd) -> Tuple[Optional[int], Optional[datetime]]:
        """
        Reads PID and Timestamp from the lock file descriptor.
        
        :param fd: File descriptor to read from
        :return: Tuple of (PID, timestamp) or (None, None) if read fails
        :rtype: Tuple[Optional[int], Optional[datetime]]
        """
        try:
            os.lseek(fd, 0, os.SEEK_SET)
            content_bytes = os.read(fd, 100)
            content = content_bytes.decode('utf-8').strip()
            parts = content.split('|', 1)
            if len(parts) == 2:
                pid_str, timestamp_str = parts
                pid = int(pid_str)
                timestamp = datetime.fromisoformat(timestamp_str)
                return pid, timestamp
            else:
                logger.warning(f"Invalid content format in lock file: {content!r}")
                return None, None
        except (OSError, ValueError, TypeError) as e:
            logger.error(f"Error reading or parsing lock file content: {e}")
            return None, None

    def _write_lock_content(self, fd) -> bool:
        """
        Writes current PID and Timestamp to the lock file descriptor.
        
        :param fd: File descriptor to write to
        :return: True if write succeeded, False otherwise
        :rtype: bool
        """
        try:
            pid = os.getpid()
            timestamp_iso = datetime.now(timezone.utc).isoformat()
            content = f"{pid}|{timestamp_iso}"
            content_bytes = content.encode('utf-8')

            os.lseek(fd, 0, os.SEEK_SET)
            bytes_written = os.write(fd, content_bytes)
            os.ftruncate(fd, bytes_written)
            os.fsync(fd)
            logger.debug(f"Wrote PID {pid} and timestamp {timestamp_iso} to lock file.")
            return True
        except OSError as e:
            logger.error(f"Failed to write PID/Timestamp to lock file {self.lock_file_path}: {e}")
            return False

    def acquire(self) -> bool:
        """
        Attempts to acquire the lock using atomic creation and file locking.
        Includes stale lock detection (PID check, timestamp check).

        :return: True if the lock was acquired successfully, False otherwise
        :rtype: bool
        """
        if self._lock_fd is not None:
            logger.warning("Acquire called when lock is already held.")
            return True

        try:
            fd = os.open(self.lock_file_path, os.O_CREAT | os.O_EXCL | os.O_RDWR)
            try:
                msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
                logger.debug("Acquired lock via atomic creation.")
                self._lock_fd = fd
                if not self._write_lock_content(fd):
                    self.release()
                    return False
                self._start_timestamp_updater()
                try:
                    atexit.register(self.release)
                    logger.debug("Registered lock release via atexit.")
                except Exception as reg_err:
                    logger.error(f"Failed to register atexit handler for lock release: {reg_err}")
                    self.release()
                    return False
                logger.info(f"Successfully acquired lock file: {self.lock_file_path}")
                return True
            except IOError:
                logger.critical(f"Created lock file {self.lock_file_path} but failed to acquire msvcrt lock immediately. This shouldn't happen.")
                os.close(fd)
                try:
                    os.remove(self.lock_file_path)
                except OSError:
                    pass
                return False
            except Exception as e:
                logger.critical(f"Unexpected error during initial lock acquisition: {e}", exc_info=True)
                os.close(fd)
                try:
                    os.remove(self.lock_file_path)
                except OSError:
                    pass
                return False

        except FileExistsError:
            logger.info(f"Lock file {self.lock_file_path} exists. Checking for staleness...")
            try:
                fd = os.open(self.lock_file_path, os.O_RDWR)
            except OSError as e:
                logger.error(f"Failed to open existing lock file {self.lock_file_path} for checking: {e}")
                return False

            try:
                msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
                logger.warning(f"Acquired lock on existing file {self.lock_file_path}. Checking PID/Timestamp.")
                pid, timestamp = self._read_lock_content(fd)

                is_stale = False
                if pid is None or timestamp is None:
                    logger.warning("Could not read valid PID/Timestamp from existing lock file. Assuming stale.")
                    is_stale = True
                else:
                    pid_exists = psutil.pid_exists(pid) if psutil else True
                    timestamp_age = datetime.now(timezone.utc) - timestamp
                    logger.debug(f"Lock file check: PID={pid} (Exists: {pid_exists}), Timestamp={timestamp} (Age: {timestamp_age})")

                    if not pid_exists:
                        logger.warning(f"Detected stale lock: PID {pid} does not exist.")
                        is_stale = True
                    elif timestamp_age > timedelta(seconds=LOCK_STALE_TIMEOUT_SECONDS):
                        logger.warning(f"Detected stale lock: Timestamp {timestamp} is older than {LOCK_STALE_TIMEOUT_SECONDS}s.")
                        is_stale = True

                if is_stale:
                    logger.info("Stale lock detected. Attempting to take over.")
                    self._lock_fd = fd
                    if not self._write_lock_content(fd):
                        self.release()
                        return False
                    self._start_timestamp_updater()
                    try:
                        atexit.register(self.release)
                        logger.debug("Registered lock release via atexit (stale lock takeover).")
                    except Exception as reg_err:
                        logger.error(f"Failed to register atexit handler for lock release (stale lock): {reg_err}")
                        self.release()
                        return False
                    logger.info(f"Successfully acquired stale lock file: {self.lock_file_path}")
                    return True
                else:
                    logger.critical(f"Lock file {self.lock_file_path} held by running process PID {pid}. Cannot acquire.")
                    msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)
                    os.close(fd)

                    return False

            except IOError:
                logger.critical(f"Lock file {self.lock_file_path} is actively locked by another process.")
                os.close(fd)

                return False
            except Exception as e:
                logger.critical(f"Unexpected error checking existing lock file: {e}", exc_info=True)
                try:
                    msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)
                except (IOError, OSError):
                    pass
                os.close(fd)
                return False

        except PermissionError as e:
            logger.critical(f"Permission denied creating/accessing lock file {self.lock_file_path}: {e}")

            return False
        except OSError as e:
            logger.critical(f"OS error creating/accessing lock file {self.lock_file_path}: {e}")

            return False
        except Exception as e:
            logger.critical(f"Unexpected error acquiring lock file {self.lock_file_path}: {e}", exc_info=True)

            return False

    def release(self):
        """
        Releases the lock by stopping the updater, closing, and deleting the lock file.
        Relies on os.close() to implicitly release the msvcrt lock.
        Safe to call even if the lock wasn't acquired or already released.
        """
        logger.debug("Release lock requested.")
        self._stop_timestamp_updater()

        try:
            atexit.unregister(self.release)
            logger.debug("Unregistered lock release from atexit.")
        except (AttributeError, ValueError):
            pass

        if self._lock_fd is not None:
            fd = self._lock_fd
            self._lock_fd = None

            try:
                os.close(fd)
                logger.debug(f"Closed lock file descriptor for {self.lock_file_path}. Lock should be implicitly released.")
            except OSError as e:
                logger.error(f"Error closing lock file descriptor {self.lock_file_path}: {e}")
        else:
            logger.debug("Release called but no active lock file descriptor.")

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

    def _start_timestamp_updater(self):
        """
        Starts the background thread to update the lock file timestamp.
        """
        if self._updater_thread is None or not self._updater_thread.is_alive():
            self._stop_event.clear()
            self._updater_thread = threading.Thread(target=self._timestamp_update_loop, daemon=True)
            self._updater_thread.start()
            logger.debug("Timestamp updater thread started.")

    def _stop_timestamp_updater(self):
        """
        Signals the timestamp updater thread to stop and waits for it.
        """
        if self._updater_thread and self._updater_thread.is_alive():
            logger.debug("Stopping timestamp updater thread...")
            self._stop_event.set()
            self._updater_thread.join(timeout=5.0)
            if self._updater_thread.is_alive():
                logger.warning("Timestamp updater thread did not stop gracefully.")
            else:
                logger.debug("Timestamp updater thread stopped.")
            self._updater_thread = None

    def _timestamp_update_loop(self):
        """
        Periodically updates the timestamp in the locked file.
        """
        logger.debug("Timestamp update loop running.")
        update_interval = max(15, LOCK_STALE_TIMEOUT_SECONDS // 2)

        while not self._stop_event.wait(update_interval):
            if self._lock_fd is None:
                logger.warning("Timestamp update loop: Lock FD is None. Stopping loop.")
                break
            try:
                msvcrt.locking(self._lock_fd, msvcrt.LK_LOCK, 1)
                logger.debug("Timestamp update loop: Acquired lock.")
                try:
                    self._write_lock_content(self._lock_fd)
                finally:
                    msvcrt.locking(self._lock_fd, msvcrt.LK_UNLCK, 1)
                    logger.debug("Timestamp update loop: Released lock.")
            except (IOError, OSError) as e:
                logger.error(f"Error during timestamp update: {e}. Stopping loop.")
                break
            except Exception as e:
                logger.error(f"Unexpected error during timestamp update: {e}. Stopping loop.", exc_info=True)
                break
        logger.debug("Timestamp update loop finished.")

