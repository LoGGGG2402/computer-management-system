# Tài liệu Toàn Diện: Hoạt động, Giao tiếp và Cấu hình CMSAgent

**Ngày cập nhật:** 13 tháng 5 năm 2025

## I. Tổng Quan về Agent

CMSAgent là một ứng dụng chạy trên máy client với các nhiệm vụ chính sau:

- **Thu thập thông tin:** Lấy thông tin chi tiết về phần cứng hệ thống và theo dõi trạng thái sử dụng tài nguyên (CPU, RAM, disk) theo thời gian thực.
- **Giao tiếp với Server:** Thiết lập và duy trì kết nối an toàn với server trung tâm để gửi thông tin thu thập được và nhận các chỉ thị điều khiển.
- **Thực thi lệnh:** Nhận và thực thi các lệnh từ xa được gửi từ server (ví dụ: chạy script, thu thập log cụ thể).
- **Tự động cập nhật:** Có khả năng tự động tải và cài đặt phiên bản mới của chính nó khi có thông báo từ server.
- **Hoạt động ổn định:** Được thiết kế để chạy như một Windows Service, đảm bảo hoạt động nền, liên tục và tự khởi động cùng hệ thống.

## II. Môi Trường Hoạt Động và Yêu Cầu

- **Hệ Điều Hành Hỗ Trợ:**
    - Windows 10 (khuyến nghị phiên bản 1903 trở lên, 64-bit).
    - Windows 11 (64-bit).
    - Windows Server 2016, Windows Server 2019, Windows Server 2022 (64-bit).
    - *Lưu ý:* Cần kiểm tra kỹ lưỡng khả năng tương thích của các API hệ thống cụ thể (ví dụ: WMI, Performance Counters) nếu có kế hoạch hỗ trợ các phiên bản Windows cũ hơn hoặc phiên bản 32-bit.
- **Yêu Cầu Phần Mềm Phụ Thuộc:**
    - **.NET Runtime:** Phiên bản .NET mà agent được biên dịch (ví dụ: .NET 6.0 LTS hoặc .NET 8.0 LTS). Runtime này cần được cài đặt trên máy client nếu agent không được triển khai dưới dạng "self-contained application".
    - **Thư Viện Bên Ngoài (NuGet Packages Dự Kiến):**
        
        
        | Package | Phiên bản đề xuất | Ghi chú |
        | --- | --- | --- |
        | SocketIOClient.Net | 3.x.x | Giao tiếp WebSocket (Socket.IO) với server. |
        | Serilog | 2.x.x hoặc 3.x.x | Framework logging. |
        | Serilog.Sinks.File | 5.x.x | Ghi log vào file. |
        | Serilog.Sinks.Console | 3.x.x hoặc 4.x.x | Ghi log ra console (hữu ích khi debug). |
        | Serilog.Sinks.EventLog | 3.x.x | Ghi log vào Windows Event Log. |
        | System.Management | 6.0.x / 8.0.x | Truy cập Windows Management Instrumentation (WMI) lấy thông tin phần cứng. |
        | System.CommandLine | 2.0.0-betaX | Xử lý tham số dòng lệnh mạnh mẽ. |
        | Microsoft.Extensions.DependencyInjection | 6.0.x / 8.0.x | Triển khai Dependency Injection. |
        | Microsoft.Extensions.Hosting | 6.0.x / 8.0.x | Hỗ trợ host ứng dụng console như một Windows Service. |
        | Microsoft.Extensions.Hosting.WindowsServices | 6.0.x / 8.0.x | Tích hợp với Windows Services. |
        | Microsoft.Extensions.Logging | 6.0.x / 8.0.x | Framework logging cơ bản của .NET. |
        | Microsoft.Extensions.Logging.EventLog | 6.0.x / 8.0.x | Provider ghi log vào Event Log cho Microsoft.Extensions.Logging. |
- **Quyền Hạn Cần Thiết:**
    - **Trong Quá Trình Cài Đặt (Setup.CMSAgent.exe và CMSAgent.exe configure):**
        - Yêu cầu quyền Administrator để:
            - Ghi file vào thư mục cài đặt (ví dụ: C:\Program Files\CMSAgent).
            - Tạo và ghi file/thư mục vào thư mục dữ liệu chung (ví dụ: C:\ProgramData\CMSAgent).
            - Đăng ký, cấu hình và khởi động Windows Service.
    - **Khi Agent Hoạt Động Như Windows Service (chạy dưới tài khoản LocalSystem):**
        - Thư mục dữ liệu (C:\ProgramData\CMSAgent và các thư mục con): Tài khoản LocalSystem cần quyền Full Control.
        - Thư mục cài đặt (C:\Program Files\CMSAgent): Tài khoản LocalSystem cần quyền Read & Execute. Trong quá trình cập nhật, CMSUpdater.exe sẽ cần quyền Modify.
        - Truy cập mạng: Để kết nối đến server.
        - Đọc thông tin hệ thống: Quyền truy cập WMI, performance counters, registry.
        - Thực thi lệnh console: Lệnh sẽ chạy với quyền của LocalSystem.
        - *Xem chi tiết cách thiết lập quyền truy cập thư mục trong **Phần VIII.3**.*
    - **Khi Chạy Lệnh CLI (sau cài đặt bởi người dùng/quản trị viên):**
        - CMSAgent.exe start, stop, uninstall: Yêu cầu quyền Administrator.
        - CMSAgent.exe configure (để cấu hình lại): Yêu cầu quyền Administrator để ghi vào C:\ProgramData\CMSAgent\runtime_config\runtime_config.json.
        - CMSAgent.exe debug: Có thể chạy với quyền người dùng thông thường, nhưng khả năng truy cập một số tài nguyên hệ thống hoặc ghi vào ProgramData có thể bị hạn chế.

## III. Luồng Cài Đặt và Cấu Hình Ban Đầu

Luồng này mô tả quá trình từ khi người dùng thực thi file cài đặt cho đến khi agent được cài đặt, cấu hình lần đầu và bắt đầu hoạt động như một Windows Service.

1. **Chuẩn Bị Gói Cài Đặt (Developer Task):**
    - Một gói cài đặt (ví dụ: Setup.CMSAgent.exe) được tạo ra, chứa các thành phần:
        - CMSAgent.exe: File thực thi chính của agent.
        - CMSUpdater.exe: File thực thi cho tiến trình tự cập nhật.
        - `appsettings.json` (mặc định): File cấu hình chính mặc định.
        - Các thư viện DLL cần thiết khác.
2. **Thực Thi Trình Cài Đặt (Bởi Người Dùng/Quản Trị Viên):**
    - Người dùng chạy Setup.CMSAgent.exe với quyền Administrator.
3. **Bước 1: Sao Chép File Ứng Dụng:**
    - Trình cài đặt sao chép các file cần thiết vào thư mục cài đặt (ví dụ: C:\Program Files\CMSAgent).
