### I. Tổng Quan về Agent

CMSAgent là một ứng dụng chạy trên máy client với các nhiệm vụ chính sau:

- **Thu thập thông tin:** Lấy thông tin chi tiết về phần cứng hệ thống và theo dõi trạng thái sử dụng tài nguyên (CPU, RAM, disk) theo thời gian thực.
- **Giao tiếp với Server:** Thiết lập và duy trì kết nối an toàn với server trung tâm để gửi thông tin thu thập được và nhận các chỉ thị điều khiển.
- **Thực thi lệnh:** Nhận và thực thi các lệnh từ xa được gửi từ server (ví dụ: chạy script, thu thập log cụ thể).
- **Tự động cập nhật:** Có khả năng tự động tải và cài đặt phiên bản mới của chính nó khi có thông báo từ server.
- **Hoạt động ổn định:** Được thiết kế để chạy như một Windows Service, đảm bảo hoạt động nền, liên tục và tự khởi động cùng hệ thống.

### II. Môi Trường Hoạt Động và Yêu Cầu

1. **Hệ Điều Hành Hỗ Trợ:**
    - Windows 10 (khuyến nghị phiên bản 1903 trở lên, 64-bit).
    - Windows 11 (64-bit).
    - Windows Server 2016, Windows Server 2019, Windows Server 2022 (64-bit).
    - *Lưu ý: Cần kiểm tra kỹ lưỡng khả năng tương thích của các API hệ thống cụ thể (ví dụ: WMI, Performance Counters) nếu có kế hoạch hỗ trợ các phiên bản Windows cũ hơn hoặc phiên bản 32-bit.*
2. **Yêu Cầu Phần Mềm Phụ Thuộc:**
    - **.NET Runtime:** Phiên bản .NET mà agent được biên dịch (ví dụ: .NET 6.0 LTS hoặc .NET 8.0 LTS). Runtime này cần được cài đặt trên máy client nếu agent không được triển khai dưới dạng "self-contained application".
    - **Thư Viện Bên Ngoài (NuGet Packages Dự Kiến):**
        - `SocketIOClient.Net`: Cho giao tiếp Socket.IO với server.
        - `Serilog` (và các Sinks liên quan như `Serilog.Sinks.File`, `Serilog.Sinks.Console`, `Serilog.Sinks.EventLog`): Cho hệ thống logging nâng cao và linh hoạt.
        - `System.Management` (gói `System.Management`): Để truy cập Windows Management Instrumentation (WMI) lấy thông tin phần cứng chi tiết.
        - `System.CommandLine`: Để xử lý tham số dòng lệnh một cách mạnh mẽ và có cấu trúc.
        - `Microsoft.Extensions.DependencyInjection`: Để triển khai Dependency Injection, giúp quản lý các thành phần và tăng khả năng kiểm thử.
        - `Microsoft.Extensions.Hosting` (bao gồm `Microsoft.Extensions.Hosting.WindowsServices`): Để dễ dàng host ứng dụng console như một Windows Service.
        - `Microsoft.Extensions.Logging` (và các provider liên quan như `Microsoft.Extensions.Logging.EventLog`): Framework logging cơ bản của .NET, có thể được tích hợp với Serilog.
3. **Quyền Hạn Cần Thiết:**
    - **Trong Quá Trình Cài Đặt (`Setup.CMSAgent.exe` và `CMSAgent.exe configure`):**
        - Yêu cầu quyền **Administrator** để:
            - Ghi file vào thư mục cài đặt (ví dụ: `C:\Program Files\CMSAgent`).
            - Tạo và ghi file/thư mục vào thư mục dữ liệu chung (ví dụ: `C:\ProgramData\CMSAgent`).
            - Đăng ký, cấu hình và khởi động Windows Service.
    - **Khi Agent Hoạt Động Như Windows Service (chạy dưới tài khoản `LocalSystem`):**
        - **Thư mục dữ liệu (`C:\ProgramData\CMSAgent` và các thư mục con):** Tài khoản `LocalSystem` cần quyền **Full Control** để tạo/đọc/ghi/xóa file log, file cấu hình runtime, file cập nhật tạm thời, và báo cáo lỗi. Cần đảm bảo quyền này được thiết lập đúng trong quá trình cài đặt.
        - **Thư mục cài đặt (`C:\Program Files\CMSAgent`):** Tài khoản `LocalSystem` cần quyền **Read & Execute** để chạy agent và updater. Trong quá trình cập nhật, `CMSUpdater.exe` (chạy với quyền của service hoặc được nâng quyền) sẽ cần quyền **Modify** trên thư mục này để thay thế file.
        - **Truy cập mạng:** Để kết nối đến server.
        - **Đọc thông tin hệ thống:** Quyền truy cập WMI, performance counters, registry. `LocalSystem` mặc định có các quyền này.
        - **Thực thi lệnh console:** Lệnh sẽ chạy với quyền của `LocalSystem`.
    - **Khi Chạy Lệnh CLI (sau cài đặt bởi người dùng/quản trị viên):**
        - `CMSAgent.exe start`, `stop`, `uninstall`: Yêu cầu quyền **Administrator**.
        - `CMSAgent.exe configure` (để cấu hình lại): Yêu cầu quyền **Administrator** để ghi vào `C:\ProgramData\CMSAgent\runtime_config\runtime_config.json`.
        - `CMSAgent.exe debug`: Có thể chạy với quyền người dùng thông thường, nhưng khả năng truy cập một số tài nguyên hệ thống hoặc ghi vào `ProgramData` có thể bị hạn chế.

### III. Luồng Cài Đặt và Cấu Hình Ban Đầu

Luồng này mô tả quá trình từ khi người dùng chạy file cài đặt cho đến khi agent được cài đặt, cấu hình lần đầu và bắt đầu hoạt động như một Windows Service.

1. **Chuẩn Bị Gói Cài Đặt (Developer Task):**
    - Một gói cài đặt (ví dụ: `Setup.CMSAgent.exe`) được tạo ra, chứa các thành phần:
        - `CMSAgent.exe`: File thực thi chính của agent (bao gồm logic cho Windows Service và các lệnh CLI).
        - `CMSUpdater.exe`: File thực thi cho tiến trình tự cập nhật.
        - `agent_config.json`: File cấu hình tĩnh mặc định (chứa URL server, các khoảng thời gian mặc định, thông tin phiên bản).
        - Các thư viện DLL cần thiết khác (nếu agent không phải là bản build self-contained).
