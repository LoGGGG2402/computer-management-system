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
    
    def get_stats(self) -> Dict[str, Any]:
        """
        Get basic system statistics for status reporting.
        
        Returns:
            Dict with CPU and RAM usage information
        """
        stats = {
            "cpu": psutil.cpu_percent(interval=0.5),
            "ram": psutil.virtual_memory().percent,
            "disk": psutil.disk_usage('/').percent
        }
        
        logger.debug(f"System stats collected: {stats}")
        return stats
    
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
        return system_info
    
    def get_resource_usage(self) -> Dict[str, Any]:
        """
        Get comprehensive resource usage information.
        
        Returns:
            Dict with CPU, memory, disk and network usage
        """
        # Get CPU information
        cpu_info = {
            "percent": psutil.cpu_percent(interval=1),
            "count": psutil.cpu_count(logical=True),
        }
        
        # Get memory information
        memory = psutil.virtual_memory()
        memory_info = {
            "total": format_bytes(memory.total),
            "available": format_bytes(memory.available),
            "used": format_bytes(memory.used),
            "percent": memory.percent,
        }
        
        # Get disk information
        disk = psutil.disk_usage('/')
        disk_info = {
            "total": format_bytes(disk.total),
            "used": format_bytes(disk.used),
            "free": format_bytes(disk.free),
            "percent": disk.percent,
        }
        
        # Get network information
        network = psutil.net_io_counters()
        network_info = {
            "bytes_sent": format_bytes(network.bytes_sent),
            "bytes_recv": format_bytes(network.bytes_recv),
            "packets_sent": network.packets_sent,
            "packets_recv": network.packets_recv,
        }
        
        return {
            "cpu": cpu_info,
            "memory": memory_info,
            "disk": disk_info,
            "network": network_info
        }
    
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
        return {"processes": processes}
    
    def get_all_stats(self) -> Dict[str, Any]:
        """
        Get all system statistics in one call.
        
        Returns:
            Dict with all system statistics
        """
        all_stats = {
            "system_info": self.get_system_info(),
            "resources": self.get_resource_usage(),
            "processes": self.get_process_info(top_n=5)["processes"],
            "timestamp": time.time()
        }
        return all_stats