4. **Bước 2: Tạo Cấu Trúc Thư Mục Dữ Liệu và Thiết Lập Quyền:**
    - Tạo thư mục dữ liệu chính (ví dụ: C:\ProgramData\CMSAgent) và các thư mục con: `logs/`, `runtime_config/`, `updates/`, `error_reports/`.
    - **Quan trọng:** Thiết lập quyền "Full Control" cho LocalSystem trên `C:\\ProgramData\\CMSAgent` và các thư mục con. (Xem Phần VIII.3).
5. **Thu Thập và Xác Thực Cấu Hình Runtime (qua `CMSAgent.exe configure`):**
    - **Kích Hoạt:** Trình cài đặt thực thi: `"<Đường_dẫn_cài_đặt>\\CMSAgent.exe" configure`.
    - **Tương tác CLI:** `CMSAgent.exe configure` mở console để thu thập thông tin vị trí và thực hiện xác thực ban đầu với server.
    - **Tạo/Kiểm Tra `agentId`:** Lưu `agentId` duy nhất vào `runtime_config/runtime_config.json`.
    - **Nhập Thông Tin Vị Trí và Xác Thực Server:** Yêu cầu `roomName`, `posX`, `posY`. Gửi yêu cầu định danh đến server. Xử lý phản hồi (lỗi vị trí, yêu cầu MFA, thành công).
    - **Xử lý hủy cấu hình:** Nếu người dùng hủy (Ctrl+C), thoát mà không lưu thay đổi (trừ `agentId`).
6. **Lưu Trữ Cấu Hình Runtime và Token:**
    - Sau khi xác thực thành công, lưu `room_config` và `agent_token` (đã mã hóa) vào `runtime_config/runtime_config.json`.
7. **Đăng Ký và Khởi Động Windows Service (Bởi Trình Cài Đặt):**
    - Đăng ký `CMSAgent.exe` làm Windows Service (ServiceName: "CMSAgentService", StartType: Automatic, Account: LocalSystem).
    - Khởi động service.
8. **Hoàn Tất Cài Đặt:** Thông báo cài đặt thành công.

## IV. Luồng Hoạt Động Thường Xuyên của Agent

1. **Khởi Động Service:** SCM khởi động `CMSAgent.exe`. Agent chuyển sang trạng thái `INITIALIZING`.
2. **Thiết Lập Logging:** Khởi tạo Serilog (đọc cấu hình từ `appsettings.json`). Ghi log trạng thái `INITIALIZING`.
3. **Đảm Bảo Cấu Trúc Thư Mục Dữ Liệu:** Tạo nếu chưa có.
4. **Đảm Bảo Chỉ Một Instance:** Sử dụng Mutex (Xem Phần VIII.5).
5. **Tải Cấu Hình:**
    - Đọc cấu hình từ `appsettings.json`.
    - Đọc `runtime_config/runtime_config.json`.
    - Xác thực cấu hình (Xem Phần VII.4).
    - Giải mã `agent_token`.
6. **Kiểm Tra Tính Toàn Vẹn Cấu Hình Runtime:** Nếu thiếu hoặc không hợp lệ, chuyển trạng thái `ERROR`.
7. **Khởi Tạo Module:** HTTP client, WebSocket client, giám sát tài nguyên, thực thi lệnh, xử lý cập nhật.
8. **Lưu Ý Khi Hoạt Động Như Windows Service:** Quyền hạn cần thiết, tính sẵn sàng của file cấu hình, kết nối mạng ổn định, xử lý lỗi an toàn trong `OnStart()`, không tương tác với desktop, quản lý tài nguyên cẩn thận, luôn sử dụng đường dẫn tuyệt đối hoặc tương đối với file thực thi, và `CMSUpdater.exe` cần quyền tương tác SCM.
9. **Xác Thực và Kết Nối Ban Đầu với Server:**
    - Agent chuyển sang trạng thái `AUTHENTICATING`. Ghi log trạng thái.
    - **Kết Nối WebSocket (Socket.IO):**
        - Agent sử dụng `agentId` (là `agentId`) và `agent_token` để thiết lập kết nối WebSocket đến server.
        - **Trong quá trình handshake của WebSocket, Agent BẮT BUỘC gửi header `x-client-type: agent`.**
        - **Agent BẮT BUỘC gửi các header sau trong quá trình handshake:**
            - `Authorization: Bearer <agent_token>`
            - `X-Agent-Id: <agentId>` 
        - Server middleware sẽ tự động cố gắng trích xuất `authToken` và `agentId` từ các header này và lưu vào `socket.data`.
        - Logic xác thực đầy đủ phía server (trong `setupAgentHandlers`) sẽ sử dụng thông tin trong `socket.data` (nếu có từ header) hoặc có thể chờ sự kiện `agent:authenticate` nếu thông tin từ header không đủ hoặc không được gửi.
        - **Xác thực qua Sự kiện (Dự phòng):** Nếu agent không gửi các header xác thực, hoặc nếu logic phía server (trong `setupAgentHandlers`) xác định thông tin từ header không hợp lệ/đủ, server có thể chờ agent gửi sự kiện `agent:authenticate` với payload `{ agentId, token }`.
        - Lắng nghe sự kiện `agent:ws_auth_success` từ server. Khi nhận được, chuyển sang trạng thái `CONNECTED`. Ghi log trạng thái.
        - Nếu nhận `agent:ws_auth_failed` (ví dụ, token hết hạn/không hợp lệ):
            - Ghi log lỗi.
            - Thử thực hiện lại quy trình POST `/api/agent/identify` (sử dụng `agentId` và `room_config` đã lưu, không `forceRenewToken`).
            - Nếu `identify` thành công và nhận được token mới, cập nhật token cục bộ (mã hóa và lưu vào `runtime_config.json`), quay lại bước kết nối WebSocket (bao gồm gửi các header cần thiết).
            - Nếu `identify` yêu cầu MFA, agent trong ngữ cảnh service không thể xử lý, sẽ ghi log lỗi và chuyển sang trạng thái `DISCONNECTED`, thử lại sau một khoảng thời gian.
            - Nếu `identify` thất bại vì lý do khác, ghi log lỗi, chuyển sang trạng thái `DISCONNECTED`, thử lại sau.
    - **Gửi Thông Tin Phần Cứng Ban Đầu (HTTP POST `/api/agent/hardware-info`):**
        - Sau khi kết nối WebSocket được xác thực (`CONNECTED`) hoặc sau khi có token hợp lệ từ HTTP `identify`, thu thập thông tin phần cứng chi tiết.
        - Gửi thông tin này lên server. Nếu thất bại, ghi log lỗi và tiếp tục.
10. **Vòng Lặp Hoạt Động Chính (Trạng thái `CONNECTED`):**
    - Gửi báo cáo trạng thái định kỳ (WebSocket `agent:status_update`).
    - Kiểm tra cập nhật (GET `/api/agent/check-update` hoặc WebSocket `agent:new_version_available`). Nếu có, chuyển trạng thái `UPDATING`.
    - Xử lý lệnh từ server (WebSocket `command:execute`).
    - Báo cáo lỗi phát sinh (POST `/api/agent/report-error`). Nếu thất bại, lưu vào `error_reports/`.
