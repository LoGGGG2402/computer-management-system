# Computer Management System - Flow Diagrams

<style>
.mermaid {
    display: flex;
    justify-content: center;
    margin: 20px 0;
}
</style>

## 1. User Authentication Flow (Frontend Login)

```mermaid
%%{init: {
    'theme': 'base',
    'themeVariables': {
        'primaryColor': '#4a90e2',
        'primaryTextColor': '#ffffff',
        'primaryBorderColor': '#2171c7',
        'secondaryColor': '#82b1ff',
        'tertiaryColor': '#b6e3ff',
        'noteTextColor': '#333',
        'noteBkgColor': '#fff9c4',
        'noteBorderColor': '#f9a825',
        'actorBkg': '#e3f2fd',
        'actorBorder': '#2171c7',
        'sequenceNumberColor': '#333'
    },
    'fontFamily': 'Arial',
    'fontSize': '14px'
}}%%

sequenceDiagram
    actor FrontendUser as 👤 Frontend User
    participant FE as 💻 Frontend UI
    participant BE_AuthRoutes as 🔒 Auth Routes
    participant AuthMiddleware as 🛡️ Auth Middleware
    participant AuthController as 🎮 Auth Controller
    participant AuthService as ⚙️ Auth Service
    participant UserDB as 📊 User Database
    participant RefreshTokenDB as 🔑 Token Database

    FrontendUser->>FE: Enter Username & Password
    Note over FE: Validate input format
    
    FE->>BE_AuthRoutes: POST /api/auth/login
    Note over BE_AuthRoutes: {username, password}
    
    BE_AuthRoutes->>AuthController: handleLogin(req, res)
    AuthController->>AuthService: login(username, password)
    
    AuthService->>UserDB: Find user (username, is_active=true)
    UserDB-->>AuthService: Return user data
    
    Note over AuthService: Compare password hash<br/>Generate tokens
    AuthService->>RefreshTokenDB: Store refresh token
    RefreshTokenDB-->>AuthService: Token stored
    
    AuthService-->>AuthController: Return user & tokens
    AuthController-->>BE_AuthRoutes: Response 200 OK
    BE_AuthRoutes-->>FE: {user, accessToken}
    
    Note over FE: Store tokens & user data
    FE-->>FrontendUser: Login successful
```

## 1.1. Refresh Token Flow (Access Token Renewal)

```mermaid
sequenceDiagram
    actor FrontendUser as Frontend User
    participant FE as Frontend UI
    participant APIInterceptor as API Interceptor (axios)
    participant BE_AuthRoutes as Backend Auth Routes (/api/auth)
    participant AuthController as Auth Controller
    participant AuthService as Auth Service
    participant RefreshTokenDB as Refresh Token Database

    %% Access token expired scenario
    FE->>BE_AuthRoutes: API Request with expired access token
    BE_AuthRoutes-->>FE: 401 Unauthorized
    
    %% Automatic token refresh
    FE->>APIInterceptor: Catch 401 error
    APIInterceptor->>BE_AuthRoutes: POST /api/auth/refresh-token
    note right of APIInterceptor: HttpOnly refreshToken cookie automatically included
    BE_AuthRoutes->>AuthController: handleRefreshToken(req, res)
    AuthController->>AuthService: refreshToken(refreshToken from cookie)
    AuthService->>RefreshTokenDB: Find and validate token
    
    alt Valid Refresh Token
        RefreshTokenDB-->>AuthService: Token is valid
        AuthService->>RefreshTokenDB: Delete used token (token rotation)
        AuthService->>AuthService: Create new Access Token
        AuthService->>AuthService: Create new Refresh Token
        AuthService->>RefreshTokenDB: Store new hashed refresh token
        RefreshTokenDB-->>AuthService: Confirm new token stored
        AuthService-->>AuthController: Return {accessToken, refreshToken}
        AuthController-->>BE_AuthRoutes: Response 200 OK (accessToken + set new refreshToken cookie)
        BE_AuthRoutes-->>APIInterceptor: New access token + Set HttpOnly cookie with new refreshToken
        APIInterceptor->>APIInterceptor: Queue and retry failed request with new token
        APIInterceptor-->>FE: Continue with original request using new token
        FE-->>FrontendUser: Seamless experience (no visible interruption)
    else Expired or Invalid Refresh Token
        RefreshTokenDB-->>AuthService: Token invalid or not found
        AuthService-->>AuthController: Throw "Invalid refresh token"
        AuthController-->>BE_AuthRoutes: Response 401 Unauthorized
        BE_AuthRoutes-->>APIInterceptor: Authentication error
        APIInterceptor-->>FE: Redirect to login page
        FE-->>FrontendUser: Prompt to login again
    else Refresh Token Reuse Detected (Security Event)
        RefreshTokenDB-->>AuthService: Token already used (potential security breach)
        AuthService->>RefreshTokenDB: Invalidate all refresh tokens for user
        AuthService-->>AuthController: Throw "Token reuse detected"
        AuthController-->>BE_AuthRoutes: Response 403 Forbidden
        BE_AuthRoutes-->>APIInterceptor: Security error
        APIInterceptor-->>FE: Force logout + Clear tokens
        FE-->>FrontendUser: Session terminated, prompt to login again
    end
```

