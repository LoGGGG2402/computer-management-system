# Kế hoạch Triển Khai Luồng Cập Nhật Agent

Ok, đây là kế hoạch chi tiết đầy đủ để triển khai luồng cập nhật Agent, đã cập nhật để sử dụng trường `errors` trong model `Computer` và làm rõ các điểm thảo luận:

**I. Backend (src)**

1.  **Database Schema (`readme.md` or dedicated schema file):**
    *   **Bảng `agent_versions`:** (Cần tạo mới)
        *   `id` (UUID, Primary Key)
        *   `version` (String, Unique)
        *   `checksum_sha256` (String)
        *   `download_url` (String)
        *   `notes` (Text, Nullable)
        *   `is_stable` (Boolean, Default: false)
        *   `file_path` (String - đường dẫn lưu file trên server)
        *   `file_size` (Integer)
        *   `created_at` (Timestamp)
        *   `updated_at` (Timestamp)
    *   **Bảng `computers`:**
        *   **Đã xác nhận:** Đã có trường `errors` kiểu `JSONB` trong `computer.model.js`.
        *   **Đã xác nhận:** Đã có trường `have_active_errors` kiểu `BOOLEAN` trong `computer.model.js`.
        *   **Định nghĩa cấu trúc một phần tử lỗi trong mảng `errors`:** (Cần triển khai logic ghi/đọc theo cấu trúc này)
            *   `id` (Number/String - unique trong mảng, ví dụ: `Date.now()`)
            *   `error_type` (String - **Danh sách thống nhất cho lỗi cập nhật**: `"UpdateResourceCheckFailed"`, `"UpdateDownloadFailed"`, `"UpdateChecksumMismatch"`, `"UpdateExtractionFailed"`, `"UpdateLaunchFailed"`, `"UpdateGeneralFailure"`. Các loại lỗi khác của Agent vẫn có thể được sử dụng.)
            *   `error_message` (String - thông báo lỗi từ agent)
            *   `error_details` (Object - chứa thông tin bổ sung như `stack_trace`, `agent_version_at_error`, `download_url`, `expected_checksum`, `actual_checksum`, `reported_at_agent`)
            *   `reported_at` (Timestamp - thời gian server ghi nhận lỗi)
            *   `resolved` (Boolean, Default: false)
            *   `resolved_at` (Timestamp, Nullable)
            *   `resolution_notes` (String, Nullable)

2.  **Models (`backend/src/models/`):**
    *   `agentVersion.model.js`: (Cần tạo mới) Tạo Sequelize model cho bảng `agent_versions`.
    *   `computer.model.js`: **Đã xác nhận** model `Computer` đã định nghĩa đúng trường `errors` (kiểu `DataTypes.JSONB`) và `have_active_errors` (kiểu `DataTypes.BOOLEAN`).

3.  **Middleware (middleware):**
    *   `authJwt.js`, `authAdmin.js`: (Giả định) Đảm bảo hoạt động đúng cho các route Admin.
    *   `authAgentToken.js`:
        *   **Đã xác nhận:** Triển khai logic xác thực token gửi từ Agent trong header **`agent-id`** và **`agent-token`**.
        *   **Đã xác nhận:** Sử dụng `agentId` (từ header `agent-id`) để gọi `computerService.verifyAgentToken(agentId, token)`.
        *   **Đã xác nhận:** Nếu tìm thấy `computer` và token hợp lệ, **gắn `computer.id` vào `req.computerId` và `agentId` vào `req.agentId`**.
        *   **Đã xác nhận:** Nếu không tìm thấy computer hoặc token không hợp lệ, trả về lỗi 401 hoặc 403.
    *   `uploadFileMiddleware.js`: (Cần kiểm tra chi tiết) Cấu hình Multer (hoặc tương tự) cho việc upload file agent package (`multipart/form-data`). Lưu file vào thư mục tạm hoặc thư mục lưu trữ cố định. Gắn thông tin file vào `req.file`.