11. **Xử Lý Mất Kết Nối (Trạng thái `DISCONNECTED`):**
    - SocketIOClient.Net tự động thử kết nối lại.
    - Tạm dừng gửi báo cáo trạng thái. Lưu trữ tạm dữ liệu (Xem IV.12).
    - Khi kết nối lại, chuyển về `AUTHENTICATING` -> `CONNECTED`.
12. **Hoạt Động Offline và Lưu Trữ Tạm (Queueing):**
    - Lưu trữ tạm trên đĩa cho: báo cáo trạng thái, kết quả lệnh, báo cáo lỗi.
    - Giới hạn lưu trữ cấu hình trong `appsettings.json`.
    - Gửi lại khi kết nối được khôi phục.
13. **Quản Lý Trạng Thái Nội Bộ:** Agent quản lý và ghi log các trạng thái hoạt động chính.
    
    
    | Trạng thái | Ý nghĩa |
    | --- | --- |
    | `INITIALIZING` | Agent khởi động, đang tải cấu hình và các module ban đầu. |
    | `AUTHENTICATING` | Đang trong quá trình kết nối và xác thực WebSocket với server. |
    | `CONNECTED` | Đã kết nối và xác thực thành công với server, hoạt động bình thường. |
    | `DISCONNECTED` | Mất kết nối với server, đang trong quá trình thử kết nối lại tự động. |
    | `UPDATING` | Đang trong quá trình tải xuống và chuẩn bị cho việc cập nhật phiên bản mới. |
    | `ERROR` | Gặp lỗi nghiêm trọng không thể phục hồi (ví dụ: cấu hình hỏng), không thể hoạt động. |
    | `STOPPING` | Đang trong quá trình dừng hoạt động một cách an toàn (ví dụ: khi SCM yêu cầu). |
14. **Dừng Hoạt Động An Toàn:** Chuyển trạng thái `STOPPING`. Ngắt WebSocket, hoàn thành lệnh đang chạy, hủy timer, giải phóng Mutex.

## V. Luồng Cập Nhật Agent

1. **Kích Hoạt Cập Nhật:** Nhận thông tin phiên bản mới từ server.
2. **Chuẩn Bị Cập Nhật:**
    - Chuyển trạng thái `UPDATING`.
    - Thông báo server (`agent:update_status` với `status: "update_started"`).
    - Tải gói cập nhật. Thông báo server (`status: "update_downloaded"`).
    - Xác minh checksum. Nếu lỗi, thông báo server (`status: "update_failed", reason: "checksum_mismatch"`), quay lại `CONNECTED`.
    - Giải nén gói. Thông báo server (`status: "update_extracted"`).
    - Xác định file `CMSUpdater.exe`.
    - Khởi chạy `CMSUpdater.exe`. Thông báo server (`status: "updater_launched"`).
3. **Agent Cũ Tự Dừng:** Chuyển trạng thái `STOPPING`.
4. **Hoạt Động Của Tiến Trình Updater (`CMSUpdater.exe`):**
    - Chờ agent cũ dừng.
    - Sao lưu agent cũ.
    - Triển khai agent mới. Nếu lỗi, rollback.
    - Khởi động agent mới. Nếu lỗi, rollback, agent cũ (nếu khôi phục được) báo lỗi (`status: "update_failed", reason: "service_start_failed"`).
    - **Xử lý Crash Sau Cập Nhật (Rollback Nâng Cao):** Cơ chế "Watchdog" trong Updater tự động rollback nếu agent mới crash liên tục.
    - **Dọn Dẹp:** Nếu thành công, xóa backup, file tạm. Agent mới báo thành công (`status: "update_success"`) khi khởi động.
5. **Xử lý lỗi trong quá trình cập nhật:**
    - **Không tải được gói cập nhật:** Agent ghi log lỗi `UPDATE_DOWNLOAD_FAILED`, thử lại theo cơ chế retry (sử dụng `NetworkRetryMaxAttempts` và `NetworkRetryInitialDelaySec` từ `CMSAgentSettings:AgentSettings` trong `appsettings.json`). Nếu thất bại hoàn toàn sau các lần thử lại, agent sẽ thông báo lỗi lên server (ví dụ: `agent:update_status` với `status: "update_failed", reason: "download_failed"`) và chuyển về trạng thái `CONNECTED`.
    - **Checksum không khớp:** Agent xóa file đã tải về, ghi log lỗi `UPDATE_CHECKSUM_MISMATCH`, thông báo server (`agent:update_status` với `status: "update_failed", reason: "checksum_mismatch"`), và quay lại trạng thái `CONNECTED`.

## VI. Chuẩn Giao Tiếp Chi Tiết Agent-Server

### A. Giao Tiếp HTTP (API Endpoints)

- **URL Cơ Sở API:** Được định nghĩa trong `appsettings.json` (ví dụ: section `CMSAgentSettings:ServerUrl`), ví dụ: `https://your-server.com:3000/api/agent/`.
- **Headers Chung (Cho các yêu cầu cần xác thực):**
    - `X-Agent-Id`: `<agentId>` (Giá trị `agentId` của agent)
    - `Authorization`: `Bearer <agent_token>` (Token nhận được sau khi xác thực)
    - `Content-Type`: `application/json` (Đối với các request có body là JSON)

**1. Định danh Agent (POST `/identify`)**

- **Mục đích:** Đăng ký agent mới hoặc định danh một agent đã tồn tại với server.
- **Request Payload (JSON):**
    
    ```
    {
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "positionInfo": {
            "roomName": "Phòng Lab A",
            "posX": 10,
            "posY": 15
        },
        "forceRenewToken": false
    }
    
    ```
    
    - `agentId` (String, Bắt buộc): Device ID duy nhất của agent.
    - `positionInfo` (Object, Bắt buộc):
        - `roomName` (String, Bắt buộc): Tên phòng.
        - `posX` (Number, Bắt buộc): Tọa độ X.
        - `posY` (Number, Bắt buộc): Tọa độ Y.
    - `forceRenewToken` (Boolean, Tùy chọn, Mặc định: `false`): Nếu `true`, yêu cầu server cấp token mới ngay cả khi agent đã có token hợp lệ.
- **Response Payload (JSON) - Thành công (có token mới/gia hạn):**
    
    ```
    {
        "status": "success",
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "agentToken": "new_or_renewed_plain_text_token_string"
    }
    
    ```
    
- **Response Payload (JSON) - Thành công (agent đã tồn tại, token cũ còn hiệu lực, `forceRenewToken` là `false`):**
    
    ```
    {
        "status": "success"
    }
    
    ```
    
