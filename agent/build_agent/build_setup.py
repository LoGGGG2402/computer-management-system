import os
import subprocess
import argparse
import re
import sys

# Thêm đường dẫn để tìm thấy các module trong project
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.append(ROOT_DIR)

# Đường dẫn tới các file và thư mục quan trọng
AGENT_VERSION_FILE = os.path.join(ROOT_DIR, "agent", "version.py")
INNO_SETUP_COMPILER = r"C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
INNO_SCRIPT_PATH = os.path.abspath("./inno.iss")
AGENT_SCRIPT_PATH = os.path.abspath("./build_agent.py")
UPDATER_SCRIPT_PATH = os.path.abspath("./build_updater.py")
AGENT_EXE_PATH = os.path.join(os.path.dirname(AGENT_SCRIPT_PATH), "dist_agent", "agent.exe")
UPDATER_EXE_PATH = os.path.join(os.path.dirname(UPDATER_SCRIPT_PATH), "dist_updater", "updater.exe")

def update_version_in_file(file_path, new_version):
    """
    Updates the __version__ variable in a Python file.
    
    :param file_path: Path to the file to update
    :type file_path: str
    :param new_version: New version string to set
    :type new_version: str
    :return: True if successful, False otherwise
    :rtype: bool
    """
    print(f"Updating version in file: {file_path} to {new_version}")
    try:
        # Đảm bảo thư mục cha tồn tại
        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        
        # Kiểm tra nếu file đã tồn tại
        if os.path.exists(file_path):
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Tìm và thay thế __version__
            lines = content.splitlines()
            found = False
            for i, line in enumerate(lines):
                if line.strip().startswith("__version__"):
                    lines[i] = f'__version__ = "{new_version}"'
                    found = True
                    break
            
            if not found:
                lines.append(f'__version__ = "{new_version}"')
            
            content_new = "\n".join(lines) + "\n"
        else:
            # Tạo file mới nếu không tồn tại
            content_new = f'__version__ = "{new_version}"\n'
            print(f"Creating new version file: {file_path}")
        
        # Ghi nội dung vào file
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content_new)
        
        print(f"Version updated in {file_path}")
        return True
        
    except Exception as e:
        print(f"Error updating version in {file_path}: {e}")
        return False

