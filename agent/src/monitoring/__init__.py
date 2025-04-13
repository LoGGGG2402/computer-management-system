# -*- coding: utf-8 -*-
"""
System monitoring modules.

Responsible for monitoring system resources (CPU, RAM, Disk),
hardware information, and potentially other system health metrics.
"""
# Expose the monitor class for easier import from this package
from .system_monitor import SystemMonitor

__all__ = [
    'SystemMonitor'
]
