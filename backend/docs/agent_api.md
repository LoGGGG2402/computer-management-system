# CMS API Documentation

This document describes in detail the API endpoints of the Computer Management System, including HTTP methods, paths, required headers, parameters, and response structures for agent interfaces.

## Authentication Token Information

Throughout this document authentication tokens are referenced:

- **Agent Tokens**: All references to `<agent_token>` refer to tokens obtained from either the `/api/agent/identify` endpoint (for already registered agents) or from the `/api/agent/verify-mfa` endpoint (for new agents completing registration). Unlike user tokens, agent tokens do not expire and do not have a refresh mechanism.

## Agent Token Security

- **Token Storage**: Agent tokens are cryptographically secure, unique identifiers assigned to each registered agent.
- **Token Validation**: Each API request is validated against the database to ensure the token is valid and matches the claimed agent ID.
- **Security Events**: Any suspicious activity, such as invalid token usage or unexpected agent behavior, is logged and may trigger security alerts.
- **Token Revocation**: Administrator users can manually revoke agent tokens through the admin interface if an agent is compromised or needs to be redeployed.

## Table of Contents
- [CMS API Documentation](#cms-api-documentation)
  - [Authentication Token Information](#authentication-token-information)
  - [Agent Token Security](#agent-token-security)
  - [Table of Contents](#table-of-contents)
- [Agent Interfaces](#agent-interfaces)
  - [Agent HTTP API](#agent-http-api)
    - [Agent Authentication](#agent-authentication)
      - [Agent Identification](#agent-identification)
      - [Verify Agent MFA](#verify-agent-mfa)
    - [Agent Information Updates](#agent-information-updates)
      - [Update Agent Hardware Info](#update-agent-hardware-info)
      - [Report Agent Error](#report-agent-error)
    - [Agent Versioning](#agent-versioning)
      - [Check for Agent Updates](#check-for-agent-updates)
      - [Download Agent Package](#download-agent-package)
  - [Agent WebSocket API](#agent-websocket-api)
    - [Agent WebSocket Connection](#agent-websocket-connection)
    - [Agent Authentication](#agent-authentication-1)
    - [Agent Events](#agent-events)
      - [Agent Status Update](#agent-status-update)
      - [Agent Command Result](#agent-command-result)
- [Server Broadcast Events](#server-broadcast-events)
  - [Execute Command](#execute-command)
  - [New Version Available Notification](#new-version-available-notification)
---
# Agent Interfaces

This section describes the interfaces used by agent software running on managed computers.

## Agent HTTP API

### Agent Authentication

**Authentication Note:** All agent API endpoints that require authentication will use the token obtained from the `/api/agent/identify` or `/api/agent/verify-mfa` endpoints. This token should be included in the `Authorization: Bearer <agent_token>` header along with the `X-Agent-ID: string (agentId)` header.

**Client-Side Implementation Guidance:**
- Agent tokens should be stored securely on the device.
- The agent should include the token in all authenticated requests.
- If a request returns a 401 Unauthorized error, the agent should attempt to re-identify using the stored agentId.
- If the re-identification requires MFA, the agent should await MFA verification before continuing.

#### Agent Identification

Identifies an agent with the system and returns a token if already registered, or prompts for MFA if it's a new agent or position has changed.

- **Method:** `POST`
- **Path:** `/api/agent/identify`
- **Headers:** `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "agentId": "string (required)",
    "positionInfo": {
      "roomName": "string (required)",
      "posX": "integer (required)",
      "posY": "integer (required)"
    }
  }
  ```
- **Field Constraints:**
  - `agentId`: 8-36 characters, unique identifier for the agent
  - `positionInfo`: Object containing information about the agent's location:
    - `roomName`: String (3-100 characters), name of the room where agent is located
    - `posX`: Integer between 0 and the room's maximum column index
    - `posY`: Integer between 0 and the room's maximum row index
- **Response Success (200 OK):**
  - MFA Required Case:
  ```json
  {
    "status": "mfa_required"
  }
  ```
  - Already Registered Case:
  ```json
  {
    "status": "success",
    "agentToken": "string"
  }
  ```
  - Position Error Case:
  ```json
  {
    "status": "position_error",
    "message": "string"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Missing required fields" }`
  - `404 Not Found`: `{ "status": "error", "message": "Room not found" }`
  - `409 Conflict`: `{ "status": "error", "message": "Position already occupied" }`
  - `500 Internal Server Error`: `{ "status": "error", "message": "Failed to process agent identification" }`

#### Verify Agent MFA

Verifies the MFA code for a new agent registration and returns an agent token upon successful verification.

- **Method:** `POST`
- **Path:** `/api/agent/verify-mfa`
- **Headers:** `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "agentId": "string (required)",
    "mfaCode": "string (required)"
  }
  ```
- **Field Constraints:**
  - `agentId`: 8-36 characters, unique identifier for the agent
  - `mfaCode`: 6 characters, case-insensitive alphanumeric verification code
- **Response Success (200 OK):**
  ```json
  {
    "status": "success",
    "agentToken": "string"
  }
  ```
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Missing required fields" }`
  - `401 Unauthorized`: `{ "status": "error", "message": "Invalid or expired MFA code" }`
  - `404 Not Found`: `{ "status": "error", "message": "Agent ID not found" }`
  - `500 Internal Server Error`: `{ "status": "error", "message": "Failed to verify MFA code" }`

### Agent Information Updates

#### Update Agent Hardware Info

Updates the hardware information for the specified agent.

- **Method:** `POST`
- **Path:** `/api/agent/hardware-info`
- **Headers:**
  - `X-Agent-ID`: `string (agentId)` (Required)
  - `Authorization`: `Bearer <agent_token>` (Required - Token obtained from `/api/agent/identify` or `/api/agent/verify-mfa`)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "total_disk_space": "integer (required)",
    "gpu_info": "string",
    "cpu_info": "string",
    "total_ram": "integer",
    "os_info": "string"
  }
  ```
- **Field Constraints:**
  - `total_disk_space`: Positive integer representing total disk space in MB
  - `gpu_info`: String (0-500 characters), description of GPU hardware
  - `cpu_info`: String (0-500 characters), description of CPU hardware
  - `total_ram`: Positive integer representing total RAM in MB
  - `os_info`: String (0-200 characters), description of operating system
- **Response Success (204 No Content)**
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Total disk space is required" }`
  - `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  - `500 Internal Server Error`: `{ "status": "error", "message": "Failed to update hardware information" }`

#### Report Agent Error

Reports an error encountered by the agent to the system.

- **Method:** `POST`
- **Path:** `/api/agent/report-error`
- **Headers:**
  - `X-Agent-ID`: `string (agentId)` (Required)
  - `Authorization`: `Bearer <agent_token>` (Required - Token obtained from `/api/agent/identify` or `/api/agent/verify-mfa`)
  - `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "type": "string (required)",
    "message": "string (required)",
    "details": "object (optional)"
  }
  ```
- **Field Constraints:**
  - `type`: String (2-50 characters), type of error encountered
  - `message`: String (5-255 characters), descriptive error message
  - `details`: JSON object containing additional error information, max size 2KB
- **Standardized `type` list for update errors:**
  - `"DownloadFailed"`: Error downloading update package
  - `"ChecksumMismatch"`: Error when downloaded file checksum doesn't match
  - `"ExtractionFailed"`: Error extracting package
  - `"UpdateLaunchFailed"`: Error launching update process
  - `"StartAgentFailed"`: Error starting agent after update
  - `"UpdateGeneralFailure"`: Other general errors during update
- **Response Success (204 No Content)**
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Error type and message are required" }`
  - `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  - `500 Internal Server Error`: `{ "status": "error", "message": "Failed to report error" }`

### Agent Versioning

#### Check for Agent Updates

Checks if a newer version of the agent software is available.

- **Method:** `GET`
- **Path:** `/api/agent/check-update`
- **Headers:**
  - `X-Agent-ID`: `string (agentId)` (Required)
  - `Authorization`: `Bearer <agent_token>` (Required - Token obtained from `/api/agent/identify` or `/api/agent/verify-mfa`)
- **Query Parameters:**
  - `current_version`: String (required - current agent version, e.g., "1.1.0")
- **Field Constraints:**
  - `current_version`: String following semantic versioning format (X.Y.Z)
- **Response Success when update available (200 OK):**
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
- **Response Success when no update available (204 No Content)**
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Current version parameter is required" }`
  - `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  - `500 Internal Server Error`: `{ "status": "error", "message": "Failed to check for agent updates" }`

#### Download Agent Package

Downloads a specific agent package version.

- **Method:** `GET`
- **Path:** `/api/agent/agent-packages/:filename`
- **Headers:**
  - `X-Agent-ID`: `string (agentId)` (Required)
  - `Authorization`: `Bearer <agent_token>` (Required - Token obtained from `/api/agent/identify` or `/api/agent/verify-mfa`)
- **Path Parameters:**
  - `filename`: String (required - name of the package file to download)
- **Response Success:** Package file content (application/octet-stream)
- **Response Error:**
  - `400 Bad Request`: `{ "status": "error", "message": "Invalid filename format" }`
  - `401 Unauthorized`: `{ "status": "error", "message": "Unauthorized (Invalid agent credentials)" }`
  - `404 Not Found`: `{ "status": "error", "message": "File not found" }`
  - `500 Internal Server Error`: `{ "status": "error", "message": "Error serving file" }`

## Agent WebSocket API

### Agent WebSocket Connection

Establishes a real-time WebSocket connection between the agent and the server using Socket.IO.

- **URL:** `/socket.io`
- **Headers:**
  - `X-Client-Type`: `"agent"` (Required)
  - `Authorization`: `Bearer <agent_token>` (Required - Token obtained from `/api/agent/identify` or `/api/agent/verify-mfa` endpoints)
  - `X-Agent-ID`: `string (agentId)` (Required)

### Agent Authentication

Verifies the agent's identity and permissions when establishing a WebSocket connection.

- **Note:** Authentication is handled entirely through WebSocket connection headers when establishing the connection.
- **Connection Authentication Result:**
  - On success: Agent receives the standard Socket.io `connect` event
  - On failure: Agent receives a `connect_error` event with an error message
  - Example error object:
    ```javascript
    {
      message: "Authentication failed: Invalid agent credentials";
    }
    ```
  - Possible `connect_error` Messages:
    ```json
    [
      { "message": "Authentication failed: Invalid agent credentials" },
      { "message": "Authentication failed: Missing required headers" },
      { "message": "Internal error: Unable to establish WebSocket connection" }
    ]
    ```

### Agent Events

#### Agent Status Update

Sends current resource usage statistics from the agent to the server.

- **Event:** `agent:status_update`
- **Data:**
  ```json
  {
    "cpuUsage": "number (percentage)",
    "ramUsage": "number (percentage)",
    "diskUsage": "number (percentage)"
  }
  ```
- **Field Constraints:**
  - `cpuUsage`: Number between 0.0 and 100.0 (percentage of CPU utilization)
  - `ramUsage`: Number between 0.0 and 100.0 (percentage of RAM utilization)
  - `diskUsage`: Number between 0.0 and 100.0 (percentage of disk space utilization)
- **No Direct Response**

#### Agent Command Result

Reports the result of a command execution back to the server.

- **Event:** `agent:command_result`
- **Data:**
  ```json
  {
    "commandId": "string (uuid)",
    "commandType": "string (default: 'console')",
    "success": "boolean",
    "result": {
      "stdout": "string",
      "stderr": "string",
      "exitCode": "integer"
    }
  }
  ```
- **Field Constraints:**
  - `commandId`: UUID string (RFC4122 compliant, 36 characters) uniquely identifying the command
  - `commandType`: String,
  - `success`: Boolean indicating whether the command executed successfully
  - `result`: Object containing command execution details:
    - `stdout`: String (command standard output, may be empty)
    - `stderr`: String (command error output, may be empty)
    - `exitCode`: Integer (command exit code, 0 typically means success)
- **No Direct Response**

---

# Server Broadcast Events

This section describes events that are initiated by the server and broadcast to agent clients. These events deliver instructions or notifications without requiring client requests.

## Execute Command

Event broadcast to agent clients instructing them to execute a specific command on the managed computer.

- **Event:** `command:execute`
- **Data:**
  ```json
  {
    "command": "string",
    "commandId": "string (uuid)",
    "commandType": "string (required, default: 'console')"
  }
  ```
- **Field Constraints:**
  - `command`: String, the actual command to execute (maximum 2000 characters)
  - `commandId`: UUID string (RFC4122 compliant, 36 characters) uniquely identifying the command
  - `commandType`: String
- **Example:**
  ```json
  {
    "command": "systeminfo",
    "commandId": "550e8400-e29b-41d4-a716-446655440000",
    "commandType": "cmd"
  }
  ```

## New Version Available Notification

Event broadcast to agent clients notifying them that a new version of the agent software is available for download and installation.

- **Event:** `agent:new_version_available`
- **Data:**
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
- **Field Constraints:**
  - `status`: Always "success" for this event
  - `update_available`: Always true for this event
  - `version`: String following semantic versioning format (X.Y.Z)
  - `download_url`: String, URL path to download the new version package
  - `checksum_sha256`: String (64 characters), SHA-256 hash of the package file for validation
  - `notes`: String, release notes for the new version (may be empty)
- **Example:**
  ```json
  {
    "status": "success",
    "update_available": true,
    "version": "1.2.0",
    "download_url": "/api/agent/agent-packages/agent-1.2.0.zip",
    "checksum_sha256": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
    "notes": "Fixed startup issues and improved performance"
  }
  ```