## 1.2. Logout Flow (Token Invalidation)

```mermaid
sequenceDiagram
    actor FrontendUser as Frontend User
    participant FE as Frontend UI
    participant BE_AuthRoutes as Backend Auth Routes (/api/auth)
    participant AuthController as Auth Controller
    participant AuthService as Auth Service
    participant RefreshTokenDB as Refresh Token Database

    FrontendUser->>FE: Click Logout
    FE->>BE_AuthRoutes: POST /api/auth/logout
    note right of FE: HttpOnly refreshToken cookie automatically included
    BE_AuthRoutes->>AuthController: handleLogout(req, res)
    AuthController->>AuthService: logout(refreshToken from cookie)
    AuthService->>RefreshTokenDB: Find and remove token
    RefreshTokenDB-->>AuthService: Token removed
    AuthService-->>AuthController: Logout successful
    AuthController-->>BE_AuthRoutes: Response 200 OK + Clear refreshToken cookie
    BE_AuthRoutes-->>FE: Logout confirmed + Cookie cleared
    FE->>FE: Clear access token from memory
    FE->>FE: Redirect to login page
    FE-->>FrontendUser: Logout successful
```

## 2. New Agent Registration Flow (Identify & MFA Verify)

```mermaid
%%{init: {
    'theme': 'base',
    'themeVariables': {
        'primaryColor': '#4a90e2',
        'primaryTextColor': '#ffffff',
        'primaryBorderColor': '#2171c7',
        'secondaryColor': '#82b1ff',
        'tertiaryColor': '#b6e3ff',
        'noteTextColor': '#333',
        'noteBkgColor': '#fff9c4',
        'noteBorderColor': '#f9a825',
        'actorBkg': '#e3f2fd',
        'actorBorder': '#2171c7'
    },
    'fontFamily': 'Arial',
    'fontSize': '14px'
}}%%

sequenceDiagram
    participant AgentApp as 🤖 Agent App
    participant BE_AgentRoutes as 📡 Agent Routes
    participant AgentController as 🎮 Agent Controller
    participant ComputerService as 💻 Computer Service
    participant RoomService as 🏢 Room Service
    participant MfaService as 🔐 MFA Service
    participant WebSocketService as ⚡ WebSocket Service
    participant AdminFE as 👤 Admin Frontend
    participant ComputerDB as 💾 Computer DB
    participant RoomDB as 🏗️ Room DB
    participant MFACache as 🔒 MFA Cache

    AgentApp->>BE_AgentRoutes: POST /api/agent/identify
    Note over BE_AgentRoutes: {agentId, roomName, posX, posY}
    
    BE_AgentRoutes->>AgentController: handleIdentifyRequest
    AgentController->>ComputerService: findComputerByAgentId
    ComputerService->>ComputerDB: Query computer
    
    alt Computer exists with token
        ComputerDB-->>ComputerService: Return computer
        ComputerService-->>AgentController: Return computer
        AgentController-->>BE_AgentRoutes: 200 OK {status: "success"}
        BE_AgentRoutes-->>AgentApp: Can use existing token
    else New computer registration
        ComputerService->>RoomService: validateRoomPosition
        RoomService->>RoomDB: Check room & position
        RoomDB-->>RoomService: Position valid
        
        AgentController->>MfaService: generateMFACode
        MfaService->>MFACache: Store code temporarily
        
        AgentController->>WebSocketService: notifyAdminsNewAgent
        WebSocketService->>AdminFE: Display MFA code
        
        AgentController-->>BE_AgentRoutes: 200 OK {status: "mfa_required"}
        BE_AgentRoutes-->>AgentApp: Prompt for MFA code
    end
```

## 2.1. Agent Hardware Information Flow (HTTP)

