# CMS API Documentation

This document describes in detail the API endpoints of the Computer Management System, including HTTP methods, paths, required headers, parameters, and response structures.

## Authentication Token Information

Throughout this document, authentication tokens are referenced:

- **Access Tokens (AT)**: All references to `<jwt_token_string>` or `<admin_jwt_token_string>` refer to JWT access tokens obtained from the `/api/auth/login` or `/api/auth/refresh-token` endpoints. These tokens have a short lifespan (typically 15 minutes to 1 hour) and are used to authenticate API requests. Admin tokens are obtained when logging in with an account that has admin privileges.

- **Refresh Tokens (RT)**: Long-lived tokens (typically 7 days to 30 days) that are used solely to obtain new access tokens when they expire. Refresh tokens are stored as HttpOnly cookies and are automatically included in requests to the `/api/auth/refresh-token` endpoint. They cannot be accessed by JavaScript and are rotated (a new one is issued) each time they are used. For security, hashed versions of these tokens are stored in the database and invalidated when used.

## Token Security and Database Management

- **Token Storage**: Refresh tokens are stored in the `refresh_tokens` table in hashed form using bcrypt. This ensures that even if the database is compromised, the actual tokens cannot be extracted.

- **Token Rotation**: Each time a refresh token is used, it is invalidated and a new one is issued, preventing replay attacks.

- **Automatic Cleanup**: A scheduled task runs daily at midnight to remove expired refresh tokens from the database. This prevents the token table from growing indefinitely.

- **Security Events**: Any suspicious token activity (such as reuse of a previously invalidated token) triggers security measures, including possibly invalidating all tokens for the affected user.

- **Password Change**: When a user changes their password, all their refresh tokens are immediately invalidated, forcing re-authentication on all devices.

