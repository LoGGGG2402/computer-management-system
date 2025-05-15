# CMSAgent Flow Diagrams

This section provides flowcharts describing the main operational processes of CMSAgent, based on "CMSAgent Comprehensive Documentation v7.4" and "CMSAgent System Architecture". The diagrams are presented using Mermaid syntax.

## 1. Installation and Initial Configuration Flow (Part III - Comprehensive Documentation)

```mermaid
graph TD
    A["User runs Setup.CMSAgent.exe with Admin rights"] --> B{"Check Admin rights"};
    B -- "Insufficient rights" --> BA["Display error message, exit"];
    B -- "Sufficient rights" --> C["Copy application files to C:\\Program Files\\CMSAgent"];
    C --> D["Create data directory structure at C:\\ProgramData\\CMSAgent"];
    D --> E["Set Full Control permissions for LocalSystem on C:\\ProgramData\\CMSAgent"];
    E --> F["Installer executes 'CMSAgent.exe configure'"];
    F --> G{"Open interactive Console"};
    G --> H["Check/Create Device ID in runtime_config.json"];
    H --> I{"Enter location information (RoomName, PosX, PosY)"};
    I -- "Cancel (Ctrl+C)" --> IA["Exit configuration process"];
    I -- "Entry complete" --> J["Send POST request /api/agent/identify to Server"];
    J --> K{"Process Server response"};
    K -- "Location error (position_error)" --> L{"Ask user to try again?"};
    L -- "Yes" --> I;
    L -- "No" --> IA;
    K -- "MFA required (mfa_required)" --> M["Display MFA requirement notification"];
    M --> N{"Enter MFA code"};
    N -- "Cancel/Empty" --> IA;
    N -- "Entry complete" --> O["Send POST /api/agent/verify-mfa"];
    O --> P{"Process MFA response"};
    P -- "MFA Failed" --> Q{"Ask user to try again?"};
    Q -- "Yes" --> N;
    Q -- "No" --> IA;
    P -- "MFA Successful (with agentToken)" --> R["Save temporary agentToken"];
    K -- "Identification successful (with agentToken)" --> R;
    K -- "Identification successful (agent already exists, no new token)" --> S["Notify that agent is already registered"];
    S --> T["Save runtime configuration and token (encrypted)"];
    R --> T;
    K -- "Other errors (HTTP 500, network)" --> U{"Ask user to try again?"};
    U -- "Yes" --> J;
    U -- "No" --> IA;
    T --> V["Notification of successful configuration save"];
    V --> W["Installer registers CMSAgent as Windows Service"];
    W -- "ServiceName: CMSAgentService" --> W1["DisplayName: Computer Management System Agent"];
    W1 -- "StartType: Automatic" --> W2["ServiceAccount: LocalSystem"];
    W2 --> X["Installer starts the Service"];
    X --> Y["Installation complete, success notification"];
```

## 2. Regular Agent Operation Flow (Part IV - Comprehensive Documentation)