```mermaid
sequenceDiagram
    participant AgentApp as Agent Application
    participant BE_AgentRoutes as Backend Agent Routes (/api/agent)
    participant AgentAuthMiddleware as Agent Auth Middleware (authAgentToken.js)
    participant AgentController as Agent Controller
    participant ComputerService as Computer Service
    participant ComputerDB as Computer Database

    AgentApp->>BE_AgentRoutes: POST /api/agent/hardware-info (hardwareData: {os_info, cpu_info, ...})
    note right of AgentApp: Sent with headers 'X-Agent-ID' and 'Authorization: Bearer <agent_token>'
    BE_AgentRoutes->>AgentAuthMiddleware: verifyAgentToken(req, res, next)
    AgentAuthMiddleware->>ComputerService: verifyAgentToken(agentId, token)
    ComputerService->>ComputerDB: Find computer by agentId, compare token hash
    alt Valid token
        ComputerDB-->>ComputerService: Return computer.id
        ComputerService-->>AgentAuthMiddleware: computer.id
        AgentAuthMiddleware->>BE_AgentRoutes: next() (attach req.computerId, req.agentId)
        BE_AgentRoutes->>AgentController: handleHardwareInfo(req, res)
        AgentController->>ComputerService: updateComputer(req.computerId, hardwareData)
        ComputerService->>ComputerDB: Update hardware info for computerId
        ComputerDB-->>ComputerService: Success
        ComputerService-->>AgentController: Success
        AgentController-->>BE_AgentRoutes: Response 204 No Content
        BE_AgentRoutes-->>AgentApp: Confirmation of success
    else Invalid token
        ComputerService-->>AgentAuthMiddleware: null
        AgentAuthMiddleware-->>BE_AgentRoutes: Response 401 Unauthorized
        BE_AgentRoutes-->>AgentApp: Authentication error
    end
```

## 2.2. Agent Error Reporting Flow (HTTP)

```mermaid
sequenceDiagram
    participant AgentApp as Agent Application
    participant BE_AgentRoutes as Backend Agent Routes (/api/agent)
    participant AgentAuthMiddleware as Agent Auth Middleware
    participant AgentController as Agent Controller
    participant ComputerService as Computer Service
    participant ComputerDB as Computer Database

    AgentApp->>BE_AgentRoutes: POST /api/agent/report-error (errorData: {type, message, details})
    note right of AgentApp: Sent with headers 'X-Agent-ID' and 'Authorization: Bearer <agent_token>'
    BE_AgentRoutes->>AgentAuthMiddleware: verifyAgentToken(req, res, next)
    critical Agent Token Authentication Flow (As in section 2.1)
        AgentAuthMiddleware->>BE_AgentRoutes: next() (attach req.computerId, req.agentId)
    end
    BE_AgentRoutes->>AgentController: handleErrorReport(req, res)
    AgentController->>ComputerService: reportComputerError(req.computerId, errorData)
    ComputerService->>ComputerDB: Find computer, add error to 'errors' array (JSONB), update 'have_active_errors'
    ComputerDB-->>ComputerService: {error: newError, computerId}
    ComputerService-->>AgentController: {error: newError, computerId}
    AgentController-->>BE_AgentRoutes: Response 204 No Content
    BE_AgentRoutes-->>AgentApp: Confirmation of success
```

## 3. WebSocket Connection and Authentication Flow

```mermaid
%%{init: {
    'theme': 'base',
    'themeVariables': {
        'primaryColor': '#4a90e2',
        'primaryTextColor': '#ffffff',
        'primaryBorderColor': '#2171c7',
        'secondaryColor': '#82b1ff',
        'tertiaryColor': '#b6e3ff',
        'noteTextColor': '#333',
        'noteBkgColor': '#fff9c4',
        'noteBorderColor': '#f9a825',
        'actorBkg': '#e3f2fd',
        'actorBorder': '#2171c7'
    },
    'fontFamily': 'Arial',
    'fontSize': '14px'
}}%%

sequenceDiagram
    participant Client as 👥 Client
    participant BE_SocketIO as ⚡ Socket.IO Server
    participant SocketMiddleware as 🛡️ Socket Middleware
    participant FrontendHandler as 💻 Frontend Handler
    participant AgentHandler as 🤖 Agent Handler
    participant WebSocketService as 🔌 WebSocket Service

    Client->>BE_SocketIO: Connect WebSocket
    Note over Client,BE_SocketIO: Headers: X-Client-Type, Authorization, X-Agent-ID

    BE_SocketIO->>SocketMiddleware: Authenticate connection
    Note over SocketMiddleware: Extract & validate token
    
    alt Valid Frontend Client
        SocketMiddleware->>FrontendHandler: Setup handlers
        FrontendHandler->>WebSocketService: Join user room
        Note over WebSocketService: Add to user_[userId]
        
        alt Is Admin
            FrontendHandler->>WebSocketService: Join admin room
            Note over WebSocketService: Add to admin_room
        end
        
        FrontendHandler-->>Client: Connection accepted
        
    else Valid Agent Client
        SocketMiddleware->>AgentHandler: Setup handlers
        AgentHandler->>WebSocketService: Join agent room
        Note over WebSocketService: Add to agent_[computerId]
        AgentHandler-->>Client: Connection accepted
        
    else Invalid Client
        SocketMiddleware-->>Client: Connection rejected
    end
```

