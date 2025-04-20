# Chi Tiết Các API Hệ Thống Quản Lý Máy Tính

Tài liệu này mô tả chi tiết các API endpoint của hệ thống, bao gồm phương thức HTTP, đường dẫn, header yêu cầu, tham số, và cấu trúc response.

## 1. API Xác thực (Authentication)

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
      "status": "success",
      "data": {
        "id": "integer",
        "username": "string",
        "role": "string ('admin' or 'user')",
        "is_active": "boolean",
        "token": "string (JWT)",
        "expires_at": "string"
      }
    }
    ```
* **Response Error:**
    * `401 Unauthorized`: `{ "status": "error", "message": "Invalid credentials" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Username and password are required" }`

### Lấy thông tin User hiện tại
* **Method:** `GET`
* **Path:** `/api/auth/me`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "username": "string",
        "role": "string ('admin' or 'user')",
        "is_active": "boolean",
        "created_at": "timestamp",
        "updated_at": "timestamp"
      }
    }
    ```

## 2. API Quản lý Users (Admin Only)

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
      "role": "string (optional, default: 'user', 'admin' or 'user')",
      "is_active": "boolean (optional, default: true)"
    }
    ```
* **Response Success (201 Created):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "username": "string",
        "role": "string",
        "is_active": "boolean",
        "created_at": "timestamp",
        "updated_at": "timestamp"
      },
      "message": "User created successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Username and password are required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Username already exists" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to create user" }`

### Lấy danh sách Users
* **Method:** `GET`
* **Path:** `/api/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?page=integer` (Số trang, mặc định: 1)
    * `?limit=integer` (Số lượng user trên mỗi trang, mặc định: 10)
    * `?username=string` (Tìm kiếm gần đúng theo username)
    * `?role=admin|user` (Lọc theo vai trò)
    * `?is_active=true|false` (Lọc theo trạng thái active)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "total": "integer",
        "currentPage": "integer",
        "totalPages": "integer",
        "users": [
          {
            "id": "integer",
            "username": "string",
            "role": "string",
            "is_active": "boolean",
            "created_at": "timestamp",
            "updated_at": "timestamp"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `500 Internal Server Error`: `{ "status": "error", "message": "Failed to fetch users" }`

### Lấy User theo ID
* **Method:** `GET`
* **Path:** `/api/users/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer", 
        "username": "string",
        "role": "string",
        "is_active": "boolean",
        "created_at": "timestamp",
        "updated_at": "timestamp"
      }
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`

### Cập nhật User
* **Method:** `PUT`
* **Path:** `/api/users/:id`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (ID của user cần cập nhật)
* **Request Body:** (Chỉ chứa các trường cần cập nhật)
    ```json
    {
      "role": "string ('admin' or 'user') (optional)",
      "is_active": "boolean (optional)",
      "username": "string (optional)",
      "password": "string (optional)"
    }
    ```
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "username": "string",
        "role": "string",
        "is_active": "boolean",
        "created_at": "timestamp",
        "updated_at": "timestamp"
      },
      "message": "User updated successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Username already exists" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to update user" }`

### Vô hiệu hóa User
* **Method:** `DELETE`
* **Path:** `/api/users/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):** 
    ```json
    {
      "status": "success",
      "message": "User inactivated successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`

### Kích hoạt lại User
* **Method:** `PUT`
* **Path:** `/api/users/:id/reactivate`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer", 
        "username": "string",
        "role": "string",
        "is_active": true,
        "created_at": "timestamp",
        "updated_at": "timestamp"
      },
      "message": "User reactivated successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`

## 3. API Quản lý Rooms

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
        "columns": "integer",
        "rows": "integer"
      }
    }
    ```
* **Response Success (201 Created):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "description": "string",
        "layout": {
          "columns": "integer",
          "rows": "integer"
        },
        "created_at": "timestamp",
        "updated_at": "timestamp"
      },
      "message": "Room created successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Room name is required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to create room" }`