def update_version_in_inno_script(inno_script_path, new_version):
    """
    Updates the #define MyAppVersion in Inno Setup script.
    
    :param inno_script_path: Path to the Inno Setup script
    :type inno_script_path: str
    :param new_version: New version string to set
    :type new_version: str
    :return: True if successful, False otherwise
    :rtype: bool
    """
    print(f"Updating version in Inno script: {inno_script_path} to {new_version}")
    try:
        if not os.path.exists(inno_script_path):
            print(f"Error: Inno script file not found: {inno_script_path}")
            return False
            
        with open(inno_script_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        found = False
        with open(inno_script_path, 'w', encoding='utf-8') as f:
            for line in lines:
                if line.strip().startswith("#define MyAppVersion"):
                    f.write(f'#define MyAppVersion "{new_version}"\n')
                    found = True
                    print(f"Updated MyAppVersion to {new_version}")
                else:
                    f.write(line)
        
        if not found:
            print(f"Warning: #define MyAppVersion not found in file {inno_script_path}")
            return False
            
        return True
    except Exception as e:
        print(f"Error updating version in Inno script {inno_script_path}: {e}")
        return False

def run_script(script_path, script_name):
    """
    Runs a Python script and checks for errors.
    
    :param script_path: Path to the script to run
    :type script_path: str
    :param script_name: Name of the script for logging purposes
    :type script_name: str
    :return: True if successful, False otherwise
    :rtype: bool
    """
    print(f"---- Starting {script_name} ----")
    try:
        if not os.path.exists(script_path):
            print(f"ERROR: Script {script_path} not found")
            return False
            
        # Chạy script với Python và lấy kết quả
        process = subprocess.Popen([sys.executable, script_path], 
                                   stdout=subprocess.PIPE, 
                                   stderr=subprocess.PIPE, 
                                   text=True, 
                                   encoding='utf-8')
        stdout, stderr = process.communicate()
        
        if stdout:
            print(f"Output from {script_name}:\n{stdout}")
        if stderr:
            print(f"Error output from {script_name}:\n{stderr}")
            
        if process.returncode != 0:
            print(f"ERROR: {script_name} ended with error code {process.returncode}.")
            return False
            
        print(f"---- {script_name} completed successfully ----\n")
        return True
    except Exception as e:
        print(f"ERROR running {script_name}: {e}")
        return False

def compile_inno_setup(inno_script_path, current_version):
    """
    Compiles Inno Setup script.
    
    :param inno_script_path: Path to the Inno Setup script
    :type inno_script_path: str
    :param current_version: Current version for setup file naming
    :type current_version: str
    :return: True if successful, False otherwise
    :rtype: bool
    """
    print("---- Starting Inno Setup script compilation ----")
    if not os.path.exists(INNO_SETUP_COMPILER):
        print(f"ERROR: Inno Setup Compiler not found at: {INNO_SETUP_COMPILER}")
        print("Please install Inno Setup and check the INNO_SETUP_COMPILER path.")
        return False
        
    if not os.path.exists(inno_script_path):
        print(f"ERROR: Inno script file not found: {inno_script_path}")
        return False
        
    try:
        cmd = [INNO_SETUP_COMPILER, '/Q', inno_script_path]
        print(f"Running command: {' '.join(cmd)}")
        
        # Chạy từ thư mục chứa script build_setup.py (tức là agent/build_agent/)
        process = subprocess.Popen(cmd, 
                                   stdout=subprocess.PIPE, 
                                   stderr=subprocess.PIPE, 
                                   text=True, 
                                   encoding='utf-8', 
                                   creationflags=subprocess.CREATE_NO_WINDOW, 
                                   cwd=os.path.dirname(inno_script_path))
        stdout, stderr = process.communicate()
        
        if stdout:
            print(f"Output from Inno Setup Compiler:\n{stdout}")
        if stderr:
            print(f"Information from Inno Setup Compiler (stderr):\n{stderr}") 

        # Kiểm tra file output có tồn tại không
        expected_setup_filename = f"CMSAgent_Setup_{current_version}.exe"
        output_dir_for_setup = os.path.dirname(inno_script_path)
        final_setup_path = os.path.join(output_dir_for_setup, expected_setup_filename)
        
        setup_file_found = os.path.exists(final_setup_path)

        if setup_file_found:
            print(f"---- Inno Setup script compiled successfully! ----")
            print(f"Setup file created at: {final_setup_path}")
            return True
        else:
            # Kiểm tra xem có lỗi rõ ràng từ ISCC không
            iscc_reported_error = "Error" in (stdout or "") or "Error" in (stderr or "") or process.returncode != 0
            if not iscc_reported_error:
                 print(f"WARNING: Inno Setup Compiler reported no errors but expected setup file not found: {final_setup_path}.")
                 print("Please check:")
                 print(f"  1. 'OutputBaseFilename' configuration in '{os.path.basename(inno_script_path)}'")
                 print(f"  2. ISCC.exe output above for clues")
                 print(f"  3. Write permissions to directory: {output_dir_for_setup}")
                 return False
            
            print(f"ERROR: Inno Setup script compilation failed or setup file not found: {final_setup_path}.")
            if process.returncode != 0: 
                print(f"ISCC error code: {process.returncode}")
            return False
            
    except Exception as e:
        print(f"ERROR compiling Inno Setup script: {e}")
        return False

def main():
    """
    Main function that orchestrates the build process.
    """
    parser = argparse.ArgumentParser(description="Build agent, updater and create setup file.")
    parser.add_argument("version", help="Version for the build, e.g.: 1.0.1")
    args = parser.parse_args()
    new_version = args.version

    print(f"Starting build process for version: {new_version}\n")

    # Cập nhật version cho agent
    if not update_version_in_file(AGENT_VERSION_FILE, new_version):
        print("Cannot update agent version. Stopping build process.")
        sys.exit(1)
    print("--- Agent version update completed ---\n")

    # Cập nhật version trong Inno Setup script
    if not update_version_in_inno_script(INNO_SCRIPT_PATH, new_version):
        print("Cannot update version in Inno Script. Stopping build process.")
        sys.exit(1)
    print("--- Inno Script version update completed ---\n")

    # Build agent
    if not run_script(AGENT_SCRIPT_PATH, "build_agent.py"):
        print("Agent build failed. Stopping build process.")
        sys.exit(1)
    
    # Kiểm tra file agent.exe được tạo thành công
    if not os.path.exists(AGENT_EXE_PATH):
        print(f"ERROR: Agent.exe not created at {AGENT_EXE_PATH} after running build_agent.py. Stopping build.")
        sys.exit(1)
    print(f"Found agent.exe at: {AGENT_EXE_PATH}")

    # Build updater
    if not run_script(UPDATER_SCRIPT_PATH, "build_updater.py"):
        print("Updater build failed. Stopping build process.")
        sys.exit(1)
        
    # Kiểm tra file updater.exe được tạo thành công
    if not os.path.exists(UPDATER_EXE_PATH):
        print(f"ERROR: Updater.exe not created at {UPDATER_EXE_PATH} after running build_updater.py. Stopping build.")
        sys.exit(1)
    print(f"Found updater.exe at: {UPDATER_EXE_PATH}")

    # Biên dịch Inno Setup script
    if not compile_inno_setup(INNO_SCRIPT_PATH, new_version):
        print("Setup file creation failed.")
        sys.exit(1)

    print(f"\n==== BUILD PROCESS COMPLETED FOR VERSION {new_version} ====")
    sys.exit(0)

if __name__ == "__main__":
    main() 