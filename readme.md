This project aims to create a web application for managing and monitoring computers in a company, organized by Room, with the ability to send commands (via WebSocket), monitor real-time status (online/offline determined via WS, %CPU/%RAM sent by Agent via HTTP), display visual positioning, user permission system (Admin, User), secure Agent registration mechanism (MFA/Token), computer error reporting and resolution features, and data filtering capabilities on list APIs. The Agent is developed in .NET to ensure high performance and good compatibility with Windows.

#

* **Agent (.NET):** Runs on Windows machines. Collects system information (OS, CPU, GPU, RAM, Disk, %CPU, %RAM). Communicates with the Backend primarily via HTTP(S) for registration, status reporting, and sending command results (authenticated by Agent Token). Maintains WebSocket connection to receive commands from Backend and report status changes. Runs as a Windows Service to ensure continuous operation. Automatically updates to newer versions through CMSUpdater. Stores data offline when connection is lost.
* **Backend (Node.js):** Processing center. Handles HTTP(S) requests from Agent and Frontend. Manages WebSocket connections with Agent (authentication, sending commands) and Frontend (sending MFA, broadcasting status, command results). Manages business logic, permissions, DB communication (storing errors in JSONB). Manages real-time status cache. Processes API filter parameters.
* **Frontend (React):** Web interface. Displays interface based on role. Shows MFA notifications (Admin). Displays real-time computer status and error information (received via WS). Sends HTTP(S) requests to Backend (login, management, error reporting, error resolution, command requests). Provides filters and sends filter parameters to APIs.
* **Database (Example: PostgreSQL):** Stores configuration data (users, rooms, computers, agent_token_hash, user assignments, errors JSONB).

#

* **Backend:** Node.js, Express.js, Socket.IO, PostgreSQL (or MongoDB), Sequelize (or Mongoose), JWT, bcrypt, node-cache (or Redis), otp-generator, crypto/uuid. Agent Token HTTP header authentication middleware. Query parameters processing library (e.g., built into Express).
* **Frontend:** React, Vite, Tailwind CSS, React Router DOM, Socket.IO Client, axios. Context API/Zustand/Redux. CSS Positioning/SVG/Canvas. Form management library (e.g., React Hook Form).
* **Agent:** .NET (9.0 LTS), Serilog, SocketIOClient.Net, System.Management, Microsoft.Extensions.Hosting, Polly, System.CommandLine. Secure token storage mechanism using Windows DPAPI. Inno Setup for creating installers. Windows Service for continuous operation.

#

* **`users` Table:**
    * `id`: SERIAL PRIMARY KEY
    * `username`: VARCHAR(255) UNIQUE NOT NULL
    * `password_hash`: VARCHAR(255) NOT NULL
    * `role`: VARCHAR(50) NOT NULL CHECK (role IN ('admin', 'user'))
    * `is_active`: BOOLEAN DEFAULT true
    * `created_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
    * `updated_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
* **`rooms` Table:**
    * `id`: SERIAL PRIMARY KEY
    * `name`: VARCHAR(255) NOT NULL
    * `description`: TEXT
    * `layout`: JSONB - Defines the structure and layout of the room in the format:
      ```json
      {
        "width": "integer - Room width in pixels",
        "height": "integer - Room height in pixels",
        "background": "string - Background color code (e.g., '#f5f5f5')",
        "grid": {
          "columns": "integer - Number of computers horizontally (X axis)",
          "rows": "integer - Number of computers vertically (Y axis)",
          "spacing_x": "integer - Horizontal spacing between computers (pixels)",
          "spacing_y": "integer - Vertical spacing between computers (pixels)"
        }
      }
      ```
      This structure allows for creating a grid with a maximum of `columns` × `rows` computers. Each computer will be positioned using the coordinates `pos_x` and `pos_y` in the `computers` table.
    * `created_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
    * `updated_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
