## Chuẩn Giao Tiếp và Cấu Hình CMSAgent

Tài liệu này mô tả chi tiết các chuẩn giao tiếp (request/response) giữa CMSAgent và server trung tâm, cùng với các thông số cấu hình thiết yếu cho hoạt động của agent.

### Phần 1: Chuẩn Giao Tiếp Agent-Server

Phần này tập trung vào cách agent tương tác với server thông qua HTTP API và WebSocket.

**A. Giao Tiếp HTTP (API Endpoints)**

Agent sử dụng HTTP cho các tác vụ ban đầu và một số hoạt động không yêu cầu thời gian thực.

- **URL Cơ Sở API:**
    - Được định nghĩa bởi tham số `server_url` trong file `agent_config.json`.
    - Ví dụ: `http://<your-server-ip>:3000/api/agent/`
- **Headers Chung Cho Các Yêu Cầu Cần Xác Thực:**
    - `X-Agent-Id`: Giá trị là `device_id` của agent.
    - `Authorization`: `Bearer <agent_token>` (sử dụng token sau khi agent đã xác thực thành công).
    - `Content-Type`: `application/json` (đối với các request có body là JSON).

**1. Định danh Agent (`POST /identify`)**

- **Mục đích:** Đăng ký agent mới hoặc định danh một agent đã tồn tại với server.
- **Request Payload (JSON):**
    
    ```
    {
      "unique_agent_id": "string", // device_id của agent
      "positionInfo": {            // Đối tượng chứa thông tin vị trí
        "roomName": "string",      // Tên phòng được cấu hình
        "posX": "string",          // Tọa độ X trong phòng (agent hiện tại gửi dưới dạng chuỗi)
        "posY": "string"           // Tọa độ Y trong phòng (agent hiện tại gửi dưới dạng chuỗi)
      }
    }
    
    ```
    
- **Response Payload (JSON) - Thành công:**
    
    ```
    {
      "status": "success",
      "agentId": "string",     // ID của agent được server gán hoặc xác nhận
      "agentToken": "string"   // Token xác thực mới hoặc hiện tại
    }
    
    ```
    