- **Response Payload (JSON) - Yêu cầu MFA:**
    
    ```
    {
        "status": "mfa_required",
        "message": "MFA is required for this agent."
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi vị trí (HTTP 400):**
    
    ```
    {
        "status": "position_error",
        "message": "Position (10,15) in Room 'Phòng Lab A' is already occupied or invalid."
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi khác (ví dụ: `agentId` trống - HTTP 400):**
    
    ```
    {
        "status": "error",
        "message": "Agent ID is required"
    }
    
    ```
    

**2. Xác thực MFA (POST `/verify-mfa`)**

- **Mục đích:** Hoàn tất quá trình định danh bằng cách gửi mã MFA do người dùng cung cấp.
- **Request Payload (JSON):**
    
    ```
    {
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "mfaCode": "123456"
    }
    
    ```
    
- **Response Payload (JSON) - Thành công:**
    
    ```
    {
        "status": "success",
        "agentId": "AGENT-HOSTNAME-MACADDRESS",
        "agentToken": "plain_text_token_string_after_mfa"
    }
    
    ```
    
- **Response Payload (JSON) - Thất bại (HTTP 401):**
    
    ```
    {
        "status": "error",
        "message": "Invalid or expired MFA code"
    }
    
    ```
    

**3. Gửi Thông Tin Phần Cứng (POST `/hardware-info`)**

- **Mục đích:** Cung cấp thông tin chi tiết về phần cứng của máy client cho server.
- **Request Payload (JSON):**
    
    ```
    {
        "os_info": "Microsoft Windows 10 Pro 10.0.19042 Build 19042",
        "cpu_info": "Intel(R) Core(TM) i7-8700 CPU @ 3.20GHz",
        "gpu_info": "NVIDIA GeForce GTX 1080 Ti",
        "total_ram": 17179869184,
        "total_disk_space": 511749009408
    }
    
    ```
    
    - `os_info` (String): Thông tin hệ điều hành.
    - `cpu_info` (String): Thông tin CPU.
    - `gpu_info` (String): Thông tin GPU.
    - `total_ram` (Number): Tổng RAM (bytes).
    - `total_disk_space` (Number, Bắt buộc): Tổng dung lượng ổ C: (bytes).
- **Response - Thành công:** HTTP 204 No Content.
- **Response Payload (JSON) - Lỗi (HTTP 400, ví dụ `total_disk_space` thiếu):**
    
    ```
    {
        "status": "error",
        "message": "Total disk space is required"
    }
    
    ```
    

**4. Kiểm Tra Cập Nhật (GET `/check-update`)**

- **Mục đích:** Kiểm tra xem có phiên bản agent mới nào khả dụng trên server không.
- **Query Parameters:**
    - `current_version` (String): Phiên bản hiện tại của agent (ví dụ: "1.0.2", lấy từ `appsettings.json` hoặc assembly).
- **Response Payload (JSON) - Có cập nhật:**
    
    ```
    {
        "status": "success",
        "update_available": true,
        "version": "1.1.0",
        "download_url": "/download/agent-packages/agent_v1.1.0.zip",
        "checksum_sha256": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2",
        "notes": "Các tính năng mới và sửa lỗi quan trọng."
    }
    
    ```
    
- **Response - Không có cập nhật:** HTTP 204 No Content.

**5. Báo Cáo Lỗi (POST `/report-error`)**

- **Mục đích:** Gửi thông tin về các lỗi phát sinh trong agent lên server.
- **Request Payload (JSON):**
    
    ```
    {
        "error_type": "WEBSOCKET_CONNECTION_FAILED",
        "error_message": "Failed to connect to WebSocket after multiple retries.",
        "error_details": {
            "stack_trace": "...",
            "agent_version": "1.0.2",
            "server_url_attempted": "wss://server.example.com/socket.io/",
            "last_error_code": "CONNECTION_REFUSED",
            "retry_attempts": 5
        },
        "timestamp": "2025-05-11T10:30:00Z"
    }
    
    ```
    
    - **`error_type` (String, Bắt buộc):** Phân loại lỗi. Các giá trị ví dụ:
        - `WEBSOCKET_CONNECTION_FAILED`
        - `WEBSOCKET_AUTH_FAILED`
        - `HTTP_REQUEST_FAILED`
        - `CONFIG_LOAD_FAILED`
        - `CONFIG_VALIDATION_FAILED`
        - `TOKEN_DECRYPTION_FAILED`
        - `HARDWARE_INFO_COLLECTION_FAILED`
        - `STATUS_REPORTING_FAILED`
        - `COMMAND_EXECUTION_FAILED`
        - `COMMAND_QUEUE_FULL`
        - `UPDATE_DOWNLOAD_FAILED`
        - `UPDATE_CHECKSUM_MISMATCH`
        - `UPDATE_EXTRACTION_FAILED`
        - `UPDATE_ROLLBACK_FAILED`
        - `UPDATE_SERVICE_START_FAILED`
        - `LOGGING_FAILED`
        - `RESOURCE_LIMIT_EXCEEDED`
        - `UNHANDLED_EXCEPTION`
        - `OFFLINE_QUEUE_ERROR`
        - `LOG_UPLOAD_REQUESTED`
- **Payload ví dụ cho `error_type: "LOG_UPLOAD_REQUESTED"`:**
    
    ```
    {
        "error_type": "LOG_UPLOAD_REQUESTED",
        "error_message": "Log upload requested by server for specific date range/file",
        "error_details": {
            "log_filename": "agent_logs_20250510-20250512.zip",
            "log_content_base64": "UEsDBBQAAgAI..."
        },
        "timestamp": "2025-05-12T14:00:00Z"
    }
    
    ```
    
- **Response - Thành công:** HTTP 204 No Content.

**6. Tải Gói Cập Nhật Agent (GET `/download/agent-packages/:filename`)**

- **Mục đích:** Tải file gói cập nhật. Yêu cầu xác thực agent.
- **URL Parameters:**
    - `:filename` (String): Tên file của gói cập nhật (ví dụ: `agent_v1.1.0.zip`).
- **Response:** Dữ liệu file (File stream).
- **Lỗi:** HTTP 404 Not Found nếu file không tồn tại, HTTP 401 Unauthorized nếu token không hợp lệ, HTTP 500 Internal Server Error nếu có lỗi khác.

### B. Giao Tiếp WebSocket (Socket.IO)

- **URL Kết Nối:** Từ `ServerUrl` trong `appsettings.json`.
- **Xác thực:**
    - Agent **BẮT BUỘC** gửi header `x-client-type: agent` trong quá trình handshake.
    - Agent **BẮT BUỘC** gửi `agentId` và `token` trong `socket.handshake.headers` (cụ thể là `agent-id` và `Authorization: Bearer <token>`). Server middleware (`io.use`) sẽ trích xuất các thông tin này.
- **Các Sự Kiện Server Gửi Cho Agent:**
    - `agent:ws_auth_success`: Payload: `{ "status": "success", "message": "Authentication successful" }`. Ý nghĩa: Xác thực WebSocket thành công.
    - `agent:ws_auth_failed`: Payload: `{ "status": "error", "message": "Authentication failed (Invalid ID or token)" }`. Ý nghĩa: Xác thực WebSocket thất bại.
    - `command:execute`: Payload: `{ "commandId": "...", "command": "...", "commandType": "..." }`. Ý nghĩa: Yêu cầu agent thực thi lệnh.
    - `agent:new_version_available`: Payload: `{ "new_stable_version": "...", "timestamp": "..." }`. Ý nghĩa: Thông báo có phiên bản agent mới.
- **Các Sự Kiện Agent Gửi Lên Server:**
    - `agent:authenticate`: Payload: `{ "agentId": "...", "token": "..." }`.
    - `agent:status_update`: Payload chi tiết ở mục C.
    - `agent:command_result`: Payload ví dụ: `{ "commandId": "...", "success": true/false, "type": "...", "result": { ... } }`.
    - `agent:update_status`: Payload: `{ "status": "...", "reason": "...", "new_version": "..." }`.

### C. Thông Tin Trạng Thái (Stats) Gửi Lên Server (qua WebSocket `agent:status_update`)

- **Payload (JSON):**
    
    ```
    {
        "cpuUsage": 25.5,
        "ramUsage": 60.1,
        "diskUsage": 75.0
    }
    
    ```
    
- **Tần suất:** Theo `StatusReportIntervalSec` trong `appsettings.json`.

## VII. Cấu Hình Agent Chi Tiết

**1. Cấu Hình Ứng Dụng (Lưu trong `appsettings.json`)**

File `appsettings.json` là file cấu hình chính. *Lưu ý: Từ phiên bản 7.3, file này thay thế hoàn toàn `agent_config.json` trước đây.*

```
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "CMSAgent.Communication.SocketIOClientWrapper": "Debug",
        "CMSAgent.Core.UpdateHandler": "Debug"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\ProgramData\\CMSAgent\\logs\\agent_.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 14,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "EventLog",
        "Args": {
          "source": "CMSAgentService",
          "logName": "Application",
          "manageEventSource": true
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  },
  "CMSAgentSettings": {
    "AppName": "CMSAgent",
    "Version": "1.1.0",
    "ServerUrl": "https://your-server.com:3000",
    "AgentSettings": {
      "StatusReportIntervalSec": 30,
      "EnableAutoUpdate": true,
      "AutoUpdateIntervalSec": 86400,
      "NetworkRetryMaxAttempts": 5,
      "NetworkRetryInitialDelaySec": 5,
      "TokenRefreshIntervalSec": 86400,
      "OfflineQueue": {
        "MaxSizeMb": 100,
        "MaxAgeHours": 24,
        "StatusReportsMaxCount": 1000,
        "CommandResultsMaxCount": 500,
        "ErrorReportsMaxCount": 200
      }
    },
    "HttpClientSettings": {
      "RequestTimeoutSec": 15
    },
    "WebSocketSettings": {
      "ReconnectDelayInitialSec": 5,
      "ReconnectDelayMaxSec": 60,
      "ReconnectAttemptsMax": null
    },
    "CommandExecutorSettings": {
      "DefaultTimeoutSec": 300,
      "MaxParallelCommands": 2,
      "MaxQueueSize": 100,
      "ConsoleEncoding": "utf-8"
    },
    "ResourceLimits": {
      "MaxCpuPercentage": 75,
      "MaxRamMegabytes": 512
    }
  }
}

```

**2. Cấu Hình Runtime (Lưu trong `runtime_config/runtime_config.json`)**

```
{
  "agentId": "AGENT-XYZ123-DEVICEID",
  "room_config": {
    "roomName": "Phòng Họp A",
    "posX": 10,
    "posY": 15
  },
  "agent_token_encrypted": "BASE64_ENCRYPTED_TOKEN_STRING"
}

```

**3. Đường Dẫn Lưu Trữ**

- `C:\\ProgramData\\CMSAgent`
    - `logs/`
    - `runtime_config/runtime_config.json`
    - `updates/`
        - `download/`
        - `extracted/`
        - `backup/`
    - `error_reports/` (Sử dụng làm queue offline cho báo cáo lỗi)
    - `offline_queue/` (Thư mục chung cho các queue offline khác: status_reports, command_results)

**4. Xác thực Cấu hình**

- Agent sử dụng .NET Options Pattern để tải và xác thực các section cấu hình từ `appsettings.json` (ví dụ: `CMSAgentSettings`). Các lớp Options tương ứng (ví dụ: `CmsAgentSettingsOptions.cs`) sử dụng Data Annotations để kiểm tra tính hợp lệ của dữ liệu.
- **Ví dụ về .NET Options Pattern:** Trong `Program.cs` hoặc nơi khởi tạo service, cấu hình được binding:
    
    ```
    // Giả sử builder.Configuration đã nạp appsettings.json
    services.Configure<CmsAgentSettingsOptions>(
        builder.Configuration.GetSection("CMSAgentSettings")
    );
    // Sau đó, CmsAgentSettingsOptions có thể được inject và sử dụng.
    // Các thuộc tính trong CmsAgentSettingsOptions có thể được trang trí bằng Data Annotations
    // [Required], [Range(1, 3600)], [Url] để tự động xác thực khi options được tạo.
    
    ```
    
- Đối với `runtime_config.json`, agent sẽ thực hiện kiểm tra thủ công sự tồn tại và định dạng cơ bản của các trường bắt buộc khi tải.

## VIII. Bảo Mật

**1. Mã Hóa Token (`agent_token_encrypted` trong `runtime_config.json`)**

- **Mã Hóa:** Khi lệnh `CMSAgent.exe configure` nhận được `agentToken` (dạng plain text) từ server, trước khi lưu vào `runtime_config.json`, token này sẽ được mã hóa. Sử dụng `System.Security.Cryptography.ProtectedData.Protect(Encoding.UTF8.GetBytes(plainToken), null, DataProtectionScope.LocalMachine)`. `userData` là `agentToken` chuyển sang `byte[]`. `optionalEntropy` có thể là `null`. `scope` là `DataProtectionScope.LocalMachine`. Kết quả `byte[]` đã mã hóa sẽ được chuyển đổi sang chuỗi Base64 để lưu vào file JSON.
- **Giải Mã:** Khi `CMSAgent.exe` (chạy như Windows Service) khởi động, đọc chuỗi Base64 `agent_token_encrypted` từ `runtime_config.json`. Chuyển đổi Base64 trở lại `byte[]`. Sử dụng `System.Security.Cryptography.ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine)`. Kết quả `byte[]` được giải mã sẽ được chuyển đổi trở lại thành chuỗi `agentToken` (UTF8).
- **Quản lý khóa:** Khóa mã hóa được Windows quản lý tự động thông qua Data Protection API (DPAPI) và gắn với máy cục bộ khi dùng `DataProtectionScope.LocalMachine`.

**2. Bảo Mật Kết Nối**

- Bắt buộc HTTPS cho API và WSS cho WebSocket.
- Cân nhắc certificate pinning.

**3. Quyền Truy Cập Thư Mục và Thiết Lập**

- **Thư mục cài đặt (`C:\\Program Files\\CMSAgent` hoặc tương đương):**
    - Quyền Read & Execute cho tài khoản LocalSystem (tài khoản chạy service) và nhóm Authenticated Users.
    - Quyền Modify cho Administrators và SYSTEM để cho phép cập nhật và gỡ cài đặt.
    - Trong quá trình cập nhật, `CMSUpdater.exe` (nếu chạy với quyền LocalSystem hoặc được nâng quyền lên Administrator) sẽ có quyền ghi đè file.
- **Thư mục dữ liệu (`C:\\ProgramData\\CMSAgent`):**
    - Tài khoản LocalSystem cần quyền Full Control trên thư mục này và các thư mục con (`logs`, `runtime_config`, `updates`, `error_reports`, `offline_queue`).
    - Nhóm Administrators nên có quyền Full Control để quản lý và xem xét.
    - Người dùng thông thường (ví dụ: Authenticated Users) không nên có quyền ghi vào thư mục `runtime_config` để bảo vệ cấu hình và token. Quyền đọc log có thể được xem xét tùy theo chính sách.
- **Cách thiết lập quyền (lệnh `icacls` đã kiểm chứng và hoàn chỉnh):**
    - Cho thư mục dữ liệu chính:
        
        ```
        icacls "C:\ProgramData\CMSAgent" /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)F" /inheritance:r /Q
        
        ```
        
        *Giải thích: `(OI)` - Object Inherit, `(CI)` - Container Inherit, `F` - Full Control. `/inheritance:r` - Xóa bỏ các quyền kế thừa cũ trước khi áp dụng quyền mới. `/Q` - Quiet mode.*
        
    - Cho thư mục cấu hình runtime (hạn chế quyền cho Administrators):
        
        ```
        icacls "C:\ProgramData\CMSAgent\runtime_config" /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)RX" /inheritance:r /Q
        
        ```
        
        *Giải thích: `RX` - Read & Execute.*
        
    - *Lưu ý: Các lệnh này cần được thực thi với quyền Administrator trong quá trình cài đặt.*

**4. Giảm Thiểu Rủi Ro Với Tài Khoản LocalSystem**

- **Nguyên tắc Least Privilege:** Nghiên cứu khả năng sử dụng một tài khoản dịch vụ tùy chỉnh (Custom Service Account) với chỉ những quyền tối thiểu cần thiết. Điều này phức tạp hơn trong việc thiết lập quyền nhưng an toàn hơn.
- **Xác thực và Phân quyền Lệnh Từ Xa:**
    - Server nên xác thực mạnh mẽ các yêu cầu từ frontend trước khi gửi lệnh đến agent.
    - Cân nhắc việc phân loại các lệnh theo mức độ nguy hiểm. Các lệnh nguy hiểm (ví dụ: ghi file, thay đổi cấu hình hệ thống) có thể yêu cầu xác thực bổ sung hoặc chỉ được phép từ các quản trị viên cấp cao.
    - Agent có thể có một danh sách trắng (whitelist) các lệnh an toàn hoặc kiểm tra chữ ký số của các script/lệnh trước khi thực thi.
- **Input Validation:** Agent phải xác thực kỹ lưỡng mọi dữ liệu nhận được từ server (đặc biệt là nội dung lệnh) để tránh các lỗ hổng như command injection.

**5. Tên Mutex Đảm Bảo Duy Nhất**

- Để tránh xung đột với các ứng dụng khác, tên Mutex sẽ bao gồm một định danh duy nhất cho sản phẩm hoặc công ty.
- **Định dạng:** `Global\\CMSAgentSingletonMutex_<YourCompanyOrProductGUID>`
    - Ví dụ: `Global\\CMSAgentSingletonMutex_E17A2F8D-9B74-4A6A-8E0A-3F9F7B1B3C5D`
    - GUID này sẽ được tạo một lần và cố định trong code của agent.

**6. Làm Mới Token Chủ Động**

- **Nếu Server cung cấp thời gian hết hạn (Expiration Time):** Agent sẽ lưu trữ và lên lịch làm mới token trước khi hết hạn.
- **Nếu Server không cung cấp thời gian hết hạn:** Agent sẽ thử làm mới token định kỳ (ví dụ: mỗi 24 giờ, được cấu hình trong `appsettings.json` qua `CMSAgentSettings:AgentSettings:TokenRefreshIntervalSec`) bằng cách gửi `POST /api/agent/identify` với `forceRenewToken: true`. Nếu thất bại, agent sẽ quay lại cơ chế làm mới khi gặp lỗi 401 từ WebSocket/HTTP.

## IX. Thông Tin về Logging

**1. Vị Trí File Log:**

- **Agent Service:** `C:\\ProgramData\\CMSAgent\\logs\\agent_YYYYMMDD.log`. Số ngày giữ lại được cấu hình trong `appsettings.json` (ví dụ: `Serilog:WriteTo:File:Args:retainedFileCountLimit`).
- **Updater:** `C:\\ProgramData\\CMSAgent\\logs\\updater_YYYYMMDD_HHMMSS.log`.
- **Tiến trình cấu hình:** Ghi ra console và file `configure_YYYYMMDD_HHMMSS.log`.

**2. Cấu Hình Mức Độ Log (qua `appsettings.json`)**
Cấu hình Serilog chi tiết (bao gồm `MinimumLevel`, `Override`, `WriteTo`, `Enrich`) được đặt trong section `"Serilog"` của `appsettings.json` (xem mục VII.1).

**3. Nội Dung Log Mẫu và Cách Đọc**
Mỗi dòng log bắt buộc bao gồm: Timestamp, Level, SourceContext (Namespace của lớp ghi log), Message, và Exception (nếu có).
Ví dụ:

```
2025-05-12 22:15:01.123 +07:00 [INF] [CMSAgent.Core.AgentService] Agent service starting... State: INITIALIZING
2025-05-12 22:15:03.456 +07:00 [DBG] [CMSAgent.Configuration.ConfigLoader] Runtime config loaded successfully from C:\ProgramData\CMSAgent\runtime_config\runtime_config.json
2025-05-12 22:15:05.789 +07:00 [INF] [CMSAgent.Communication.WebSocketConnector] Attempting WebSocket connection to wss://your-server.com:3000... State: AUTHENTICATING
2025-05-12 22:15:06.112 +07:00 [INF] [CMSAgent.Communication.WebSocketConnector] WebSocket connected and authenticated. State: CONNECTED
2025-05-12 22:15:08.990 +07:00 [ERR] [CMSAgent.Core.CommandExecutor] Failed to execute command cmd-uuid-xyz. Timeout expired after 300 seconds.
   System.TimeoutException: The operation has timed out.
   at CMSAgent.Commands.Handlers.ConsoleCommandHandler.ExecuteAsync(CommandRequest commandRequest, CancellationToken cancellationToken)

```

Khi gỡ lỗi, tìm các log `ERROR` hoặc `FATAL`. Xem xét log `WARN`, `INFO`, `DEBUG` xung quanh để hiểu ngữ cảnh.

**4. Windows Event Log**
Agent service sẽ ghi các sự kiện quan trọng (khởi động thành công, dừng, lỗi nghiêm trọng không thể ghi vào file log) vào Windows Event Log (thường là "Application" log) với một "Source" (Nguồn sự kiện) tùy chỉnh, ví dụ: "CMSAgentService", theo cấu hình trong `appsettings.json`. Việc đăng ký Event Source sẽ được thực hiện trong quá trình cài đặt agent (với quyền admin).

**5. Chức năng nâng cao: Thu Thập Log Từ Xa:**

- **Cơ chế:** Server có thể yêu cầu agent gửi các file log gần đây hoặc một phần log cụ thể thông qua một lệnh đặc biệt qua WebSocket (`commandType: "system_get_logs"`) hoặc một API riêng (`POST /api/agent/upload-log`).
- **An toàn:** Việc này sẽ được thực hiện một cách an toàn, có xác thực và giới hạn để tránh lạm dụng. Agent chỉ gửi log khi nhận được yêu cầu hợp lệ từ server đã xác thực.
- **Nén:** Log sẽ được nén (ZIP) trước khi gửi để giảm băng thông.
- **Payload (ví dụ khi dùng `POST /api/agent/report-error`):**
    
    ```
    {
        "error_type": "LOG_UPLOAD_REQUESTED",
        "error_message": "Log upload requested by server for specific date range/file",
        "error_details": {
            "log_filename": "agent_logs_20250510-20250512.zip",
            "log_content_base64": "UEsDBBQAAgAI..."
        },
        "timestamp": "2025-05-12T14:00:00Z"
    }
    
    ```
    
    Hoặc, nếu sử dụng một endpoint riêng như `POST /api/agent/upload-log`, request body có thể là `multipart/form-data` chứa file log đã nén.
    

## X. Xử Lý Lỗi và Khắc Phục Sự Cố

**1. Mã Trạng Thái HTTP Phổ Biến và Cách Xử Lý của Agent:**

- **200 OK:** Yêu cầu thành công. Tiếp tục xử lý response.
- **204 No Content:** Yêu cầu thành công, không có nội dung trả về. Agent coi là thành công.
- **400 Bad Request:** Yêu cầu không hợp lệ từ phía agent (thiếu trường, sai định dạng). Agent xử lý: Ghi log chi tiết request và response. Không nên thử lại yêu cầu y hệt. Thông báo cho người dùng (nếu trong quá trình configure) hoặc báo lỗi lên server (nếu trong quá trình hoạt động).
- **401 Unauthorized:** Lỗi xác thực (token không hợp lệ/hết hạn). Agent xử lý: Nếu đang configure và lỗi MFA: Cho người dùng nhập lại. Nếu đang hoạt động: Ghi log. Agent thử làm mới token bằng cách gọi lại POST `/identify` (không `forceRenewToken`). Nếu vẫn thất bại, ngắt kết nối WebSocket, và thử lại toàn bộ quá trình kết nối/xác thực sau một khoảng thời gian tăng dần (exponential backoff).
- **403 Forbidden:** Đã xác thực nhưng không có quyền. Ghi log, báo lỗi lên server.
- **404 Not Found:** Endpoint không tồn tại hoặc tài nguyên không tìm thấy (ví dụ: tải file cập nhật không có). Ghi log.
- **409 Conflict:** Xung đột tài nguyên (ví dụ: cố gắng đăng ký vị trí đã có người dùng). Agent xử lý (trong `configure`): Thông báo cho người dùng chọn vị trí khác.
- **429 Too Many Requests:** Server báo agent gửi quá nhiều yêu cầu. Agent xử lý: Đọc header `Retry-After` (nếu có) và chờ. Nếu không, sử dụng cơ chế exponential backoff trước khi thử lại.
- **500 Internal Server Error, 502 Bad Gateway, 503 Service Unavailable, 504 Gateway Timeout:** Lỗi từ phía server hoặc mạng. Agent xử lý: Ghi log. Thử lại yêu cầu sau một khoảng thời gian tăng dần (exponential backoff). Giới hạn số lần thử lại cho một yêu cầu cụ thể (cấu hình trong `appsettings.json`).

**2. Xử Lý Lỗi Nghiêm Trọng của Agent:**

```
| Lỗi                                       | Hành động của Agent                                                                                                | Ghi log/Event Log (Mức độ)                                       |
| :---------------------------------------- | :------------------------------------------------------------------------------------------------------------------ | :--------------------------------------------------------------- |
| Mất Kết Nối Mạng Kéo Dài                  | Chuyển sang `DISCONNECTED`, lưu queue offline, giảm tần suất thử kết nối lại sau 1 giờ.                               | `DISCONNECTED` (Information), lỗi kết nối cụ thể (Warning/Error) |
| File Cấu Hình (`appsettings.json`) Hỏng/Không hợp lệ | Ghi lỗi vào Event Log. Agent không khởi động được hoặc thoát. Service không tự động khởi động lại liên tục. | `CONFIG_LOAD_FAILED` hoặc `CONFIG_VALIDATION_FAILED` (Fatal)   |
| File `runtime_config.json` Hỏng/Thiếu    | Ghi lỗi vào Event Log. Agent không thể xác thực, chuyển sang `ERROR` hoặc thoát.                                     | `CONFIG_LOAD_FAILED` (Fatal)                                   |
| Không Thể Ghi Log (vào file/Event Log)    | Thử ghi lỗi vào kênh log còn lại. Nếu tất cả thất bại, agent dừng an toàn.                                           | Lỗi ghi log (Error/Fatal vào kênh còn lại)                     |
| Lỗi Không Mong Muốn (Unhandled Exception) | Bắt lỗi, ghi chi tiết stack trace vào Event Log. Cố gắng báo cáo lỗi `UNHANDLED_EXCEPTION` lên server. Dừng an toàn. | `UNHANDLED_EXCEPTION` (Fatal)                                  |

```

**3. Hướng Dẫn Khắc Phục Sự Cố Thường Gặp:**

- **Sự cố 1: Agent Service không khởi động / dừng ngay.**
    - Kiểm tra: Windows Event Viewer (Application, System logs) Tìm lỗi liên quan đến "CMSAgentService". File Log Agent: `C:\\ProgramData\\CMSAgent\\logs\\`. Tìm `ERROR` hoặc `FATAL`. Quyền Tài Khoản Service: Đảm bảo LocalSystem có đủ quyền. File Cấu Hình: Kiểm tra sự tồn tại và tính hợp lệ của `appsettings.json` và `runtime_config/runtime_config.json`. .NET Runtime: Đảm bảo phiên bản yêu cầu đã được cài đặt. Dependencies: Đảm bảo DLLs cần thiết có mặt. Mutex: Kiểm tra Task Manager xem có `CMSAgent.exe` nào khác đang chạy không.
- **Sự cố 2: Agent Service đang chạy nhưng không thấy kết nối/dữ liệu trên Server.**
    - Kiểm tra: File Log Agent: Lỗi kết nối, WebSocket, HTTP, xác thực. `ServerUrl` trong `appsettings.json`: URL chính xác, server có thể truy cập. Tường lửa: Trên agent và server. `agent_token`: Có thể hết hạn/không hợp lệ. Log nên có HTTP 401 hoặc `agent:ws_auth_failed`. Trạng thái Server: Đảm bảo server backend và Socket.IO đang hoạt động.
- **Sự cố 3: Lỗi trong quá trình `CMSAgent.exe configure`.**
    - Kiểm tra: Chạy với quyền Administrator. Thông báo lỗi trên CLI: Đọc kỹ. Kết nối đến Server: Đảm bảo máy có thể kết nối đến `ServerUrl` (trong `appsettings.json`). Thông tin nhập: Đảm bảo thông tin phòng, tọa độ, mã MFA được nhập chính xác.
- **Sự cố 4: Quá trình tự cập nhật thất bại.**
    - Kiểm tra: File Log Agent: Log của UpdateHandler. File Log Updater: Log của `CMSUpdater.exe` trong `C:\\ProgramData\\CMSAgent\\logs\\`. Dung lượng đĩa: Đảm bảo đủ dung lượng trống. Quyền ghi: LocalSystem cần quyền ghi vào thư mục cài đặt. File bị khóa: Một file của agent có thể đang bị sử dụng.
- **Sự cố 5: Lệnh gửi từ Server không được Agent thực thi hoặc báo lỗi.**
    - Kiểm tra: File Log Agent: Log liên quan đến CommandExecutor hoặc CommandHandler cụ thể. Kết nối WebSocket: Đảm bảo agent vẫn đang kết nối. Nội dung lệnh: Lệnh có thể sai cú pháp. Quyền thực thi: Lệnh được thực thi với quyền của LocalSystem.
- **Sự cố 6: Không thu thập được thông tin phần cứng.**
    - Kiểm tra: Quyền truy cập WMI: Đảm bảo tài khoản LocalSystem có quyền truy cập WMI. Dịch vụ WMI: Mở `services.msc`, kiểm tra dịch vụ "Windows Management Instrumentation" đang chạy. Thử khởi động lại. Log Agent: Tìm lỗi liên quan đến SystemMonitor hoặc truy cập WMI.
- **Sự cố 7: Agent tiêu tốn quá nhiều CPU/RAM.**
    - Kiểm tra: File Log Agent (mức Debug/Verbose): Xác định module hoặc hoạt động nào đang gây ra. Performance Profiler: Sử dụng công cụ profiler của .NET để phân tích sâu hơn. Cấu hình `ResourceLimits` trong `appsettings.json`.

## XI. Phụ Lục: Cấu Trúc Tham Số Dòng Lệnh và Ví Dụ

### A. CMSAgent.exe

- **`configure`**: Cấu hình agent lần đầu hoặc cấu hình lại.
    - Tham số: Không. Luôn tương tác CLI.
    - Hoạt động: Yêu cầu nhập thông tin phòng, xác thực server (MFA nếu cần), lưu cấu hình runtime.
    - Quyền: Administrator.
    - Ví dụ sử dụng: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" configure`
    - Mã lỗi: 0 (Thành công), 1 (Lỗi chung), 2 (Thiếu quyền), 3 (Hủy), 4 (Lỗi kết nối/xác thực server), 5 (Lỗi lưu config runtime).
- **`uninstall`**: Gỡ bỏ agent.
    - Tham số tùy chọn: `-remove-data` (Xóa thư mục dữ liệu của agent).
    - Hoạt động: Dừng service, gỡ đăng ký, xóa file.
    - Quyền: Administrator.
    - Ví dụ: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" uninstall --remove-data`
    - *Giải thích:* Gỡ cài đặt agent và xóa toàn bộ thư mục dữ liệu của agent tại `C:\\ProgramData\\CMSAgent`.
    - Mã lỗi: 0 (Thành công), 1 (Lỗi chung), 2 (Thiếu quyền), 6 (Lỗi dừng/gỡ service).
- **`start`**: Khởi động Windows Service của agent.
    - Quyền: Administrator.
    - Ví dụ sử dụng: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" start`
    - Mã lỗi: 0 (Thành công/Yêu cầu gửi SCM), 1 (Lỗi chung), 2 (Thiếu quyền), 7 (Service không cài đặt/lỗi khởi động).
- **`stop`**: Dừng Windows Service của agent.
    - Quyền: Administrator.
    - Ví dụ sử dụng: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" stop`
    - Mã lỗi: 0 (Thành công/Yêu cầu gửi SCM), 1 (Lỗi chung), 2 (Thiếu quyền), 8 (Service không cài đặt/lỗi dừng).
- **`debug`**: Chạy agent trong console hiện tại.
    - Ví dụ: `"C:\\Program Files\\CMSAgent\\CMSAgent.exe" debug`
    - *Giải thích:* Chạy agent ở chế độ console thay vì Windows Service. Log sẽ được hiển thị trực tiếp trên console, hữu ích cho việc gỡ lỗi và theo dõi hoạt động thời gian thực.

### B. CMSUpdater.exe

- **Tham số bắt buộc:**
    - `pid <process_id>`: PID của tiến trình CMSAgent.exe cũ cần dừng.
    - `new-agent-path "<đường_dẫn_thư_mục_agent_mới>"`: Đường dẫn đến thư mục chứa file agent mới đã giải nén.
    - `current-agent-install-dir "<đường_dẫn_cài_đặt_agent_cũ>"`: Đường dẫn thư mục cài đặt hiện tại.
    - `updater-log-dir "<đường_dẫn_thư_mục_logs>"`: Nơi ghi file log của updater.
    - `current-agent-version "<phiên_bản_agent_cũ>"`: Phiên bản agent hiện tại (dùng cho tên backup).
- **Mã lỗi (Exit Codes) có thể có:**
    - 0: Cập nhật thành công.
    - 10: Lỗi: Không thể dừng agent cũ.
    - 11: Lỗi: Sao lưu agent cũ thất bại.
    - 12: Lỗi: Triển khai agent mới thất bại.
    - 13: Lỗi: Khởi động service agent mới thất bại.
    - 14: Lỗi: Rollback thất bại.
    - 15: Lỗi tham số dòng lệnh.
    - 16: Lỗi: Timeout chờ agent cũ dừng.
    - 99: Lỗi chung không xác định của Updater.