* **`computers` Table:** (Updated)
    * `id`: SERIAL PRIMARY KEY
    * `name`: VARCHAR(255) (Initially NULL)
    * `room_id`: INTEGER REFERENCES rooms(id) ON DELETE SET NULL (Initially NULL)
    * `pos_x`: INTEGER DEFAULT 0
    * `pos_y`: INTEGER DEFAULT 0
    * `ip_address`: VARCHAR(50)
    * `unique_agent_id`: VARCHAR(255) UNIQUE NOT NULL
    * `agent_token_hash`: VARCHAR(255) (NULL until successful registration)
    * `last_update`: TIMESTAMPTZ
    * `os_info`: VARCHAR(255)
    * `total_ram`: BIGINT
    * `cpu_info`: VARCHAR(255)
    * `errors`: JSONB DEFAULT '[]'::jsonb (Stores an array of error objects. Example structure of an error object: `{ "id": "uuid", "type": "string", "description": "text", "reported_by": "integer (user_id)", "reported_at": "timestamp", "status": "string ('active'|'resolved')", "resolved_by": "integer (user_id, optional)", "resolved_at": "timestamp (optional)" }`)
    * `created_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
    * `updated_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
* **`user_room_assignments` Table:**
    * `user_id`: INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE
    * `room_id`: INTEGER NOT NULL REFERENCES rooms(id) ON DELETE CASCADE
    * PRIMARY KEY (user_id, room_id)

#

(This section maintains the detailed specifications as presented in the previous version)

##
* `POST /api/auth/login`
* `GET /api/auth/me`

##
* `POST /api/users`
* `GET /api/users` (Supports filtering: `?role=`, `?is_active=`, `?username=`)
* `PUT /api/users/:id`
* `DELETE /api/users/:id`

##
* `POST /api/rooms` (Admin Only)
* `GET /api/rooms` (Supports filtering: `?name=`, `?assigned_user_id=`)
* `GET /api/rooms/:id`
* `PUT /api/rooms/:id` (Admin Only)
* `DELETE /api/rooms/:id` (Admin Only)

##
* `POST /api/rooms/:roomId/assign`
* `DELETE /api/rooms/:roomId/unassign`
* `GET /api/rooms/:roomId/users`

##
* `GET /api/computers` (Supports filtering: `?room_id=`, `?name=`, `?status=`, `?has_errors=`, `?unique_agent_id=`)
* `PUT /api/computers/:id` (Admin updates details)
* `GET /api/computers/:id` (Response includes `errors`)
* `DELETE /api/computers/:id` (Admin deletes computer)
* `POST /api/computers/:id/command` (Send command execution request)
* `POST /api/computers/:id/errors` (Report error)
* `PUT /api/computers/:computerId/errors/:errorId/resolve` (Resolve error - Admin Only)

##
* `POST /api/agent/identify`
* `POST /api/agent/verify-mfa`
* `PUT /api/agent/status` (Body: `cpu`, `ram`)
* `POST /api/agent/command-result` (Body: `commandId`, `stdout`, `stderr`, `exitCode`)

#

## 
1.  **Agent:** Sends HTTP `POST /api/agent/identify` with body `{"unique_agent_id": "agent-uuid-123"}`.
2.  **Backend:**
    * Receives request, extracts `unique_agent_id`.
    * Queries DB: `SELECT id, agent_token_hash FROM computers WHERE unique_agent_id = 'agent-uuid-123'`.
    * **If record not found or `agent_token_hash` is NULL:** (New Agent)
        * Creates MFA code (e.g., 6 digits, using `otp-generator`): `mfaCode = '123456'`.
        * Creates expiration timestamp: `expiresAt = Date.now() + 5 * 60 * 1000` (5 minutes).
        * Temporarily stores in cache/map: `mfaStore['agent-uuid-123'] = { code: '123456', expires: expiresAt }`. (Requires cleanup mechanism for expired cache).
        * Gets list of online Admin `socket.id`s (from `socket.data.role`).
        * Sends WS event `admin:new_agent_mfa` to each Admin: `socket.emit('admin:new_agent_mfa', { unique_agent_id: 'agent-uuid-123', mfaCode: '123456' })`.
        * Returns HTTP Response (200 OK): `{"status": "mfa_required"}`.
    * **If record found and `agent_token_hash` is not NULL:** (Registered Agent)
        * Returns HTTP Response (200 OK): `{"status": "authentication_required"}`.