### Lấy danh sách Rooms
* **Method:** `GET`
* **Path:** `/api/rooms`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Query Parameters:**
    * `?page=integer` (Số trang, mặc định: 1)
    * `?limit=integer` (Số lượng room trên mỗi trang, mặc định: 10)
    * `?name=string` (Tìm kiếm gần đúng theo tên room)
    * `?assigned_user_id=integer` (Lọc phòng theo ID của user được gán)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "total": "integer",
        "currentPage": "integer",
        "totalPages": "integer",
        "rooms": [
          {
            "id": "integer",
            "name": "string",
            "description": "string",
            "layout": {
              "columns": "integer",
              "rows": "integer"
            },
            "created_at": "timestamp",
            "updated_at": "timestamp"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `500 Internal Server Error`: `{ "status": "error", "message": "Failed to fetch rooms" }`

### Lấy chi tiết Room
* **Method:** `GET`
* **Path:** `/api/rooms/:id`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "description": "string",
        "layout": {
          "columns": "integer",
          "rows": "integer"
        },
        "created_at": "timestamp",
        "updated_at": "timestamp",
        "computers": [
          {
            "id": "integer",
            "name": "string",
            "status": "string ('online'|'offline')",
            "have_active_errors": "boolean",
            "last_update": "timestamp",
            "room_id": "integer",
            "pos_x": "integer",
            "pos_y": "integer",
            "os_info": "object",
            "cpu_info": "object",
            "gpu_info": "object",
            "total_ram": "integer",
            "total_disk_space": "integer"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Room ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`

### Cập nhật Room (Admin Only)
* **Method:** `PUT`
* **Path:** `/api/rooms/:id`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "name": "string (optional)",
      "description": "string (optional)"
    }
    ```
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "description": "string",
        "layout": {
          "columns": "integer",
          "rows": "integer"
        },
        "created_at": "timestamp",
        "updated_at": "timestamp"
      },
      "message": "Room updated successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Room ID is required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to update room" }`

## 4. API Quản lý Phân công Room (Admin Only)

### Gán User vào Room
* **Method:** `POST`
* **Path:** `/api/rooms/:roomId/assign`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "userIds": ["integer", "integer", ...] 
    }
    ```
* **Response Success (200 OK):** 
    ```json
    {
      "status": "success",
      "data": {
        "count": "integer"
      },
      "message": "X users assigned to room successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Room ID and user IDs array are required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to assign users to room" }`

### Gỡ User khỏi Room
* **Method:** `POST`
* **Path:** `/api/rooms/:roomId/unassign`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "userIds": ["integer", "integer", ...]
    }
    ```
* **Response Success (200 OK):** 
    ```json
    {
      "status": "success",
      "data": {
        "count": "integer"
      },
      "message": "X users unassigned from room successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Room ID and user IDs array are required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to unassign users from room" }`

### Lấy danh sách User trong Room
* **Method:** `GET`
* **Path:** `/api/rooms/:roomId/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "users": [
          {
            "id": "integer",
            "username": "string",
            "role": "string",
            "is_active": "boolean",
            "created_at": "timestamp",
            "updated_at": "timestamp"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Room ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Failed to get users in room" }`

## 5. API Quản lý Computers

### Lấy danh sách Computers
* **Method:** `GET`
* **Path:** `/api/computers`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Query Parameters:**
    * `?page=integer` (Số trang, mặc định: 1)
    * `?limit=integer` (Số lượng computer trên mỗi trang, mặc định: 10)
    * `?name=string` (Tìm kiếm gần đúng theo tên máy)
    * `?roomId=integer` (Lọc theo phòng)
    * `?status=online|offline` (Lọc theo trạng thái online/offline)
    * `?has_errors=true|false` (Lọc theo có lỗi active hay không)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "total": "integer",
        "currentPage": "integer",
        "totalPages": "integer",
        "computers": [
          {
            "id": "integer",
            "name": "string",
            "status": "string ('online'|'offline')",
            "have_active_errors": "boolean",
            "last_update": "timestamp",
            "room_id": "integer",
            "pos_x": "integer",
            "pos_y": "integer",
            "os_info": "object",
            "cpu_info": "object",
            "gpu_info": "object",
            "total_ram": "integer",
            "total_disk_space": "integer",
            "room": {
              "id": "integer",
              "name": "string"
            }
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `500 Internal Server Error`: `{ "status": "error", "message": "Failed to fetch computers" }`

### Lấy chi tiết Computer
* **Method:** `GET`
* **Path:** `/api/computers/:id`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "status": "string ('online'|'offline')",
        "have_active_errors": "boolean",
        "last_update": "timestamp",
        "room_id": "integer",
        "pos_x": "integer",
        "pos_y": "integer",
        "os_info": "object",
        "cpu_info": "object",
        "gpu_info": "object",
        "total_ram": "integer",
        "total_disk_space": "integer",
        "room": {
          "id": "integer",
          "name": "string"
        }
      }
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`

### Xóa Computer (Admin Only)
* **Method:** `DELETE`
* **Path:** `/api/computers/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "message": "Computer deleted successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`

### Lấy danh sách Lỗi của Computer
* **Method:** `GET`
* **Path:** `/api/computers/:id/errors`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "errors": [
          {
            "id": "integer",
            "error_type": "string",
            "error_message": "string",
            "error_details": "object",
            "reported_at": "timestamp",
            "resolved": "boolean",
            "resolved_at": "timestamp",
            "resolution_notes": "string"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Computer not found or no errors available" }`

### Báo cáo Lỗi cho Computer
* **Method:** `POST`
* **Path:** `/api/computers/:id/errors`
* **Headers:**
    * `Authorization: Bearer <jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "error_type": "string (required)",
      "error_message": "string (required)",
      "error_details": "object (optional)"
    }
    ```
* **Response Success (201 Created):**
    ```json
    {
      "status": "success",
      "data": {
        "error": {
          "id": "integer",
          "error_type": "string",
          "error_message": "string",
          "error_details": "object",
          "reported_at": "timestamp",
          "resolved": false
        },
        "computerId": "integer"
      },
      "message": "Error reported successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Error type and message are required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to report error" }`

### Xử lý Lỗi của Computer
* **Method:** `PUT`
* **Path:** `/api/computers/:id/errors/:errorId/resolve`
* **Headers:**
    * `Authorization: Bearer <jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "resolution_notes": "string (optional)"
    }
    ```
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "error": {
          "id": "integer",
          "error_type": "string",
          "error_message": "string",
          "error_details": "object",
          "reported_at": "timestamp",
          "resolved": true,
          "resolved_at": "timestamp",
          "resolution_notes": "string"
        },
        "computerId": "integer"
      },
      "message": "Error resolved successfully"
    }
    ```
* **Response Error:**
    * `400 Bad Request`: `{ "status": "error", "message": "Computer ID and Error ID are required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to resolve error" }`

## 6. API dành cho Agent

### Đăng ký Agent (Identify)
* **Method:** `POST`
* **Path:** `/api/agent/identify`
* **Headers:** `Content-Type: application/json`
* **Request Body:**
    ```json
    {
      "unique_agent_id": "string (required)",
      "positionInfo": {
        "roomName": "string (required)",
        "posX": "integer (required)",
        "posY": "integer (required)"
      },
      "forceRenewToken": "boolean (optional)"
    }
    ```
* **Response Success (200 OK):**
    * Trường hợp yêu cầu MFA: 
    ```json
    {
      "status": "mfa_required"
    }
    ```
    * Trường hợp đã đăng ký: 
    ```json
    {
      "status": "success",
      "agentToken": "string (if token renewal)"
    }
    ```
    * Trường hợp lỗi vị trí:
    ```json
    {
      "status": "position_error",
      "message": "string"
    }
    ```

### Xác minh MFA từ Agent
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
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "agentToken": "string"
    }
    ```
* **Response Error:**
    * `401 Unauthorized`: `{ "status": "error", "message": "Invalid or expired MFA code" }`

### Cập nhật Thông tin Phần cứng
* **Method:** `POST`
* **Path:** `/api/agent/hardware-info`
* **Headers:**
    * `X-Agent-ID: string (unique_agent_id)`
    * `Authorization: Bearer <agent_token_string>`
* **Request Body:**
    ```json
    {
      "total_disk_space": "integer (required)",
      "gpu_info": "object (optional)",
      "cpu_info": "object (optional)",
      "total_ram": "integer (optional)",
      "os_info": "object (optional)"
    }
    ```
* **Response Success (204 No Content)**

## 7. API Thống kê (Statistics)

### Lấy Thống kê Hệ thống (Admin Only)
* **Method:** `GET`
* **Path:** `/api/stats`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "totalUsers": "integer",
        "totalRooms": "integer",
        "totalComputers": "integer", 
        "onlineComputers": "integer",
        "offlineComputers": "integer",
        "computersWithErrors": "integer",
        "unresolvedErrors": [
          {
            "computerId": "integer",
            "computerName": "string",
            "errorId": "integer",
            "error_type": "string",
            "error_message": "string",
            "error_details": "object",
            "reported_at": "timestamp"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:**
    * `500 Internal Server Error`: `{ "status": "error", "message": "Failed to retrieve system statistics" }`

## 8. WebSocket Events

### Kết nối WebSocket

* **URL:** `/socket.io`
* **Headers:**
    * `X-Client-Type`: `"frontend"` hoặc `"agent"` (Required)
    * `Authorization`: `Bearer <jwt_token>` (Optional)
    * `Agent-ID`: `string (unique_agent_id)` (Optional, chỉ cho agent)

### Events Frontend -> Backend

#### Xác thực Frontend
* **Event:** `frontend:authenticate`
* **Data:** 
    ```json
    { 
      "token": "string (JWT token)" 
    }
    ```
* **Response Event:** `auth_response`
* **Response Data:** 
    ```json
    { 
      "status": "success|error", 
      "message": "string",
      "userId": "integer (if success)",
      "role": "string (if success)" 
    }
    ```

#### Đăng ký nhận cập nhật từ Computer 
* **Event:** `frontend:subscribe`
* **Data:** 
    ```json
    { 
      "computerId": "integer" 
    }
    ```
* **Response Event:** `subscribe_response`
* **Response Data:** 
    ```json
    { 
      "status": "success|error", 
      "message": "string (if error)",
      "computerId": "integer"
    }
    ```

#### Hủy đăng ký nhận cập nhật từ Computer
* **Event:** `frontend:unsubscribe`
* **Data:** 
    ```json
    { 
      "computerId": "integer" 
    }
    ```
* **Response Event:** `unsubscribe_response`
* **Response Data:** 
    ```json
    { 
      "status": "success|error", 
      "message": "string (if error)",
      "computerId": "integer"
    }
    ```

#### Gửi lệnh tới Computer
* **Event:** `frontend:send_command`
* **Data:** 
    ```json
    { 
      "computerId": "integer",
      "command": "string",
      "commandType": "string (optional, default: 'console')"
    }
    ```
* **Acknowledgement Response:** 
    ```json
    { 
      "status": "success|error", 
      "message": "string (if error)",
      "computerId": "integer",
      "commandId": "string (if success or generated)",
      "commandType": "string (if success)" 
    }
    ```
* **Error Responses:**
    * Không có token xác thực: `{ "status": "error", "message": "Not authenticated" }`
    * ID máy tính không hợp lệ: `{ "status": "error", "message": "Valid Computer ID is required" }`
    * Không có nội dung lệnh: `{ "status": "error", "message": "Command content is required", "computerId": "integer" }`
    * Không có quyền truy cập: `{ "status": "error", "message": "Access denied to send commands to this computer", "computerId": "integer" }`
    * Agent không kết nối: `{ "status": "error", "message": "Agent is not connected", "computerId": "integer", "commandId": "string", "commandType": "string" }`
    * Lỗi server: `{ "status": "error", "message": "Failed to send command due to server error", "computerId": "integer" }`

### Events Agent -> Backend

#### Xác thực Agent
* **Event:** `agent:authenticate`
* **Data:** 
    ```json
    { 
      "agentId": "string",
      "token": "string"
    }
    ```
* **Response Event nếu thành công:** `agent:ws_auth_success`
* **Response Data thành công:** 
    ```json
    {
      "status": "success",
      "message": "Authentication successful"
    }
    ```
* **Response Event nếu thất bại:** `agent:ws_auth_failed`
* **Response Data thất bại:** 
    ```json
    {
      "status": "error",
      "message": "string (Authentication failed (Invalid ID or token) | Missing agent ID or token | Internal server error during authentication)"
    }
    ```

#### Cập nhật Trạng thái từ Agent
* **Event:** `agent:status_update`
* **Data:** 
    ```json
    {
      "cpuUsage": "number (percentage)",
      "ramUsage": "number (percentage)",
      "diskUsage": "number (percentage)"
    }
    ```
* **No Direct Response**

#### Gửi Kết quả Lệnh từ Agent
* **Event:** `agent:command_result`
* **Data:** 
    ```json
    {
      "commandId": "string (uuid)",
      "type": "string (default: 'console')",
      "success": "boolean",
      "result": "jsonObject"
    }
    ```
* **No Direct Response**

### Events Backend -> Frontend

#### Cập nhật Trạng thái Máy tính
* **Event:** `computer:status_updated`
* **Data:**
    ```json
    {
      "computerId": "integer",
      "status": "string ('online'|'offline')",
      "cpuUsage": "number (percentage)",
      "ramUsage": "number (percentage)",
      "diskUsage": "number (percentage)",
      "timestamp": "timestamp"
    }
    ```

#### Kết quả Lệnh
* **Event:** `command:completed`
* **Data:**
    ```json
    {
      "commandId": "string (uuid)",
      "computerId": "integer",
      "type": "string",
      "success": "boolean",
      "result": "object",
      "timestamp": "timestamp"
    }
    ```

#### Thông báo MFA cho Admin
* **Event:** `admin:new_agent_mfa`
* **Data:**
    ```json
    {
      "mfaCode": "string",
      "positionInfo": {
        "roomId": "integer (optional)",
        "posX": "integer (optional)",
        "posY": "integer (optional)",
        "roomName": "string (optional)"
      },
      "timestamp": "timestamp"
    }
    ```

#### Thông báo Đăng ký Agent thành công
* **Event:** `admin:agent_registered`
* **Data:**
    ```json
    {
      "computerId": "integer",
      "positionInfo": {
        "roomId": "integer",
        "posX": "integer",
        "posY": "integer"
      },
      "timestamp": "timestamp"
    }
    ```

### Events Backend -> Agent

#### Yêu cầu Thực thi Lệnh
* **Event:** `command:execute`
* **Data:**
    ```json
    {
      "command": "string",
      "commandId": "string (uuid)",
      "commandType": "string (optional, default: 'console')"
    }
    ```

## 9. API Quản lý Phiên bản Agent

### Upload Phiên bản Agent mới (Admin Only)
* **Method:** `POST`
* **Path:** `/api/admin/agents/versions`
* **Headers:** 
  * `Authorization: Bearer <admin_jwt_token_string>` (Required)
  * `Content-Type: multipart/form-data`
* **Request Body:**
  * `package`: File (required - file package của agent, định dạng .zip, .gz, hoặc .tar)
  * `version`: String (required - phiên bản của agent, ví dụ: "1.2.0")
  * `notes`: String (optional - ghi chú về phiên bản)
* **Response Success (201 Created):**
  ```json
  {
    "status": "success",
    "message": "Agent version 1.2.0 uploaded successfully",
    "data": {
      "id": "uuid",
      "version": "string",
      "checksum_sha256": "string",
      "download_url": "string",
      "notes": "string",
      "is_stable": false,
      "file_path": "string",
      "file_size": "number",
      "created_at": "timestamp",
      "updated_at": "timestamp"
    }
  }
  ```
* **Response Error:**
  * `400 Bad Request`: `{ "status": "error", "message": "No agent package file uploaded" }`
  * `400 Bad Request`: `{ "status": "error", "message": "Version is required" }`
  * `400 Bad Request`: `{ "status": "error", "message": "Only archive files (.zip, .gz, .tar) are allowed" }`

### Đặt Trạng thái Stable cho Phiên bản Agent (Admin Only)
* **Method:** `PUT`
* **Path:** `/api/admin/agents/versions/:versionId`
* **Headers:**
  * `Authorization: Bearer <admin_jwt_token_string>` (Required)
  * `Content-Type: application/json`
* **Request Body:**
  ```json
  {
    "is_stable": "boolean (required)"
  }
  ```
* **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "message": "Agent version 1.2.0 stability updated",
    "data": {
      "id": "uuid",
      "version": "string",
      "checksum_sha256": "string",
      "download_url": "string",
      "notes": "string",
      "is_stable": "boolean",
      "file_path": "string",
      "file_size": "number",
      "created_at": "timestamp",
      "updated_at": "timestamp"
    }
  }
  ```
* **Response Error:**
  * `400 Bad Request`: `{ "status": "error", "message": "is_stable parameter is required" }`
  * `404 Not Found`: `{ "status": "error", "message": "Agent version with ID xyz not found" }`

### Lấy Danh sách Phiên bản Agent (Admin Only)
* **Method:** `GET`
* **Path:** `/api/admin/agents/versions`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": [
      {
        "id": "uuid",
        "version": "string",
        "checksum_sha256": "string",
        "download_url": "string",
        "notes": "string",
        "is_stable": "boolean",
        "file_path": "string",
        "file_size": "number",
        "created_at": "timestamp",
        "updated_at": "timestamp"
      },
      ...
    ]
  }
  ```

### Kiểm tra Bản cập nhật Agent (Cho Agent)
* **Method:** `GET`
* **Path:** `/api/agent/check_update`
* **Headers:**
  * `agent-id: string (unique_agent_id)` (Required)
  * `agent-token: string` (Required)
* **Query Parameters:**
  * `current_version`: String (optional - phiên bản hiện tại của agent, ví dụ: "1.1.0")
* **Response Success khi có bản cập nhật (200 OK):**
  ```json
  {
    "status": "success",
    "update_available": true,
    "version": "string",
    "download_url": "string",
    "checksum_sha256": "string",
    "notes": "string"
  }
  ```
* **Response Success khi không có bản cập nhật (204 No Content)**
* **Response Error:**
  * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  * `500 Internal Server Error`: `{ "status": "error", "message": "Failed to check for agent updates" }`

### Báo cáo Lỗi từ Agent
* **Method:** `POST`
* **Path:** `/api/agent/report-error`
* **Headers:**
  * `agent-id: string (unique_agent_id)` (Required)
  * `agent-token: string` (Required)
  * `Content-Type: application/json`
* **Request Body:**
  ```json
  {
    "error_type": "string (required)",
    "message": "string (required)",
    "details": "object (optional)",
    "timestamp": "string (optional)",
    "agent_version": "string (optional)",
    "stack_trace": "string (optional)"
  }
  ```
* **Danh sách `error_type` thống nhất cho lỗi cập nhật:**
  * `"UpdateResourceCheckFailed"`: Lỗi khi kiểm tra tài nguyên trước khi cập nhật
  * `"UpdateDownloadFailed"`: Lỗi khi tải package cập nhật
  * `"UpdateChecksumMismatch"`: Lỗi khi checksum file tải về không khớp
  * `"UpdateExtractionFailed"`: Lỗi khi giải nén package
  * `"UpdateLaunchFailed"`: Lỗi khi khởi chạy quá trình cập nhật
  * `"UpdateGeneralFailure"`: Các lỗi chung khác trong quá trình cập nhật
* **Response Success (204 No Content)**
* **Response Error:**
  * `400 Bad Request`: `{ "status": "error", "message": "Error type and message are required" }`
  * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`

### Tải Gói Package Agent (Yêu cầu xác thực Agent)
* **Method:** `GET`
* **Path:** `/api/agent/agent-packages/:filename`
* **Headers:**
  * `agent-id: string (unique_agent_id)` (Required)
  * `agent-token: string` (Required)
* **Response Success:** File nội dung package
* **Response Error:**
  * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  * `404 Not Found`: `{ "status": "error", "message": "File not found" }`
  * `500 Internal Server Error`: `{ "status": "error", "message": "Error serving file" }`

## 10. WebSocket Events liên quan đến Agent Versioning

### Backend -> Agent

#### Thông báo có phiên bản Agent mới
* **Event:** `agent:new_version_available`
* **Data:**
  ```json
  {
    "new_stable_version": "string (e.g. '1.2.0')",
    "timestamp": "timestamp"
  }
  ```
