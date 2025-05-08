import PyInstaller.__main__
import os
import shutil
import sys
import subprocess
import platform


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.append(ROOT_DIR)


AGENT_MAIN_PY = os.path.join(ROOT_DIR, "agent", "main.py")
ICON_PATH = os.path.abspath("./icon.ico")  
OUTPUT_DIR = os.path.abspath("./dist_agent")
BUILD_DIR = os.path.abspath("./build_agent")
SPEC_FILE = os.path.abspath("./agent.spec")
CONFIG_DIR = os.path.join(ROOT_DIR, "agent", "config")
CONFIG_FILE = os.path.join(CONFIG_DIR, "agent_config.json")


def check_requirements():
    """
    Checks if all required packages are installed.
    
    :return: True if all requirements are met, False otherwise
    :rtype: bool
    """
    print("Checking requirements...")
    required_packages = [
        "pyinstaller",
        "pywin32",  
        "psutil",
        "websocket-client",
        "requests"
    ]
    
    missing_packages = []
    
    for package in required_packages:
        try:
            __import__(package.replace("-", "_"))
        except ImportError:
            missing_packages.append(package)
    
    if missing_packages:
        print(f"Error: Missing required packages: {', '.join(missing_packages)}")
        print("Please install them using: pip install " + " ".join(missing_packages))
        return False
    
    print("All required packages are installed.")
    return True


def build_agent():
    """
    Builds the agent executable using PyInstaller.
    
    :return: True if build succeeds, False otherwise
    :rtype: bool
    """
    print("Starting agent build...")

    
    if platform.system() != "Windows":
        print("Warning: Building on non-Windows platform. The resulting executable may not work properly as a Windows service.")
    
    
    if not check_requirements():
        return False

    
    if not os.path.exists(os.path.dirname(AGENT_MAIN_PY)):
        print(f"Error: Agent source directory not found: {os.path.dirname(AGENT_MAIN_PY)}")
        return False
    
    if not os.path.exists(AGENT_MAIN_PY):
        print(f"Error: Agent main.py file not found: {AGENT_MAIN_PY}")
        return False
    
    
    if not os.path.exists(ICON_PATH):
        print(f"Warning: Icon file not found: {ICON_PATH}, will use default icon")
        icon_option = []
    else:
        print(f"Will use icon from file: {ICON_PATH}")
        icon_option = [f'--icon={ICON_PATH}']

    
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)

    
    if not os.path.exists(CONFIG_FILE):
        print(f"Error: Configuration file not found: {CONFIG_FILE}")
        return False

    
    additional_data = [
        f'--add-data={CONFIG_FILE};config'
    ]

    
    pyinstaller_options = [
        '--name=agent',
        '--onefile',
        '--console',  
        '--hidden-import=win32timezone',  
        '--hidden-import=servicemanager',  
        '--hidden-import=win32serviceutil',  
        '--hidden-import=win32service',  
        '--hidden-import=win32event',  
        '--hidden-import=win32api',  
        '--hidden-import=win32security',  
        '--hidden-import=win32con',  
        f'--distpath={OUTPUT_DIR}',
        f'--workpath={BUILD_DIR}',
        f'--specpath={os.path.dirname(SPEC_FILE)}'
    ] + icon_option + additional_data + [
        AGENT_MAIN_PY 
    ]

    print(f"Running PyInstaller with parameters: {' '.join(pyinstaller_options)}")

    try:
        PyInstaller.__main__.run(pyinstaller_options)
        agent_exe_path = os.path.join(OUTPUT_DIR, 'agent.exe')
        if os.path.exists(agent_exe_path):
            print(f"Agent build successful! Agent.exe saved at: {agent_exe_path}")
            
            
            output_config_dir = os.path.join(OUTPUT_DIR, "config")
            os.makedirs(output_config_dir, exist_ok=True)
            shutil.copy2(CONFIG_FILE, os.path.join(output_config_dir, os.path.basename(CONFIG_FILE)))
            print(f"Copied config file to: {output_config_dir}")
            
            
            cleanup_temp_files()
            return True
        else:
            print(f"Error: Expected executable not found at {agent_exe_path}")
            return False

    except Exception as e:
        print(f"Error during agent build: {e}")
        print("Check paths, required files and PyInstaller installation.")
        return False


def cleanup_temp_files():
    """
    Cleans up temporary files after build.
    """
    if os.path.exists(SPEC_FILE):
        os.remove(SPEC_FILE)
        print(f"Deleted spec file: {SPEC_FILE}")

    if os.path.exists(BUILD_DIR) and os.path.isdir(BUILD_DIR):
        shutil.rmtree(BUILD_DIR)
        print(f"Deleted build directory: {BUILD_DIR}")

    print("Temporary files cleaned up.")


if __name__ == '__main__':
    success = build_agent()
    sys.exit(0 if success else 1)