## 3.1. Frontend Sending Commands to Agent Flow (via WebSocket)

```mermaid
sequenceDiagram
    actor User as Frontend User
    participant FE as Frontend UI
    participant BE_SocketIO as Backend Socket.IO Server
    participant FrontendHandler as Frontend WS Handler
    participant WebSocketService as WebSocket Service
    participant ComputerService as Computer Service
    participant AgentApp as Agent Application (on computer)
    participant AgentHandler as Agent WS Handler

    User->>FE: Perform command sending action (e.g., restart PC for computerId X)
    FE->>BE_SocketIO: Emit 'frontend:send_command' ({computerId, command, commandType}), ack_callback
    note right of FE: Socket is authenticated, user has userId, role in socket.data
    BE_SocketIO->>FrontendHandler: handleCommandSend(socket, data, ack)
    FrontendHandler->>ComputerService: checkUserComputerAccess(socket.data.userId, data.computerId) (if user is not admin)
    alt User has no access to computerId
        ComputerService-->>FrontendHandler: false
        FrontendHandler-->>FE: ack_callback({status: 'error', message: 'Access denied...'})
    else User has access
        ComputerService-->>FrontendHandler: true
        FrontendHandler->>WebSocketService: storePendingCommand(commandId, socket.data.userId, data.computerId)
        FrontendHandler->>WebSocketService: sendCommandToAgent(data.computerId, data.command, commandId, data.commandType)
        alt Agent (computerId) is connected to WebSocket
            WebSocketService->>AgentApp: Emit 'command:execute' ({commandId, command, commandType}) to room agent_<computerId>
            WebSocketService-->>FrontendHandler: true (command sent successfully to agent room)
            FrontendHandler-->>FE: ack_callback({status: 'success', commandId, commandType})
            FE-->>User: Notify command has been sent
            AgentApp->>AgentApp: Execute command (e.g., run script, restart)
            AgentApp->>BE_SocketIO: Emit 'agent:command_result' ({commandId, type, success, result})
            BE_SocketIO->>AgentHandler: handleAgentCommandResult(socket, data)
            AgentHandler->>WebSocketService: notifyCommandCompletion(commandId, data.result)
            WebSocketService->>FE: Emit 'command:completed' ({commandId, computerId, type, success, result}) to room user_<userId_who_sent_command>
            FE-->>User: Display command result
        else Agent (computerId) not connected to WebSocket
            WebSocketService-->>FrontendHandler: false (agent not connected)
            FrontendHandler->>WebSocketService: Delete pending command (if created)
            FrontendHandler-->>FE: ack_callback({status: 'error', message: 'Agent is not connected', commandId, commandType})
            FE-->>User: Notify Agent not connected
        end
    end
```

## 3.2. Frontend Subscribe/Unsubscribe to Computer Monitoring Flow

```mermaid
sequenceDiagram
    participant FE as Frontend UI
    participant BE_SocketIO as Backend Socket.IO Server
    participant FrontendHandler as Frontend WS Handler
    participant WebSocketService as WebSocket Service
    participant ComputerService as Computer Service

    FE->>BE_SocketIO: Emit 'frontend:subscribe' ({computerId})
    BE_SocketIO->>FrontendHandler: handleComputerSubscription(socket, data)
    FrontendHandler->>ComputerService: checkUserComputerAccess(socket.data.userId, data.computerId) (if user is not admin)
    alt User has no access or computerId is invalid
        FrontendHandler->>FE: Emit 'subscribe_response' ({status: 'error', message, computerId})
    else User has access
        FrontendHandler->>WebSocketService: joinComputerRoom(socket, data.computerId) (socket join room computer_<computerId>_subscribers)
        FrontendHandler->>FE: Emit 'subscribe_response' ({status: 'success', computerId})
        FrontendHandler->>WebSocketService: getAgentRealtimeStatus(data.computerId)
        WebSocketService-->>FrontendHandler: currentStatus (if available)
        FrontendHandler->>FE: Emit 'computer:status_updated' (currentStatus) (send current status right after subscription)
    end

    FE->>BE_SocketIO: Emit 'frontend:unsubscribe' ({computerId})
    BE_SocketIO->>FrontendHandler: handleComputerUnsubscription(socket, data)
    FrontendHandler->>WebSocketService: (socket leave room computer_<computerId>_subscribers)
    FrontendHandler->>FE: Emit 'unsubscribe_response' ({status: 'success', computerId})
```

