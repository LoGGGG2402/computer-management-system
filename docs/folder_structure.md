

Below is the current folder structure of the project, including three main components: Backend (Node.js/Express), Frontend (React/Vite), and Agent (Python).

#

```
computer-management-system/
├── package.json              
├── readme.md                 
├── maintain_agent.md         
│
├── agent/                    
│   ├── requirements.txt      
│   ├── agent/                
│   │   ├── __init__.py       
│   │   ├── main.py           
│   │   ├── version.py        
│   │   ├── command_handlers/ 
│   │   │   ├── __init__.py
│   │   │   ├── base_handler.py       
│   │   │   ├── console_handler.py    
│   │   │   └── system_handler.py     
│   │   ├── communication/    
│   │   │   ├── __init__.py
│   │   │   ├── http_client.py        
│   │   │   ├── server_connector.py   
│   │   │   └── ws_client.py          
│   │   ├── config/           
│   │   │   ├── __init__.py
│   │   │   ├── config_manager.py     
│   │   │   └── state_manager.py      
│   │   ├── core/             
│   │   │   ├── __init__.py
│   │   │   ├── agent.py             
│   │   │   ├── agent_state.py       
│   │   │   └── command_executor.py  
│   │   ├── ipc/              
│   │   │   ├── __init__.py
│   │   │   ├── named_pipe_client.py 
│   │   │   └── named_pipe_server.py 
│   │   ├── monitoring/       
│   │   │   ├── __init__.py
│   │   │   └── system_monitor.py    
│   │   ├── system/           
│   │   │   ├── __init__.py
│   │   │   ├── directory_utils.py   
│   │   │   ├── lock_manager.py      
│   │   │   └── windows_utils.py     
│   │   ├── ui/               
│   │   │   ├── __init__.py
│   │   │   └── ui_console.py        
│   │   └── utils/            
│   │       ├── __init__.py
│   │       ├── logger.py            
│   │       └── utils.py             
│   └── config/               
│       └── agent_config.json 
│
├── backend/                  
│   ├── create_db.sh          
│   ├── package.json          
│   └── src/                  
│       ├── app.js            
│       ├── server.js         
│       ├── config/           
│       │   ├── auth.config.js 
│       │   └── db.config.js  
│       ├── controllers/      
│       │   ├── admin.controller.js    
│       │   ├── agent.controller.js    
│       │   ├── auth.controller.js     
│       │   ├── computer.controller.js 
│       │   ├── room.controller.js     
│       │   └── user.controller.js     
│       ├── database/         
│       │   ├── migrations/   
│       │   ├── models/       
│       │   └── seeders/      
│       ├── middleware/       
│       │   ├── authAccess.js           
│       │   ├── authAgentToken.js      
│       │   ├── authUser.js            
│       │   └── uploadFileMiddleware.js 
│       ├── routes/           
│       │   ├── admin.routes.js
│       │   ├── agent.routes.js
│       │   ├── auth.routes.js
│       │   ├── computer.routes.js
│       │   ├── index.js      
│       │   ├── room.routes.js
│       │   └── user.routes.js
│       ├── services/         
│       │   ├── admin.service.js
│       │   ├── auth.service.js
│       │   ├── computer.service.js
│       │   ├── mfa.service.js
│       │   ├── room.service.js
│       │   ├── user.service.js
│       │   └── websocket.service.js
│       ├── sockets/          
│       │   ├── index.js
│       │   └── handlers/     
│       └── utils/            
│           └── logger.js     
│
├── docs/                     
│   ├── activity_flows.md     
│   ├── api.md                
│   └── folder_structure.md   
│
└── frontend/                 
    ├── eslint.config.js      
    ├── index.html            
    ├── package.json          
    ├── README.md             
    ├── vite.config.js        
    ├── public/               
    │   └── vite.svg          
    └── src/                  
        ├── App.jsx           
        ├── index.css         
        ├── main.jsx          
        ├── assets/           
        │   └── react.svg     
        ├── components/       
        │   ├── common/       
        │   ├── computer/     
        │   └── room/         
        ├── contexts/         
        │   ├── AuthContext.jsx       
        │   ├── CommandHandleContext.jsx 
        │   └── SocketContext.jsx     
        ├── hooks/            
        │   ├── useCopyToClipboard.js 
        │   ├── useFormatting.js      
        │   ├── useModalState.js      
        │   └── useSimpleFetch.js     
        ├── layouts/          
        │   ├── Header.jsx    
        │   └── MainLayout.jsx 
        ├── pages/            
        │   ├── LoginPage.jsx 
        │   ├── Admin/        
        │   ├── computer/     
        │   ├── dashboard/    
        │   ├── room/         
        │   └── user/         
        ├── router/           
        │   └── index.jsx     
        └── services/         
            ├── api.js        
            ├── auth.service.js     
            ├── computer.service.js 
            ├── room.service.js     
            ├── user.service.js     
            └── admin.service.js    
```

#

##
- Modular structure with entry point in `main.py`
- Main logic in `core/agent.py` and state management in `core/agent_state.py`
- The `command_handlers/` module contains handlers for various commands
- The `communication/` module handles HTTP and WebSocket communication with the server
- The `monitoring/` module collects system information
- The `ipc/` module handles inter-process communication
- The `system/` module contains utilities for system interaction
- The `config/` module manages Agent configuration and state
- The `utils/` module provides utilities such as logging

##
- MVC pattern with Sequelize ORM
- Controllers in `controllers/` handle API requests
- Routes in `routes/` define endpoints
- Services in `services/` contain business logic
- WebSocket is handled in `sockets/`
- Database migrations in `database/migrations/`
- Authentication and access control middleware

##
- Feature-based structure with directories `components/`, `pages/`, `contexts/`, `hooks/`
- Uses React Router for routing management
- Services in `services/` handle communication with Backend API
- Contexts manage global state
- Pages are organized by function (admin, room, computer)