3.  **Agent:** Receives response.
    * If `status === 'mfa_required'`: Shows MFA input request to user.
4.  **(User):** Receives MFA code from Admin, enters it into Agent.
5.  **Agent:** Sends HTTP `POST /api/agent/verify-mfa` with body `{"unique_agent_id": "agent-uuid-123", "mfaCode": "user_entered_code"}`.
6.  **Backend:**
    * Receives request, extracts `unique_agent_id`, `mfaCode`.
    * Gets stored MFA info: `storedMfa = mfaStore['agent-uuid-123']`.
    * Checks:
        * Does `storedMfa` exist?
        * `Date.now() < storedMfa.expires?` (Is it still valid?)
        * `mfaCode === storedMfa.code?` (Does the code match?)
    * **If all valid:**
        * Removes MFA from cache: `delete mfaStore['agent-uuid-123']`.
        * Creates new Agent Token (secure random string): `plainToken = crypto.randomBytes(32).toString('hex')`.
        * Hashes token: `hashedToken = await bcrypt.hash(plainToken, 10)`.
        * Creates new record in DB: `INSERT INTO computers (unique_agent_id, agent_token_hash, ...) VALUES ('agent-uuid-123', hashedToken, ...)` or `UPDATE computers SET agent_token_hash = hashedToken WHERE unique_agent_id = 'agent-uuid-123'` (if record exists but has no hash). Gets new/existing `computerId`.
        * Sends WS event `admin:agent_registered` to all Admins: `{ unique_agent_id: 'agent-uuid-123', computerId: newComputerId }`.
        * Returns HTTP Response (200 OK): `{"agentToken": plainToken}`.
    * **If invalid:**
        * Returns HTTP Response (e.g., 401 Unauthorized): `{"message": "Invalid or expired MFA code"}`.
7.  **Agent:** Receives response.
    * If successful (has `agentToken`): Saves token to secure location (`tokenManager.save(agentToken)`). Proceeds to establish WebSocket connection.
    * If failed: Reports error to user.

##

* **HTTP Request (e.g., Status Update):**
    1.  **Agent:** Sends `PUT /api/agent/status` with headers `X-Agent-ID: agent-uuid-123`, `Authorization: Bearer <saved_agent_token>`, and body `{ cpu, ram }`.
    2.  **Backend (Middleware `authAgentToken`):**
        * Extracts `unique_agent_id` from `X-Agent-ID` and `token` from `Authorization`.
        * Checks existence of both headers.
        * Queries DB: `SELECT id, agent_token_hash FROM computers WHERE unique_agent_id = 'agent-uuid-123'`.
        * If record not found or `agent_token_hash` is NULL: Returns 401 Unauthorized error.
        * Compares token: `isValid = await bcrypt.compare(token, record.agent_token_hash)`.
        * If `isValid`: Attaches info to request: `req.computerId = record.id`, `req.unique_agent_id = 'agent-uuid-123'`. Calls `next()`.
        * If not `isValid`: Returns 401 Unauthorized error.
    3.  **Backend (Controller):** If middleware calls `next()`, processes the request (updates status).

* **WebSocket Connection:**
    1.  **Agent:** Connects to WS server. Immediately after connecting, sends event `agent:authenticate_ws` with payload `{ unique_agent_id: 'agent-uuid-123', agentToken: '<saved_agent_token>' }`.
    2.  **Backend (WS Handler/Middleware):**
        * Receives event `agent:authenticate_ws`.
        * Queries DB: `SELECT id, agent_token_hash FROM computers WHERE unique_agent_id = 'agent-uuid-123'`.
        * If not found or hash is NULL: Sends event `agent:ws_auth_failed`, disconnects.
        * Compares token: `isValid = await bcrypt.compare(agentToken, record.agent_token_hash)`.
        * If `isValid`:
            * Stores info in socket: `socket.data.computerId = record.id`, `socket.data.unique_agent_id = 'agent-uuid-123'`, `socket.data.isAuthenticated = true`.
            * Saves `socket.id` to map: `agentCommandSockets[record.id] = socket.id`.
            * Sends event `agent:ws_auth_success`.
            * (Triggers Online Status Update Flow)
        * If not `isValid`: Sends event `agent:ws_auth_failed`, disconnects.

