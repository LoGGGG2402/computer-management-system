# -*- coding: utf-8 -*-
"""
System monitoring module providing functions to gather system resource usage
and basic hardware information.
(No changes needed for dependency injection refactor)
"""
import psutil
import platform
import socket
import subprocess
import logging
import json
from typing import Dict, Any

logger = logging.getLogger(__name__)

class SystemMonitor:
    """
    Class responsible for monitoring system resources and hardware.
    """

    def __init__(self):
        """Initialize the system monitor."""
        logger.debug("SystemMonitor initialized")
        # Pre-fetch static info if needed, but currently fetched on demand

    def get_usage_stats(self) -> Dict[str, float]:
        """
        Gets current CPU, RAM, and root disk usage percentages.

        Returns:
            Dict[str, float]: Dictionary with 'cpu', 'ram', 'disk' usage percentages.
                              Returns 0.0 for metrics that cannot be retrieved.
        """
        stats = {"cpu": 0.0, "ram": 0.0, "disk": 0.0}
        try:
            # CPU Usage (non-blocking, short interval)
            # Consider making interval configurable if needed
            stats["cpu"] = psutil.cpu_percent(interval=0.1)

            # RAM Usage
            virtual_mem = psutil.virtual_memory()
            stats["ram"] = virtual_mem.percent

            # Disk Usage (Root partition '/')
            # Consider making the partition path configurable
            try:
                disk_usage_obj = psutil.disk_usage('/')
                stats["disk"] = disk_usage_obj.percent
            except FileNotFoundError:
                 logger.warning("Root partition '/' not found for disk usage stats. Returning 0.0.")
                 stats["disk"] = 0.0
            except Exception as e_disk:
                 logger.error(f"Error getting disk usage stats for '/': {e_disk}", exc_info=True)
                 stats["disk"] = 0.0


        except Exception as e:
            logger.error(f"Error collecting system usage stats: {e}", exc_info=True)
            # Return partial stats if possible, or the defaults (0.0)

        logger.debug(f"System usage stats collected: {stats}")
        return stats

    def _get_gpu_info(self) -> str:
        """
        Attempts to detect the primary GPU model name using platform-specific commands.
        This is a best-effort approach and may not work on all systems or configurations.

        Returns:
            str: GPU model name or "N/A" if detection fails or is not supported.
        """
        gpu_info = "N/A"
        system = platform.system()
        logger.debug(f"Attempting GPU detection on system: {system}")

        try:
            if system == "Windows":
                # Use WMIC to get video controller names
                # CREATE_NO_WINDOW flag prevents console window popup
                command = ["wmic", "path", "win32_VideoController", "get", "name"]
                result = subprocess.run(
                    command,
                    capture_output=True,
                    text=True,
                    check=False, # Don't raise error if command fails
                    creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0) # Use 0 if flag not available (non-Windows)
                )
                output = result.stdout.strip()
                if result.returncode == 0 and output:
                    # WMIC output has a header line ("Name"), then names
                    lines = output.splitlines()
                    if len(lines) > 1 and lines[1].strip():
                        gpu_info = lines[1].strip() # Get the first GPU listed
                else:
                     logger.warning(f"WMIC command for GPU failed or returned empty. Code: {result.returncode}, Stderr: {result.stderr.strip()}")


            elif system == "Darwin": # macOS
                # Use system_profiler for display info
                command = ["system_profiler", "SPDisplaysDataType"]
                result = subprocess.run(command, capture_output=True, text=True, check=False)
                output = result.stdout
                if result.returncode == 0 and output:
                    # Look for "Chipset Model:"
                    for line in output.splitlines():
                        line_stripped = line.strip()
                        if line_stripped.startswith("Chipset Model:"):
                            gpu_info = line_stripped.split("Chipset Model:", 1)[1].strip()
                            break # Take the first one found
                else:
                     logger.warning(f"system_profiler command for GPU failed or returned empty. Code: {result.returncode}, Stderr: {result.stderr.strip()}")


            elif system == "Linux":
                # Use lspci (requires pciutils package)
                try:
                    # Simpler grep for VGA or 3D controller
                    command = "lspci | grep -i -E 'vga|3d controller'"
                    result = subprocess.run(command, shell=True, capture_output=True, text=True, check=True) # Check=True will raise error if lspci fails or no match
                    output = result.stdout.strip()
                    if output:
                        # Example output: 01:00.0 VGA compatible controller: NVIDIA Corporation GP107 [GeForce GTX 1050 Ti] (rev a1)
                        # Try to extract the name part after the second colon
                        first_line = output.splitlines()[0] # Take first matching device
                        parts = first_line.split(':', 2) # Split max 2 times
                        if len(parts) > 2:
                            gpu_info_raw = parts[2].strip()
                            # Further refinement to extract name in brackets or after vendor
                            if '[' in gpu_info_raw and ']' in gpu_info_raw:
                                gpu_info = gpu_info_raw[gpu_info_raw.find('[')+1:gpu_info_raw.find(']')]
                            else:
                                # Simple extraction (might include vendor)
                                gpu_info = gpu_info_raw.split('(')[0].strip() # Remove revision etc.
                        else:
                             gpu_info = first_line # Fallback to full line if split fails
                except FileNotFoundError:
                     logger.warning("lspci command not found. Cannot detect GPU info on Linux. Is 'pciutils' installed?")
                except subprocess.CalledProcessError:
                     logger.warning("lspci command failed or no VGA/3D device found via grep.")
                except Exception as e_lspci:
                     logger.error(f"Error running lspci for GPU info: {e_lspci}")

        except FileNotFoundError as e:
             logger.warning(f"Command for GPU detection not found: {e.filename}")
        except Exception as e:
            logger.error(f"Error getting GPU info on {system}: {e}", exc_info=True)

        logger.debug(f"Detected GPU info: {gpu_info}")
        return gpu_info

    def get_hardware_info(self) -> Dict[str, Any]:
        """
        Gathers basic hardware and network information.

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
            # OS Info
            hardware_info["os_info"] = f"{platform.release()}"
            hardware_info["cpu_info"] = platform.processor() if platform.processor() else "N/A"
            hardware_info["gpu_info"] = self._get_gpu_info()
            hardware_info["total_ram"] = psutil.virtual_memory().total
            hardware_info["total_disk_space"] = psutil.disk_usage('/').total
            hardware_info["ip_address"] = socket.gethostbyname(socket.gethostname())

        except Exception as e:
            logger.error(f"Unexpected error collecting hardware information: {e}", exc_info=True)
            # Return partially filled dict

        logger.info("Hardware information collection completed.")
        # Use json.dumps for potentially cleaner debug output of the dict
        logger.debug(f"Hardware Info: {json.dumps(hardware_info, indent=2)}")
        return hardware_info