2. **Thực Thi Trình Cài Đặt (Bởi Người Dùng/Quản Trị Viên):**
    - Người dùng chạy `Setup.CMSAgent.exe` **với quyền Administrator**.
    - **Bước 1: Sao Chép File Ứng Dụng:**
        - Trình cài đặt sao chép `CMSAgent.exe`, `CMSUpdater.exe`, `agent_config.json`, và các DLLs vào thư mục cài đặt (ví dụ: `C:\Program Files\CMSAgent`).
    - **Bước 2: Tạo Cấu Trúc Thư Mục Dữ Liệu:**
        - Trình cài đặt tạo thư mục dữ liệu chính cho agent (ví dụ: `C:\ProgramData\CMSAgent`).
        - Các thư mục con được tạo bên trong thư mục dữ liệu:
            - `logs/`: Để lưu trữ file log.
            - `runtime_config/`: Để lưu trữ file cấu hình runtime (`runtime_config.json`).
            - `updates/`: Thư mục tạm cho quá trình tải và giải nén gói cập nhật.
            - `error_reports/`: Để lưu các báo cáo lỗi chưa gửi được lên server.
3. **Thu Thập và Xác Thực Cấu Hình Runtime (qua `CMSAgent.exe configure`):**
    - **Kích Hoạt:** Sau khi sao chép file, trình cài đặt sẽ thực thi lệnh: `"<Đường_dẫn_cài_đặt>\CMSAgent.exe" configure`.
    - **Mở Giao Diện Dòng Lệnh (CLI):** `CMSAgent.exe configure` sẽ luôn mở một cửa sổ console để tương tác với người dùng nhằm thu thập thông tin cấu hình.
        - **Ví dụ tương tác mẫu:**
            
            ```
            CMS Agent Configuration Utility
            ---------------------------------
            Đang kiểm tra/tạo Device ID... Device ID: AGENT-XYZ123
            Vui lòng nhập tên phòng (Room Name): Phòng Họp A
            Vui lòng nhập tọa độ X: 10
            Vui lòng nhập tọa độ Y: 15
            Đang gửi thông tin đến server...
            
            ```
            
    - **Tạo/Kiểm Tra `device_id`:**
        - Kiểm tra file `runtime_config/runtime_config.json`. Nếu chưa có `device_id`, tự động tạo một `device_id` duy nhất (ví dụ: kết hợp hostname và MAC address) và lưu vào `runtime_config.json`.
    - **Vòng Lặp Nhập Thông Tin Vị Trí (Tương tác CLI):**
        1. **Yêu cầu `roomName`:** Hiển thị "Vui lòng nhập tên phòng (Room Name): ".
        2. **Yêu cầu `posX`:** Hiển thị "Vui lòng nhập tọa độ X: ".
        3. **Yêu cầu `posY`:** Hiển thị "Vui lòng nhập tọa độ Y: ".
        4. **Xác thực đầu vào cơ bản:** `roomName` không được trống, `posX`/`posY` phải là số (hoặc chuỗi số theo định dạng server chấp nhận). Nếu không hợp lệ, yêu cầu nhập lại. Nếu người dùng hủy (Ctrl+C), thoát tiến trình cấu hình.
    - **Gửi Yêu Cầu Định Danh Server (HTTP `POST /api/agent/identify`):**
        - **Request Payload:** (Xem chi tiết Phần VI.A.1)
    - **Xử Lý Phản Hồi Từ Server:**
        - **Lỗi Vị Trí (`status: "position_error"`):**
            - CLI hiển thị: "Lỗi: Thông tin vị trí không hợp lệ hoặc đã được sử dụng. Chi tiết từ server: [Nội dung message từ server]."
            - CLI hỏi: "Bạn có muốn thử nhập lại thông tin vị trí không? (Y/N): ". Nếu 'N', thoát. Nếu 'Y', quay lại yêu cầu `roomName`.
        - **Yêu Cầu MFA (`status: "mfa_required"`):**
            - CLI thông báo: "Xác thực thành công bước đầu. Server yêu cầu Xác thực Đa Yếu Tố (MFA)."
            - **Vòng Lặp Nhập Mã MFA (Tương tác CLI):**
                1. CLI yêu cầu: "Vui lòng nhập mã MFA từ ứng dụng xác thực của bạn: ". Người dùng nhập mã. Nếu bỏ trống hoặc hủy, thoát.
                2. Gửi HTTP `POST /api/agent/verify-mfa` với `unique_agent_id` và `mfaCode`.
                3. **MFA Thất Bại:** CLI hiển thị: "Lỗi: Mã MFA không chính xác hoặc đã hết hạn. Chi tiết từ server: [Nội dung message từ server]." Hỏi: "Bạn có muốn thử nhập lại không? (Y/N): ". Nếu 'N', thoát. Nếu 'Y', quay lại yêu cầu `mfa_code`.
                4. **MFA Thành Công (server trả `status: "success"` và `agentToken`):** Lưu `agentToken` tạm thời. CLI thông báo: "Xác thực MFA thành công."
        - **Định Danh Thành Công (có `agentToken`):** Lưu `agentToken` tạm thời. CLI thông báo: "Định danh agent thành công."
        - **Định Danh Thành Công (không `agentToken` mới - agent đã tồn tại):** Thử tải token cục bộ. Nếu không có, ghi log lỗi. CLI thông báo: "Agent đã được đăng ký trước đó."
        - **Lỗi Khác (ví dụ: HTTP 500, lỗi mạng):** CLI hiển thị: "Lỗi: Không thể kết nối đến server hoặc server gặp lỗi không xác định. Chi tiết: [Mô tả lỗi]." Hỏi: "Bạn có muốn thử lại không? (Y/N): ". Nếu 'N', thoát.
4. **Lưu Trữ Cấu Hình Runtime và Token:**
    - Sau khi server đã chấp nhận thông tin vị trí và `agentToken` đã được nhận:
        - Thông tin `room_config` và `agent_token` (đã mã hóa) được lưu vào `runtime_config/runtime_config.json`. (Xem chi tiết quy trình mã hóa ở Phần VIII.1)
        - CLI thông báo: "Đã lưu cấu hình và token."