4.  **Routes (routes):**
    *   `admin.routes.js`: (Cần thêm các route sau)
        *   `POST /agents/versions`: Route tới `admin.controller.js -> handleAgentUpload`. Áp dụng `authJwt`, `authAdmin`, `uploadFileMiddleware`.
        *   `PUT /agents/versions/:versionId`: Route tới `admin.controller.js -> setAgentVersionStability`. Áp dụng `authJwt`, `authAdmin`.
        *   `GET /agents/versions`: Route tới `admin.controller.js -> getAgentVersions` (để Frontend lấy danh sách). Áp dụng `authJwt`, `authAdmin`.
    *   `agent.routes.js`: (Cần thêm các route sau)
        *   `GET /check_update`: Route tới `agent.controller.js -> handleCheckUpdate`. Áp dụng `authAgentToken`.
        *   `POST /report-error`: Route tới `agent.controller.js -> handleErrorReport`. Áp dụng `authAgentToken` (đã cập nhật để có `req.computerId`).
    *   Thêm route (hoặc cấu hình static serving) để Agent tải file package: `GET /agent-packages/:filename`. **Bắt buộc áp dụng `authAgentToken`** (sử dụng header `agent-id` và `agent-token`) để bảo mật.

5.  **Controllers (controllers):**
    *   `admin.controller.js`: (Cần thêm các method sau)
        *   `handleAgentUpload`: Nhận `req.file` và `req.body` (version, notes). Gọi `adminService.processAgentUpload`. Trả về 201 hoặc lỗi.
        *   `setAgentVersionStability`: Nhận `versionId` từ `req.params`, `is_stable` từ `req.body`. Gọi `adminService.updateStabilityFlag`. Nếu thành công và `is_stable` là true, gọi `websocketService.notifyAgentsOfNewVersion`. Trả về 200 hoặc lỗi.
        *   `getAgentVersions`: Gọi `adminService.getAllVersions`. Trả về danh sách versions.
    *   `agent.controller.js`: (Cần thêm các method sau)
        *   `handleCheckUpdate`: Nhận `current_version` từ `req.query`. Gọi `agentService.getLatestStableVersionInfo`. Trả về 200 (nếu có update) hoặc 204.
        *   `handleErrorReport`:
            *   Lấy `computerId` từ `req.computerId`.
            *   Lấy payload lỗi từ `req.body`.
            *   Chuẩn bị `errorData` cho `computerService` (sử dụng `error_type` chi tiết từ agent):
                *   `error_type`: `req.body.error_type`
                *   `error_message`: `req.body.message`
                *   `error_details`: `{ reported_at_agent: req.body.timestamp, agent_version_at_error: req.body.agent_version, stack_trace: req.body.stack_trace, ...req.body.details }`
            *   Gọi `computerService.reportComputerError(computerId, errorData)`.
            *   Trả về 204 nếu thành công, hoặc lỗi nếu có exception.

6.  **Services (services):**
    *   `admin.service.js` (hoặc `agentVersion.service.js`): (Cần thêm các method sau)
        *   `processAgentUpload(file, versionData)`: Tính checksum SHA256 của `file.path`. Tạo `download_url` (ví dụ: `/api/agent/agent-packages/${file.filename}`). Lưu file vào vị trí cố định nếu cần. Lưu metadata vào bảng `agent_versions`. Trả về record đã tạo.
        *   `updateStabilityFlag(versionId, isStable)`: Tìm và cập nhật `agent_versions`. Trả về record đã cập nhật.
        *   `getAllVersions()`: Truy vấn và trả về danh sách tất cả các phiên bản agent.
    *   `agent.service.js`: (Cần tạo file và thêm method sau)
        *   `getLatestStableVersionInfo(currentVersion)`: Tìm bản ghi `agent_versions` mới nhất có `is_stable = true`. So sánh với `currentVersion` (nếu có). Trả về `{ version, download_url, checksum_sha256 }` hoặc `null`.
    *   `computer.service.js`: (Cần thêm các method sau)
        *   `findComputerByAgentId(agentId)`: **Đã có**.
        *   `verifyAgentToken(agentId, token)`: **Đã có**.
        *   `reportComputerError(computerId, errorData)`:
            *   Tìm `computer` bằng `computerId`.
            *   Tạo `errorId` mới (ví dụ: `Date.now()`).
            *   Tạo object lỗi hoàn chỉnh: `{ id: errorId, reported_at: new Date(), resolved: false, ...errorData }`.
            *   Lấy mảng `errors` hiện tại của computer, thêm object lỗi mới vào.
            *   Cập nhật computer với mảng `errors` mới và `have_active_errors: true`.
            *   Trả về object lỗi đã tạo (hoặc chỉ cần thành công).
        *   `resolveComputerError(computerId, errorId, resolutionNotes)`: (Cần thêm) Logic tìm lỗi trong mảng `errors` và cập nhật trạng thái `resolved`, `resolved_at`, `resolution_notes`, `have_active_errors`.
        *   `getComputerErrors(id)`: (Cần thêm) Trả về mảng `errors` của computer.
    *   `websocket.service.js`: (Cần thêm method sau)
        *   `notifyAgentsOfNewVersion()`: Lấy version stable mới nhất từ `agentService` hoặc `adminService`. Gửi sự kiện `agent:new_version_available` với payload `{ new_stable_version: "x.y.z" }` đến các agent đang kết nối.