## Table of Contents
- [CMS API Documentation](#cms-api-documentation)
  - [Authentication Token Information](#authentication-token-information)
  - [Token Security and Database Management](#token-security-and-database-management)
  - [Table of Contents](#table-of-contents)
- [Frontend Interfaces](#frontend-interfaces)
  - [Frontend HTTP API](#frontend-http-api)
    - [Frontend Authentication](#frontend-authentication)
      - [Login](#login)
      - [Refresh Token](#refresh-token)
      - [Logout](#logout)
      - [Get Current User Info](#get-current-user-info)
    - [User Management](#user-management)
      - [Create User](#create-user)
      - [Get Users](#get-users)
      - [Get User by ID](#get-user-by-id)
      - [Update User](#update-user)
      - [Deactivate User](#deactivate-user)
      - [Reactivate User](#reactivate-user)
    - [Room Management](#room-management)
      - [Create Room](#create-room)
      - [Get Rooms](#get-rooms)
      - [Get Room by ID](#get-room-by-id)
      - [Update Room](#update-room)
      - [Assign Users to Room](#assign-users-to-room)
      - [Unassign Users from Room](#unassign-users-from-room)
      - [Get Users in Room](#get-users-in-room)
    - [Computer Management](#computer-management)
      - [Get Computers](#get-computers)
      - [Get Computer by ID](#get-computer-by-id)
      - [Delete Computer](#delete-computer)
      - [Get Computer Errors](#get-computer-errors)
      - [Report Computer Error (Frontend)](#report-computer-error-frontend)
      - [Resolve Computer Error](#resolve-computer-error)
    - [System Statistics](#system-statistics)
      - [Get System Statistics](#get-system-statistics)
    - [Agent Version Management (Admin)](#agent-version-management-admin)
      - [Upload Agent Version](#upload-agent-version)
      - [Update Agent Version Stability](#update-agent-version-stability)
      - [Get Agent Versions](#get-agent-versions)
  - [Frontend WebSocket API](#frontend-websocket-api)
    - [Frontend WebSocket Connection](#frontend-websocket-connection)
    - [Frontend Authentication](#frontend-authentication-1)
    - [Frontend Events](#frontend-events)
      - [Subscribe to Computer](#subscribe-to-computer)
      - [Unsubscribe from Computer](#unsubscribe-from-computer)
      - [Send Command to Computer](#send-command-to-computer)
- [Server Broadcast Events](#server-broadcast-events)
  - [Computer Status Update](#computer-status-update)
  - [Command Completed](#command-completed)
  - [New Agent MFA Notification](#new-agent-mfa-notification)
  - [Agent Registered Notification](#agent-registered-notification)
---

# Frontend Interfaces

This section describes the interfaces used by the frontend application to interact with the server.

## Frontend HTTP API

### Frontend Authentication

**Authentication Note:** All API endpoints that require authentication will use the JWT Access Token obtained from the `/api/auth/login` or `/api/auth/refresh-token` endpoints. This token should be included in the `Authorization: Bearer <jwt_token_string>` header.

When an Access Token expires, the frontend should automatically request a new one by calling the `/api/auth/refresh-token` endpoint, which uses the HttpOnly Refresh Token cookie to authenticate the request. The server implements a token rotation strategy, where each use of a Refresh Token invalidates it and issues a new one.

**Client-Side Implementation Guidance:**
- Access Tokens (AT) should be stored in memory (variables or state management) and never in localStorage or sessionStorage.
- The frontend should implement interceptors for HTTP requests to automatically handle 401 errors by requesting a new AT using the RT cookie.
- When multiple API calls fail simultaneously due to expired AT, only one refresh attempt should be made (using a queue or flag mechanism).
- After a successful refresh, all pending requests should be retried with the new AT.
- If the refresh attempt fails, the user should be redirected to the login page.

#### Login

Authenticates a user and returns an access token along with a refresh token cookie for subsequent API requests.
- **Method:** `POST`
- **Path:** `/api/auth/login`
- **Headers:** `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "username": "string (required)",
    "password": "string (required)"
  }
  ```
- **Field Constraints:**
  - `username`: 3-50 characters, alphanumeric with underscores and hyphens only, must start with a letter, regex pattern: `^[a-zA-Z][a-zA-Z0-9_-]{2,49}$`
  - `password`: 8-128 characters, must contain at least one uppercase letter, one lowercase letter, one number, and one special character, regex pattern: `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,128}$`
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "id": "integer (positive non-zero)",
      "username": "string (3-50 characters)",
      "role": "string ('admin' or 'user' only)",
      "is_active": "boolean (true or false)",
      "token": "string (JWT Access Token, typically 100-300 characters)",
      "expires_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
    }
  }
  ```
- **Cookies Set:**
  - `refreshToken`: HttpOnly cookie containing the refresh token. This cookie cannot be accessed by JavaScript and is used to obtain new access tokens. It has the following security attributes:
    - `httpOnly`: true (Cannot be accessed by JavaScript)
    - `secure`: true (In production environment)
    - `sameSite`: 'Strict'
    - `maxAge`: Matches the refresh token lifespan (typically 7-30 days)
- **Response Error:**
  - `401 Unauthorized`: `{ "status": "error", "message": "Invalid credentials" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Username and password are required" }`

#### Refresh Token

Obtains a new access token using the refresh token cookie when the current access token expires.

- **Method:** `POST`
- **Path:** `/api/auth/refresh-token`
- **Headers:** None required (the refreshToken cookie is automatically sent)
- **Cookies Required:**
  - `refreshToken`: HttpOnly cookie set during login or previous token refresh
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "token": "string (new JWT Access Token, typically 100-300 characters)",
      "expires_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
    }
  }
  ```
- **Cookies Set:**
  - `refreshToken`: New HttpOnly cookie containing a new refresh token (token rotation). The old refresh token is simultaneously invalidated in the database as part of the security mechanism. The cookie has the same security attributes as described in the login endpoint.
- **Security Notes:**
  - The system implements token rotation where each refresh token can only be used once. After use, it is immediately invalidated, and a new token is issued.
  - The database maintains a record of all active refresh tokens with their expiry timestamps.
  - If a previously used token is detected, the system will consider this a potential token theft and invalidate all tokens for that user.
- **Response Error:**
  - `401 Unauthorized`: `{ "status": "error", "message": "Invalid or expired refresh token" }`
  - `403 Forbidden`: `{ "status": "error", "message": "Refresh token reuse detected" }` (when token reuse is detected)

#### Logout

Ends the user's session by invalidating their refresh token and clearing the refresh token cookie.

- **Method:** `POST`
- **Path:** `/api/auth/logout`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Optional)
- **Cookies Required:**
  - `refreshToken`: HttpOnly cookie to be invalidated
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "message": "Logged out successfully"
  }
  ```
- **Cookies Cleared:**
  - `refreshToken`: The refresh token cookie is cleared with matching path and domain settings
- **Security Actions:**
  - The endpoint removes the refresh token from the database, ensuring it cannot be reused even if extracted
  - The system will delete the entry from the `refresh_tokens` table associated with this token
- **Response Notes:**
  - Even if the access token is not provided, the endpoint will still clear the refresh token cookie and invalidate the token in the database
  - Access tokens remain valid until they expire, but without a refresh token, they cannot be renewed

#### Get Current User Info

Retrieves information about the currently authenticated user based on their access token.

- **Method:** `GET`
- **Path:** `/api/auth/me`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "id": "integer (positive non-zero)",
      "username": "string (3-50 characters)",
      "role": "string ('admin' or 'user' only)",
      "is_active": "boolean (true or false)",
      "created_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
      "updated_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
    }
  }
  ```

### User Management

#### Create User

Creates a new user account in the system (admin privilege required).

- **Method:** `POST`
- **Path:** `/api/users`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "username": "string (required)",
    "password": "string (required)",
    "role": "string (optional, default: 'user', 'admin' or 'user')",
    "is_active": "boolean (optional, default: true)"
  }
  ```
- **Field Constraints:**
  - `username`: 3-50 characters, alphanumeric with underscores and hyphens only, must start with a letter
  - `password`: 8-128 characters, must contain at least one uppercase letter, one lowercase letter, one number, and one special character
  - `role`: Must be either 'admin' or 'user'
  - `is_active`: Boolean value (true or false)
- **Response Success (201 Created):**
  ```json
  {
    "status": "success",
    "data": {
      "id": "integer",
      "username": "string",
      "role": "string",
      "is_active": "boolean",
      "created_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
      "updated_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
    },
    "message": "User created successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Username and password are required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Username already exists" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to create user" }`

#### Get Users

Retrieves a paginated list of users with optional filtering (admin privilege required).

- **Method:** `GET`
- **Path:** `/api/users`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Query Parameters (Filtering):**
  - `?page=integer` (Page number, default: 1, must be a positive integer)
  - `?limit=integer` (Number of users per page, default: 10, range: 1-100)
  - `?username=string` (Fuzzy search by username, minimum 2 characters)
  - `?role=admin|user` (Filter by role, must be exactly 'admin' or 'user')
  - `?is_active=true|false` (Filter by active status, case-sensitive boolean value)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "total": "integer (non-negative)",
      "currentPage": "integer (positive)",
      "totalPages": "integer (non-negative)",
      "users": [
        {
          "id": "integer (positive non-zero)",
          "username": "string (3-50 characters)",
          "role": "string ('admin' or 'user' only)",
          "is_active": "boolean (true or false)",
          "created_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
          "updated_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
        },
        ...
      ]
    }
  }
  ```
- **Response Error:**
  - `500 Internal Server Error`: `{ "status": "error", "message": "Unable to retrieve users" }`

#### Get User by ID

Retrieves detailed information about a specific user by their ID (admin privilege required).

- **Method:** `GET`
- **Path:** `/api/users/:id`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "User not found" }`

#### Update User

Updates user information such as role, active status, username or password (admin privilege required).

- **Method:** `PUT`
- **Path:** `/api/users/:id`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` 
  - `Content-Type: application/json`
- **Path Parameters:** `id` (ID of the user to update)
- **Request Body:** (Only contains fields to update)
  ```json
  {
    "role": "string ('admin' or 'user') (optional)",
    "is_active": "boolean (optional)"
  }
  ```
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Username already exists" }`
  - `404 Not Found`: `{ "status": "error", "message": "User not found" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to update user" }`

#### Deactivate User

Sets a user account to inactive state without permanently deleting it (admin privilege required).

- **Method:** `DELETE`
- **Path:** `/api/users/:id`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "message": "User deactivated successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "User not found" }`

#### Reactivate User

Reactivates a previously deactivated user account (admin privilege required).

- **Method:** `PUT`
- **Path:** `/api/users/:id/reactivate`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "User ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "User not found" }`

### Room Management

#### Create Room

Creates a new room in the system with layout configuration (admin privilege required).

- **Method:** `POST`
- **Path:** `/api/rooms`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "name": "string (required)",
    "description": "string (optional)",
    "layout": {
      "rows": "integer (required)",
      "columns": "integer (required)"
    }
  }
  ```
- **Field Constraints:**
  - `name`: 3-100 characters, alphanumeric with spaces and common punctuation, regex pattern: `^[\w\s.,;:!?()-]{3,100}$`
  - `description`: 0-1000 characters, UTF-8 encoded text
  - `layout.rows`: Integer between 1 and 50 (representing maximum room dimensions), must be a positive integer
  - `layout.columns`: Integer between 1 and 50 (representing maximum room dimensions), must be a positive integer
- **Response Success (201 Created):**
  ```json
  {
    "status": "success",
    "data": {
      "id": "integer",
      "name": "string",
      "description": "string",
      "layout": {
        "rows": "integer",
        "columns": "integer"
      },
      "created_at": "string (ISO-8601 datetime format)",
      "updated_at": "string (ISO-8601 datetime format)"
    },
    "message": "Room created successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Room name is required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to create room" }`

#### Get Rooms

Retrieves a paginated list of rooms with optional filtering parameters.

- **Method:** `GET`
- **Path:** `/api/rooms`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
- **Query Parameters:**
  - `?page=integer` (Page number, default: 1, must be a positive integer)
  - `?limit=integer` (Number of rooms per page, default: 10, range: 1-100)
  - `?name=string` (Fuzzy search by room name, minimum 2 characters)
  - `?assigned_user_id=integer` (Filter rooms by assigned user ID, must be a positive integer)
- **Response Success (200 OK):**
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
            "rows": "integer",
            "columns": "integer"
          },
          "created_at": "timestamp",
          "updated_at": "timestamp"
        },
        ...
      ]
    }
  }
  ```
- **Response Error:**
  - `500 Internal Server Error`: `{ "status": "error", "message": "Unable to retrieve rooms" }`

#### Get Room by ID

Retrieves detailed information about a specific room, including any computers positioned in the room.

- **Method:** `GET`
- **Path:** `/api/rooms/:id`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "id": "integer (positive non-zero)",
      "name": "string (3-100 characters)",
      "description": "string (0-1000 characters)",
      "layout": {
        "rows": "integer (1-50)",
        "columns": "integer (1-50)"
      },
      "created_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
      "updated_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
      "computers": [
        {
          "id": "integer (positive non-zero)",
          "name": "string (1-100 characters)",
          "status": "string (enum: 'online' or 'offline' only)",
          "have_active_errors": "boolean (true or false)",
          "last_update": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
          "room_id": "integer (positive non-zero, matches parent room ID)",
          "pos_x": "integer (0 to layout.columns-1)",
          "pos_y": "integer (0 to layout.rows-1)",
          "os_info": "string (JSON object serialized as string, contains OS details)",
          "cpu_info": "string (JSON object serialized as string, contains CPU details)",
          "gpu_info": "string (JSON object serialized as string, contains GPU details)",
          "total_ram": "integer (in megabytes, non-negative)",
          "total_disk_space": "integer (in megabytes, non-negative)"
        },
        ...
      ]
    }
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Room ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Room not found" }`

#### Update Room

Updates room information such as name and description (admin privilege required).

- **Method:** `PUT`
- **Path:** `/api/rooms/:id`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "name": "string (required, 3-100 characters)",
    "description": "string (required, 0-1000 characters)"
  }
  ```
- **Field Constraints:**
  - `name`: 3-100 characters, alphanumeric with spaces and common punctuation, regex pattern: `^[\w\s.,;:!?()-]{3,100}$`
  - `description`: 0-1000 characters, UTF-8 encoded text
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "id": "integer",
      "name": "string",
      "description": "string",
      "layout": {
        "rows": "integer",
        "columns": "integer"
      },
      "created_at": "timestamp",
      "updated_at": "timestamp"
    },
    "message": "Room updated successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Room ID is required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to update room" }`

#### Assign Users to Room

Assigns one or more users to a specific room, granting them access to computers in that room (admin privilege required).

- **Method:** `POST`
- **Path:** `/api/rooms/:roomId/assign`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "userIds": ["integer", "integer", ...]
  }
  ```
- **Field Constraints:**
  - `userIds`: Array of integers, each representing a valid user ID (positive non-zero integers)
  - Array must not be empty and must contain unique values
  - Maximum of 100 user IDs per request
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "count": "integer"
    },
    "message": "X users assigned to room successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Room ID and user IDs array are required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to assign users to room" }`

#### Unassign Users from Room

Removes room access for one or more users, revoking their ability to interact with computers in that room (admin privilege required).

- **Method:** `POST`
- **Path:** `/api/rooms/:roomId/unassign`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "userIds": ["integer", "integer", ...]
  }
  ```
- **Field Constraints:**
  - `userIds`: Array of integers, each representing a valid user ID (positive non-zero integers)
  - Array must not be empty and must contain unique values
  - Maximum of 100 user IDs per request
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "count": "integer"
    },
    "message": "X users unassigned from room successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Room ID and user IDs array are required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to unassign users from room" }`

#### Get Users in Room

Retrieves a list of all users assigned to a specific room (admin privilege required).

- **Method:** `GET`
- **Path:** `/api/rooms/:roomId/users`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Room ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Unable to retrieve users in room" }`

### Computer Management

#### Get Computers

Retrieves a paginated list of computers with optional filtering parameters.

- **Method:** `GET`
- **Path:** `/api/computers`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
- **Query Parameters:**
  - `?page=integer` (Page number, default: 1, must be a positive integer)
  - `?limit=integer` (Number of computers per page, default: 10, range: 1-100)
  - `?name=string` (Fuzzy search by computer name, minimum 2 characters)
  - `?roomId=integer` (Filter by room, must be a positive integer)
  - `?status=online|offline` (Filter by online/offline status, case-sensitive enum value)
  - `?has_errors=true|false` (Filter by active errors, case-sensitive boolean value)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "total": "integer (non-negative)",
      "currentPage": "integer (positive)",
      "totalPages": "integer (non-negative)",
      "computers": [
        {
          "id": "integer (positive non-zero)",
          "name": "string (1-100 characters)",
          "status": "string (enum: 'online' or 'offline' only)",
          "have_active_errors": "boolean (true or false)",
          "last_update": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
          "room_id": "integer (positive non-zero)",
          "pos_x": "integer (0 to room's layout.columns-1)",
          "pos_y": "integer (0 to room's layout.rows-1)",
          "os_info": "object (JSON object with OS details including name, version, architecture)",
          "cpu_info": "object (JSON object with CPU details including model, cores, speed)",
          "gpu_info": "object (JSON object with GPU details including model, memory)",
          "total_ram": "integer (in megabytes, non-negative)",
          "total_disk_space": "integer (in megabytes, non-negative)",
          "room": {
            "id": "integer (positive non-zero, matching room_id)",
            "name": "string (3-100 characters)"
          }
        },
        ...
      ]
    }
  }
  ```
- **Response Error:**
  - `500 Internal Server Error`: `{ "status": "error", "message": "Unable to retrieve computers" }`

#### Get Computer by ID

Retrieves detailed information about a specific computer, including its hardware specifications and current status.

- **Method:** `GET`
- **Path:** `/api/computers/:id`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`

#### Delete Computer

Permanently removes a computer from the system (admin privilege required).

- **Method:** `DELETE`
- **Path:** `/api/computers/:id`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "message": "Computer deleted successfully"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`

#### Get Computer Errors

Retrieves a list of all reported errors for a specific computer, including both resolved and unresolved issues.

- **Method:** `GET`
- **Path:** `/api/computers/:id/errors`
- **Headers:** `Authorization: Bearer <jwt_token_string>` (Required)
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Computer not found" }`
  - `404 Not Found`: `{ "status": "error", "message": "No errors found for this computer" }`

#### Report Computer Error (Frontend)

Reports a new error or issue for a specific computer from the frontend interface.

- **Method:** `POST`
- **Path:** `/api/computers/:id/errors`
- **Headers:**
  - `Authorization: Bearer <jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "error_type": "string (required)",
    "error_message": "string (required)",
    "error_details": "object (optional)"
  }
  ```
- **Field Constraints:**
  - `error_type`: 2-50 characters, one of predefined error types: "hardware", "software", "network", "peripheral", "other"
  - `error_message`: 5-255 characters, descriptive error message
  - `error_details`: Optional JSON object containing additional error information, max size 2KB
- **Response Success (201 Created):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Computer ID is required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Error type and message are required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to report error" }`

#### Resolve Computer Error

Marks a previously reported computer error as resolved and adds resolution notes.

- **Method:** `PUT`
- **Path:** `/api/computers/:id/errors/:errorId/resolve`
- **Headers:**
  - `Authorization: Bearer <jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "resolution_notes": "string (required)"
  }
  ```
- **Field Constraints:**
  - `resolution_notes`: 5-1000 characters, description of how the error was resolved
  - Must not be empty or contain only whitespace characters
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Computer ID and Error ID are required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Resolution notes are required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Error not found" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Unable to resolve error" }`

### System Statistics

#### Get System Statistics

Retrieves comprehensive system statistics including counts of users, rooms, computers, and detailed error information (admin privilege required).

- **Method:** `GET`
- **Path:** `/api/admin/stats`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "data": {
      "totalUsers": "integer (non-negative)",
      "totalRooms": "integer (non-negative)",
      "totalComputers": "integer (non-negative)",
      "onlineComputers": "integer (non-negative)",
      "offlineComputers": "integer (non-negative)",
      "computersWithErrors": "integer (non-negative)",
      "unresolvedErrors": [
        {
          "computerId": "integer (positive non-zero)",
          "computerName": "string (1-100 characters)",
          "errorId": "integer (positive non-zero)",
          "error_type": "string (one of predefined error types)",
          "error_message": "string (5-255 characters)",
          "error_details": "object (optional, JSON object with additional details)",
          "reported_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
        },
        ...
      ]
    }
  }
  ```
- **Response Error:**
  - `500 Internal Server Error`: `{ "status": "error", "message": "Unable to retrieve system statistics" }`

### Agent Version Management (Admin)

#### Upload Agent Version

Uploads a new agent software package version to the system (admin privilege required).

- **Method:** `POST`
- **Path:** `/api/admin/agents/versions`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: multipart/form-data`
- **Request Body:**
  - `package`: File (required - agent package file, format .zip, .gz, or .tar, max size 50MB)
  - `version`: String (required - agent version, must follow semantic versioning format: X.Y.Z where X, Y, Z are non-negative integers, e.g., "1.2.0")
  - `notes`: String (optional - version notes, maximum 2000 characters)
- **Response Success (201 Created):**
  ```json
  {
    "status": "success",
    "message": "Agent version 1.2.0 uploaded successfully",
    "data": {
      "id": "uuid (RFC4122 compliant UUID string, 36 characters)",
      "version": "string (semantic version: X.Y.Z format)",
      "checksum_sha256": "string (64 character hexadecimal SHA-256 hash)",
      "download_url": "string (valid URL to download the agent package)",
      "notes": "string (0-2000 characters)",
      "is_stable": false,
      "file_path": "string (server file path)",
      "file_size": "number (file size in bytes, positive integer)",
      "created_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)",
      "updated_at": "string (ISO-8601 datetime format: YYYY-MM-DDTHH:MM:SS.sssZ)"
    }
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "No agent package file uploaded" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Version is required" }`
  - `400 Bad Request`: `{ "status": "error", "message": "Only archive files (.zip, .gz, .tar) are allowed" }`

#### Update Agent Version Stability

Updates the stability status of an agent version, determining whether it should be used for automatic updates (admin privilege required).

- **Method:** `PUT`
- **Path:** `/api/admin/agents/versions/:versionId`
- **Headers:**
  - `Authorization: Bearer <admin_jwt_token_string>` (Required)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "is_stable": "boolean (required)"
  }
  ```
- **Field Constraints:**
  - `is_stable`: Boolean value (true or false), determines whether the agent version is considered stable for production use
- **Response Success (200 OK):**
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
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "is_stable parameter is required" }`
  - `404 Not Found`: `{ "status": "error", "message": "Agent version with ID xyz not found" }`

#### Get Agent Versions

Retrieves a list of all available agent software versions with their stability status and metadata (admin privilege required).

- **Method:** `GET`
- **Path:** `/api/admin/agents/versions`
- **Headers:** `Authorization: Bearer <admin_jwt_token_string>` (Required)
- **Response Success (200 OK):**
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

## Frontend WebSocket API

### Frontend WebSocket Connection

Establishes a real-time WebSocket connection between the frontend client and the server using Socket.IO.

- **URL:** `/socket.io`
- **Headers:**
  - `X-Client-Type`: `"frontend"` (Required)
  - `Authorization`: `Bearer <jwt_token>` (Required - JWT Access Token obtained from `/api/auth/login` or `/api/auth/refresh-token` endpoint)

### Frontend Authentication

Verifies the user's identity and permissions when establishing a WebSocket connection.

- **Note:** Authentication is handled entirely through WebSocket connection headers when establishing the connection.
- **Connection Authentication Result:**
  - On success: Client receives the standard Socket.io `connect` event
  - On failure: Client receives a `connect_error` event with an error message
  - Example error object:
    ```javascript
    {
      message: "Authentication failed: Invalid token";
    }
    ```
  - Possible `connect_error` Messages:
    ```json
    [
      { "message": "Authentication failed: Invalid token" },
      { "message": "Authentication failed: Token expired" },
      { "message": "Authentication failed: Missing X-Client-Type header" },
      { "message": "Authentication failed: Invalid X-Client-Type header" },
      { "message": "Authentication failed: Missing Authorization header" },
      { "message": "Authentication failed: User account is deactivated" },
      { "message": "Connection refused: Server is at capacity" },
      { "message": "Connection refused: Rate limit exceeded" },
      { "message": "Connection refused: Maintenance mode active" },
      { "message": "Internal error: Unable to establish WebSocket connection" }
    ]
    ```

### Frontend Events

#### Subscribe to Computer

Registers the frontend client to receive real-time updates about a specific computer's status and command results.

- **Event:** `frontend:subscribe`
- **Data:**
  ```json
  {
    "computerId": "integer"
  }
  ```
- **Field Constraints:**
  - `computerId`: Positive non-zero integer representing a valid computer ID
  - Must reference a computer that exists in the system
- **Response Event:** `subscribe_response`
- **Response Data:**
  ```json
  {
    "status": "success|error",
    "message": "string (if error)",
    "computerId": "integer"
  }
  ```
- **Success Response:**
  - `{ "status": "success", "computerId": 123 }`
- **Error Responses:**
  ```json
  [
    { "status": "error", "message": "Not authenticated", "computerId": 123 },
    { "status": "error", "message": "Valid Computer ID is required" },
    { "status": "error", "message": "Access denied to this computer", "computerId": 123 },
    { "status": "error", "message": "Subscription failed due to server error", "computerId": 123 },
    { "status": "error", "message": "Computer not found", "computerId": 123 }
  ]
  ```

#### Unsubscribe from Computer

Stops receiving real-time updates about a specific computer that the client previously subscribed to.

- **Event:** `frontend:unsubscribe`
- **Data:**
  ```json
  {
    "computerId": "integer"
  }
  ```
- **Field Constraints:**
  - `computerId`: Positive non-zero integer representing a valid computer ID
  - Must reference a computer that the client is currently subscribed to
- **Response Event:** `unsubscribe_response`
- **Response Data:**
  ```json
  {
    "status": "success|error",
    "message": "string (if error)",
    "computerId": "integer"
  }
  ```
- **Success Response:**
  - `{ "status": "success", "computerId": 123 }`
- **Error Responses:**
  ```json
  [
    { "status": "error", "message": "Valid Computer ID is required", "computerId": null },
    { "status": "error", "message": "Unsubscription failed", "computerId": 123 }
  ]
  ```

#### Send Command to Computer

Sends a remote command to a specific computer agent for execution and receives a response when completed.

- **Event:** `frontend:send_command`
- **Data:**
  ```json
  {
    "computerId": "integer",
    "command": "string",
    "commandType": "string (optional, default: 'console')"
  }
  ```
- **Field Constraints:**
  - `computerId`: Positive non-zero integer representing a valid computer ID
  - `command`: Non-empty string, maximum length 2000 characters
  - `commandType`: String'console'
- **Acknowledgement Response:**
  ```json
  {
    "status": "success|error",
    "message": "string (if error)",
    "computerId": "integer",
    "commandId": "string (if success or generated)",
    "commandType": "string (if success)"
  }
  ```
- **Success Response:**
  - `{ "status": "success", "computerId": 123, "commandId": "550e8400-e29b-41d4-a716-446655440000", "commandType": "console" }`
- **Error Responses:**
  ```json
  [
    { "status": "error", "message": "Not authenticated" },
    { "status": "error", "message": "Valid Computer ID is required" },
    { "status": "error", "message": "Command content is required", "computerId": 123 },
    { "status": "error", "message": "Access denied to send commands to this computer", "computerId": 123 },
    { "status": "error", "message": "Agent is not connected", "computerId": 123, "commandId": "550e8400-e29b-41d4-a716-446655440000", "commandType": "console" },
    { "status": "error", "message": "Failed to send command due to server error", "computerId": 123 },
    { "status": "error", "message": "Invalid command type specified", "computerId": 123 }
  ]
  ```

---


# Server Broadcast Events

This section describes events that are initiated by the server and broadcast to appropriate connected clients. These events deliver real-time updates without requiring client requests.

## Computer Status Update

Event broadcast to subscribed clients when a computer's status or resource usage changes. Clients must have previously subscribed to a computer using the `frontend:subscribe` event to receive these updates.

- **Event:** `computer:status_updated`
- **Data:**
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
- **Field Constraints:**
  - `computerId`: Positive non-zero integer representing a valid computer ID
  - `status`: String, must be one of: 'online', 'offline'
  - `cpuUsage`: Number between 0.0 and 100.0 (percentage of CPU utilization)
  - `ramUsage`: Number between 0.0 and 100.0 (percentage of RAM utilization)
  - `diskUsage`: Number between 0.0 and 100.0 (percentage of disk space utilization)
  - `timestamp`: String in ISO-8601 datetime format (YYYY-MM-DDTHH:MM:SS.sssZ)
- **Examples:**
  - Online Status:
    ```json
    {
      "computerId": 42,
      "status": "online",
      "cpuUsage": 25.6,
      "ramUsage": 74.2,
      "diskUsage": 68.7,
      "timestamp": "2025-05-18T15:30:45.123Z"
    }
    ```
  - Offline Status:
    ```json
    {
      "computerId": 42,
      "status": "offline",
      "cpuUsage": 0,
      "ramUsage": 0,
      "diskUsage": 0,
      "timestamp": "2025-05-18T16:42:12.345Z"
    }
    ```

## Command Completed

Event broadcast to subscribed clients when a remote command sent to a computer has finished execution. This event is sent to clients that initiated the command through the `frontend:send_command` event.

- **Event:** `command:completed`
- **Data:**
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
- **Field Constraints:**
  - `commandId`: UUID string (RFC4122 compliant, 36 characters) uniquely identifying the command
  - `computerId`: Positive non-zero integer representing a valid computer ID
  - `type`: String, one of: 'console', 'powershell', 'cmd', 'bash', 'system', 'service'
  - `success`: Boolean indicating whether the command executed successfully
  - `result`: JSON object containing command output with fields:
    - `stdout`: String (command standard output, may be empty)
    - `stderr`: String (command error output, may be empty)
    - `exitCode`: Number (command exit code, 0 typically means success)
  - `timestamp`: String in ISO-8601 datetime format (YYYY-MM-DDTHH:MM:SS.sssZ)
- **Examples:**
  - Successful Command:
    ```json
    {
      "commandId": "550e8400-e29b-41d4-a716-446655440000",
      "computerId": 42,
      "type": "console",
      "success": true,
      "result": {
        "stdout": "Total RAM: 16384 MB\nFree RAM: 8192 MB\n",
        "stderr": "",
        "exitCode": 0
      },
      "timestamp": "2025-05-18T15:32:12.456Z"
    }
    ```
  - Failed Command:
    ```json
    {
      "commandId": "550e8400-e29b-41d4-a716-446655440001",
      "computerId": 42,
      "type": "powershell",
      "success": false,
      "result": {
        "stdout": "",
        "stderr": "Error: Command not found. The term 'invalidcmd' is not recognized.",
        "exitCode": 1
      },
      "timestamp": "2025-05-18T15:35:22.789Z"
    }
    ```

## New Agent MFA Notification

Event broadcast to admin clients when a new agent requests registration and requires MFA verification. This notification is only sent to users with admin privileges who are connected to the WebSocket server.

- **Event:** `admin:new_agent_mfa`
- **Data:**
  ```json
  {
    "mfaCode": "string",
    "positionInfo": {
      "roomId": "integer",
      "posX": "integer",
      "posY": "integer",
      "roomName": "string"
    },
    "timestamp": "timestamp"
  }
  ```
- **Field Constraints:**
  - `mfaCode`: String of 6 alphanumeric characters, case-insensitive verification code
  - `positionInfo`: Object containing information about the agent's location:
    - `roomId`: Positive non-zero integer representing a valid room ID
    - `posX`: Integer between 0 and the room's maximum column index
    - `posY`: Integer between 0 and the room's maximum row index
    - `roomName`: String (3-100 characters)
  - `timestamp`: String in ISO-8601 datetime format (YYYY-MM-DDTHH:MM:SS.sssZ)
- **Example:**
  ```json
  {
    "mfaCode": "A7B9C2",
    "positionInfo": {
      "roomId": 15,
      "posX": 3,
      "posY": 2,
      "roomName": "Computer Lab 101"
    },
    "timestamp": "2025-05-18T14:22:33.123Z"
  }
  ```

## Agent Registered Notification

Event broadcast to admin clients when a new agent successfully registers in the system.

- **Event:** `admin:agent_registered`
- **Data:**
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
- **Field Constraints:**
  - `computerId`: Positive non-zero integer representing the newly registered computer ID
  - `positionInfo`: Object containing information about the agent's location:
    - `roomId`: Positive non-zero integer representing a valid room ID
    - `posX`: Integer between 0 and the room's maximum column index
    - `posY`: Integer between 0 and the room's maximum row index
  - `timestamp`: String in ISO-8601 datetime format (YYYY-MM-DDTHH:MM:SS.sssZ)