## 4. Agent Status Update Flow (WebSocket)

```mermaid
%%{init: {
    'theme': 'base',
    'themeVariables': {
        'primaryColor': '#4a90e2',
        'primaryTextColor': '#ffffff',
        'primaryBorderColor': '#2171c7',
        'secondaryColor': '#82b1ff',
        'tertiaryColor': '#b6e3ff',
        'noteTextColor': '#333',
        'noteBkgColor': '#fff9c4',
        'noteBorderColor': '#f9a825',
        'actorBkg': '#e3f2fd',
        'actorBorder': '#2171c7'
    },
    'fontFamily': 'Arial',
    'fontSize': '14px'
}}%%

sequenceDiagram
    participant AgentApp as 🤖 Agent
    participant BE_SocketIO as ⚡ Socket.IO
    participant AgentHandler as 🎮 Agent Handler
    participant WebSocketService as 🔌 WS Service
    participant FE_Subscribers as 👥 Frontend UI

    loop Every 30 seconds
        Note over AgentApp: Collect system metrics<br/>(CPU, RAM, Disk)
        
        AgentApp->>BE_SocketIO: agent:status_update
        Note over BE_SocketIO: {cpuUsage, ramUsage, diskUsage}
        
        BE_SocketIO->>AgentHandler: Handle status update
        AgentHandler->>WebSocketService: Update cache
        Note over WebSocketService: Store in agentRealtimeStatus
        
        WebSocketService->>FE_Subscribers: Broadcast update
        Note over FE_Subscribers: Update UI with new metrics
    end

    Note over AgentApp,FE_Subscribers: Disconnection Handling
    AgentApp--xBE_SocketIO: Connection lost
    BE_SocketIO->>AgentHandler: Handle disconnect
    
    Note over AgentHandler: Wait for reconnect<br/>(30 second timeout)
    
    alt No reconnect
        AgentHandler->>WebSocketService: Mark offline
        WebSocketService->>FE_Subscribers: Broadcast offline status
    else Reconnected
        Note over WebSocketService: Keep online status
    end
```

## 4.1. Agent WebSocket Disconnection Flow

```mermaid
sequenceDiagram
    participant AgentApp as Agent Application
    participant BE_SocketIO as Backend Socket.IO Server
    participant SocketIndex as sockets/index.js
    participant AgentHandler as Agent WS Handler
    participant WebSocketService as WebSocket Service
    participant FE_Subscribers as Frontend UI (Subscribers)

    AgentApp--xBE_SocketIO: WebSocket disconnection
    BE_SocketIO->>SocketIndex: Handle 'disconnect' event for agent socket
    SocketIndex->>AgentHandler: handleAgentDisconnect(socket)
    AgentHandler->>WebSocketService: handleAgentDisconnect(socket)
    WebSocketService->>WebSocketService: setTimeout(..., AGENT_OFFLINE_CHECK_DELAY_MS)
    note right of WebSocketService: Wait for a delay to see if agent reconnects
    
    alt After AGENT_OFFLINE_CHECK_DELAY_MS
        WebSocketService->>WebSocketService: isAgentConnected(computerId) (check socket count in room agent_<computerId>)
        alt Agent has no other connections (isAgentConnected = false)
            WebSocketService->>WebSocketService: agentRealtimeStatus.delete(computerId)
            WebSocketService->>FE_Subscribers: Emit 'computer:status_updated' ({computerId, status:'offline', ...}) to room computer_<computerId>_subscribers
        else Agent still has other connections (e.g., quick reconnect)
            WebSocketService->>WebSocketService: Do nothing, agent still considered online
        end
    end
```

## 5. Admin Agent Version Management Flow