7.  **API Documentation (`docs/api.md`):** (Cần cập nhật)
    *   Thêm/Cập nhật tài liệu cho các endpoint Admin: `POST /agents/versions`, `PUT /agents/versions/:versionId`, `GET /agents/versions`.
    *   Thêm/Cập nhật tài liệu cho các endpoint Agent: `GET /check_update`, `POST /report-error` (ghi rõ payload request và response 204, lưu ý lỗi được ghi vào `Computer.errors`). **Nhấn mạnh yêu cầu xác thực Agent bằng header `agent-id` và `agent-token`**.
    *   Thêm/Cập nhật tài liệu cho endpoint tải file: `GET /agent-packages/:filename`. **Nhấn mạnh yêu cầu xác thực Agent bằng header `agent-id` và `agent-token`**.
    *   Thêm tài liệu cho WebSocket event: `agent:new_version_available`.
    *   **Cập nhật:** Liệt kê danh sách `error_type` thống nhất cho lỗi cập nhật (`"UpdateResourceCheckFailed"`, `"UpdateDownloadFailed"`, ...) trong mô tả payload của `POST /api/agent/report-error`.

**II. Agent (agent)**

1.  **Core Logic (`agent/agent/core/agent.py`):** (Cần thêm/sửa đổi logic)
    *   Agent class:
        *   **Đã xác nhận:** Sử dụng `AgentState` từ `agent_state.py`.
        *   Thêm thuộc tính: `self._current_version` (đọc từ `agent.version.__version__` hoặc config), `self._update_info` (lưu thông tin phiên bản mới).
        *   `_set_state(new_state)`: **Đã có**.
        *   `_handle_new_version_event(payload)`: (Cần thêm) Xử lý sự kiện WebSocket `agent:new_version_available`. So sánh version, nếu mới hơn gọi `initiate_update`.
        *   `_check_for_updates_on_server()`: (Cần thêm) Gọi `http_client.check_for_update(self._current_version)`. Nếu có `update_info`, gọi `initiate_update`. Lên lịch chạy định kỳ/sau khi xác thực.
        *   `initiate_update(update_info)`: (Cần thêm)
            *   Kiểm tra `self.get_state() == AgentState.IDLE`.
            *   `_set_state(AgentState.UPDATING_STARTING)`.
            *   Kiểm tra tài nguyên (ổ đĩa). Nếu lỗi -> `_report_error_to_backend("UpdateResourceCheckFailed", ...)`, `_set_state(AgentState.IDLE)`.
            *   `_set_state(AgentState.UPDATING_DOWNLOADING)`. Gọi `http_client.download_file`. Nếu lỗi -> `_report_error_to_backend("UpdateDownloadFailed", ...)`, `_set_state(AgentState.IDLE)`.
            *   `_set_state(AgentState.UPDATING_VERIFYING)`. Tính checksum file tải về, so sánh với `update_info['checksum_sha256']`. Nếu lỗi -> `_report_error_to_backend("UpdateChecksumMismatch", ...)`, xóa file, `_set_state(AgentState.IDLE)`.
            *   `_set_state(AgentState.UPDATING_EXTRACTING_UPDATER)` (Nếu updater nằm trong package). Giải nén updater. Nếu lỗi -> `_report_error_to_backend("UpdateExtractionFailed", ...)`, dọn dẹp, `_set_state(AgentState.IDLE)`.
            *   `_set_state(AgentState.UPDATING_PREPARING_SHUTDOWN)`. Chuẩn bị tham số cho updater (đường dẫn package mới, đường dẫn cài đặt hiện tại, PID của agent hiện tại). Chạy `updater.exe` bằng `subprocess.Popen`. Nếu lỗi -> `_report_error_to_backend("UpdateLaunchFailed", ...)`, dọn dẹp, `_set_state(AgentState.IDLE)`.
            *   Gọi `graceful_shutdown()`.
        *   `graceful_shutdown()`:
            *   **Đã có:** Logic cơ bản để dừng các thành phần.
            *   Cần đảm bảo `_set_state(AgentState.SHUTTING_DOWN)` được gọi đúng lúc.
            *   Thêm `logging.shutdown()` để flush logs.
            *   **Đã có:** Giải phóng lock (`LockManager.release()`).
            *   Log hoàn tất và thoát (`sys.exit(0)`).
        *   `_report_error_to_backend(error_type, message, details=None, stack_trace=None)`: (Cần thêm) Tạo payload JSON với **`error_type` từ danh sách thống nhất** (ví dụ: `"UpdateDownloadFailed"`) và gọi `http_client.report_error`.
    *   `Agent.start()`: **Đã có**. Thêm gọi `_check_for_updates_on_server` sau khi xác thực thành công.

