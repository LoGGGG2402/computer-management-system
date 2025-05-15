This document describes in detail the API endpoints of the system, including HTTP methods, paths, required headers, parameters, and response structures.

#

## Authentication
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

## Get Current User Info
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

#

## Create User
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

## Get Users
* **Method:** `GET`
* **Path:** `/api/users`
* **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
* **Query Parameters (Filtering):**
    * `?page=integer` (Page number, default: 1)
    * `?limit=integer` (Number of users per page, default: 10)
    * `?username=string` (Fuzzy search by username)
    * `?role=admin|user` (Filter by role)
    * `?is_active=true|false` (Filter by active status)
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

## Get User by ID
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

## Update User
* **Method:** `PUT`
* **Path:** `/api/users/:id`
* **Headers:**
    * `Authorization: Bearer <admin_jwt_token_string>` (Required)
    * `Content-Type: application/json`
* **Path Parameters:** `id` (ID of the user to update)
* **Request Body:** (Only contains fields to update)
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

## Deactivate User
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

## Reactivate User
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

#

## Create Room
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

## Get Rooms
* **Method:** `GET`
* **Path:** `/api/rooms`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Query Parameters:**
    * `?page=integer` (Page number, default: 1)
    * `?limit=integer` (Number of rooms per page, default: 10)
    * `?name=string` (Fuzzy search by room name)
    * `?assigned_user_id=integer` (Filter rooms by assigned user ID)
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

## Get Room by ID
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

## Update Room
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

#

## Assign Users to Room
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

## Unassign Users from Room
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

## Get Users in Room
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

#

## Get Computers
* **Method:** `GET`
* **Path:** `/api/computers`
* **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
* **Query Parameters:**
    * `?page=integer` (Page number, default: 1)
    * `?limit=integer` (Number of computers per page, default: 10)
    * `?name=string` (Fuzzy search by computer name)
    * `?roomId=integer` (Filter by room)
    * `?status=online|offline` (Filter by online/offline status)
    * `?has_errors=true|false` (Filter by active errors)
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

## Get Computer by ID
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

## Delete Computer
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

## Get Computer Errors
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

## Report Computer Error
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

## Resolve Computer Error
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

#

## Agent Identification
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
    * MFA Required Case: 
    ```json
    {
      "status": "mfa_required"
    }
    ```
    * Already Registered Case: 
    ```json
    {
      "status": "success",
      "agentToken": "string (if token renewal)"
    }
    ```
    * Position Error Case:
    ```json
    {
      "status": "position_error",
      "message": "string"
    }
    ```

## Verify Agent MFA
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

## Update Agent Hardware Info
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

#

## Get System Statistics
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

#

## WebSocket Connection

* **URL:** `/socket.io`
* **Headers:**
    * `X-Client-Type`: `"frontend"` or `"agent"` (Required)
    * `Authorization`: `Bearer <jwt_token>` (Optional)
    * `Agent-ID`: `string (unique_agent_id)` (Optional, only for agent)

## Frontend Events

### Frontend Authentication
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

### Subscribe to Computer
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

### Unsubscribe from Computer
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

### Send Command to Computer
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
    * No authentication token: `{ "status": "error", "message": "Not authenticated" }`
    * Invalid computer ID: `{ "status": "error", "message": "Valid Computer ID is required" }`
    * No command content: `{ "status": "error", "message": "Command content is required", "computerId": "integer" }`
    * No access permission: `{ "status": "error", "message": "Access denied to send commands to this computer", "computerId": "integer" }`
    * Agent not connected: `{ "status": "error", "message": "Agent is not connected", "computerId": "integer", "commandId": "string", "commandType": "string" }`
    * Server error: `{ "status": "error", "message": "Failed to send command due to server error", "computerId": "integer" }`

## Agent Events

### Agent Authentication
* **Event:** `agent:authenticate`
* **Data:** 
    ```json
    { 
      "agentId": "string",
      "token": "string"
    }
    ```
* **Success Response Event:** `agent:ws_auth_success`
* **Success Response Data:** 
    ```json
    {
      "status": "success",
      "message": "Authentication successful"
    }
    ```
* **Failure Response Event:** `agent:ws_auth_failed`
* **Failure Response Data:** 
    ```json
    {
      "status": "error",
      "message": "string (Authentication failed (Invalid ID or token) | Missing agent ID or token | Internal server error during authentication)"
    }
    ```

### Agent Status Update
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

### Agent Command Result
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

## Server Broadcast Events

### Computer Status Update
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

### Command Completed
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

### New Agent MFA Notification
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

### Agent Registered Notification
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

## Server to Agent Events

### Execute Command
* **Event:** `command:execute`
* **Data:**
    ```json
    {
      "command": "string",
      "commandId": "string (uuid)",
      "commandType": "string (optional, default: 'console')"
    }
    ```

#

## Upload Agent Version
* **Method:** `POST`
* **Path:** `/api/admin/agents/versions`
* **Headers:** 
  * `Authorization: Bearer <admin_jwt_token_string>` (Required)
  * `Content-Type: multipart/form-data`
* **Request Body:**
  * `package`: File (required - agent package file, format .zip, .gz, or .tar)
  * `version`: String (required - agent version, e.g., "1.2.0")
  * `notes`: String (optional - version notes)
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

## Update Agent Version Stability
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

## Get Agent Versions
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

## Check for Agent Updates
* **Method:** `GET`
* **Path:** `/api/agent/check-update`
* **Headers:**
  * `agent-id: string (unique_agent_id)` (Required)
  * `agent-token: string` (Required)
* **Query Parameters:**
  * `current_version`: String (optional - current agent version, e.g., "1.1.0")
* **Response Success when update available (200 OK):**
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
* **Response Success when no update available (204 No Content)**
* **Response Error:**
  * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  * `500 Internal Server Error`: `{ "status": "error", "message": "Failed to check for agent updates" }`

## Report Agent Error
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
* **Standardized `error_type` list for update errors:**
  * `"UpdateResourceCheckFailed"`: Error checking resources before update
  * `"UpdateDownloadFailed"`: Error downloading update package
  * `"UpdateChecksumMismatch"`: Error when downloaded file checksum doesn't match
  * `"UpdateExtractionFailed"`: Error extracting package
  * `"UpdateLaunchFailed"`: Error launching update process
  * `"UpdateGeneralFailure"`: Other general errors during update
* **Response Success (204 No Content)**
* **Response Error:**
  * `400 Bad Request`: `{ "status": "error", "message": "Error type and message are required" }`
  * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`

## Download Agent Package
* **Method:** `GET`
* **Path:** `/api/agent/agent-packages/:filename`
* **Headers:**
  * `agent-id: string (unique_agent_id)` (Required)
  * `agent-token: string` (Required)
* **Response Success:** Package file content
* **Response Error:**
  * `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  * `404 Not Found`: `{ "status": "error", "message": "File not found" }`
  * `500 Internal Server Error`: `{ "status": "error", "message": "Error serving file" }`

#

## Agent Update Events

### New Version Available Notification
* **Event:** `agent:new_version_available`
* **Data:**
  ```json
  {
    "new_stable_version": "string (e.g. '1.2.0')",
    "timestamp": "timestamp"
  }
  ```