5. **Đăng Ký và Khởi Động Windows Service (Bởi Trình Cài Đặt):**
    - Sau khi `CMSAgent.exe configure` hoàn tất thành công (trả về mã thoát 0), trình cài đặt `Setup.CMSAgent.exe` (vẫn đang chạy với quyền Administrator) sẽ thực hiện:
        - **Đăng Ký Service:** Gọi các hàm API của Windows hoặc sử dụng một thư viện/công cụ để đăng ký `CMSAgent.exe` làm Windows Service với các thông số:
            - `ServiceName`: Ví dụ, "CMSAgentService".
            - `DisplayName`: Ví dụ, "Computer Management System Agent".
            - `Description`: Mô tả chức năng.
            - `BinaryPathName`: Đường dẫn đầy đủ đến `CMSAgent.exe`.
            - `StartType`: `Automatic`.
            - `ServiceAccount`: `LocalSystem`.
        - Cấu hình các tùy chọn phục hồi cho service (ví dụ: tự động khởi động lại nếu lỗi).
        - **Khởi Động Service:** Gửi yêu cầu khởi động service vừa đăng ký tới Service Control Manager (SCM).
6. **Hoàn Tất Cài Đặt:**
    - Trình cài đặt `Setup.CMSAgent.exe` kết thúc, hiển thị thông báo cài đặt thành công cho người dùng.

### IV. Luồng Hoạt Động Thường Xuyên của Agent

Sau khi cài đặt và service đã khởi chạy, agent sẽ hoạt động như sau:

1. **Khởi Động Service (Bởi SCM khi Windows khởi động hoặc theo yêu cầu):**
    - **Thiết Lập Logging, Đảm Bảo Thư Mục, Đảm Bảo Single Instance.**
    - **Tải Cấu Hình và Trạng Thái:**
        - Đọc `agent_config.json`.
        - Đọc `runtime_config/runtime_config.json` (`device_id`, `room_config`, `agent_token_encrypted`).
        - Giải mã và tải `agent_token`. (Xem chi tiết quy trình giải mã ở Phần VIII.1)
    - **Kiểm Tra Tính Toàn Vẹn Cấu Hình:** Nếu thiếu, ghi log lỗi, dừng hoặc hoạt động hạn chế.
    - **Khởi Tạo Các Module.**
2. **Điều Kiện và Lưu Ý Quan Trọng Khi Agent Hoạt Động Như Một Windows Service:**
    - **Quyền Hoạt Động:**
        - **Tài Khoản Service:** Agent service thường được cấu hình để chạy dưới tài khoản `LocalSystem`. Tài khoản này có quyền truy cập rộng rãi trên hệ thống cục bộ.
        - **Quyền Truy Cập Thư Mục:** Đảm bảo tài khoản service (`LocalSystem`) có đủ quyền (Read, Write, Modify) trên thư mục cài đặt của agent và thư mục dữ liệu.
        - **Quyền Truy Cập Mạng:** Service cần quyền truy cập mạng để giao tiếp với server.
    - **Tính Sẵn Sàng của Cấu Hình:**
        - **File Cấu Hình Tĩnh (`agent_config.json`):** Phải tồn tại và hợp lệ.
        - **File Trạng Thái Động (`runtime_config.json`):** `device_id`, `room_config`, `agent_token_encrypted` phải tồn tại và hợp lệ.
        - Nếu thiếu hoặc lỗi, agent nên ghi log và có thể dừng hoặc hoạt động hạn chế.
    - **Kết Nối Mạng:** Yêu cầu kết nối mạng ổn định. Nếu không, ghi log và thử lại.
    - **Xử Lý Lỗi Khởi Động:** Xử lý lỗi trong `OnStart()` an toàn, ghi Event Log và báo SCM.
    - **Không Tương Tác Với Desktop:** Service không hiển thị UI. Mọi thông báo qua log.
    - **Quản Lý Tài Nguyên:** Cẩn thận để tránh rò rỉ tài nguyên (bộ nhớ, CPU, handles).
    - **Đường Dẫn Làm Việc:** Luôn dùng đường dẫn tuyệt đối hoặc tương đối với file thực thi, không dựa vào thư mục làm việc hiện tại của service (`C:\Windows\System32`).
    - **Cập Nhật Service:** `CMSUpdater.exe` cần quyền tương tác SCM.
3. **Xác Thực và Kết Nối Ban Đầu với Server:**
    - **Kết Nối WebSocket (Socket.IO):** Dùng `agent_id` và `agent_token`. Chờ `agent:ws_auth_success`. Nếu `agent:ws_auth_failed`, thử `POST /api/agent/identify` (không `forceRenewToken`). Nếu thành công và có token mới, cập nhật và thử lại WebSocket. Nếu yêu cầu MFA, ghi log và thử lại sau.
    - **Gửi Thông Tin Phần Cứng Ban Đầu (HTTP `POST /api/agent/hardware-info`):** Sau khi WebSocket xác thực, gửi thông tin phần cứng.
4. **Vòng Lặp Hoạt Động Chính (Sau khi WebSocket đã xác thực):**
    - **Gửi Báo Cáo Trạng Thái Định Kỳ (WebSocket `agent:status_update`):** (Xem chi tiết Phần VI.C)
    - **Kiểm Tra Cập Nhật Phiên Bản Mới:** Chủ động (HTTP) hoặc bị động (WebSocket). Nếu có, kích hoạt Luồng Cập Nhật.
    - **Xử Lý Lệnh Từ Server (WebSocket `command:execute`):** (Xem chi tiết Phần VI.B) Đưa vào hàng đợi, worker xử lý, gửi kết quả qua `agent:command_result`.
    - **Báo Cáo Lỗi Phát Sinh (HTTP `POST /api/agent/report-error`):** (Xem chi tiết Phần VI.A.5) Nếu lỗi, lưu vào `error_reports/`.
5. **Dừng Hoạt Động An Toàn (Khi SCM yêu cầu dừng service):**
    - Ngắt WebSocket, dừng worker, hủy timer, giải phóng Mutex. Ghi log, thoát.

### V. Luồng Cập Nhật Agent

Luồng này mô tả quá trình agent tự động cập nhật lên phiên bản mới khi phát hiện có sẵn.

1. **Kích Hoạt Cập Nhật:**
    - Agent nhận thông tin về phiên bản mới (URL tải tương đối, version, checksum) từ server.
