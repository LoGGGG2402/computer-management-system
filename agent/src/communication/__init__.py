# -*- coding: utf-8 -*-
"""
Communication modules.

Handles communication with the management server via HTTP and WebSockets.
"""
# Expose client classes for easier import from this package
from .http_client import HttpClient
from .ws_client import WSClient

__all__ = [
    'HttpClient',
    'WSClient'
]