##
1.  **Agent:** Periodically (30s), uses `psutil` to get `cpu = psutil.cpu_percent()`, `ram = psutil.virtual_memory().percent`.
2.  **Agent:** Sends HTTP `PUT /api/agent/status` with authentication headers and body `{"cpu": cpu, "ram": ram}`.
3.  **Backend (HTTP Controller):**
    * Middleware `authAgentToken` successfully authenticates, `req.computerId` is available.
    * Gets `cpu`, `ram` from `req.body`.
    * Updates cache: `agentRealtimeStatus[req.computerId] = { ...agentRealtimeStatus[req.computerId], cpu: cpu, ram: ram }`.
    * Updates DB: `UPDATE computers SET last_update = NOW() WHERE id = req.computerId`.
    * Returns HTTP Response (204 No Content).
4.  **Backend (Logic after HTTP Response or using event emitter):**
    * Gets current online status: `isOnline = !!agentCommandSockets[req.computerId]`.
    * Gets computer's `roomId` from DB (or cache if available).
    * Gets complete data from cache: `currentStatus = agentRealtimeStatus[req.computerId]`.
    * Creates payload: `{ computerId: req.computerId, status: isOnline ? 'online' : 'offline', cpu: currentStatus.cpu, ram: currentStatus.ram }`.
    * Broadcasts via WS to Frontend room: `io.to('room_' + roomId).emit('computer:status_updated', payload)`.

##

* **Online (When WS authentication succeeds):**
    1.  **Backend (WS Handler):** After successful `bcrypt.compare` in `agent:authenticate_ws`:
        * Gets `computerId`, `roomId`.
        * Gets last CPU/RAM from cache (if available): `lastCpu = agentRealtimeStatus[computerId]?.cpu`, `lastRam = agentRealtimeStatus[computerId]?.ram`.
        * Updates cache: `agentRealtimeStatus[computerId] = { status: 'online', cpu: lastCpu, ram: lastRam }`.
        * Saves `socket.id`: `agentCommandSockets[computerId] = socket.id`.
        * Broadcasts WS: `io.to('room_' + roomId).emit('computer:status_updated', { computerId: computerId, status: 'online', cpu: lastCpu, ram: lastRam })`.

* **Offline (When WS connection is lost):**
    1.  **Backend (WS disconnect Handler):**
        * Finds `computerId` corresponding to the disconnected `socket.id` (iterates through `agentCommandSockets`).
        * If `computerId` is found:
            * Removes from map: `delete agentCommandSockets[computerId]`.
            * Gets last CPU/RAM from cache: `lastCpu = agentRealtimeStatus[computerId]?.cpu`, `lastRam = agentRealtimeStatus[computerId]?.ram`.
            * Updates cache: `agentRealtimeStatus[computerId] = { status: 'offline', cpu: lastCpu, ram: lastRam }`.
            * Gets `roomId`.
            * Broadcasts WS: `io.to('room_' + roomId).emit('computer:status_updated', { computerId: computerId, status: 'offline', cpu: lastCpu, ram: lastRam })`.
            * (Optional) Updates `status_db` in DB.

##
1.  **Frontend:** User enters command, clicks send -> Calls `POST /api/computers/:id/command` with body `{"command": "user_command"}` and JWT header.
2.  **Backend (HTTP Controller):**
    * Authenticates JWT, checks access rights to room for computer `:id`.
    * Creates `commandId = uuid.v4()`.
    * Gets `userId` from JWT.
    * Temporarily stores: `pendingCommands[commandId] = { userId: userId, computerId: computerId }` (Uses cache/Redis with TTL).
    * Finds `socketId = agentCommandSockets[computerId]`.
    * **If `socketId` exists:**
        * Sends WS event to Agent: `io.to(socketId).emit('command:execute', { command: "user_command", commandId: commandId })`.
        * Returns HTTP Response (202 Accepted): `{"message": "Command sent", "commandId": commandId}`.
    * **If `socketId` doesn't exist (Agent offline WS):**
        * Returns HTTP Response (e.g., 503 Service Unavailable): `{"message": "Agent is offline"}`.
