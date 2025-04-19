# Folder Structure of Computer Management System Project

Below is the current folder structure of the project, including three main components: Backend (Node.js/Express), Frontend (React/Vite), and Agent (Python).

## Folder Structure

```
computer-management-system/
├── package.json              # Root level package.json
├── readme.md                 # Project overview information
├── maintain_agent.md         # Agent maintenance documentation
│
├── agent/                    # Root directory for Agent (Python)
│   ├── requirements.txt      # Required Python libraries
│   ├── agent/                # Main source code of the Agent
│   │   ├── __init__.py       # Initialization file
│   │   ├── main.py           # Entry point of the Agent
│   │   ├── version.py        # Version information
│   │   ├── command_handlers/ # Command handlers module
│   │   │   ├── __init__.py
│   │   │   ├── base_handler.py       # Base abstract handler
│   │   │   ├── console_handler.py    # Console command handler
│   │   │   └── system_handler.py     # System command handler
│   │   ├── communication/    # Communication module
│   │   │   ├── __init__.py
│   │   │   ├── http_client.py        # HTTP Client
│   │   │   ├── server_connector.py   # Connection to server
│   │   │   └── ws_client.py          # WebSocket Client
│   │   ├── config/           # Configuration management module
│   │   │   ├── __init__.py
│   │   │   ├── config_manager.py     # Configuration management
│   │   │   └── state_manager.py      # State management
│   │   ├── core/             # Core processing module
│   │   │   ├── __init__.py
│   │   │   ├── agent.py             # Main Agent logic
│   │   │   ├── agent_state.py       # Agent state management
│   │   │   └── command_executor.py  # Command execution
│   │   ├── ipc/              # Inter-Process Communication
│   │   │   ├── __init__.py
│   │   │   ├── named_pipe_client.py # IPC client via named pipe
│   │   │   └── named_pipe_server.py # IPC server via named pipe
│   │   ├── monitoring/       # Monitoring module
│   │   │   ├── __init__.py
│   │   │   └── system_monitor.py    # System monitoring
│   │   ├── system/           # System interaction module
│   │   │   ├── __init__.py
│   │   │   ├── directory_utils.py   # Directory utilities
│   │   │   ├── lock_manager.py      # Lock management
│   │   │   └── windows_utils.py     # Windows utilities
│   │   ├── ui/               # User interface module
│   │   │   ├── __init__.py
│   │   │   └── ui_console.py        # Console interface
│   │   └── utils/            # Support utilities
│   │       ├── __init__.py
│   │       ├── logger.py            # Logging handler
│   │       └── utils.py             # Utility functions
│   └── config/               # Agent configuration
│       └── agent_config.json # Agent configuration file
│
├── backend/                  # Root directory for Backend (Node.js/Express)
│   ├── create_db.sh          # Database creation script
│   ├── package.json          # Backend dependencies management
│   └── src/                  # Main source code of the Backend
│       ├── app.js            # Express app initialization
│       ├── server.js         # HTTP and Socket.IO server startup
│       ├── config/           # Application configuration
│       │   ├── auth.config.js # Authentication and JWT configuration
│       │   └── db.config.js  # Database connection configuration
│       ├── controllers/      # HTTP request handlers
│       │   ├── admin.controller.js    # Admin request handler
│       │   ├── agent.controller.js    # Agent request handler
│       │   ├── auth.controller.js     # Authentication handler
│       │   ├── computer.controller.js # Computer management
│       │   ├── room.controller.js     # Room management
│       │   └── user.controller.js     # User management
│       ├── database/         # Database interaction
│       │   ├── migrations/   # Database migrations
│       │   ├── models/       # Model definitions
│       │   └── seeders/      # Sample data
│       ├── middleware/       # Middleware
│       │   ├── authAccess.js           # Access control check
│       │   ├── authAgentToken.js      # Agent token authentication
│       │   ├── authUser.js            # JWT token authentication
│       │   └── uploadFileMiddleware.js # File upload handling
│       ├── routes/           # API route definitions
│       │   ├── admin.routes.js
│       │   ├── agent.routes.js
│       │   ├── auth.routes.js
│       │   ├── computer.routes.js
│       │   ├── index.js      # Aggregation and export of all routes
│       │   ├── room.routes.js
│       │   └── user.routes.js
│       ├── services/         # Business logic
│       │   ├── admin.service.js
│       │   ├── auth.service.js
│       │   ├── computer.service.js
│       │   ├── mfa.service.js
│       │   ├── room.service.js
│       │   ├── user.service.js
│       │   └── websocket.service.js
│       ├── sockets/          # WebSocket connections handling
│       │   ├── index.js
│       │   └── handlers/     # WebSocket event handlers
│       └── utils/            # Utilities
│           └── logger.js     # Logging handler
│
├── docs/                     # Project documentation
│   ├── activity_flows.md     # Description of activity flows
│   ├── api.md                # Detailed API documentation
│   └── folder_structure.md   # Folder structure description (this file)
│
└── frontend/                 # Root directory for Frontend (React/Vite)
    ├── eslint.config.js      # ESLint configuration
    ├── index.html            # Main HTML page
    ├── package.json          # Frontend dependencies management
    ├── README.md             # Information and instructions for Frontend
    ├── vite.config.js        # Vite configuration
    ├── public/               # Static resources
    │   └── vite.svg          # Vite logo
    └── src/                  # Main source code of the Frontend
        ├── App.jsx           # Main application component
        ├── index.css         # Global CSS
        ├── main.jsx          # React application entry point
        ├── assets/           # Resources like images, fonts
        │   └── react.svg     # React logo
        ├── components/       # Reusable components
        │   ├── common/       # Common reusable components
        │   ├── computer/     # Computer management components
        │   └── room/         # Room management components
        ├── contexts/         # React Contexts
        │   ├── AuthContext.jsx       # Authentication state management
        │   ├── CommandHandleContext.jsx # Command handling management
        │   └── SocketContext.jsx     # Socket connection management
        ├── hooks/            # Custom React hooks
        │   ├── useCopyToClipboard.js # Hook for clipboard copying
        │   ├── useFormatting.js      # Hook for data formatting
        │   ├── useModalState.js      # Hook for modal state management
        │   └── useSimpleFetch.js     # Hook for simple API calls
        ├── layouts/          # Layout components
        │   ├── Header.jsx    # Header component
        │   └── MainLayout.jsx # Main application layout
        ├── pages/            # Main pages
        │   ├── LoginPage.jsx # Login page
        │   ├── Admin/        # Admin pages
        │   ├── computer/     # Computer management pages
        │   ├── dashboard/    # Dashboard page
        │   ├── room/         # Room management pages
        │   └── user/         # User management pages
        ├── router/           # Routing configuration
        │   └── index.jsx     # Main route definitions
        └── services/         # Services for Backend communication
            ├── api.js        # Axios configuration and common HTTP handlers
            ├── auth.service.js     # Authentication service
            ├── computer.service.js # Computer management service
            ├── room.service.js     # Room management service
            ├── user.service.js     # User management service
            └── admin.service.js    # System administration service
```

## Structure Characteristics:

### Agent (Python)
- Modular structure with entry point in `main.py`
- Main logic in `core/agent.py` and state management in `core/agent_state.py`
- The `command_handlers/` module contains handlers for various commands
- The `communication/` module handles HTTP and WebSocket communication with the server
- The `monitoring/` module collects system information
- The `ipc/` module handles inter-process communication
- The `system/` module contains utilities for system interaction
- The `config/` module manages Agent configuration and state
- The `utils/` module provides utilities such as logging

### Backend (Node.js/Express)
- MVC pattern with Sequelize ORM
- Controllers in `controllers/` handle API requests
- Routes in `routes/` define endpoints
- Services in `services/` contain business logic
- WebSocket is handled in `sockets/`
- Database migrations in `database/migrations/`
- Authentication and access control middleware

### Frontend (React/Vite)
- Feature-based structure with directories `components/`, `pages/`, `contexts/`, `hooks/`
- Uses React Router for routing management
- Services in `services/` handle communication with Backend API
- Contexts manage global state
- Pages are organized by function (admin, room, computer)