```mermaid
sequenceDiagram
    actor AdminUser as Admin
    participant AdminFE as Admin Frontend
    participant BE_AdminRoutes as Backend Admin Routes (/api/admin/agents/versions)
    participant AuthMiddleware as Auth Middleware (authUser.js + authAccess.js)
    participant UploadMiddleware as Upload File Middleware (uploadAgentPackage)
    participant AdminController as Admin Controller
    participant AdminService as Admin Service
    participant AgentVersionDB as AgentVersion Database
    participant FileSystem as Server File System
    participant WebSocketService as WebSocket Service
    participant AllConnectedAgents as All Connected Agents (via WebSocket)

    %% Upload new version
    AdminUser->>AdminFE: Select agent file, enter version, notes, (optional: client_checksum)
    AdminFE->>BE_AdminRoutes: POST /versions (multipart/form-data: {package, version, notes, checksum})
    BE_AdminRoutes->>AuthMiddleware: verifyToken & authAccess({requiredRole: 'admin'})
    AuthMiddleware->>BE_AdminRoutes: next()
    BE_AdminRoutes->>UploadMiddleware: uploadAgentPackage(req, res, next)
    UploadMiddleware->>FileSystem: Save file to uploads/agent-packages/ (filename may contain version, timestamp)
    UploadMiddleware-->>BE_AdminRoutes: next() (attach req.file)
    BE_AdminRoutes->>AdminController: handleAgentUpload(req, res)
    AdminController->>AdminService: processAgentUpload(req.file, {version, notes, client_checksum})
    AdminService->>FileSystem: Read req.file.path to calculate serverChecksum (SHA256)
    alt Client checksum provided AND doesn't match serverChecksum
        AdminService->>FileSystem: Delete uploaded file (req.file.path)
        AdminService-->>AdminController: Throw error "File integrity check failed"
        AdminController-->>BE_AdminRoutes: Response 400 Bad Request
    else Checksum OK or not provided
        AdminService->>AgentVersionDB: Create AgentVersion record (version, serverChecksum, download_url (based on filename), notes, req.file.path, req.file.size, is_stable=false)
        AgentVersionDB-->>AdminService: Return created agentVersion
        AdminService-->>AdminController: agentVersion
        AdminController-->>BE_AdminRoutes: Response 201 Created (agentVersion)
        BE_AdminRoutes-->>AdminFE: Success notification, display new version
    end
    
    %% Set version as stable
    AdminUser->>AdminFE: Select version X, click "Set Stable"
    AdminFE->>BE_AdminRoutes: PUT /versions/:versionId ({is_stable: true})
    BE_AdminRoutes->>AuthMiddleware: verifyToken & authAccess({requiredRole: 'admin'})
    AuthMiddleware->>BE_AdminRoutes: next()
    BE_AdminRoutes->>AdminController: setAgentVersionStability(req, res)
    AdminController->>AdminService: updateStabilityFlag(req.params.versionId, req.body.is_stable)
    AdminService->>AgentVersionDB: Find AgentVersion by versionId
    alt Version doesn't exist
        AdminService-->>AdminController: Throw error "Agent version not found"
    else Version exists
        alt req.body.is_stable === true
            AdminService->>AgentVersionDB: UPDATE agent_versions SET is_stable=false WHERE id != versionId
            AdminService->>AgentVersionDB: UPDATE agent_versions SET is_stable=true WHERE id = versionId
        else req.body.is_stable === false
            AdminService->>AgentVersionDB: UPDATE agent_versions SET is_stable=false WHERE id = versionId
        end
        AgentVersionDB-->>AdminService: Return updatedVersion
        AdminService-->>AdminController: updatedVersion
        alt req.body.is_stable === true
            AdminController->>WebSocketService: notifyAgentsOfNewVersion(updatedVersion)
            WebSocketService-->>AllConnectedAgents: Emit 'agent:new_version_available' (versionInfo) to all agent rooms
        end
        AdminController-->>BE_AdminRoutes: Response 200 OK (updatedVersion)
        BE_AdminRoutes-->>AdminFE: Success notification
    end

    %% Get version list
    AdminUser->>AdminFE: Request to view agent version list
    AdminFE->>BE_AdminRoutes: GET /versions
    BE_AdminRoutes->>AuthMiddleware: verifyToken & authAccess({requiredRole: 'admin'})
    AuthMiddleware->>BE_AdminRoutes: next()
    BE_AdminRoutes->>AdminController: getAgentVersions(req, res)
    AdminController->>AdminService: getAllVersions()
    AdminService->>AgentVersionDB: Get all AgentVersions (order by is_stable DESC, created_at DESC)
    AgentVersionDB-->>AdminService: versions list
    AdminService-->>AdminController: versions
    AdminController-->>BE_AdminRoutes: Response 200 OK (versions)
    BE_AdminRoutes-->>AdminFE: Display versions list
```

## 6. Agent Update Check and Download Flow