2. **Chuẩn Bị Cập Nhật (Bên trong `CMSAgent.exe` hiện tại):**
    - Chuyển trạng thái agent sang `UPDATING`.
    - **Tải Gói Cập Nhật:** Xây dựng URL tuyệt đối từ `server_url` và `download_url` tương đối. Tải gói (ví dụ: `.zip`) về `updates/download/`.
    - **Xác Minh Tính Toàn Vẹn:** Tính SHA256 của file đã tải, so sánh với checksum từ server. Nếu không khớp, hủy cập nhật, ghi log.
    - **Giải Nén Gói Cập Nhật:** Giải nén vào `updates/extracted/agent_vX.Y.Z/`. Gói này chứa `CMSAgent.exe` mới và `CMSUpdater.exe` mới (nếu có).
    - **Xác Định File Thực Thi Updater:** Ưu tiên `CMSUpdater.exe` mới trong gói. Nếu không có hoặc lỗi, dùng bản updater hiện tại.
    - **Khởi Chạy `CMSUpdater.exe`:**
        - Chạy `CMSUpdater.exe` như một tiến trình riêng biệt.
        - **Truyền Tham Số Dòng Lệnh cho Updater:** `-pid`, `-new-agent-path`, `-current-agent-install-dir`, `-updater-log-dir`.
    - **Agent Cũ Tự Dừng:** Sau khi khởi chạy updater, `CMSAgent.exe` (service) bắt đầu quá trình dừng an toàn.
3. **Hoạt Động Của Tiến Trình Updater (`CMSUpdater.exe`):**
    - **Thiết Lập Logging Riêng:** Ghi log vào thư mục được chỉ định.
    - **Chờ Agent Cũ Dừng Hoàn Toàn:** Dùng PID, có timeout (ví dụ: 2-5 phút). Nếu timeout, ghi log lỗi và thoát.
    - **Sao Lưu Agent Cũ:** Đổi tên thư mục cài đặt của agent cũ (ví dụ: `CMSAgent_backup_<timestamp>`). Nếu lỗi, thoát.
    - **Triển Khai Agent Mới:** Di chuyển/sao chép nội dung từ thư mục agent mới đã giải nén vào thư mục cài đặt gốc.
        - **Nếu lỗi:** Thực hiện **Rollback** (khôi phục thư mục sao lưu của agent cũ), ghi log, thoát.
    - **Khởi Động Agent Mới (Windows Service):** Sử dụng lệnh SCM (ví dụ: `sc.exe start CMSAgentService`).
        - **Nếu lỗi:** Thực hiện **Rollback** (khôi phục agent cũ và thử khởi động lại service cũ), ghi log, thoát.
    - **Dọn Dẹp:** Nếu agent mới khởi động thành công:
        - Xóa thư mục sao lưu của agent cũ.
        - Xóa các file/thư mục tạm trong `updates/` (gói zip, thư mục giải nén).
    - Ghi log cập nhật thành công.
    - `CMSUpdater.exe` thoát.

### VI. Chuẩn Giao Tiếp Chi Tiết Agent-Server

**A. Giao Tiếp HTTP (API Endpoints)**

- **URL Cơ Sở API:** Được định nghĩa trong `agent_config.json` (trường `server_url`), ví dụ: `http://<your-server-ip>:3000/api/agent/`
- **Headers Chung (Cho các yêu cầu cần xác thực):**
    - `X-Agent-Id`: `<device_id>` (Giá trị `device_id` của agent)
    - `Authorization`: `Bearer <agent_token>` (Token nhận được sau khi xác thực)
    - `Content-Type`: `application/json` (Đối với các request có body là JSON)

**1. Định danh Agent (`POST /identify`)**

- **Mục đích:** Đăng ký agent mới hoặc định danh một agent đã tồn tại với server.
- **Request Payload (JSON):**
    
    ```
    {
        "unique_agent_id": "AGENT-HOSTNAME-MACADDRESS", // String: Device ID duy nhất của agent
        "positionInfo": {
            "roomName": "Phòng Lab A", // String: Tên phòng
            "posX": 10,               // Number: Tọa độ X (theo agent.controller.js)
            "posY": 15                // Number: Tọa độ Y (theo agent.controller.js)
        },
        "forceRenewToken": false      // Boolean (Tùy chọn, mặc định false): Yêu cầu làm mới token
    }
    
    ```
    
- **Response Payload (JSON) - Thành công (có token mới/gia hạn):**
    
    ```
    {
        "status": "success",
        "agentId": "AGENT-HOSTNAME-MACADDRESS", // String: ID của agent (thường là unique_agent_id)
        "agentToken": "new_or_renewed_plain_text_token_string" // String: Token xác thực mới
    }
    
    ```
    
- **Response Payload (JSON) - Thành công (agent đã tồn tại, token cũ còn hiệu lực):**
    
    ```
    {
        "status": "success"
        // Không có trường agentToken
    }
    
    ```
    
