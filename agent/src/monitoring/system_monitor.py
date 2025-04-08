"""
System monitoring module for the Computer Management System Agent.
This module provides functionality to monitor various system resources.
"""
import psutil
import platform
import time
from typing import Dict, Any

from src.utils.utils import format_bytes
from src.utils.logger import get_logger

# Get logger for this module
logger = get_logger(__name__)

class SystemMonitor:
    """
    Class responsible for monitoring system resources.
    """
    
    def __init__(self):
        """Initialize the system monitor."""
        logger.debug("SystemMonitor initialized")
    
    def get_system_info(self) -> Dict[str, Any]:
        """
        Get basic system information.
        
        Returns:
            Dict with system information
        """
        system_info = {
            "system": platform.system(),
            "node_name": platform.node(),
            "release": platform.release(),
            "version": platform.version(),
            "machine": platform.machine(),
            "processor": platform.processor(),
        }
        logger.debug(f"System info: {system_info}")
        return system_info
    
    def get_cpu_info(self) -> Dict[str, Any]:
        """
        Get CPU usage information.
        
        Returns:
            Dict with CPU usage information
        """
        cpu_info = {
            "cpu_percent": psutil.cpu_percent(interval=1),
            "cpu_count": psutil.cpu_count(logical=True),
        }
        logger.debug(f"CPU info: {cpu_info}")
        return cpu_info
    
    def get_memory_info(self) -> Dict[str, Any]:
        """
        Get memory usage information.
        
        Returns:
            Dict with memory usage information
        """
        memory = psutil.virtual_memory()
        memory_info = {
            "total": format_bytes(memory.total),
            "available": format_bytes(memory.available),
            "used": format_bytes(memory.used),
            "percent": memory.percent,
        }
        logger.debug(f"Memory info: {memory_info}")
        return memory_info
    
    def get_disk_info(self) -> Dict[str, Any]:
        """
        Get disk usage information.
        
        Returns:
            Dict with disk usage information
        """
        disk = psutil.disk_usage('/')
        disk_info = {
            "total": format_bytes(disk.total),
            "used": format_bytes(disk.used),
            "free": format_bytes(disk.free),
            "percent": disk.percent,
        }
        logger.debug(f"Disk info: {disk_info}")
        return disk_info
    
    def get_network_info(self) -> Dict[str, Any]:
        """
        Get network information.
        
        Returns:
            Dict with network information
        """
        network = psutil.net_io_counters()
        network_info = {
            "bytes_sent": format_bytes(network.bytes_sent),
            "bytes_recv": format_bytes(network.bytes_recv),
            "packets_sent": network.packets_sent,
            "packets_recv": network.packets_recv,
        }
        logger.debug(f"Network info: {network_info}")
        return network_info
    
    def get_process_info(self, top_n: int = 10) -> Dict[str, Any]:
        """
        Get information about running processes.
        
        Args:
            top_n (int): Number of top processes to return
            
        Returns:
            Dict with process information
        """
        processes = []
        for proc in psutil.process_iter(attrs=['pid', 'name', 'cpu_percent', 'memory_info']):
            try:
                processes.append(proc.info)
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        processes = sorted(processes, key=lambda p: p['cpu_percent'], reverse=True)[:top_n]
        logger.debug(f"Top {top_n} processes: {processes}")
        return {"processes": processes}
    
    def get_all_stats(self) -> Dict[str, Any]:
        """
        Get all system statistics in one call.
        
        Returns:
            Dict with all system statistics
        """
        all_stats = {
            "system_info": self.get_system_info(),
            "cpu_info": self.get_cpu_info(),
            "memory_info": self.get_memory_info(),
            "disk_info": self.get_disk_info(),
            "network_info": self.get_network_info(),
            "process_info": self.get_process_info(),
        }
        logger.debug(f"All stats: {all_stats}")
        return all_stats