- **Response Payload (JSON) - Yêu cầu MFA:**
    
    ```
    {
      "status": "mfa_required",
      "message": "string" // Thông báo từ server, ví dụ: "MFA code is required."
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi Vị trí:**
    
    ```
    {
      "status": "position_error",
      "message": "string" // Mô tả lỗi liên quan đến thông tin vị trí
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi Khác:**
    
    ```
    {
      "status": "error",
      "message": "string" // Mô tả lỗi chung
    }
    
    ```
    

**2. Xác thực Đa Yếu Tố - MFA (`POST /verify-mfa`)**

- **Mục đích:** Gửi mã MFA do người dùng nhập để hoàn tất quá trình định danh khi server yêu cầu.
- **Request Payload (JSON):**
    
    ```
    {
      "unique_agent_id": "string", // device_id của agent
      "mfaCode": "string"          // Mã MFA người dùng đã nhập
    }
    
    ```
    
- **Response Payload (JSON) - Thành công:**
    
    ```
    {
      "status": "success",
      "agentId": "string",
      "agentToken": "string" // Token xác thực sau khi MFA thành công
    }
    
    ```
    
- **Response Payload (JSON) - Thất bại:**
    
    ```
    {
      "status": "error",     // Hoặc một mã lỗi cụ thể cho MFA (ví dụ: "mfa_invalid_code")
      "message": "string"    // Mô tả lỗi (ví dụ: "Invalid MFA code or code expired.")
    }
    
    ```
    

**3. Gửi Thông Tin Phần Cứng (`POST /hardware-info`)**

- **Mục đích:** Gửi thông tin chi tiết về phần cứng của máy client lên server. Thông tin này thường là tĩnh hoặc ít thay đổi, được gửi một lần sau khi agent xác thực thành công hoặc khi có yêu cầu từ server/thay đổi đáng kể.
- **Request Payload (JSON):** (Dựa trên những gì `system_monitor.py` thu thập)
    
    ```
    {
      "os_info": "string",             // Ví dụ: "Microsoft Windows 10 Pro Caption,Version 10.0.19042" (Kết quả từ WMIC)
      "cpu_info": "string",            // Ví dụ: "Intel(R) Core(TM) i7-8700 CPU @ 3.20GHz" (Tên CPU từ WMIC)
      "gpu_info": "string",            // Ví dụ: "NVIDIA GeForce GTX 1080 Ti" (Tên GPU từ WMIC)
      "total_ram_bytes": "number",     // Tổng dung lượng RAM (bytes), ví dụ: 17179869184 (cho 16GB)
      "total_disk_space_bytes": "number",// Tổng dung lượng đĩa của ổ C: (bytes)
      "ip_address": "string"           // Địa chỉ IP chính của máy
    }
    
    ```
    
- **Response Payload (JSON) - Thành công:**
    
    ```
    {
      "status": "success",
      "message": "Hardware info received successfully."
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi:**
    
    ```
    {
      "status": "error",
      "message": "string" // Mô tả lỗi
    }
    
    ```
    

**4. Kiểm Tra Cập Nhật (`GET /check-update`)**

- **Mục đích:** Kiểm tra xem có phiên bản agent mới nào khả dụng trên server không.
- **Query Parameters:**
    - `current_version` (string): Phiên bản hiện tại của agent đang chạy (ví dụ: "1.0.2").
- **Response Payload (JSON) - Có cập nhật:**
    
    ```
    {
      "version": "string",          // Phiên bản mới của agent
      "download_url": "string",     // URL để tải gói cập nhật
      "checksum_sha256": "string"   // Mã SHA256 checksum của gói cập nhật để xác minh
    }
    
    ```
    
- **Response - Không có cập nhật:**
    - HTTP Status `204 No Content`.
    - Hoặc một đối tượng JSON rỗng `{}` với HTTP Status `200 OK`.
- **Response Payload (JSON) - Lỗi:**
    
    ```
    {
      "status": "error",
      "message": "string" // Mô tả lỗi
    }
    
    ```
    

**5. Báo Cáo Lỗi (`POST /report-error`)**

- **Mục đích:** Gửi thông tin về các lỗi phát sinh trong quá trình hoạt động của agent lên server.
- **Request Payload (JSON):**
    
    ```
    {
      "error_type": "string",    // Phân loại lỗi (ví dụ: "AGENT_CRASH", "UPDATE_FAILED", "COMMAND_EXECUTION_ERROR")
      "error_message": "string", // Thông điệp lỗi chính, mô tả ngắn gọn về lỗi
      "error_details": {         // Đối tượng JSON chứa các chi tiết bổ sung về lỗi
        "stack_trace": "string", // (Nếu có) Stack trace của lỗi
        "agent_version": "string",// Phiên bản agent khi lỗi xảy ra
        // ... các trường thông tin tùy chỉnh khác liên quan đến ngữ cảnh lỗi
      },
      "timestamp": "string"      // Thời gian xảy ra lỗi, định dạng ISO 8601 (ví dụ: "2025-05-10T15:30:00Z")
    }
    
    ```
    
- **Response Payload (JSON) - Thành công:**
    
    ```
    {
      "status": "success",
      "message": "Error report received successfully."
    }
    
    ```
    
- **Response Payload (JSON) - Lỗi:**
    
    ```
    {
      "status": "error",
      "message": "string" // Mô tả lỗi khi server xử lý báo cáo
    }
    
    ```
    

**B. Giao Tiếp WebSocket (Socket.IO)**

Sau khi xác thực qua HTTP và có token, agent thiết lập kết nối WebSocket để nhận lệnh và gửi cập nhật trạng thái theo thời gian thực.

- **URL Kết Nối:** Được lấy từ `server_url` trong `agent_config.json`.
- **Xác thực Kết Nối WebSocket:**
    - Khi client Socket.IO kết nối, agent sẽ gửi `agent_id` (là `device_id`) và `agent_token` trong headers hoặc payload `auth` của gói tin Socket.IO, tùy theo cách server yêu cầu.
- **Các Sự Kiện Server Gửi Cho Agent:**
    - **`agent:ws_auth_success`**:
        - **Payload:** Thường là một đối tượng JSON xác nhận (có thể rỗng hoặc chứa thông tin bổ sung).
        - **Ý nghĩa:** Server xác nhận agent đã được xác thực thành công qua kênh WebSocket. Agent sẵn sàng nhận lệnh và gửi dữ liệu.
    - **`agent:ws_auth_failed`**:
        - **Payload:** Đối tượng JSON chứa lý do xác thực thất bại.
        - **Ý nghĩa:** Server từ chối yêu cầu xác thực WebSocket. Agent cần xử lý (ví dụ: thử xác thực lại qua HTTP hoặc dừng hoạt động).
    - **`command:execute`**:
        - **Payload (JSON):**
            
            ```
            {
              "commandId": "string",     // ID duy nhất của lệnh để theo dõi
              "command": "string",       // Nội dung lệnh cần thực thi (ví dụ: "ipconfig /all")
              "commandType": "string"    // Loại lệnh ("console", "system", "custom_action", etc.)
            }
            
            ```
            
        - **Ý nghĩa:** Server yêu cầu agent thực thi một lệnh cụ thể.
    - **`agent:new_version_available`**:
        - **Payload (JSON):**
            
            ```
            {
              "version": "string",          // Phiên bản mới của agent
              "download_url": "string",     // URL để tải gói cập nhật
              "checksum_sha256": "string"   // Mã SHA256 checksum của gói
            }
            
            ```
            
        - **Ý nghĩa:** Server thông báo có phiên bản agent mới, agent nên bắt đầu quá trình tự cập nhật.
- **Các Sự Kiện Agent Gửi Lên Server:**
    - **`agent:status_update`**: (Chi tiết trong mục C)
    - **`agent:command_result`**:
        - **Payload (JSON):**
            
            ```
            {
              "agentId": "string",     // device_id của agent
              "commandId": "string",   // ID của lệnh đã được thực thi (tương ứng với commandId nhận được)
              "success": "boolean",    // true nếu lệnh thực thi thành công, false nếu có lỗi
              "type": "string",        // Loại lệnh đã thực thi
              "result": {
                "stdout": "string",    // Output chuẩn (standard output) của lệnh
                "stderr": "string",    // Output lỗi (standard error) của lệnh
                "exitCode": "number"   // Mã thoát (exit code) của lệnh
              }
            }
            
            ```
            
        - **Ý nghĩa:** Agent gửi kết quả sau khi thực thi một lệnh nhận được từ server.

**C. Thông Tin Trạng Thái (Stats) Gửi Lên Server**

Agent định kỳ gửi các thông tin trạng thái tài nguyên hệ thống lên server thông qua sự kiện WebSocket `agent:status_update`.

- **Sự kiện WebSocket:** `agent:status_update`
- **Mục đích:** Cung cấp cho server cái nhìn tổng quan và cập nhật về tình trạng hoạt động của máy client.
- **Payload (JSON):**
    
    ```
    {
      "agentId": "string",    // device_id của agent, để server xác định trạng thái này thuộc về máy nào.
      "cpuUsage": "number",   // Phần trăm (%) sử dụng CPU hiện tại của toàn hệ thống.
      "ramUsage": "number",   // Phần trăm (%) sử dụng RAM hiện tại của toàn hệ thống.
      "diskUsage": "number"   // Phần trăm (%) sử dụng dung lượng đĩa của ổ đĩa chính (thường là ổ C:\ trên Windows).
    }
    
    ```
    
- **Tần suất gửi:** Được xác định bởi cấu hình `agent.status_report_interval_sec` trong file `agent_config.json`.

### Phần 2: Các Cấu Hình Agent Cần Thiết

**1. Cấu Hình Tĩnh (Lưu trong `agent_config.json`)**

Các thiết lập này được đọc một lần khi agent khởi động và thường không thay đổi trong quá trình hoạt động.

- **`server_url`**: (Chuỗi - String)
    - **Mô tả:** Địa chỉ URL cơ sở của server backend mà agent sẽ kết nối.
    - **Bắt buộc:** Có.
    - *Ví dụ:* `"http://your-server.com:3000"`
- **`agent.status_report_interval_sec`**: (Số nguyên - Integer)
    - **Mô tả:** Khoảng thời gian (tính bằng giây) giữa các lần agent tự động gửi báo cáo trạng thái hệ thống (CPU, RAM, disk) lên server.
    - *Ví dụ:* `30`
- **`agent.enable_auto_update`**: (Boolean - `true` hoặc `false`)
    - **Mô tả:** Xác định liệu agent có được phép tự động kiểm tra và thực hiện quá trình cập nhật phiên bản mới hay không. *Lưu ý: Logic agent Python hiện tại gọi `check_for_updates_proactively` một lần khi khởi động; việc kiểm tra định kỳ dựa trên `auto_update_interval_sec` có thể chưa được triển khai đầy đủ trong `agent.py`.*
    - *Ví dụ:* `true`
- **`agent.auto_update_interval_sec`**: (Số nguyên - Integer)
    - **Mô tả:** Nếu `enable_auto_update` là `true`, đây là khoảng thời gian (tính bằng giây) agent sẽ chủ động kiểm tra phiên bản mới trên server. *Xem ghi chú ở `agent.enable_auto_update`.*
    - *Ví dụ:* `86400` (tương đương 1 ngày)
- **`http_client.request_timeout_sec`**: (Số nguyên - Integer)
    - **Mô tả:** Thời gian chờ tối đa (tính bằng giây) cho mỗi yêu cầu HTTP mà agent gửi đi.
    - *Ví dụ:* `15`
- **`websocket.reconnect_delay_initial_sec`**: (Số nguyên - Integer)
    - **Mô tả:** Thời gian chờ ban đầu (tính bằng giây) trước khi agent thử kết nối lại WebSocket sau khi bị mất kết nối.
    - *Ví dụ:* `5`
- **`websocket.reconnect_delay_max_sec`**: (Số nguyên - Integer)
    - **Mô tả:** Thời gian chờ tối đa (tính bằng giây) giữa các lần thử kết nối lại WebSocket.
    - *Ví dụ:* `60`
- **`websocket.reconnect_attempts_max`**: (Số nguyên hoặc `null`)
    - **Mô tả:** Số lần thử kết nối lại WebSocket tối đa. Nếu là `null` (hoặc không có), agent sẽ thử kết nối lại vô hạn.
    - *Ví dụ:* `null`
- **`command_executor.default_timeout_sec`**: (Số nguyên - Integer)
    - **Mô tả:** Thời gian chờ tối đa (tính bằng giây) cho một lệnh console (được gửi từ server) được thực thi.
    - *Ví dụ:* `300` (5 phút)
- **`command_executor.max_parallel_commands`**: (Số nguyên - Integer)
    - **Mô tả:** Số lượng lệnh tối đa mà agent có thể xử lý và thực thi đồng thời.
    - *Ví dụ:* `2`
- **`command_executor.max_queue_size`**: (Số nguyên - Integer)
    - **Mô tả:** Kích thước tối đa của hàng đợi lệnh. Nếu hàng đợi đầy, các lệnh mới có thể bị từ chối.
    - *Ví dụ:* `100`
- **`command_executor.console_encoding`**: (Chuỗi - String)
    - **Mô tả:** Bảng mã (encoding) được sử dụng để giải mã output (stdout, stderr) từ các lệnh console.
    - *Ví dụ:* `"utf-8"`, `"cp1252"`

**2. Trạng Thái Động (Quản lý bởi `StateManager`, thường lưu trong `agent_state.json`)**

Các thông tin này được tạo ra hoặc cập nhật trong quá trình hoạt động của agent và được lưu trữ cục bộ.

- **`device_id`**: (Chuỗi - String)
    - **Mô tả:** Một định danh duy nhất cho mỗi máy cài đặt agent. Được tạo tự động trong lần chạy đầu tiên.
    - **Bắt buộc:** Có (sau lần chạy đầu tiên).
- **`room_config`**: (Đối tượng JSON - Object)
    - **Mô tả:** Lưu trữ thông tin vị trí của máy tính (tên phòng, tọa độ) do người dùng cung cấp. `StateManager` lưu trữ cấu trúc này.
    - **Bắt buộc:** Có (sau khi được cấu hình).
    - *Cấu trúc lưu trữ ví dụ:* `{"room": "Phòng Lab A", "position": {"x": 10, "y": 20}}`
    - *Lưu ý:* Khi gửi đi trong request `/identify`, cấu trúc này được chuyển đổi thành đối tượng `positionInfo` (xem Phần 1.A.1).
- **`agent_token`**: (Chuỗi - String)
    - **Mô tả:** Token xác thực do server cấp sau khi agent định danh thành công.
    - **Bắt buộc:** Có (sau khi xác thực thành công).
    - *Lưu trữ:* Nên được lưu trữ một cách an toàn (ví dụ: sử dụng Windows Credential Manager hoặc mã hóa file).

**3. Đường Dẫn Lưu Trữ (`StateManager.storage_path`)**

- **Mô tả:** Đường dẫn đến thư mục gốc nơi agent lưu trữ tất cả dữ liệu của nó (logs, file trạng thái, báo cáo lỗi chờ gửi, file cập nhật tạm thời).
- **Xác định:** Tự động dựa trên quyền chạy của agent (SYSTEM hoặc người dùng).
    - *Ví dụ (Windows, khi agent chạy với quyền SYSTEM):* `C:\ProgramData\CMSAgent`

**4. Thông Tin Phiên Bản (Ví dụ: từ `agent/version.py` hoặc tương đương trong C#)**

- **`__version__`**: (Chuỗi - String)
    - **Mô tả:** Chỉ định phiên bản hiện tại của mã nguồn agent.
    - *Ví dụ:* `"1.0.2"`
- **`__app_name__`**: (Chuỗi - String)
    - **Mô tả:** Tên chính thức của ứng dụng agent.
    - *Ví dụ:* `"CMSAgent"`

Việc hiểu rõ các chuẩn giao tiếp và cấu hình này là rất quan trọng để đảm bảo agent hoạt động chính xác, hiệu quả và an toàn khi tương tác với server trung tâm.