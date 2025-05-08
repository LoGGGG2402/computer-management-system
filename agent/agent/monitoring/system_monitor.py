"""
System monitoring functionality for tracking computer resources.
"""
import json


import subprocess
import psutil
import socket
from typing import Dict, Any

from agent.utils import get_logger

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
            
            logger.warning(f"WMIC command for GPU detection not found: {e.filename}")
        except Exception as e: 
            logger.error(f"Error getting GPU info: {e}", exc_info=True)

        logger.debug(f"Detected GPU info: {gpu_info}")
        return gpu_info

    def get_hardware_info(self) -> Dict[str, Any]:
        """
        Gathers basic hardware and network information for Windows systems using WMIC and psutil.

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
            
            try:
                result_os = subprocess.run(
                    ["wmic", "os", "get", "Caption,Version"],
                    capture_output=True, text=True, check=False, creationflags=subprocess.CREATE_NO_WINDOW
                )
                if result_os.returncode == 0 and result_os.stdout:
                    lines_os = result_os.stdout.strip().splitlines()
                    if len(lines_os) > 1 and lines_os[1].strip():
                        hardware_info["os_info"] = lines_os[1].strip()
                    else: 
                        logger.warning("Could not parse OS details from WMIC. Using generic 'Windows'.")
                        hardware_info["os_info"] = "Windows"
                else: 
                    logger.warning(f"WMIC command for OS info failed or returned empty. Using generic 'Windows'. Stderr: {result_os.stderr.strip()}")
                    hardware_info["os_info"] = "Windows"
            except Exception as e_wmic_os: 
                logger.error(f"Error getting OS info via WMIC: {e_wmic_os}. Using generic 'Windows'.", exc_info=True)
                hardware_info["os_info"] = "Windows"

            
            try:
                result_cpu = subprocess.run(
                    ["wmic", "cpu", "get", "name"],
                    capture_output=True, text=True, check=False, creationflags=subprocess.CREATE_NO_WINDOW
                )
                if result_cpu.returncode == 0 and result_cpu.stdout:
                    lines_cpu = result_cpu.stdout.strip().splitlines()
                    if len(lines_cpu) > 1 and lines_cpu[1].strip():
                        hardware_info["cpu_info"] = lines_cpu[1].strip()
                    else: 
                        logger.warning("Could not parse CPU info from WMIC.")
                else: 
                     logger.warning(f"WMIC command for CPU info failed or returned empty. Stderr: {result_cpu.stderr.strip()}")
            except Exception as e_wmic_cpu: 
                logger.error(f"Error getting CPU info via WMIC: {e_wmic_cpu}", exc_info=True)

            hardware_info["gpu_info"] = self._get_gpu_info()
            
            hardware_info["total_ram"] = psutil.virtual_memory().total
            
            try:
                hardware_info["total_disk_space"] = psutil.disk_usage('C:\\').total
            except FileNotFoundError: 
                logger.warning("System drive C: not found for total disk space. Returning 0.")
                hardware_info["total_disk_space"] = 0
            except Exception as e_disk_total: 
                logger.error(f"Error getting total disk space for C:: {e_disk_total}", exc_info=True)
                hardware_info["total_disk_space"] = 0
            
            try:
                hardware_info["ip_address"] = socket.gethostbyname(socket.gethostname())
            except socket.gaierror: 
                logger.warning("Could not determine IP address (hostname not resolvable).")
                hardware_info["ip_address"] = "N/A"

        except Exception as e: 
            logger.error(f"Unexpected error collecting hardware information: {e}", exc_info=True)

        logger.info("Hardware information collection completed.")
        logger.debug(f"Hardware Info: {json.dumps(hardware_info, indent=2)}")
        return hardware_info
