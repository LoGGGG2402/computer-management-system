import PyInstaller.__main__
import os
import shutil
import sys

# Thêm đường dẫn để tìm thấy các module trong project
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.append(ROOT_DIR)

# Xác định các đường dẫn tương đối với vị trí hiện tại của script
AGENT_MAIN_PY = os.path.join(ROOT_DIR, "agent", "main.py")
ICON_PATH = os.path.abspath("./icon.ico")  # Đường dẫn đến file icon.ico
OUTPUT_DIR = os.path.abspath("./dist_agent")
BUILD_DIR = os.path.abspath("./build_agent")
SPEC_FILE = os.path.abspath("./agent.spec")

def build_agent():
    """
    Builds the agent executable using PyInstaller.
    
    :return: True if build succeeds, False otherwise
    :rtype: bool
    """
    print("Starting agent build...")

    # Kiểm tra sự tồn tại của thư mục và file nguồn
    if not os.path.exists(os.path.dirname(AGENT_MAIN_PY)):
        print(f"Error: Agent source directory not found: {os.path.dirname(AGENT_MAIN_PY)}")
        return False
    
    if not os.path.exists(AGENT_MAIN_PY):
        print(f"Error: Agent main.py file not found: {AGENT_MAIN_PY}")
        return False
    
    # Kiểm tra file icon tồn tại
    if not os.path.exists(ICON_PATH):
        print(f"Warning: Icon file not found: {ICON_PATH}, will use default icon")
        icon_option = []
    else:
        print(f"Will use icon from file: {ICON_PATH}")
        icon_option = [f'--icon={ICON_PATH}']

    # Chuẩn bị thư mục output nếu chưa tồn tại
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)

    pyinstaller_options = [
        '--name=agent',
        '--onefile',
        # '--windowed',  # Comment hoặc xóa dòng này để chế độ console làm việc
        f'--distpath={OUTPUT_DIR}',
        f'--workpath={BUILD_DIR}',
        f'--specpath={os.path.dirname(SPEC_FILE)}'
    ] + icon_option + [
        AGENT_MAIN_PY 
    ]

    print(f"Running PyInstaller with parameters: {' '.join(pyinstaller_options)}")

    try:
        PyInstaller.__main__.run(pyinstaller_options)
        print(f"Agent build successful! Agent.exe saved at: {os.path.join(OUTPUT_DIR, 'agent.exe')}")

        # Dọn dẹp các file tạm thời
        cleanup_temp_files()
        return True

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