# Chi Tiết Đặc Tả API Hệ Thống Quản Lý Máy Tính

Dưới đây là mô tả chi tiết cho các API endpoint của Backend, bao gồm phương thức HTTP, đường dẫn, header yêu cầu, tham số request body, và cấu trúc response dự kiến.

## 1. Xác thực User/Admin (Authentication)

### Đăng nhập User/Admin
* **Method:** `POST`
* **Path:** `/api/auth/login`
* **Headers:** `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "username": "string (required)",
      "password": "string (required)"
    }
    ```
* **Response Success (200 OK):**
    ```json
    {
      "token": "string (JWT)",
      "user": {
        "id": "integer",
        "username": "string",
        "role": "string ('admin' or 'user')"
      }
    }
    ```
* **Response Error:**
    * `401 Unauthorized`: `{ "message": "Invalid credentials" }`
    * `400 Bad Request`: `{ "message": "Username and password are required" }`

### Lấy thông tin User/Admin hiện tại
* **Method:** `GET`
* **Path:** `/api/auth/me`
* **Headers:**
    * `Authorization: Bearer <jwt_token_string>` (Required)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "id": "integer",
      "username": "string",
      "role": "string ('admin' or 'user')",
      "is_active": "boolean"
    }
    ```
* **Response Error:**
    * `401 Unauthorized`: `{ "message": "Unauthorized or Invalid Token" }`

## 2. Quản lý Users (Yêu cầu quyền Admin)

### Tạo User mới
* **Method:** `POST`
* **Path:** `/api/users`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "username": "string (required)",
      "password": "string (required)",
      "role": "string (required, 'admin' or 'user')",
      "is_active": "boolean (optional, default: true)"
    }
    ```
* **Response Success (201 Created):** (Trả về thông tin user mới tạo, không bao gồm password hash)
    ```json
    {
      "id": "integer",
      "username": "string",
      "role": "string",
      "is_active": "boolean"
    }
    ```
* **Response Error:**
    * `401 Unauthorized`: `{ "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "message": "Admin role required" }`
    * `400 Bad Request`: `{ "message": "Validation errors (e.g., username exists, missing fields)" }`

