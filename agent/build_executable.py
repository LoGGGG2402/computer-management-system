# -*- coding: utf-8 -*-
"""
Build script for the Computer Management System Agent using PyInstaller.

This script bundles the agent source code from the 'src' directory into a single
executable file. It specifically includes the 'agent_config.json' file within
the executable.

Requirements:
  - PyInstaller: Install using 'pip install pyinstaller'
  - Python 3.x

Usage:
  1. Place this script in the root directory of the agent project (the directory
     containing 'src', 'config', 'storage', etc.).
  2. Ensure 'config/agent_config.json' exists.
  3. Run the script from the terminal in that directory: python build_executable.py
  4. The executable will be created in the 'dist' subdirectory.

IMPORTANT CODE MODIFICATIONS REQUIRED:
  - Modify 'src/main.py' to correctly locate the embedded 'agent_config.json'
    when running as a bundled executable (using sys._MEIPASS).
  - Modify 'src/main.py' (or relevant modules like StateManager, logger setup)
    to handle relative paths defined in the config (like 'storage_path')
    appropriately when running bundled. Typically, relative paths should resolve
    relative to the executable's location (os.path.dirname(sys.executable))
    rather than the temporary _MEIPASS directory.

Example modification in src/main.py for config path:

import sys
import os

def get_config_path(args): # args from argparse
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        # Running bundled: config is embedded in 'config' dir within bundle
        return os.path.join(sys._MEIPASS, 'config', 'agent_config.json')
    else:
        # Running as script: use command-line arg or default
        return args.config # Already calculated by argparse

# Call this function in main() before initializing ConfigManager
# config_path = get_config_path(args)
# config = ConfigManager(config_path)

Example modification in src/main.py for relative storage_path:

storage_path_from_config = config.get('storage_path', 'storage')
if not os.path.isabs(storage_path_from_config):
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        # Bundled: Make relative to executable's directory
        executable_dir = os.path.dirname(sys.executable)
        resolved_storage_path = os.path.abspath(os.path.join(executable_dir, storage_path_from_config))
    else:
        # Script: Make relative to project root (assuming main.py structure)
        project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        resolved_storage_path = os.path.abspath(os.path.join(project_root, storage_path_from_config))
else:
    resolved_storage_path = storage_path_from_config

# Use resolved_storage_path for lock file, logs, state manager etc.

"""

import os
import platform
import shutil
import sys

try:
    import PyInstaller.__main__
except ImportError:
    print("Error: PyInstaller is not installed.")
    print("Please install it using: pip install pyinstaller")
    sys.exit(1)

# --- Configuration ---
APP_NAME = "cms_agent"
ENTRY_SCRIPT = os.path.join("src", "main.py")
CONFIG_FILE_SOURCE = os.path.join("config", "agent_config.json")
CONFIG_FILE_DEST_IN_BUNDLE = "config" # Destination directory inside the bundle

# Determine platform specific path separator for --add-data
# On Windows: 'source;destination'
# On Linux/macOS: 'source:destination'
path_separator = ';' if platform.system() == "Windows" else ':'

# --- Build Steps ---
def build():
    """Runs the PyInstaller build process."""
    print(f"Starting build for {APP_NAME}...")

    if not os.path.exists(ENTRY_SCRIPT):
        print(f"Error: Entry script not found at '{ENTRY_SCRIPT}'")
        sys.exit(1)

    if not os.path.exists(CONFIG_FILE_SOURCE):
        print(f"Error: Configuration file not found at '{CONFIG_FILE_SOURCE}'")
        sys.exit(1)

    # Clean previous builds
    print("Cleaning previous build directories ('build', 'dist')...")
    if os.path.isdir("build"):
        shutil.rmtree("build")
    if os.path.isdir("dist"):
        shutil.rmtree("dist")
    spec_file = f"{APP_NAME}.spec"
    if os.path.exists(spec_file):
        os.remove(spec_file)

    # PyInstaller arguments
    pyinstaller_args = [
        '--name', APP_NAME,
        '--onefile',          # Create a single executable file
        # '--noconsole',      # Uncomment to hide console window (for background process)
        '--log-level', 'INFO', # PyInstaller log level
        # Add the config file to the bundle.
        # It will be placed in a 'config' directory inside the bundle's temp _MEIPASS dir.
        '--add-data', f"{CONFIG_FILE_SOURCE}{path_separator}{CONFIG_FILE_DEST_IN_BUNDLE}",
        # Add other data files if needed (e.g., assets, icons)
        # '--add-data', f"path/to/asset.dat{path_separator}assets",
        # '--icon', 'path/to/icon.ico', # Example: Add an icon
        ENTRY_SCRIPT
    ]

    print(f"Running PyInstaller with args: {' '.join(pyinstaller_args)}")

    try:
        PyInstaller.__main__.run(pyinstaller_args)
        print("\nBuild completed successfully!")
        print(f"Executable created in: {os.path.abspath('dist')}")
    except Exception as e:
        print(f"\nBuild failed with error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    # Ensure the script is run from the project root directory
    if not (os.path.exists("src") and os.path.exists("config")):
         print("Error: This script must be run from the agent's root directory")
         print("(The directory containing 'src', 'config', etc.)")
         sys.exit(1)

    build()