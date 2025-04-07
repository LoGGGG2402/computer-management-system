"""
System monitoring module for the Computer Management System Agent.
This module provides functionality to monitor various system resources.
"""
import logging
import psutil
import platform
import time
from typing import Dict, Any

from ..utils.utils import format_bytes

logger = logging.getLogger(__name__)

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
        info = {
            "hostname": platform.node(),
            "platform": platform.system(),
            "platform_release": platform.release(),
            "platform_version": platform.version(),
            "architecture": platform.machine(),
            "processor": platform.processor(),
            "physical_cores": psutil.cpu_count(logical=False),
            "total_cores": psutil.cpu_count(logical=True),
            "total_memory": psutil.virtual_memory().total,
            "memory_formatted": format_bytes(psutil.virtual_memory().total),
            "boot_time": psutil.boot_time(),
            "boot_time_formatted": time.strftime("%Y-%m-%d %H:%M:%S", 
                                               time.localtime(psutil.boot_time()))
        }
        
        return info
    
    def get_cpu_info(self) -> Dict[str, Any]:
        """
        Get CPU usage information.
        
        Returns:
            Dict with CPU usage information
        """
        cpu_info = {
            "physical_cores": psutil.cpu_count(logical=False),
            "total_cores": psutil.cpu_count(logical=True),
            "cpu_freq": {
                "current": psutil.cpu_freq().current if psutil.cpu_freq() else None,
                "min": psutil.cpu_freq().min if psutil.cpu_freq() else None,
                "max": psutil.cpu_freq().max if psutil.cpu_freq() else None
            },
            "cpu_percent": psutil.cpu_percent(interval=1),
            "cpu_percent_per_core": psutil.cpu_percent(interval=1, percpu=True)
        }
        
        return cpu_info
    
    def get_memory_info(self) -> Dict[str, Any]:
        """
        Get memory usage information.
        
        Returns:
            Dict with memory usage information
        """
        svmem = psutil.virtual_memory()
        
        memory_info = {
            "total": svmem.total,
            "total_formatted": format_bytes(svmem.total),
            "available": svmem.available,
            "available_formatted": format_bytes(svmem.available),
            "used": svmem.used,
            "used_formatted": format_bytes(svmem.used),
            "percent": svmem.percent
        }
        
        # Swap information
        swap = psutil.swap_memory()
        memory_info["swap"] = {
            "total": swap.total,
            "total_formatted": format_bytes(swap.total),
            "free": swap.free,
            "free_formatted": format_bytes(swap.free),
            "used": swap.used,
            "used_formatted": format_bytes(swap.used),
            "percent": swap.percent
        }
        
        return memory_info
    
    def get_disk_info(self) -> Dict[str, Any]:
        """
        Get disk usage information.
        
        Returns:
            Dict with disk usage information
        """
        partitions = psutil.disk_partitions()
        disk_info = []
        
        for partition in partitions:
            try:
                partition_usage = psutil.disk_usage(partition.mountpoint)
                info = {
                    "device": partition.device,
                    "mountpoint": partition.mountpoint,
                    "fstype": partition.fstype,
                    "total": partition_usage.total,
                    "total_formatted": format_bytes(partition_usage.total),
                    "used": partition_usage.used,
                    "used_formatted": format_bytes(partition_usage.used),
                    "free": partition_usage.free,
                    "free_formatted": format_bytes(partition_usage.free),
                    "percent": partition_usage.percent
                }
                disk_info.append(info)
            except (PermissionError, FileNotFoundError):
                # Some disk partitions aren't accessible
                pass
                
        # Total disk I/O
        disk_io = psutil.disk_io_counters()
        if disk_io:
            disk_io_info = {
                "read_bytes": disk_io.read_bytes,
                "read_bytes_formatted": format_bytes(disk_io.read_bytes),
                "write_bytes": disk_io.write_bytes,
                "write_bytes_formatted": format_bytes(disk_io.write_bytes),
                "read_count": disk_io.read_count,
                "write_count": disk_io.write_count
            }
        else:
            disk_io_info = {}
            
        return {
            "partitions": disk_info,
            "io": disk_io_info
        }
    
    def get_network_info(self) -> Dict[str, Any]:
        """
        Get network information.
        
        Returns:
            Dict with network information
        """
        network_info = {}
        
        # Network interfaces
        network_interfaces = psutil.net_if_addrs()
        interfaces = []
        
        for interface_name, addr_list in network_interfaces.items():
            interface = {
                "name": interface_name,
                "addresses": []
            }
            
            for addr in addr_list:
                address_info = {
                    "family": str(addr.family),
                    "address": addr.address,
                    "netmask": addr.netmask,
                    "broadcast": addr.broadcast
                }
                interface["addresses"].append(address_info)
                
            interfaces.append(interface)
            
        network_info["interfaces"] = interfaces
        
        # IO counters
        io_counters = psutil.net_io_counters()
        if io_counters:
            network_info["io"] = {
                "bytes_sent": io_counters.bytes_sent,
                "bytes_sent_formatted": format_bytes(io_counters.bytes_sent),
                "bytes_recv": io_counters.bytes_recv,
                "bytes_recv_formatted": format_bytes(io_counters.bytes_recv),
                "packets_sent": io_counters.packets_sent,
                "packets_recv": io_counters.packets_recv
            }
            
        # Connections
        try:
            connections = psutil.net_connections()
            conn_info = []
            
            for conn in connections:
                conn_info.append({
                    "fd": conn.fd,
                    "family": conn.family,
                    "type": conn.type,
                    "local_addr": f"{conn.laddr.ip}:{conn.laddr.port}" if conn.laddr else None,
                    "remote_addr": f"{conn.raddr.ip}:{conn.raddr.port}" if conn.raddr else None,
                    "status": conn.status,
                    "pid": conn.pid
                })
                
            network_info["connections"] = conn_info
        except (PermissionError, psutil.AccessDenied):
            # This might require elevated privileges
            network_info["connections"] = "Access denied"
            
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
        
        for proc in psutil.process_iter(['pid', 'name', 'username', 'memory_percent', 'cpu_percent', 'create_time', 'status']):
            try:
                # Get process info
                proc_info = proc.info
                proc_info['memory_info'] = proc.memory_info()
                proc_info['memory_rss_formatted'] = format_bytes(proc.memory_info().rss)
                
                # Update CPU percent
                proc.cpu_percent()
                
                processes.append(proc_info)
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                pass
                
        # Sort by memory usage and get top N
        processes = sorted(processes, key=lambda proc: proc['memory_percent'], reverse=True)
        top_by_memory = processes[:top_n]
        
        # Sort by CPU usage and get top N
        processes = sorted(processes, key=lambda proc: proc['cpu_percent'], reverse=True)
        top_by_cpu = processes[:top_n]
        
        return {
            "total_count": len(processes),
            "top_by_memory": top_by_memory,
            "top_by_cpu": top_by_cpu
        }
    
    def get_all_stats(self) -> Dict[str, Any]:
        """
        Get all system statistics in one call.
        
        Returns:
            Dict with all system statistics
        """
        stats = {
            "timestamp": time.time(),
            "system_info": self.get_system_info(),
            "cpu": self.get_cpu_info(),
            "memory": self.get_memory_info(),
            "disk": self.get_disk_info(),
            "network": self.get_network_info(),
            "processes": self.get_process_info(top_n=5)
        }
        
        return stats