### Lấy danh sách Users
* **Method:** `GET`
* **Path:** `/api/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?role=admin|user` (Lọc theo vai trò)
    * `?is_active=true|false` (Lọc theo trạng thái active)
    * `?username=...` (Tìm kiếm gần đúng theo username)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    [
      {
        "id": "integer",
        "username": "string",
        "role": "string",
        "is_active": "boolean"
      },
      ...
    ]
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`

### Cập nhật User
* **Method:** `PUT`
* **Path:** `/api/users/:id`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (integer, ID của user cần cập nhật)
* **Request Body:** (Chỉ chứa các trường cần cập nhật)
    ```json
    {
      "role": "string ('admin' or 'user')",
      "is_active": "boolean"
      // Không cho phép cập nhật username/password qua endpoint này
    }
    ```
* **Response Success (200 OK):** (Thông tin user sau khi cập nhật)
    ```json
    {
      "id": "integer",
      "username": "string",
      "role": "string",
      "is_active": "boolean"
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `400 Bad Request`

### Xóa User (hoặc Inactivate)
* **Method:** `DELETE`
* **Path:** `/api/users/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của user cần xóa)
* **Request Body:** (None)
* **Response Success (204 No Content):** (Không có body)
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`

## 3. Quản lý Rooms

### Tạo Room mới (Admin Only)
* **Method:** `POST`
* **Path:** `/api/rooms`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "name": "string (required)",
      "description": "string (optional)",
      "layout": {
        "width": "integer - Chiều rộng của phòng trong pixels",
        "height": "integer - Chiều cao của phòng trong pixels",
        "background": "string - Mã màu nền (ví dụ: '#f5f5f5')",
        "grid": {
          "columns": "integer - Số máy tính theo chiều ngang (trục X)",
          "rows": "integer - Số máy tính theo chiều dọc (trục Y)",
          "spacing_x": "integer - Khoảng cách ngang giữa các máy tính (pixels)",
          "spacing_y": "integer - Khoảng cách dọc giữa các máy tính (pixels)"
        }
      }
    }
    ```
* **Response Success (201 Created):**
    ```json
    {
      "id": "integer",
      "name": "string",
      "description": "string",
      "layout": "object"
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `400 Bad Request`

### Lấy danh sách Rooms (Admin lấy tất cả, User lấy phòng được gán)
* **Method:** `GET`
* **Path:** `/api/rooms`
* **Headers:** `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?name=...` (Tìm kiếm gần đúng theo tên room)
    * `?assigned_user_id=...` (Chỉ lấy các room mà user ID này được gán - Admin có thể dùng)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    [
      {
        "id": "integer",
        "name": "string"
        // Có thể thêm số lượng máy tính nếu cần
      },
      ...
    ]
    ```
* **Response Error:** `401 Unauthorized`

### Lấy chi tiết Room (Admin lấy được, User chỉ lấy được nếu được gán)
* **Method:** `GET`
* **Path:** `/api/rooms/:id`
* **Headers:** `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của room)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "id": "integer",
      "name": "string",
      "description": "string",
      "layout": "object",
      "computers": [ // Danh sách máy tính thuộc phòng này
        {
          "id": "integer",
          "name": "string",
          "pos_x": "integer",
          "pos_y": "integer",
          "unique_agent_id": "string",
          "status": "string ('online'|'offline')", // Lấy từ cache
          "has_active_errors": "boolean" // Tính toán từ trường errors
        },
        ...
      ]
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden` (Nếu user không có quyền truy cập room), `404 Not Found`

### Cập nhật Room (Admin Only)
* **Method:** `PUT`
* **Path:** `/api/rooms/:id`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (integer, ID của room)
* **Request Body:**
    ```json
    {
      "name": "string (required)",
      "description": "string (optional)",
      "layout": {
        "width": "integer - Chiều rộng của phòng trong pixels",
        "height": "integer - Chiều cao của phòng trong pixels",
        "background": "string - Mã màu nền (ví dụ: '#f5f5f5')",
        "grid": {
          "columns": "integer - Số máy tính theo chiều ngang (trục X)",
          "rows": "integer - Số máy tính theo chiều dọc (trục Y)",
          "spacing_x": "integer - Khoảng cách ngang giữa các máy tính (pixels)",
          "spacing_y": "integer - Khoảng cách dọc giữa các máy tính (pixels)"
        }
      }
    }
    ```
* **Response Success (200 OK):** (Thông tin room sau cập nhật)
    ```json
    {
      "id": "integer",
      "name": "string",
      "description": "string",
      "layout": "object"
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `400 Bad Request`

### Xóa Room (Admin Only)
* **Method:** `DELETE`
* **Path:** `/api/rooms/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của room)
* **Request Body:** (None)
* **Response Success (204 No Content):**
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`

## 4. Quản lý Phân công Room (Yêu cầu quyền Admin)

### Gán User vào Room
* **Method:** `POST`
* **Path:** `/api/rooms/:roomId/assign`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `roomId` (integer)
* **Request Body:**
    ```json
    {
      "userId": "integer (required)"
    }
    ```
* **Response Success (201 Created):** `{ "message": "User assigned successfully" }`
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found` (Room hoặc User không tồn tại), `409 Conflict` (User đã được gán)

### Gỡ User khỏi Room
* **Method:** `DELETE`
* **Path:** `/api/rooms/:roomId/unassign`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `roomId` (integer)
* **Request Body:**
    ```json
    {
      "userId": "integer (required)"
    }
    ```
* **Response Success (200 OK):** `{ "message": "User unassigned successfully" }`
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found` (Room, User hoặc Assignment không tồn tại)

### Lấy danh sách User trong Room
* **Method:** `GET`
* **Path:** `/api/rooms/:roomId/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `roomId` (integer)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    [
      {
        "id": "integer",
        "username": "string"
      },
      ...
    ]
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`

## 5. Quản lý Computers (Bởi Admin/User Frontend)

### Lấy danh sách Computers
* **Method:** `GET`
* **Path:** `/api/computers`
* **Headers:** `Authorization: Bearer <jwt>` (Required)
* **Query Parameters (Filtering):**
    * `?room_id=integer` (Lọc theo phòng)
    * `?name=string` (Tìm kiếm gần đúng theo tên máy)
    * `?status=online|offline` (Lọc theo trạng thái online/offline - Backend cần lấy từ cache/map WS)
    * `?has_errors=true` (Chỉ lấy máy đang có lỗi 'active')
    * `?unique_agent_id=string` (Tìm theo ID agent)
* **Response Success (200 OK):** (Mảng các đối tượng computer, đã lọc. Admin thấy hết, User thấy máy trong phòng được gán)
    ```json
    [
      {
        "id": "integer",
        "name": "string",
        "room_id": "integer",
        "unique_agent_id": "string",
        "status": "string ('online'|'offline')", // Lấy từ cache
        "has_active_errors": "boolean" // Tính toán từ trường errors
        // ... các trường cần thiết khác cho danh sách
      },
      ...
    ]
    ```
* **Response Error:** `401 Unauthorized`

### Cập nhật chi tiết Computer (Admin Only)
* **Method:** `PUT`
* **Path:** `/api/computers/:id`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:** (Chỉ các trường Admin có thể sửa)
    ```json
    {
      "name": "string (optional)",
      "room_id": "integer (optional)",
      "pos_x": "integer (optional)",
      "pos_y": "integer (optional)"
    }
    ```
* **Response Success (200 OK):** (Thông tin computer sau cập nhật)
    ```json
    {
      "id": "integer",
      "name": "string",
      "room_id": "integer",
      "pos_x": "integer",
      "pos_y": "integer",
      "unique_agent_id": "string",
      // ... các trường khác đọc từ DB
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `400 Bad Request`

### Lấy chi tiết Computer (Admin/User được quyền truy cập Room)
* **Method:** `GET`
* **Path:** `/api/computers/:id`
* **Headers:** `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "id": "integer",
      "name": "string",
      "room_id": "integer",
      "pos_x": "integer",
      "pos_y": "integer",
      "ip_address": "string",
      "unique_agent_id": "string",
      "last_seen": "timestamp",
      "windows_version": "string",
      "total_ram": "integer",
      "cpu_info": "string",
      "errors": [ // Mảng các đối tượng lỗi
        {
          "id": "string (uuid)",
          "type": "string",
          "description": "string",
          "reported_by": "integer",
          "reported_at": "timestamp",
          "status": "string ('active'|'resolved')",
          "resolved_by": "integer (optional)",
          "resolved_at": "timestamp (optional)",
          "resolution_notes": "string (optional)"
        },
        ...
      ]
      // Không trả về Agent Token Hash
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`

### Xóa Computer (Admin Only)
* **Method:** `DELETE`
* **Path:** `/api/computers/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:** (None)
* **Response Success (204 No Content):**
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`

### Gửi yêu cầu thực thi Lệnh (Admin/User được quyền truy cập Room)
* **Method:** `POST`
* **Path:** `/api/computers/:id/command`
* **Headers:**
    * `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:**
    ```json
    {
      "command": "string (required)"
    }
    ```
* **Response Success (202 Accepted):** (Yêu cầu đã được chấp nhận, kết quả sẽ trả về qua WebSocket)
    ```json
    {
      "message": "Command sent to agent",
      "commandId": "string (uuid)" // ID để theo dõi kết quả
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found` (Computer không tồn tại), `400 Bad Request`, `503 Service Unavailable` (Agent không online WS)

### Báo cáo lỗi (User/Admin có quyền truy cập room)
* **Method:** `POST`
* **Path:** `/api/computers/:id/errors`
* **Headers:** `Authorization: Bearer <jwt>` (Required)
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:**
    ```json
    {
      "type": "string (required, vd: 'Hardware', 'Software', 'Network')",
      "description": "string (required)"
    }
    ```
* **Response Success (201 Created):** (Trả về lỗi vừa tạo)
    ```json
    {
      "id": "string (uuid của lỗi)",
      "type": "string",
      "description": "string",
      "reported_by": "integer",
      "reported_at": "timestamp",
      "status": "active"
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `400 Bad Request`

### Xử lý/Xóa lỗi (Admin Only)
* **Method:** `PUT`
* **Path:** `/api/computers/:computerId/errors/:errorId/resolve`
* **Headers:** `Authorization: Bearer <admin_jwt>` (Required)
* **Path Parameters:** `computerId` (integer), `errorId` (string, uuid của lỗi cần xử lý)
* **Request Body:** (Optional)
    ```json
    {
      "resolution_notes": "string (optional)"
    }
    ```
* **Response Success (200 OK):** (Trả về lỗi đã được cập nhật)
    ```json
    {
      "id": "string (uuid của lỗi)",
      "type": "string",
      "description": "string",
      "reported_by": "integer",
      "reported_at": "timestamp",
      "status": "resolved",
      "resolved_by": "integer",
      "resolved_at": "timestamp",
      "resolution_notes": "string"
    }
    ```
* **Response Error:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `400 Bad Request`

## 6. API Dành Riêng Cho Agent

### Agent định danh lần đầu
* **Method:** `POST`
* **Path:** `/api/agent/identify`
* **Headers:** `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "unique_agent_id": "string (required)"
    }
    ```
* **Response Success (200 OK):** `{ "status": "string ('mfa_required' or 'authentication_required')" }`
* **Response Error:** `400 Bad Request`

### Agent gửi mã MFA
* **Method:** `POST`
* **Path:** `/api/agent/verify-mfa`
* **Headers:** `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "unique_agent_id": "string (required)",
      "mfaCode": "string (required)"
    }
    ```
* **Response Success (200 OK):** (Trả về Agent Token nếu MFA hợp lệ) `{ "agentToken": "string (plain Agent Token)" }`
* **Response Error:**
    * `401 Unauthorized`: `{ "message": "Invalid or expired MFA code" }`
    * `400 Bad Request`: `{ "message": "Missing fields" }`
    * `404 Not Found`: `{ "message": "Agent ID not found or MFA not initiated" }`

### Agent gửi cập nhật trạng thái định kỳ (%CPU, %RAM)
* **Method:** `PUT`
* **Path:** `/api/agent/status`
* **Headers:**
    * `X-Agent-ID: <unique_agent_id_string>` (Required)
    * `Authorization: Bearer <agent_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "cpu": "number (required, percentage)",
      "ram": "number (required, percentage)"
    }
    ```
* **Response Success (204 No Content):**
* **Response Error:** `401 Unauthorized` (Invalid Agent Token or ID), `400 Bad Request`

### Agent gửi kết quả Lệnh
* **Method:** `POST`
* **Path:** `/api/agent/command-result`
* **Headers:**
    * `X-Agent-ID: <unique_agent_id_string>` (Required)
    * `Authorization: Bearer <agent_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "commandId": "string (required, uuid received from backend)",
      "stdout": "string (optional)",
      "stderr": "string (optional)",
      "exitCode": "integer (required)"
    }
    ```
* **Response Success (204 No Content):**
* **Response Error:** `401 Unauthorized`, `400 Bad Request`, `404 Not Found` (Command ID không hợp lệ)