3.  **Agent (WS Handler):**
    * Receives event `command:execute` with `{ command, commandId }`.
    * Executes command: `result = subprocess.run(...)`.
    * Gets `stdout`, `stderr`, `exitCode` from `result`.
4.  **Agent (HTTP Client):**
    * Sends HTTP `POST /api/agent/command-result` with Agent Token authentication headers and body `{"commandId": commandId, "stdout": ..., "stderr": ..., "exitCode": ...}`.
5.  **Backend (HTTP Controller):**
    * Authenticates Agent Token.
    * Gets `commandId` and results from `req.body`.
    * Finds pending command info: `commandInfo = pendingCommands[commandId]`.
    * **If `commandInfo` found:**
        * Gets `userId = commandInfo.userId`, `computerId = commandInfo.computerId`.
        * Removes from temporary storage: `delete pendingCommands[commandId]`.
        * Sends WS event to User: `io.to('user_' + userId).emit('command:completed', { commandId: commandId, computerId: computerId, result: { stdout: ..., stderr: ..., exitCode: ... } })`.
        * Returns HTTP Response (204 No Content) to Agent.
    * **If `commandInfo` not found (error or already processed):**
        * Returns HTTP Response (e.g., 404 Not Found) to Agent.

##
1.  **Frontend:** User enters `type`/`description` -> Calls `POST /api/computers/:id/errors` with body `{ type, description }` and JWT header.
2.  **Backend (HTTP Controller):**
    * Authenticates JWT, checks room access permission.
    * Gets `userId` from JWT.
    * Creates `errorId = uuid.v4()`.
    * Creates error object: `newError = { id: errorId, type, description, reported_by: userId, reported_at: new Date(), status: 'active' }`.
    * Updates DB (PostgreSQL example): `UPDATE computers SET errors = errors || jsonb_build_object('id', errorId, ...) WHERE id = computerId`. (Requires exact syntax to append to JSONB array).
    * Returns HTTP Response (201 Created) with `newError`.

##
1.  **Frontend (Admin):** Clicks Resolve button for error `errorId` on computer `computerId` -> Calls `PUT /api/computers/:computerId/errors/:errorId/resolve` with Admin JWT header.
2.  **Backend (HTTP Controller):**
    * Authenticates JWT, checks Admin permission.
    * Gets `adminId` from JWT.
    * Reads `errors` array from computer `:computerId`.
    * Finds `index` of error object with `id === errorId` and `status === 'active'` in the array.
    * **If found at `index`:**
        * Creates updated error object: `updatedError = { ...errors[index], status: 'resolved', resolved_by: adminId, resolved_at: new Date() }`.
        * Updates array in DB (PostgreSQL example): `UPDATE computers SET errors = jsonb_set(errors, '{index}', to_jsonb(updatedError)) WHERE id = computerId`. (Requires exact syntax to update element in JSONB array).
        * Returns HTTP Response (200 OK) with `updatedError`.
    * **If not found:** Returns 404 Not Found error.

#