```mermaid
graph TD
    subgraph Agent Startup
        A["SCM starts CMSAgent.exe"] --> B("Status: INITIALIZING");
        B --> C["Setup Logging"];
        C --> D["Ensure data directory structure"];
        D --> E["Ensure single instance (Mutex)"];
        E -- "Another instance exists" --> EA["Log error, exit"];
        E -- "Mutex acquired" --> F["Load configuration from appsettings.json and runtime_config.json"];
        F --> G["Validate configuration"];
        G -- "Configuration error" --> GA["Status: ERROR, exit"];
        G -- "Valid configuration" --> H["Decrypt agent_token"];
        H -- "Decryption error" --> GA;
        H -- "Decryption successful" --> I["Initialize Modules"];
    end

    subgraph Connection and Authentication
        I --> J("Status: AUTHENTICATING");
        J --> K["Connect WebSocket to Server"];
        K -- "Failed" --> L{"Retry WS connection according to configuration"};
        L --> K;
        K -- "Successful" --> M["Send authentication information (Header/Event)"];
        M --> N{"Wait for WS authentication response"};
        N -- "agent:ws_auth_success" --> O("Status: CONNECTED");
        N -- "agent:ws_auth_failed" --> P["Try POST /api/agent/identify"];
        P -- "Identify successful, new token" --> Q["Update token, return to WS connection"];
        Q --> K;
        P -- "Identify requires MFA / Other failure" --> R("Status: DISCONNECTED");
        R --> L;
    end

    O --> S["Send initial hardware information (POST /hardware-info)"];
    S -- "Error" --> SA["Log, continue"];

    subgraph Main Operation Loop
        O --> T["Start main loop (Status: CONNECTED)"];
        T --> U["Send periodic status reports (agent:status_update)"];
        T --> V{"Check for new version updates?"};
        V -- "New version available" --> W["Activate Agent Update Flow (Part V)"];
        W --> WB("Status: UPDATING");
        T --> X{"Listen for commands from Server (command:execute)"};
        X -- "Command received" --> Y["Add command to queue"];
        Y --> Z["Worker processes command"];
        Z --> AA["Execute command, gather results"];
        AA --> AB["Send results (agent:command_result)"];
        T --> AC{"Any unexpected errors?"};
        AC -- "Yes" --> AD["Report error (POST /api/agent/report-error)"];
        AD -- "Send failed" --> AE["Save error to error_reports/"];
        T --> AF{"WebSocket connection healthy?"};
        AF -- "Connection lost" --> R;
    end

    subgraph Shutdown
        AG["SCM requests Service stop"] --> AH("Status: STOPPING");
        AH --> AI["Disconnect WebSocket"];
        AI --> AJ["Complete processing commands in progress"];
        AJ --> AK["Cancel Timers"];
        AK --> AL["Release Mutex"];
        AL --> AM["Log shutdown complete, exit"];
    end
```

## 3. Server Command Processing Flow (Part IV.10 - Comprehensive Documentation)

```mermaid
graph TD
    A["Agent in CONNECTED state, listening on WebSocket"] --> B{"Receive 'command:execute' event from Server"};
    B -- "New command (commandId, command, commandType)" --> C["Add command to Command Queue"];
    C --> D{"Is queue full? (max_queue_size)"};
    D -- "Full" --> DA["Log COMMAND_QUEUE_FULL error, possibly reject command"];
    D -- "Not full" --> E["A Worker Thread retrieves command from queue"];
    E --> F["CommandHandlerFactory creates Handler based on commandType"];
    F --> G{"Handler type?"};
    G -- "ConsoleCommandHandler" --> H["Execute console command"];
    G -- "SystemActionCommandHandler" --> I["Execute system action"];
    G -- "Other Handler" --> J["Execute according to that handler's logic"];
    subgraph Command Execution
        K["Begin command execution"] --> L["Monitor execution time (limited by default_timeout_sec)"];
        L --> M["Collect stdout, stderr (if any), exitCode"];
        M --> N{"Command completed/Timeout/Error?"};
    end
    H --> K;
    I --> K;
    J --> K;
    N -- "Successfully completed" --> O["Prepare result: success=true, result={stdout, stderr, exitCode}"];
    N -- "Command error/Timeout" --> P["Prepare result: success=false, result={errorMessage, errorCode}"];
    O --> Q["Send result (commandId, success, type, result) to Server via WebSocket 'agent:command_result'"];
    P --> Q;
    Q --> A;
```

## 4. Agent Update Flow (Part V - Comprehensive Documentation)