```mermaid
sequenceDiagram
    participant AgentApp as Agent Application
    participant BE_AgentRoutes as Backend Agent Routes (/api/agent)
    participant AgentAuthMiddleware as Agent Auth Middleware
    participant AgentController as Agent Controller
    participant AgentService as Agent Service
    participant AgentVersionDB as AgentVersion Database
    participant FileSystem as Server File System

    %% Check for updates
    AgentApp->>BE_AgentRoutes: GET /check-update?current_version=1.0.0
    note right of AgentApp: Sent with headers 'X-Agent-ID' and 'Authorization: Bearer <agent_token>'
    BE_AgentRoutes->>AgentAuthMiddleware: verifyAgentToken(req, res, next)
    critical Agent Token Authentication Flow (As in section 2.1)
        AgentAuthMiddleware->>BE_AgentRoutes: next()
    end
    BE_AgentRoutes->>AgentController: handleCheckUpdate(req, res)
    AgentController->>AgentService: getLatestStableVersionInfo(req.query.current_version)
    AgentService->>AgentVersionDB: Find AgentVersion (is_stable=true, order by created_at DESC, limit 1)
    AgentVersionDB-->>AgentService: Return latestStableVersion (or null)
    alt Newer version available (latestStableVersion.version > current_version)
        AgentService-->>AgentController: Return {version, download_url, checksum_sha256, notes}
        AgentController-->>BE_AgentRoutes: Response 200 OK ({status: "success", update_available: true, ...updateInfo})
        BE_AgentRoutes-->>AgentApp: Receive update information (updateInfo)
    else No new version or current version is the latest
        AgentService-->>AgentController: null
        AgentController-->>BE_AgentRoutes: Response 204 No Content
        BE_AgentRoutes-->>AgentApp: No update available
    end

    %% Download update package (if available)
    alt Agent decides to download update based on updateInfo
        AgentApp->>BE_AgentRoutes: GET /agent-packages/:filename (filename from updateInfo.download_url)
        note right of AgentApp: Sent with headers 'X-Agent-ID' and 'Authorization: Bearer <agent_token>'
        BE_AgentRoutes->>AgentAuthMiddleware: verifyAgentToken(req, res, next)
        critical Agent Token Authentication Flow (As in section 2.1)
            AgentAuthMiddleware->>BE_AgentRoutes: next()
        end
        BE_AgentRoutes->>AgentController: handleAgentPackageDownload(req, res)
        AgentController->>FileSystem: res.sendFile(filePath) (filePath based on AGENT_PACKAGES_DIR and req.params.filename)
        alt File exists
            FileSystem-->>BE_AgentRoutes: Stream file
            BE_AgentRoutes-->>AgentApp: Receive package file
            AgentApp->>AgentApp: Verify checksum, extract, install update
        else File doesn't exist
            FileSystem-->>AgentController: Error (e.g., ENOENT)
            AgentController-->>BE_AgentRoutes: Response 404 Not Found
            BE_AgentRoutes-->>AgentApp: File download error
        end
    end
```

## 7. User Management Flow (Admin) - Example: Get User List

```mermaid
sequenceDiagram
    actor AdminUser as Admin
    participant AdminFE as Admin Frontend
    participant BE_UserRoutes as Backend User Routes (/api/users)
    participant AuthMiddleware as Auth Middleware (authUser.js + authAccess.js)
    participant UserController as User Controller
    participant UserService as User Service
    participant UserDB as User Database

    AdminUser->>AdminFE: Request to view user list (may include filter, pagination)
    AdminFE->>BE_UserRoutes: GET /api/users?page=1&limit=10&username=abc&role=user
    BE_UserRoutes->>AuthMiddleware: verifyToken(req,res,next)
    AuthMiddleware->>AuthMiddleware: Verify JWT
    alt Valid token
        AuthMiddleware->>AuthMiddleware: authAccess({requiredRole: 'admin'}) (check req.user.role)
        alt User is Admin
            AuthMiddleware->>BE_UserRoutes: next()
            BE_UserRoutes->>UserController: getAllUsers(req, res)
            UserController->>UserService: getAllUsers(page, limit, search, role, is_active)
            UserService->>UserDB: Query Users (with where, limit, offset, order, attributes exclude password_hash)
            UserDB-->>UserService: {count, rows}
            UserService-->>UserController: {total, currentPage, totalPages, users}
            UserController-->>BE_UserRoutes: Response 200 OK (result)
            BE_UserRoutes-->>AdminFE: Display user list
        else User is not Admin
            AuthMiddleware-->>BE_UserRoutes: Response 403 Forbidden
            BE_UserRoutes-->>AdminFE: Permission error
        end
    else Invalid token
        AuthMiddleware-->>BE_UserRoutes: Response 401 Unauthorized
        BE_UserRoutes-->>AdminFE: Authentication error
    end
```

## 8. Room Management Flow (Admin/User) - Example: Get Room Details

