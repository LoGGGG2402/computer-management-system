"""
System monitor module for tracking system resource usage.
"""
import platform
import psutil
import socket
import json
import subprocess
from typing import Dict, Any
from ..utils import get_logger

logger = get_logger(__name__)

class SystemMonitor:
    """
    Class responsible for monitoring system resources and hardware.
    
    This class provides functionality to collect system metrics including CPU, RAM, and disk usage.
    It also gathers hardware information such as OS details, CPU and GPU information, total RAM,
    disk space, and network configuration. The implementation is optimized for Windows operating
    systems and uses platform-specific APIs like WMI for hardware detection.
    
    Key capabilities:
    - Real-time system resource usage monitoring (CPU, RAM, disk)
    - Hardware inventory collection including GPU detection
    - System identification through network and platform information
    
    All operations are designed to fail gracefully with appropriate logging when
    hardware information cannot be retrieved.
    """

    def __init__(self):
        """
        Initialize the system monitor.
        """
        logger.debug("SystemMonitor initialized for Windows")

    def get_usage_stats(self) -> Dict[str, float]:
        """
        Gets current CPU, RAM, and system drive (C:) usage percentages.

        :return: Dictionary with usage percentages
        :rtype: Dict[str, float]
        """
        stats = {"cpu": 0.0, "ram": 0.0, "disk": 0.0}
        try:
            stats["cpu"] = psutil.cpu_percent(interval=0.1)

            virtual_mem = psutil.virtual_memory()
            stats["ram"] = virtual_mem.percent

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

        logger.debug(f"System usage stats collected: {stats}")
        return stats

    def _get_gpu_info(self) -> str:
        """
        Attempts to detect the primary GPU model name using WMI on Windows.
        
        :return: GPU model name or "N/A" if detection fails
        :rtype: str
        """
        gpu_info = "N/A"
        logger.debug("Attempting GPU detection on Windows")

        try:
            command = ["wmic", "path", "win32_VideoController", "get", "name"]
            result = subprocess.run(
                command,
                capture_output=True,
                text=True,
                check=False,
                creationflags=subprocess.CREATE_NO_WINDOW
            )
            output = result.stdout.strip()
            if result.returncode == 0 and output:
                lines = output.splitlines()
                if len(lines) > 1 and lines[1].strip():
                    gpu_info = lines[1].strip()
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

        :return: Dictionary containing hardware details
        :rtype: Dict[str, Any]
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
            windows_version = platform.version()
            windows_edition = platform.win32_edition() if hasattr(platform, 'win32_edition') else ""
            hardware_info["os_info"] = f"Windows {platform.release()} {windows_edition} ({windows_version})"
            
            hardware_info["cpu_info"] = platform.processor() if platform.processor() else "N/A"
            
            hardware_info["gpu_info"] = self._get_gpu_info()
            
            hardware_info["total_ram"] = psutil.virtual_memory().total
            hardware_info["total_disk_space"] = psutil.disk_usage('C:\\').total
            
            hardware_info["ip_address"] = socket.gethostbyname(socket.gethostname())

        except Exception as e:
            logger.error(f"Unexpected error collecting hardware information: {e}", exc_info=True)

        logger.info("Hardware information collection completed.")
        logger.debug(f"Hardware Info: {json.dumps(hardware_info, indent=2)}")
        return hardware_info
