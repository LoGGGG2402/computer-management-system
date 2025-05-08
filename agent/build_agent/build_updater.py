import PyInstaller.__main__
import os
import shutil
import sys

# Thêm đường dẫn để tìm thấy các module trong project
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.append(ROOT_DIR)

# Xác định các đường dẫn tương đối với vị trí hiện tại của script
UPDATER_MAIN_PY = os.path.join(ROOT_DIR, "updater", "updater_main.py")
ICON_PATH = os.path.abspath("./icon.ico")  # Đường dẫn đến file icon.ico
OUTPUT_DIR = os.path.abspath("./dist_updater")
BUILD_DIR = os.path.abspath("./build_updater")
SPEC_FILE = os.path.abspath("./updater.spec")

def build_updater():
    """
    Builds the updater executable using PyInstaller.
    
    :return: True if build succeeds, False otherwise
    :rtype: bool
    """
    print("Starting updater build...")

    # Kiểm tra sự tồn tại của thư mục và file nguồn
    if not os.path.exists(os.path.dirname(UPDATER_MAIN_PY)):
        print(f"Error: Updater source directory not found: {os.path.dirname(UPDATER_MAIN_PY)}")
        return False
    
    if not os.path.exists(UPDATER_MAIN_PY):
        print(f"Error: Updater main.py file not found: {UPDATER_MAIN_PY}")
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
        '--name=updater',
        '--onefile',
        '--windowed',
        f'--distpath={OUTPUT_DIR}',
        f'--workpath={BUILD_DIR}',
        f'--specpath={os.path.dirname(SPEC_FILE)}',
    ] + icon_option + [
        UPDATER_MAIN_PY 
    ]

    print(f"Running PyInstaller with parameters: {' '.join(pyinstaller_options)}")
    
    try:
        PyInstaller.__main__.run(pyinstaller_options)
        print(f"Updater build successful! Updater.exe saved at: {os.path.join(OUTPUT_DIR, 'updater.exe')}")

        # Dọn dẹp các file tạm thời
        cleanup_temp_files()
        return True

    except Exception as e:
        print(f"Error during updater build: {e}")
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
    success = build_updater()
    sys.exit(0 if success else 1) 