* **Node.js Project Setup:** Use Express.js, install required libraries (Sequelize/Mongoose, Socket.IO, bcrypt, JWT, node-cache/Redis client, otp-generator, ...). Directory structure (routes, controllers, models, services, middleware, config...).
* **Database Connection:** Configure and initialize connection to PostgreSQL/MongoDB. Define Models/Schemas corresponding to DB design.
* **Authentication & Authorization Implementation:** Login API, create JWT. JWT authentication middleware for Frontend. Admin permission check middleware. Room access permission check middleware. Agent Token authentication middleware for Agent APIs.
* **API Routes Implementation:** Create routers/controllers for functional groups (auth, users, rooms, computers, agent, errors). Apply appropriate middleware. Write processing logic in controllers. Implement filtering logic in GET list APIs.
* **WebSocket Implementation (Socket.IO):** Integrate server. WS authentication middleware (JWT Frontend, Agent Token Agent). Manage connections, rooms (user, room, admin). Manage `agentCommandSockets` map. Manage `agentRealtimeStatus` cache. Handle/Emit WS events (authenticate, disconnect, subscribe, status update, command, MFA, registration).
* **Business Logic Implementation:** Handle MFA registration. Handle CPU/RAM status updates from HTTP and WS broadcast. Handle command sending/receiving (HTTP request -> WS send -> Agent execute -> HTTP result -> WS notify). Handle error reporting (create object, update JSONB). Handle error resolution (find, update JSONB).
* **Logging and Error Handling:** Integrate logging system (e.g., Winston), build centralized error handling mechanism.

#

* **React Project Setup:** Use Vite, install React Router DOM, Axios, Socket.IO Client, Tailwind CSS.
* **Directory Structure:** Organize components, pages, services, contexts, hooks, assets...
* **Routing:** Set up routes (login, dashboard, rooms, room detail, admin pages...). Use ProtectedRoute.
* **Authentication:** Login page, call login API. Store JWT. Auth Context. Send JWT header. Handle logout.
* **Backend Communication:** Use Axios/fetch for HTTP API calls. Set up Socket.IO connection, WS authentication. Socket Context.
* **State Management:** Context API (Auth, Socket), useState/useReducer or Zustand/Redux.
* **Build Components & Pages:** Create reusable UI components. Build pages. RoomDetailPage/ComputerCard/ComputerIcon component showing error indicators. Computer detail page displaying error list. Error Reporting Form component. Error Resolution feature (Admin). Add Filters to list pages.
* **Handle WebSocket Events:** Listen for `computer:status_updated` (update online/offline, CPU/RAM, error indicators if needed). Listen for `command:completed`. Admin listens for `admin:new_agent_mfa`, `admin:agent_registered`.
* **Styling:** Use Tailwind CSS.

#

* **.NET Project Setup:** Use .NET 9.0 LTS, create console or Windows Service project. Install required libraries (`Serilog`, `SocketIOClient.Net`, `System.Management`, `Microsoft.Extensions.Hosting`, `Polly`, `System.CommandLine`).
* **Startup & Configuration:** Read configuration (server address). Create/Read `unique_agent_id`. Check stored Agent Token.
* **Initial Registration / Authentication Flow (HTTP):** Use `HttpClient` to call `/api/agent/identify`. Process response. If `mfa_required`, use console to get code, call `/api/agent/verify-mfa`. If successful, store received token using Windows DPAPI.
* **WebSocket Connection and Authentication:** Use `SocketIOClient.Net`. After connecting, send event `agent:authenticate_ws` containing `unique_agent_id` and `agentToken`. Process authentication response from server. Run WS message receiving flow in a separate thread.
* **Send Periodic Status (HTTP):** Use `System.Threading.Timer` for repetition. Use `System.Management` to get system information. Send `HttpClient.PutAsync` to `/api/agent/status` with authentication headers.
* **Listen for and Execute Commands (WebSocket):** In WS listening thread, when receiving `command:execute` event, get `command` and `commandId`. Use `System.Diagnostics.Process` to execute. Collect `stdout`, `stderr`, `exitCode`.
* **Send Command Results (HTTP):** After command completion, call `HttpClient.PostAsync` to `/api/agent/command-result` with results and authentication headers.
* **Secure Token Storage:** Use Windows DPAPI to store token.
* **Error Handling and Reconnection:** Use `try...catch` to catch network errors (HTTP, WS), command execution errors. Implement WS reconnection logic with backoff mechanism. Use `Serilog` for logging.
* **Packaging:** Use Inno Setup to create installer. Register Windows Service to ensure continuous operation.

This plan provides a detailed roadmap through each development phase. The order and priority of features can be adjusted based on actual requirements.