2.  **State Management (`agent/agent/core/agent_state.py`):**
    *   **Đã xác nhận:** `Enum` `AgentState` tồn tại với các trạng thái: `STARTING`, `IDLE`, `FORCE_RESTARTING`, `UPDATING_STARTING`, `UPDATING_DOWNLOADING`, `UPDATING_VERIFYING`, `UPDATING_EXTRACTING_UPDATER`, `UPDATING_PREPARING_SHUTDOWN`, `SHUTTING_DOWN`, `STOPPED`.
    *   *(**Ghi chú làm rõ:**
        *   `FORCE_RESTARTING`: **Đã có**. Trạng thái này không nằm trong luồng cập nhật tự động. Nó được sử dụng khi Agent nhận yêu cầu khởi động lại qua IPC.
        *   `STOPPED`: **Đã có**. Trạng thái này được đặt ngay trước khi tiến trình Agent thoát hoàn toàn.)*

3.  **Communication (communication):**
    *   `http_client.py` (`HttpClient` class):
        *   `check_for_update(current_version)`: (Cần thêm) Gửi `GET /api/agent/check_update` với header **`X-Agent-Id`**, **`X-Agent-Token`** và query param `current_version`. Xử lý response 200 (trả về JSON), 204 (trả về None), và lỗi.
        *   `download_file(url, save_path)`: (Cần thêm) Gửi `GET` đến `url` **với header `X-Agent-Id` và `X-Agent-Token`**. Lưu response vào `save_path`. Xử lý lỗi.
        *   `report_error(payload)`: (Cần thêm) Gửi `POST /api/agent/report-error` với header **`X-Agent-Id`**, **`X-Agent-Token`** và JSON `payload`. Triển khai logic lưu vào file đệm (`error_reports.jsonl`) và thử lại nếu gửi thất bại.
    *   `ws_client.py` (`WSClient` class):
        *   `_on_message`: (Cần sửa đổi) Thêm logic kiểm tra `event == "agent:new_version_available"`. Nếu đúng, gọi `agent_core._handle_new_version_event(payload)`.

4.  **Configuration (`agent/agent/config/config_manager.py`):**
    *   **Đã xác nhận:** `ConfigManager` tồn tại. Cần đảm bảo có thể đọc/ghi `current_version` vào file cấu hình.

5.  **Main Script (`agent/agent/main.py`):**
    *   **Đã xác nhận:** Logic khởi tạo cơ bản tồn tại.
    *   (Cần thêm) Đọc `current_version` từ `agent.version.__version__` khi khởi tạo.
    *   (Cần thêm) Khởi tạo Agent core với `current_version`.
    *   **Logic cập nhật config sau update (chi tiết hơn):** (Cần thêm)
        *   Khi agent *mới* khởi động (sau khi updater chạy xong), thực hiện ở đầu `main.py` hoặc đầu `Agent.start()`:
            1.  Đọc phiên bản mã nguồn hiện tại từ `agent.version.__version__` (ví dụ: `1.2.1`).
            2.  Đọc phiên bản được lưu trong file cấu hình (`config.json`) bằng `ConfigManager.get('current_version')` (ví dụ: `1.2.0`). Xử lý trường hợp giá trị chưa tồn tại trong config.
            3.  **So sánh:** Nếu phiên bản đọc từ `config.json` khác với `agent.version.__version__`.
            4.  **Cập nhật:** Ghi log thông báo "Updating configured version from {config_version} to {code_version}". Gọi `ConfigManager.set('current_version', agent.version.__version__)` để lưu phiên bản mới nhất vào `config.json`. Logic này đảm bảo rằng chỉ khi agent mới thực sự chạy sau một lần cập nhật thành công thì phiên bản trong config mới được cập nhật.

6.  **Version File (`agent/agent/version.py`):**
    *   **Đã xác nhận:** File tồn tại. Cần đảm bảo chứa biến `__version__ = "x.y.z"`.

