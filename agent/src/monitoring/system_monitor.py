# -*- coding: utf-8 -*-
"""
System monitoring module providing functions to gather system resource usage
and basic hardware information.
Windows-only implementation.
"""
import psutil
import platform
import socket
import subprocess
import json
from typing import Dict, Any
import os

# Import the get_logger function instead of using logging directly
from src.utils.logger import get_logger

# Use the centralized logger configuration
logger = get_logger(__name__)

class SystemMonitor:
    """
    Class responsible for monitoring system resources and hardware.
    Optimized for Windows-only operation.
    """

    def __init__(self):
        """Initialize the system monitor."""
        logger.debug("SystemMonitor initialized for Windows")
        # Pre-fetch static info if needed, but currently fetched on demand

    def get_usage_stats(self) -> Dict[str, float]:
        """
        Gets current CPU, RAM, and system drive (C:) usage percentages.

        Returns:
            Dict[str, float]: Dictionary with 'cpu', 'ram', 'disk' usage percentages.
                              Returns 0.0 for metrics that cannot be retrieved.
        """
        stats = {"cpu": 0.0, "ram": 0.0, "disk": 0.0}
        try:
            # CPU Usage (non-blocking, short interval)
            stats["cpu"] = psutil.cpu_percent(interval=0.1)

            # RAM Usage
            virtual_mem = psutil.virtual_memory()
            stats["ram"] = virtual_mem.percent

            # Disk Usage (System drive C:)
            try:
                disk_usage_obj = psutil.disk_usage('C:\\')
                stats["disk"] = disk_usage_obj.percent
            except FileNotFoundError:
                logger.warning("System drive C: not found for disk usage stats. Returning 0.0.")
                stats["disk"] = 0.0
            except Exception as e_disk:
                logger.error(f"Error getting disk usage stats for C:: {e_disk}", exc_info=True)
                stats["disk"] = 0.0

        except Exception as e:
            logger.error(f"Error collecting system usage stats: {e}", exc_info=True)
            # Return partial stats if possible, or the defaults (0.0)

        logger.debug(f"System usage stats collected: {stats}")
        return stats

    def _get_gpu_info(self) -> str:
        """
        Attempts to detect the primary GPU model name using WMI on Windows.
        
        Returns:
            str: GPU model name or "N/A" if detection fails.
        """
        gpu_info = "N/A"
        logger.debug("Attempting GPU detection on Windows")

        try:
            # Use WMIC to get video controller names
            # CREATE_NO_WINDOW flag prevents console window popup
            command = ["wmic", "path", "win32_VideoController", "get", "name"]
            result = subprocess.run(
                command,
                capture_output=True,
                text=True,
                check=False, # Don't raise error if command fails
                creationflags=subprocess.CREATE_NO_WINDOW  # Windows-specific flag
            )
            output = result.stdout.strip()
            if result.returncode == 0 and output:
                # WMIC output has a header line ("Name"), then names
                lines = output.splitlines()
                if len(lines) > 1 and lines[1].strip():
                    gpu_info = lines[1].strip() # Get the first GPU listed
            else:
                logger.warning(f"WMIC command for GPU failed or returned empty. Code: {result.returncode}, Stderr: {result.stderr.strip()}")

        except FileNotFoundError as e:
            logger.warning(f"Command for GPU detection not found: {e.filename}")
        except Exception as e:
            logger.error(f"Error getting GPU info: {e}", exc_info=True)

        logger.debug(f"Detected GPU info: {gpu_info}")
        return gpu_info

    def get_hardware_info(self) -> Dict[str, Any]:
        """
        Gathers basic hardware and network information for Windows systems.

        Returns:
            Dict[str, Any]: Dictionary containing hardware details.
        """
        logger.debug("Collecting hardware information...")
        hardware_info: Dict[str, Any] = {
            "os_info": "N/A",
            "cpu_info": "N/A",
            "gpu_info": "N/A",
            "total_ram": 0,
            "total_disk_space": 0,
            "ip_address": "N/A",
        }

        try:
            # OS Info - More detailed for Windows
            windows_version = platform.version()
            windows_edition = platform.win32_edition() if hasattr(platform, 'win32_edition') else ""
            hardware_info["os_info"] = f"Windows {platform.release()} {windows_edition} ({windows_version})"
            
            # CPU Info
            hardware_info["cpu_info"] = platform.processor() if platform.processor() else "N/A"
            
            # GPU Info
            hardware_info["gpu_info"] = self._get_gpu_info()
            
            # Memory and Storage
            hardware_info["total_ram"] = psutil.virtual_memory().total
            hardware_info["total_disk_space"] = psutil.disk_usage('C:\\').total
            
            # Network
            hardware_info["ip_address"] = socket.gethostbyname(socket.gethostname())

        except Exception as e:
            logger.error(f"Unexpected error collecting hardware information: {e}", exc_info=True)
            # Return partially filled dict

        logger.info("Hardware information collection completed.")
        # Use json.dumps for potentially cleaner debug output of the dict
        logger.debug(f"Hardware Info: {json.dumps(hardware_info, indent=2)}")
        return hardware_info
