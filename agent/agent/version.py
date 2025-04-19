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

# Release type (e.g., "alpha", "beta", "rc", "stable")
RELEASE_TYPE = "beta"

def get_version():
    """Returns the basic version string."""
    return __version__

def get_full_version():
    """Returns the full version string with build information."""
    return __version_full__

def get_version_info():
    """Returns a dictionary with all version information."""
    return {
        "version": __version__,
        "major": MAJOR,
        "minor": MINOR,
        "patch": PATCH,
        "build_date": BUILD_DATE,
        "build_number": BUILD_NUMBER,
        "release_type": RELEASE_TYPE
    }