- **Response Payload (JSON) - Yêu cầu MFA:**
    
    ```
    {
        "status": "mfa_required",
        "message": "MFA is required for this agent." // String: Thông báo từ server
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi vị trí (HTTP 400):**
    
    ```
    {
        "status": "position_error",
        "message": "Position (10,15) in Room 'Phòng Lab A' is already occupied or invalid." // String: Mô tả lỗi
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi khác (ví dụ: `unique_agent_id` trống - HTTP 400):**
    
    ```
    {
        "status": "error",
        "message": "Agent ID is required" // String: Mô tả lỗi
    }
    
    ```
    

**2. Xác thực MFA (`POST /verify-mfa`)**

- **Mục đích:** Hoàn tất quá trình định danh bằng cách gửi mã MFA do người dùng cung cấp.
- **Request Payload (JSON):**
    
    ```
    {
        "unique_agent_id": "AGENT-HOSTNAME-MACADDRESS", // String: Device ID của agent
        "mfaCode": "123456"                           // String: Mã MFA người dùng nhập
    }
    
    ```
    
- **Response Payload (JSON) - Thành công:**
    
    ```
    {
        "status": "success",
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "agentToken": "plain_text_token_string_after_mfa" // String: Token xác thực
    }
    
    ```
    
- **Response Payload (JSON) - Thất bại (HTTP 401):**
    
    ```
    {
        "status": "error",
        "message": "Invalid or expired MFA code" // String: Mô tả lỗi
    }
    
    ```
    

**3. Gửi Thông Tin Phần Cứng (`POST /hardware-info`)**

- **Mục đích:** Cung cấp thông tin chi tiết về phần cứng của máy client cho server.
- **Request Payload (JSON):**
    
    ```
    {
        "os_info": "Microsoft Windows 10 Pro 10.0.19042 Build 19042", // String: Thông tin hệ điều hành
        "cpu_info": "Intel(R) Core(TM) i7-8700 CPU @ 3.20GHz",      // String: Thông tin CPU
        "gpu_info": "NVIDIA GeForce GTX 1080 Ti",                   // String: Thông tin GPU
        "total_ram": 17179869184,                                   // Number: Tổng RAM (bytes)
        "total_disk_space": 511749009408                           // Number: Tổng dung lượng ổ C: (bytes)
    }
    
    ```
    
    *Lưu ý: Agent C# cần đảm bảo gửi đúng các trường và kiểu dữ liệu mà `agent.controller.js` (hàm `handleHardwareInfo`) xử lý: `os_info`, `total_disk_space`, `gpu_info`, `cpu_info`, `total_ram`. Các trường này là tùy chọn từ phía backend controller, ngoại trừ `total_disk_space` là bắt buộc.*
    
- **Response - Thành công:** HTTP `204 No Content`.
- **Response Payload (JSON) - Lỗi (ví dụ: `total_disk_space` thiếu - HTTP 400):**
    
    ```
    {
        "status": "error",
        "message": "Total disk space is required"
    }
    
    ```
    

**4. Kiểm Tra Cập Nhật (`GET /check-update`)**

- **Mục đích:** Kiểm tra xem có phiên bản agent mới nào khả dụng trên server không.
- **Query Parameters:**
    - `current_version`: `1.0.2` (String: Phiên bản hiện tại của agent)
- **Response Payload (JSON) - Có cập nhật:**
    
    ```
    {
        "status": "success",
        "update_available": true,
        "version": "1.1.0",                                           // String: Phiên bản mới
        "download_url": "/download/agent-packages/agent_v1.1.0.zip",  // String: Đường dẫn tương đối để tải
        "checksum_sha256": "a1b2c3d4e5f6...",                         // String: SHA256 checksum của gói
        "notes": "Các tính năng mới và sửa lỗi."                      // String (hoặc null): Ghi chú phát hành
    }
    
    ```
    
- **Response - Không có cập nhật:** HTTP `204 No Content`.

**5. Báo Cáo Lỗi (`POST /report-error`)**

- **Mục đích:** Gửi thông tin về các lỗi phát sinh trong agent lên server.
- **Request Payload (JSON):**
    
    ```
    {
        "error_type": "UPDATE_DOWNLOAD_FAILED", // String: Phân loại lỗi
        "error_message": "Failed to download update package from server due to network timeout.", // String: Mô tả lỗi
        "error_details": {                      // Object: Chi tiết lỗi
            "stack_trace": "...",                 // String: Stack trace (nếu có)
            "agent_version": "1.0.2",           // String: Phiên bản agent khi lỗi
            "context_info": "Attempting to download from http://server/api/agent/download/agent-packages/agent_v1.1.0.zip" // String: Thông tin ngữ cảnh
        },
        "timestamp": "2025-05-11T10:30:00Z"   // String: Thời gian lỗi (ISO 8601)
    }
    
    ```
    
- **Response - Thành công:** HTTP `204 No Content`.

**6. Tải Gói Cập Nhật Agent (`GET /download/agent-packages/:filename`)**

- **Mục đích:** Tải file gói cập nhật. Yêu cầu xác thực agent.
- **URL Parameters:**
    - `:filename`: Tên file của gói cập nhật (ví dụ: `agent_v1.1.0.zip`).
- **Response:** Dữ liệu file (File stream).
- **Lỗi:** HTTP `404 Not Found` nếu file không tồn tại, HTTP `500 Internal Server Error` nếu có lỗi khác phía server.

**B. Giao Tiếp WebSocket (Socket.IO)**

- **URL Kết Nối:** Từ `server_url` trong `agent_config.json`.
- **Xác thực:** Gửi `agent_id` và `agent_token` trong headers hoặc payload `auth` khi kết nối.
- **Các Sự Kiện Server Gửi Cho Agent:**
    - **`agent:ws_auth_success`**
        - **Payload:** Thường rỗng hoặc `{ "message": "Authenticated" }`.
        - **Ý nghĩa:** Xác thực WebSocket thành công.
    - **`agent:ws_auth_failed`**
        - **Payload (JSON):** `{ "message": "Invalid token" }`
        - **Ý nghĩa:** Xác thực WebSocket thất bại.
    - **`command:execute`**
        - **Payload (JSON):**
            
            ```
            {
                "commandId": "cmd-uuid-12345abc", // String: ID duy nhất của lệnh
                "command": "ipconfig /all",       // String: Nội dung lệnh
                "commandType": "console"          // String: "console", "system", etc.
            }
            
            ```
            
        - **Ý nghĩa:** Yêu cầu agent thực thi lệnh.
    - **`agent:new_version_available`**
        - **Payload (JSON):**
            
            ```
            {
                "version": "1.1.0",
                "download_url": "/download/agent-packages/agent_v1.1.0.zip", // Đường dẫn tương đối
                "checksum_sha256": "a1b2c3d4e5f6..."
            }
            
            ```
            
        - **Ý nghĩa:** Thông báo có phiên bản agent mới.
- **Các Sự Kiện Agent Gửi Lên Server:**
    - **`agent:status_update`** (Chi tiết ở mục C)
    - **`agent:command_result`**
        - **Payload (JSON) - Ví dụ cho `commandType: "console"`:**
            
            ```
            {
                "agentId": "AGENT-XYZ123-DEVICEID",
                "commandId": "cmd-uuid-12345abc",
                "success": true, // Boolean: Trạng thái thực thi
                "type": "console", // String: Loại lệnh đã thực thi
                "result": {
                    "stdout": "Windows IP Configuration...\nEthernet adapter Ethernet:\n   Connection-specific DNS Suffix  . : home\n   IPv4 Address. . . . . . . . . . . : 192.168.1.100\n   Subnet Mask . . . . . . . . . . . : 255.255.255.0\n   Default Gateway . . . . . . . . . : 192.168.1.1",
                    "stderr": "",                         // String: Output lỗi
                    "exitCode": 0                         // Number: Mã thoát
                }
            }
            
            ```
            

**C. Thông Tin Trạng Thái (Stats) Gửi Lên Server (qua WebSocket `agent:status_update`)**

- **Mục đích:** Cung cấp thông tin tài nguyên hệ thống định kỳ.
- **Payload (JSON):**
    
    ```
    {
        "agentId": "AGENT-XYZ123-DEVICEID", // String: Device ID
        "cpuUsage": 25.5,                 // Number: % CPU sử dụng (0.0 - 100.0)
        "ramUsage": 60.1,                 // Number: % RAM sử dụng (0.0 - 100.0)
        "diskUsage": 75.0                 // Number: % Disk sử dụng ổ C: (0.0 - 100.0)
    }
    
    ```
    
- **Tần suất:** Theo `agent_settings.status_report_interval_sec`.

### VII. Cấu Hình Agent Chi Tiết

**1. Cấu Hình Tĩnh (Lưu trong `agent_config.json`)**

```
{
  "app_name": "CMSAgent",
  "version": "1.1.0",
  "server_url": "http://your-server.com:3000",
  "agent_settings": {
    "status_report_interval_sec": 30,
    "enable_auto_update": true,
    "auto_update_interval_sec": 86400
  },
  "http_client_settings": {
    "request_timeout_sec": 15
  },
  "websocket_settings": {
    "reconnect_delay_initial_sec": 5,
    "reconnect_delay_max_sec": 60,
    "reconnect_attempts_max": null
  },
  "command_executor_settings": {
    "default_timeout_sec": 300,
    "max_parallel_commands": 2,
    "max_queue_size": 100,
    "console_encoding": "utf-8"
  }
}

```

- **`app_name`**: Tên ứng dụng.
- **`version`**: Phiên bản agent.
- **`server_url`**: URL server backend. **Bắt buộc.**
- **`agent_settings`**:
    - `status_report_interval_sec`: Khoảng thời gian gửi status.
    - `enable_auto_update`: Bật/tắt tự động cập nhật.
    - `auto_update_interval_sec`: Khoảng thời gian kiểm tra cập nhật.
- **`http_client_settings`**:
    - `request_timeout_sec`: Timeout cho HTTP request.
- **`websocket_settings`**:
    - `reconnect_delay_initial_sec`: Thời gian chờ ban đầu để kết nối lại WebSocket.
    - `reconnect_delay_max_sec`: Thời gian chờ tối đa giữa các lần kết nối lại.
    - `reconnect_attempts_max`: Số lần thử kết nối lại tối đa (`null` = vô hạn).
- **`command_executor_settings`**:
    - `default_timeout_sec`: Timeout cho lệnh console.
    - `max_parallel_commands`: Số lệnh chạy song song tối đa.
    - `max_queue_size`: Kích thước hàng đợi lệnh.
    - `console_encoding`: Bảng mã cho output lệnh console.

**2. Cấu Hình Runtime (Lưu trong `runtime_config/runtime_config.json`)**

```
{
  "device_id": "AGENT-XYZ123-DEVICEID",
  "room_config": {
    "roomName": "Phòng Họp A",
    "posX": "10", // Được lưu dưới dạng chuỗi để nhất quán với cách agent Python hiện tại có thể gửi
    "posY": "15"  // Được lưu dưới dạng chuỗi
  },
  "agent_token_encrypted": "BASE64_ENCRYPTED_TOKEN_STRING" // Token đã được mã hóa
}

```

- **`device_id`**: Định danh duy nhất của agent. **Bắt buộc.**
- **`room_config`**: Thông tin vị trí. **Bắt buộc.**
    - `roomName`: Tên phòng.
    - `posX`: Tọa độ X (chuỗi).
    - `posY`: Tọa độ Y (chuỗi).
- **`agent_token_encrypted`**: Token đã mã hóa. **Bắt buộc.**

**3. Đường Dẫn Lưu Trữ**

- Thư mục gốc: `C:\ProgramData\CMSAgent` (ví dụ)
    - `logs/`
    - `runtime_config/runtime_config.json`
    - `updates/`
    - `error_reports/`

### VIII. Thông Tin về Logging

Việc ghi log hiệu quả là rất quan trọng để theo dõi hoạt động của agent và gỡ lỗi khi có sự cố.

1. **Vị Trí File Log:**
    - **Agent Service (`CMSAgent.exe`):**
        - File log chính: `C:\ProgramData\CMSAgent\logs\agent_YYYYMMDD.log` (ví dụ: `agent_20250511.log`).
        - Log được ghi theo ngày, có thể có cơ chế xoay vòng (ví dụ: giữ log trong 7 ngày, hoặc giới hạn kích thước file).
    - **Updater (`CMSUpdater.exe`):**
        - File log: `C:\ProgramData\CMSAgent\logs\updater_YYYYMMDD_HHMMSS.log` (ví dụ: `updater_20250511_103000.log`).
        - Mỗi lần chạy updater sẽ tạo một file log riêng biệt với timestamp để dễ theo dõi.
    - **Tiến trình cấu hình (`CMSAgent.exe configure`):**
        - Có thể ghi log ra một file riêng trong `C:\ProgramData\CMSAgent\logs\` (ví dụ: `configure_YYYYMMDD_HHMMSS.log`) hoặc ghi chung vào file log của agent với một định danh đặc biệt.
2. **Cấu Hình Mức Độ Log:**
    - Hệ thống logging (ví dụ: Serilog) nên cho phép cấu hình mức độ log (Verbose, Debug, Information, Warning, Error, Fatal) thông qua file `appsettings.json` của agent.
    - **Ví dụ `appsettings.json` cho Serilog:**
        
        ```
        {
          "Serilog": {
            "MinimumLevel": {
              "Default": "Information", // Mức log mặc định
              "Override": {
                "Microsoft": "Warning",
                "System": "Warning",
                "CMSAgent.WebSocketClient": "Debug" // Mức log chi tiết hơn cho module cụ thể
              }
            },
            "WriteTo": [
              {
                "Name": "Console" // Ghi ra console khi chạy ở chế độ debug
              },
              {
                "Name": "File",
                "Args": {
                  "path": "C:\\ProgramData\\CMSAgent\\logs\\agent_.log", // Dấu _ sẽ được thay bằng ngày
                  "rollingInterval": "Day", // Tạo file mới mỗi ngày
                  "retainedFileCountLimit": 7, // Giữ log trong 7 ngày
                  "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                }
              },
              {
                "Name": "EventLog", // Ghi vào Windows Event Log
                "Args": {
                  "source": "CMSAgentService", // Tên nguồn sự kiện
                  "logName": "Application",
                  "manageEventSource": true // Tự động tạo nguồn sự kiện nếu chưa có
                }
              }
            ]
          }
        }
        
        ```
        
3. **Cách Đọc và Hiểu Log:**
    - Mỗi dòng log thường bao gồm:
        - **Timestamp:** Thời gian xảy ra sự kiện.
        - **Level:** Mức độ nghiêm trọng (DEBUG, INFO, WARN, ERROR, FATAL).
        - **Source/Context:** Tên module hoặc lớp phát sinh log (giúp xác định nguồn gốc vấn đề).
        - **Message:** Nội dung thông điệp log.
        - **Exception (nếu có):** Chi tiết về ngoại lệ, bao gồm stack trace.
    - Khi gỡ lỗi, bắt đầu bằng việc tìm các log ERROR hoặc FATAL.
    - Log INFO và DEBUG cung cấp thông tin chi tiết về luồng hoạt động.
4. **Windows Event Log:**
    - Agent service nên ghi các sự kiện quan trọng (khởi động, dừng, lỗi nghiêm trọng) vào Windows Event Log (Application Log) với một "Source" tùy chỉnh (ví dụ: "CMSAgentService").
    - Điều này giúp quản trị viên hệ thống theo dõi trạng thái của service ngay cả khi không truy cập được file log.

### IX. Hướng Dẫn Khắc Phục Sự Cố (Troubleshooting)

Phần này cung cấp hướng dẫn xử lý một số sự cố phổ biến có thể xảy ra với CMSAgent.

1. **Agent Service Không Khởi Động:**
    - **Triệu chứng:** Service "CMSAgentService" không ở trạng thái "Running" trong Services MMC, hoặc cố gắng start nhưng bị dừng ngay.
    - **Nguyên nhân & Giải pháp:**
        - **Kiểm tra Windows Event Log (Application Log, System Log):** Tìm các lỗi liên quan đến "CMSAgentService". Event Log thường cung cấp thông tin chi tiết về lý do service không khởi động được.
        - **Thiếu file cấu hình hoặc file bị hỏng:** Đảm bảo `agent_config.json` và `runtime_config/runtime_config.json` tồn tại trong các thư mục tương ứng và có nội dung hợp lệ.
            - Nếu `agent_config.json` thiếu/hỏng: Sao chép lại từ gói cài đặt gốc hoặc một bản sao lưu.
            - Nếu `runtime_config.json` thiếu/hỏng: Có thể cần chạy lại `CMSAgent.exe configure` (với quyền admin) để tạo lại.
        - **Lỗi đọc/ghi vào thư mục dữ liệu:** Kiểm tra quyền truy cập của tài khoản `LocalSystem` trên thư mục `C:\ProgramData\CMSAgent` và các thư mục con. Đảm bảo `LocalSystem` có quyền Full Control.
        - **Lỗi Mutex (đã có instance khác chạy):** Kiểm tra Task Manager xem có tiến trình `CMSAgent.exe` nào khác đang chạy không. Nếu có, dừng nó lại và thử khởi động service.
        - **Thiếu .NET Runtime:** Đảm bảo phiên bản .NET Runtime yêu cầu đã được cài đặt.
        - **Lỗi trong code `OnStart()` của service:** Xem chi tiết lỗi trong Event Log hoặc file log của agent (nếu đã kịp ghi).
2. **Agent Không Kết Nối Được Server:**
    - **Triệu chứng:** Log của agent báo lỗi kết nối WebSocket hoặc HTTP, không thấy agent xuất hiện trên giao diện quản lý của server.
    - **Nguyên nhân & Giải pháp:**
        - **Kiểm tra `server_url` trong `agent_config.json`:** Đảm bảo URL chính xác và server có thể truy cập được từ máy agent (ví dụ: dùng `ping` hoặc mở URL trong trình duyệt).
        - **Kiểm tra kết nối mạng của máy agent:** Đảm bảo máy có mạng và có thể ra internet hoặc mạng nội bộ nơi có server.
        - **Tường lửa:** Kiểm tra tường lửa trên máy agent và trên server, đảm bảo không chặn kết nối đến port của server (HTTP/HTTPS và port WebSocket).
        - **Lỗi xác thực (`agent_token`):**
            - Token có thể đã hết hạn hoặc không hợp lệ.
            - Kiểm tra log của agent xem có thông báo lỗi `agent:ws_auth_failed` hoặc lỗi HTTP 401 không.
            - Nếu token hết hạn, agent nên tự động thử định danh lại. Nếu không thành công, có thể cần chạy `CMSAgent.exe configure` để lấy token mới (nếu server hỗ trợ làm mới token qua `identify` hoặc yêu cầu MFA lại).
        - **Lỗi chứng chỉ SSL/TLS (nếu dùng HTTPS/WSS):** Đảm bảo chứng chỉ của server hợp lệ và được máy agent tin cậy.
3. **Lỗi Trong Quá Trình Cấu Hình (`CMSAgent.exe configure`):**
    - **Triệu chứng:** CLI báo lỗi khi nhập thông tin phòng, MFA, hoặc khi giao tiếp với server.
    - **Nguyên nhân & Giải pháp:**
        - **Không có quyền Administrator:** Lệnh `configure` cần quyền admin để ghi file vào `C:\ProgramData`. Chạy lại CMD với "Run as administrator".
        - **Thông tin nhập không hợp lệ:** Làm theo hướng dẫn trên CLI để nhập lại.
        - **Lỗi kết nối server:** Kiểm tra mạng, URL server.
        - **Server từ chối thông tin (lỗi vị trí, MFA sai):** Làm theo thông báo lỗi từ server hiển thị trên CLI.
4. **Lỗi Trong Quá Trình Tự Cập Nhật:**
    - **Triệu chứng:** Agent không cập nhật lên phiên bản mới, hoặc cập nhật thất bại giữa chừng.
    - **Nguyên nhân & Giải pháp:**
        - **Kiểm tra log của `UpdateHandler` (trong file log agent) và log của `CMSUpdater.exe` (trong file log riêng của updater).**
        - **Lỗi tải gói cập nhật:** Kiểm tra kết nối mạng, URL tải file, dung lượng đĩa.
        - **Lỗi xác minh checksum:** Gói tải về có thể bị hỏng. Xóa file trong `updates/download/` và để agent thử lại.
        - **Lỗi giải nén:** Gói cập nhật có thể bị lỗi.
        - **`CMSUpdater.exe` không chạy được hoặc lỗi:** Kiểm tra quyền, file bị khóa.
        - **Không khởi động được service mới:** Xem lại mục "Agent Service Không Khởi Động".
        - **Rollback thất bại:** Đây là trường hợp xấu, có thể cần can thiệp thủ công để khôi phục agent.
5. **Lệnh Không Được Thực Thi hoặc Báo Lỗi:**
    - **Triệu chứng:** Gửi lệnh từ server nhưng agent không thực thi, hoặc thực thi nhưng báo lỗi.
    - **Nguyên nhân & Giải pháp:**
        - **Kiểm tra kết nối WebSocket:** Đảm bảo agent vẫn đang kết nối.
        - **Xem log của `CommandExecutor` và `ICommandHandler` tương ứng.**
        - **Lỗi trong nội dung lệnh:** Lệnh có thể sai cú pháp.
        - **Quyền thực thi:** Lệnh chạy với quyền của `LocalSystem`. Nếu lệnh cần quyền của người dùng cụ thể hoặc truy cập tài nguyên mạng dưới danh nghĩa người dùng, có thể không hoạt động.
        - **Timeout:** Lệnh chạy quá thời gian `command_executor_settings.default_timeout_sec`.

### X. Phụ Lục: Cấu Trúc Tham Số Dòng Lệnh và Ví Dụ

**A. `CMSAgent.exe`**

1. **`configure`**: Cấu hình agent lần đầu hoặc cấu hình lại.
    - **Tham số:** Không có. Luôn tương tác CLI.
    - **Hoạt động:** Yêu cầu nhập thông tin phòng, xác thực server (MFA nếu cần), lưu cấu hình.
    - **Yêu cầu quyền:** Administrator.
    - **Ví dụ sử dụng:**
        
        ```
        REM Được gọi bởi trình cài đặt Setup.CMSAgent.exe hoặc người dùng
        "C:\Program Files\CMSAgent\CMSAgent.exe" configure
        
        ```
        
        *(Người dùng sẽ thấy các lời nhắc nhập liệu trên console, ví dụ:*`Vui lòng nhập tên phòng (Room Name): Phòng Lab ChínhVui lòng nhập tọa độ X: 5Vui lòng nhập tọa độ Y: 12Đang gửi thông tin đến server...Server yêu cầu Xác thực Đa Yếu Tố (MFA).Vui lòng nhập mã MFA từ ứng dụng xác thực của bạn: 123456Xác thực MFA thành công.Đã lưu cấu hình và token.Hoàn tất cấu hình agent.` *)*
        
2. **`uninstall`**: Gỡ bỏ agent.
    - **Tham số tùy chọn:**
        - `-remove-data`: Nếu có cờ này, thư mục dữ liệu (`C:\ProgramData\CMSAgent`) sẽ bị xóa.
    - **Hoạt động:** Dừng service (nếu đang chạy), gỡ đăng ký Windows Service, xóa thư mục cài đặt. Nếu có `-remove-data`, xóa thư mục dữ liệu.
    - **Yêu cầu quyền:** Administrator.
    - **Ví dụ sử dụng:**
        
        ```
        "C:\Program Files\CMSAgent\CMSAgent.exe" uninstall
        ```batch
        "C:\Program Files\CMSAgent\CMSAgent.exe" uninstall --remove-data
        
        ```
        
        *(CLI sẽ hiển thị các bước đang thực hiện, ví dụ: "Đang dừng CMSAgentService...", "Đang gỡ đăng ký CMSAgentService...", "Đã xóa thư mục cài đặt.", "Đã xóa thư mục dữ liệu." (nếu có cờ))*
        
3. **`start`**: Khởi động Windows Service của agent.
    - **Mục đích:** Khởi động service nếu nó đã được đăng ký và đang ở trạng thái dừng.
    - **Hoạt động:** Gửi yêu cầu đến SCM để khởi động service "CMSAgentService".
    - **Yêu cầu quyền:** Administrator.
    - **Ví dụ sử dụng:**
        
        ```
        "C:\Program Files\CMSAgent\CMSAgent.exe" start
        
        ```
        
        *(CLI sẽ hiển thị: `Đang thử khởi động CMSAgentService...` và sau đó là `CMSAgentService đã được khởi động thành công.` hoặc `Lỗi: Không thể khởi động CMSAgentService. Chi tiết: [Thông báo lỗi từ SCM]`)*
        
4. **`stop`**: Dừng Windows Service của agent.
    - **Mục đích:** Dừng service nếu nó đang chạy.
    - **Hoạt động:** Gửi yêu cầu đến SCM để dừng service "CMSAgentService".
    - **Yêu cầu quyền:** Administrator.
    - **Ví dụ sử dụng:**
        
        ```
        "C:\Program Files\CMSAgent\CMSAgent.exe" stop
        
        ```
        
        *(CLI sẽ hiển thị: `Đang thử dừng CMSAgentService...` và sau đó là `CMSAgentService đã được dừng thành công.` hoặc `Lỗi: Không thể dừng CMSAgentService. Chi tiết: [Thông báo lỗi từ SCM]`)*
        
5. **`debug`**: Chạy agent trực tiếp trong console hiện tại cho mục đích gỡ lỗi.
    - **Mục đích:** Cho phép nhà phát triển chạy và gỡ lỗi agent mà không cần cài đặt/khởi động như một Windows Service.
    - **Hoạt động:**
        1. Không tương tác với SCM.
        2. Thực hiện các bước khởi tạo tương tự như khi service khởi động.
        3. Nếu `runtime_config.json` chưa có thông tin, có thể yêu cầu người dùng nhập liệu (không lưu vĩnh viễn nếu không có quyền ghi).
        4. Chạy vòng lặp hoạt động chính, log ra console.
        5. Ctrl+C sẽ kích hoạt quá trình dừng an toàn.
    - **Ví dụ sử dụng:**
        
        ```
        "C:\Program Files\CMSAgent\CMSAgent.exe" debug
        
        ```
        

**B. `CMSUpdater.exe`**

- **`-pid <process_id>`**: (Bắt buộc) Process ID của `CMSAgent.exe` (service) cũ.
- **`-new-agent-path "<đường_dẫn_thư_mục_agent_mới>"`**: (Bắt buộc)
- **`-current-agent-install-dir "<đường_dẫn_cài_đặt_agent_cũ>"`**: (Bắt buộc)
- **`-updater-log-dir "<đường_dẫn_thư_mục_logs>"`**: (Bắt buộc)

**Ví dụ gọi `CMSUpdater.exe`:**

```
"C:\Program Files\CMSAgent\CMSUpdater.exe" --pid 1234 --new-agent-path "C:\ProgramData\CMSAgent\updates\extracted\agent_v1.1.0" --current-agent-install-dir "C:\Program Files\CMSAgent" --updater-log-dir "C:\ProgramData\CMSAgent\logs"
```