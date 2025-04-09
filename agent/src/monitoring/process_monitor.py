"""
Process monitoring module for the Computer Management System Agent.
This module provides functionality to monitor system processes.
"""
import psutil
from typing import Dict, Any, List

from src.utils.logger import get_logger
from src.utils.utils import format_bytes

# Get logger for this module
logger = get_logger(__name__)

class ProcessMonitor:
    """Class responsible for monitoring system processes."""
    
    def __init__(self, max_processes: int = 10):
        """
        Initialize the process monitor.
        
        Args:
            max_processes: Maximum number of processes to track
        """
        self.max_processes = max_processes
        logger.debug(f"ProcessMonitor initialized with max_processes={max_processes}")
    
    def get_top_cpu_processes(self) -> List[Dict[str, Any]]:
        """
        Get information about top CPU-consuming processes.
        
        Returns:
            List of processes sorted by CPU usage
        """
        processes = []
        for proc in psutil.process_iter(['pid', 'name', 'username', 'cpu_percent']):
            try:
                pinfo = proc.info
                pinfo['cpu_percent'] = proc.cpu_percent()
                processes.append(pinfo)
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                pass
        
        # Sort by CPU usage and get top N
        top_processes = sorted(processes, key=lambda p: p['cpu_percent'], reverse=True)[:self.max_processes]
        logger.debug(f"Retrieved top {len(top_processes)} CPU processes")
        return top_processes
    
    def get_top_memory_processes(self) -> List[Dict[str, Any]]:
        """
        Get information about top memory-consuming processes.
        
        Returns:
            List of processes sorted by memory usage
        """
        processes = []
        for proc in psutil.process_iter(['pid', 'name', 'username', 'memory_info', 'memory_percent']):
            try:
                pinfo = proc.info
                memory_info = pinfo['memory_info']
                pinfo['rss'] = memory_info.rss if memory_info else 0
                pinfo['rss_formatted'] = format_bytes(pinfo['rss'])
                processes.append(pinfo)
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                pass
        
        # Sort by memory usage and get top N
        top_processes = sorted(processes, key=lambda p: p['rss'], reverse=True)[:self.max_processes]
        logger.debug(f"Retrieved top {len(top_processes)} memory processes")
        return top_processes
    
    def get_process_by_pid(self, pid: int) -> Dict[str, Any]:
        """
        Get detailed information about a specific process.
        
        Args:
            pid: Process ID
            
        Returns:
            Process information or empty dict if not found
        """
        try:
            process = psutil.Process(pid)
            pinfo = process.as_dict(attrs=[
                'pid', 'name', 'username', 'exe', 'cmdline', 'status',
                'cpu_percent', 'memory_percent', 'create_time', 'num_threads'
            ])
            memory_info = process.memory_info()
            pinfo['rss'] = memory_info.rss
            pinfo['rss_formatted'] = format_bytes(memory_info.rss)
            pinfo['vms'] = memory_info.vms
            pinfo['vms_formatted'] = format_bytes(memory_info.vms)
            
            logger.debug(f"Retrieved details for process {pid}")
            return pinfo
        except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
            logger.warning(f"Could not retrieve details for process {pid}")
            return {}
    
    def get_process_count(self) -> Dict[str, int]:
        """
        Get counts of processes by status.
        
        Returns:
            Dictionary with process counts by status
        """
        status_counts = {'total': 0}
        for proc in psutil.process_iter(['status']):
            try:
                status = proc.info['status']
                if status in status_counts:
                    status_counts[status] += 1
                else:
                    status_counts[status] = 1
                status_counts['total'] += 1
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                pass
        
        logger.debug(f"Process counts: {status_counts}")
        return status_counts
    
    def get_all_process_stats(self) -> Dict[str, Any]:
        """
        Get all process statistics.
        
        Returns:
            Dictionary with all process statistics
        """
        stats = {
            'top_cpu_processes': self.get_top_cpu_processes(),
            'top_memory_processes': self.get_top_memory_processes(),
            'process_counts': self.get_process_count()
        }
        logger.debug("Retrieved all process statistics")
        return stats