```mermaid
sequenceDiagram
    actor User as User (Admin/Regular)
    participant FE as Frontend UI
    participant BE_RoomRoutes as Backend Room Routes (/api/rooms)
    participant AuthMiddleware as Auth Middleware (authUser.js + authAccess.js)
    participant RoomController as Room Controller
    participant RoomService as Room Service
    participant RoomDB as Room Database
    participant ComputerDB as Computer Database (via RoomService)
    participant UserRoomAssignmentDB as UserRoomAssignment Database (via authAccess)

    User->>FE: Request to view room X details (roomId)
    FE->>BE_RoomRoutes: GET /api/rooms/:roomId
    BE_RoomRoutes->>AuthMiddleware: verifyToken(req,res,next)
    AuthMiddleware->>AuthMiddleware: Verify JWT
    alt Valid token
        AuthMiddleware->>AuthMiddleware: authAccess({checkRoomIdParam: true})
        alt User is Admin (skip UserRoomAssignment check)
             AuthMiddleware->>BE_RoomRoutes: next()
        else User is Regular User
            AuthMiddleware->>UserRoomAssignmentDB: Find UserRoomAssignment (user_id=req.user.id, room_id=req.params.roomId)
            alt User is assigned to room
                UserRoomAssignmentDB-->>AuthMiddleware: Assignment found
                AuthMiddleware->>BE_RoomRoutes: next()
            else User is not assigned
                UserRoomAssignmentDB-->>AuthMiddleware: null
                AuthMiddleware-->>BE_RoomRoutes: Response 403 Forbidden ("You do not have access to this room")
                BE_RoomRoutes-->>FE: Permission error
            end
        end
        BE_RoomRoutes->>RoomController: getRoomById(req, res)
        RoomController->>RoomService: getRoomById(req.params.roomId)
        RoomService->>RoomDB: Find Room by PK, include Computers (exclude agent_token_hash, errors from computer)
        RoomDB-->>RoomService: roomData (including computers list)
        RoomService-->>RoomController: roomData
        RoomController-->>BE_RoomRoutes: Response 200 OK (roomData)
        BE_RoomRoutes-->>FE: Display room details and computers in the room
    else Invalid token
        AuthMiddleware-->>BE_RoomRoutes: Response 401 Unauthorized
        BE_RoomRoutes-->>FE: Authentication error
    end
```

## 9. Computer Error Reporting/Resolution Flow (Frontend)

```mermaid
sequenceDiagram
    actor User as User (Admin/Regular)
    participant FE as Frontend UI
    participant BE_ComputerRoutes as Backend Computer Routes (/api/computers)
    participant AuthMiddleware as Auth Middleware (authUser.js + authAccess.js)
    participant ComputerController as Computer Controller
    participant ComputerService as Computer Service
    participant ComputerDB as Computer Database
    participant UserRoomAssignmentDB as UserRoomAssignment Database (via authAccess)

    %% Report new error
    User->>FE: Report error for computer X (computerId), enter error_type, error_message, error_details
    FE->>BE_ComputerRoutes: POST /api/computers/:computerId/errors ({error_type, error_message, error_details})
    BE_ComputerRoutes->>AuthMiddleware: verifyToken & authAccess({checkComputerIdParam: true})
    critical Access Control Flow (Similar to section 8, check if user has access to computerId)
        AuthMiddleware->>BE_ComputerRoutes: next()
    end
    BE_ComputerRoutes->>ComputerController: reportComputerError(req, res)
    ComputerController->>ComputerService: reportComputerError(req.params.computerId, req.body)
    ComputerService->>ComputerDB: Find Computer, add newError (id, type, message, details, reported_at, resolved=false) to 'errors' array, set have_active_errors=true
    ComputerDB-->>ComputerService: {error: newError, computerId}
    ComputerService-->>ComputerController: result
    ComputerController-->>BE_ComputerRoutes: Response 201 Created (result)
    BE_ComputerRoutes-->>FE: Notify error report successful

    %% Resolve error
    User->>FE: Mark error Y (errorId) of computer X (computerId) as resolved, enter resolution_notes
    FE->>BE_ComputerRoutes: PUT /api/computers/:computerId/errors/:errorId/resolve ({resolution_notes})
    BE_ComputerRoutes->>AuthMiddleware: verifyToken & authAccess({checkComputerIdParam: true})
     critical Access Control Flow (Similar to section 8)
        AuthMiddleware->>BE_ComputerRoutes: next()
    end
    BE_ComputerRoutes->>ComputerController: resolveComputerError(req, res)
    ComputerController->>ComputerService: resolveComputerError(req.params.computerId, req.params.errorId, req.body.resolution_notes)
    ComputerService->>ComputerDB: Find Computer, find error errorId in 'errors' array, update resolved=true, resolved_at, resolution_notes. Update have_active_errors for Computer.
    ComputerDB-->>ComputerService: {error: updatedError, computerId}
    ComputerService-->>ComputerController: result
    ComputerController-->>BE_ComputerRoutes: Response 200 OK (result)
    BE_ComputerRoutes-->>FE: Notify error resolution successful
```