```mermaid
graph TD
    A["Agent receives new version information (HTTP or WebSocket)"] --> B("Status: UPDATING");
    B --> C["Notify Server: 'update_started'"];
    C --> D{"Download update package (.zip) to updates/download/"};
    D -- "Download error" --> DA["Handle download error (retry, report to server, return to CONNECTED)"];
    D -- "Download successful" --> DB["Notify Server: 'update_downloaded'"];
    DB --> E{"Verify update package Checksum"};
    E -- "Checksum mismatch" --> EA["Delete file, report error to server, return to CONNECTED"];
    E -- "Checksum match" --> F["Extract update package to updates/extracted/"];
    F -- "Extraction error" --> FA["Report error to server, return to CONNECTED"];
    F -- "Extraction successful" --> FB["Notify Server: 'update_extracted'"];
    FB --> G["Locate CMSUpdater.exe (prioritize new version)"];
    G --> H["Launch CMSUpdater.exe with necessary parameters"];
    H --> HA["Notify Server: 'updater_launched'"];
    HA --> I["Old Agent (Service) begins safe shutdown process"];

    subgraph CMSUpdater.exe Process
        J["CMSUpdater.exe starts"] --> K["Setup separate logging"];
        K --> L{"Wait for old CMSAgent.exe to completely stop (timeout)"};
        L -- "Timeout" --> LA["Log error, exit Updater, consider rollback if backup exists"];
        L -- "Old Agent stopped" --> M["Backup old agent installation directory"];
        M -- "Backup error" --> MA["Log critical error, exit Updater"];
        M -- "Backup successful" --> N["Move/Copy new agent files to installation directory"];
        N -- "Deployment error" --> O{"Perform Rollback"};
        O -- "Rollback successful" --> OA["Log, exit Updater"];
        O -- "Rollback failed" --> OB["Log critical error, exit Updater"];
        N -- "Deployment successful" --> P["Start new CMSAgent Service (via SCM)"];
        P -- "Error starting new Service" --> Q{"Perform Rollback, attempt to start old Service"};
        Q -- "Rollback and old Service start successful" --> QA["Old Agent reports update error to Server, exit Updater"];
        Q -- "Rollback failed / Old Service won't start" --> QB["Log critical error, exit Updater"];
        P -- "New Service started successfully" --> R{"Watchdog: Monitor new Service for a short time"};
        R -- "New Service stable" --> S["Cleanup: Delete backup, temporary files"];
        S --> SA["Log successful update"];
        SA --> SB["New Agent (after connecting) notifies Server: 'update_success'"];
        SB --> SC["CMSUpdater.exe exits"];
        R -- "New Service crashes repeatedly" --> Q;
    end
    I --> J;
```

## 5. WebSocket Authentication and Token Refresh Flow (Part IV.9, VIII.6 - Comprehensive Documentation)

```mermaid
graph TD
    A["Agent needs to connect/lost WebSocket connection"] --> B("Status: AUTHENTICATING");
    B --> C{"Valid local agent_token exists?"};
    C -- "Yes" --> D["Connect WebSocket with current token"];
    C -- "No / Token may be expired" --> E["Perform POST /api/agent/identify"];
    E -- "Successful response, new token available" --> F["Save new token (encrypted), update local token"];
    F --> D;
    E -- "Response requires MFA" --> FA["Log, cannot handle MFA automatically, status DISCONNECTED, retry later"];
    E -- "Other error response" --> FB["Log, status DISCONNECTED, retry later"];

    D --> G{"Wait for authentication response from WebSocket Server"};
    G -- "agent:ws_auth_success" --> H("Status: CONNECTED");
    G -- "agent:ws_auth_failed (e.g., invalid/expired token)" --> I["Log WS authentication error"];
    I --> E;

    subgraph Proactive Token Refresh
        J["Periodic timer (e.g., 24 hours) triggers"] --> K["Perform POST /api/agent/identify with forceRenewToken:true"];
        K -- "Successful response, new token available" --> L["Save new token (encrypted), update local token"];
        K -- "Error response" --> M["Log token refresh error, agent continues with old token if valid"];
    end

    H --> N{"Agent operating (CONNECTED)"};
    N -- "Encounters HTTP 401 error when calling API" --> I;
```