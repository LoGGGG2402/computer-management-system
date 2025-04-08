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
      "id": "integer",
      "username": "string",
      "role": "string ('admin' or 'user')",
      "token": "string (JWT)"
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
      "is_active": "boolean",
      "created_at": "timestamp",
      "updated_at": "timestamp"
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
* **Response Success (201 Created):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "username": "string",
        "role": "string",
        "is_active": "boolean"
      },
      "message": "User created successfully"
    }
    ```
* **Response Error:**
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Validation errors (e.g., username exists, missing fields)" }`

### Lấy danh sách Users
* **Method:** `GET`
* **Path:** `/api/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?page=integer` (Số trang, mặc định: 1)
    * `?limit=integer` (Số lượng user trên mỗi trang, mặc định: 10)
    * `?role=admin|user` (Lọc theo vai trò)
    * `?is_active=true|false` (Lọc theo trạng thái active)
    * `?username=...` (Tìm kiếm gần đúng theo username)
* **Request Body:** (None)
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
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`

### Lấy thông tin User theo ID
* **Method:** `GET`
* **Path:** `/api/users/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của user)
* **Request Body:** (None)
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
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`

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
        "is_active": "boolean"
      },
      "message": "User updated successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to update user" }`

### Vô hiệu hóa User (không xóa)
* **Method:** `DELETE`
* **Path:** `/api/users/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của user cần vô hiệu hóa)
* **Request Body:** (None)
* **Response Success (200 OK):** 
    ```json
    {
      "status": "success",
      "message": "User inactivated successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "User not found" }`

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
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "description": "string",
        "layout": "object"
      },
      "message": "Room created successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to create room" }`

### Lấy danh sách Rooms (Admin lấy tất cả, User lấy phòng được gán)
* **Method:** `GET`
* **Path:** `/api/rooms`
* **Headers:** `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?name=...` (Tìm kiếm gần đúng theo tên room)
    * `?assigned_user_id=integer` (Lọc phòng theo ID của user được gán)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": [
        {
          "id": "integer",
          "name": "string",
          "description": "string"
        },
        ...
      ]
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`

### Lấy chi tiết Room (Admin lấy được, User chỉ lấy được nếu được gán)
* **Method:** `GET`
* **Path:** `/api/rooms/:id`
* **Headers:** `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của room)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "description": "string",
        "layout": "object",
        "computers": [
          {
            "id": "integer",
            "name": "string",
            "pos_x": "integer",
            "pos_y": "integer",
            "unique_agent_id": "string",
            "status": "string ('online'|'offline')",
            "has_active_errors": "boolean"
          },
          ...
        ]
      }
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "You don't have access to this room" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`

### Cập nhật Room (User hoặc Admin với quyền truy cập Room)
* **Method:** `PUT`
* **Path:** `/api/rooms/:id`
* **Headers:**
    * `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (integer, ID của room)
* **Request Body:**
    ```json
    {
      "name": "string (optional)",
      "description": "string (optional)",
      "layout": "object (optional)"
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
        "layout": "object"
      },
      "message": "Room updated successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "You don't have access to this room" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to update room" }`

### Xóa Room (Admin Only)
* **Method:** `DELETE`
* **Path:** `/api/rooms/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của room)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "message": "Room deleted successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`

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
      "userIds": ["integer", "integer", ...] 
    }
    ```
* **Response Success (200 OK):** 
    ```json
    {
      "status": "success",
      "message": "Users assigned to room successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to assign users" }`

### Gỡ User khỏi Room
* **Method:** `POST`
* **Path:** `/api/rooms/:roomId/unassign`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `roomId` (integer)
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
      "message": "Users unassigned from room successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to unassign users" }`

### Lấy danh sách User trong Room
* **Method:** `GET`
* **Path:** `/api/rooms/:roomId/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `roomId` (integer)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": [
        {
          "id": "integer",
          "username": "string",
          "role": "string"
        },
        ...
      ]
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Room not found" }`

## 5. Quản lý Computers

### Lấy danh sách Computers (Admin only)
* **Method:** `GET`
* **Path:** `/api/computers`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?room_id=integer` (Lọc theo phòng)
    * `?name=string` (Tìm kiếm gần đúng theo tên máy)
    * `?status=online|offline` (Lọc theo trạng thái online/offline)
    * `?has_errors=true` (Chỉ lấy máy đang có lỗi 'active')
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": [
        {
          "id": "integer",
          "name": "string",
          "room_id": "integer",
          "unique_agent_id": "string",
          "status": "string ('online'|'offline')",
          "has_active_errors": "boolean"
        },
        ...
      ]
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`

### Lấy chi tiết Computer (User/Admin được quyền truy cập Room)
* **Method:** `GET`
* **Path:** `/api/computers/:id`
* **Headers:** `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
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
        "errors": [
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
      }
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "You don't have access to this computer" }`
    * `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`