7.  **Utilities (utils):**
    *   **Đã xác nhận:** Thư mục tồn tại. (Cần thêm) các hàm tiện ích: `calculate_sha256(filepath)`, `extract_package(package_path, destination_path)`, các hàm đọc/ghi/xử lý file đệm lỗi.

8.  **Updater (`agent/updater/` - Dự án/Thư mục mới):** (Cần tạo mới)
    *   Tạo `updater_main.py`.
    *   **Tham số dòng lệnh:** Sử dụng `argparse` để nhận:
        *   `--package-path`: Đường dẫn đến file agent package mới tải về.
        *   `--install-dir`: **Thư mục gốc cài đặt Agent**, nơi chứa file thực thi chính và các thư mục con (`config`, `logs`, ...).
        *   `--pid-to-wait`: Process ID của tiến trình agent cũ cần chờ kết thúc.
        *   `--log-dir`: **Thư mục để Updater ghi log** hoạt động của chính nó (ví dụ: `<install-dir>/logs/`). Updater sẽ tạo file log riêng, ví dụ `updater.log`.
    *   **Logic chính (tích hợp Rollback):**
        1.  Ghi log bắt đầu updater (vào file trong `--log-dir`).
        2.  Chờ tiến trình agent cũ (PID) kết thúc hoàn toàn (có timeout). Nếu timeout hoặc lỗi -> Ghi log lỗi, Thoát với mã lỗi.
        3.  **Backup:** Tạo thư mục backup tạm (ví dụ: `_backup_old/` trong `install-dir`). Di chuyển các file/thư mục quan trọng của agent cũ vào backup. **Nếu lỗi:** Ghi log lỗi backup, gọi hàm `perform_rollback()` (chủ yếu để dọn dẹp backup nếu có), Thoát với mã lỗi.
        4.  **Giải nén:** Giải nén nội dung từ `--package-path` vào `--install-dir`. **Nếu lỗi:** Ghi log lỗi giải nén, gọi hàm `perform_rollback()` (xóa file mới giải nén, khôi phục từ backup), Thoát với mã lỗi.
        5.  **Khởi chạy Agent mới:** Tìm và chạy agent mới bằng `subprocess.Popen`. **Nếu lỗi:** Ghi log lỗi khởi chạy, gọi hàm `perform_rollback()` (xóa file mới giải nén, khôi phục từ backup), Thoát với mã lỗi.
        6.  **Thành công:** Ghi log hoàn tất. Có thể xóa thư mục backup. Thoát với mã thành công (0).
    *   **Hàm `perform_rollback()`:**
        1.  Ghi log bắt đầu rollback.
        2.  Xóa các file/thư mục mới đã giải nén trong `install-dir` (nếu có).
        3.  Di chuyển lại các file/thư mục từ thư mục backup về `install-dir`. Xử lý lỗi nếu không di chuyển được.
        4.  (Tùy chọn) Thử khởi chạy lại agent *cũ* từ file thực thi trong backup (nếu quá trình backup thành công).
        5.  Xóa thư mục backup.
        6.  Ghi log rollback hoàn tất.
    *   Đóng gói script này thành `updater.exe` (bằng `pyinstaller`) và **đặt nó vào bên trong file zip agent package** được admin upload.

**III. Frontend (src)**

1.  **Services (services):**
    *   `admin.service.js`: **Đã có**. (Cần thêm các method sau)
        *   `uploadAgentVersion(formData)`: Gửi `POST /api/admin/agents/versions` với `FormData`.
        *   `markAgentVersionStable(versionId)`: Gửi `PUT /api/admin/agents/versions/{versionId}` với body `{ is_stable: true }`.
        *   `getAgentVersions()`: Gửi `GET /api/admin/agents/versions`.

2.  **Pages/Components (Admin hoặc Admin):** (Cần tạo mới)
    *   Tạo component `AgentVersionManagement`:
        *   Hiển thị bảng danh sách versions từ `getAgentVersions`.
        *   Nút "Mark Stable" gọi `markAgentVersionStable`.
        *   Form upload (file input, version input, notes input) gọi `uploadAgentVersion`.
        *   Hiển thị thông báo thành công/lỗi.

3.  **Routing (`frontend/src/router/index.jsx` hoặc tương tự):** (Cần thêm)
    *   Thêm route `/admin/agent-versions` (bảo vệ bởi quyền admin) trỏ đến component `AgentVersionManagement`.