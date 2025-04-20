"""
Agent version information for the Computer Management System.

This file contains version information for the agent software, including
the primary version string, build date, and other relevant version metadata.
Version follows semantic versioning (https://semver.org/): MAJOR.MINOR.PATCH
"""
import datetime

# Version components
MAJOR = 1
MINOR = 0
PATCH = 0

# Build information
BUILD_DATE = datetime.datetime.now().strftime("%Y-%m-%d")
BUILD_NUMBER = "001"

# Full version string
__version__ = f"{MAJOR}.{MINOR}.{PATCH}"
__version_full__ = f"{__version__}+{BUILD_NUMBER} ({BUILD_DATE})"
__app_name__ = "CMS Agent"