### Cập nhật chi tiết Computer (User/Admin được quyền truy cập Room)
* **Method:** `PUT`
* **Path:** `/api/computers/:id`
* **Headers:**
    * `Authorization: Bearer <user_or_admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:**
    ```json
    {
      "name": "string (optional)",
      "pos_x": "integer (optional)",
      "pos_y": "integer (optional)"
    }
    ```
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "data": {
        "id": "integer",
        "name": "string",
        "room_id": "integer",
        "pos_x": "integer",
        "pos_y": "integer",
        "unique_agent_id": "string",
        "updated_at": "timestamp"
      },
      "message": "Computer updated successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "You don't have access to this computer" }`
    * `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`
    * `400 Bad Request`: `{ "status": "error", "message": "Failed to update computer" }`

### Xóa Computer (Admin Only)
* **Method:** `DELETE`
* **Path:** `/api/computers/:id`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Path Parameters:** `id` (integer, ID của computer)
* **Request Body:** (None)
* **Response Success (200 OK):**
    ```json
    {
      "status": "success",
      "message": "Computer deleted successfully"
    }
    ```
* **Response Error:** 
    * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized" }`
    * `403 Forbidden`: `{ "status": "error", "message": "Admin role required" }`
    * `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`

## 6. Socket.IO Events

### Client-side Events (WebSocket từ Frontend tới Backend)

#### Authentication
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
      "role": "string (if success, 'admin' or 'user')"
    }
    ```

#### Subscribe to Rooms
* **Event:** `frontend:subscribe`
* **Data:**
    ```json
    {
      "roomIds": ["integer", "integer", ...]
    }
    ```
* **Response Event:** `subscribe_response`
* **Response Data:**
    ```json
    {
      "status": "success|error",
      "subscribedRooms": ["integer", "integer", ...],
      "failedRooms": ["integer", "integer", ...],
      "message": "string (on error)"
    }
    ```

#### Unsubscribe from Rooms
* **Event:** `frontend:unsubscribe`
* **Data:**
    ```json
    {
      "roomIds": ["integer", "integer", ...]
    }
    ```
* **Response Event:** `unsubscribe_response`
* **Response Data:**
    ```json
    {
      "status": "success|error"
    }
    ```

#### Send Command to Computer
* **Event:** `frontend:send_command`
* **Data:**
    ```json
    {
      "computerId": "integer",
      "command": "string"
    }
    ```
* **Response Event:** `command_sent`
* **Response Data:**
    ```json
    {
      "status": "success|error",
      "computerId": "integer",
      "commandId": "string (uuid)",
      "message": "string (on error)"
    }
    ```

### Agent-side Events (WebSocket từ Agent tới Backend)

#### Agent Authentication
* **Event:** `agent:authenticate_ws`
* **Data:**
    ```json
    {
      "agentId": "string",
      "token": "string"
    }
    ```
* **Response Event:** `agent:ws_auth_success` or `agent:ws_auth_failed`
* **Response Success Data:**
    ```json
    {
      "computerId": "integer"
    }
    ```
* **Response Error Data:**
    ```json
    {
      "message": "string"
    }
    ```

#### Agent Status Update
* **Event:** `agent:status_update`
* **Data:**
    ```json
    {
      "cpuUsage": "number (percentage)",
      "ramUsage": "number (percentage)"
    }
    ```

#### Agent Command Result
* **Event:** `agent:command_result`
* **Data:**
    ```json
    {
      "commandId": "string (uuid)",
      "stdout": "string",
      "stderr": "string",
      "exitCode": "integer"
    }
    ```

### Server-side Events (WebSocket từ Backend tới Frontend và Agent)

#### Computer Status Update (to Frontend)
* **Event:** `computer:status_updated`
* **Data:**
    ```json
    {
      "computerId": "integer",
      "status": "string ('online'|'offline')",
      "cpuUsage": "number (percentage)",
      "ramUsage": "number (percentage)",
      "timestamp": "timestamp"
    }
    ```

#### Command Execution (to Agent)
* **Event:** `command:execute`
* **Data:**
    ```json
    {
      "commandId": "string (uuid)",
      "command": "string"
    }
    ```

#### Command Completion (to Frontend)
* **Event:** `command:completed`
* **Data:**
    ```json
    {
      "commandId": "string (uuid)",
      "computerId": "integer",
      "stdout": "string",
      "stderr": "string",
      "exitCode": "integer"
    }
    ```

#### Admin MFA Notification (to Admin)
* **Event:** `admin:new_agent_mfa`
* **Data:**
    ```json
    {
      "unique_agent_id": "string",
      "mfaCode": "string",
      "roomInfo": {
        "room": "string",
        "roomId": "integer",
        "posX": "integer",
        "posY": "integer",
        "maxColumns": "integer",
        "maxRows": "integer"
      },
      "timestamp": "timestamp"
    }
    ```

#### Admin Agent Registration Notification (to Admin)
* **Event:** `admin:agent_registered`
* **Data:**
    ```json
    {
      "unique_agent_id": "string",
      "computerId": "integer",
      "timestamp": "timestamp"
    }
    ```
