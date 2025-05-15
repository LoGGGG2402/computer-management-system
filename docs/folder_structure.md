Below is the current folder structure of the project, including three main components: Backend (Node.js/Express), Frontend (React/Vite), and Agent (Python).

#

```
computer-management-system/
├── package.json              
├── readme.md              
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

## Backend Structure
- MVC pattern with Sequelize ORM
- Controllers in `controllers/` handle API requests
- Routes in `routes/` define endpoints
- Services in `services/` contain business logic
- WebSocket is handled in `sockets/`
- Database migrations in `database/migrations/`
- Authentication and access control middleware

## Frontend Structure
- Feature-based structure with directories `components/`, `pages/`, `contexts/`, `hooks/`
- Uses React Router for routing management
- Services in `services/` handle communication with Backend API
- Contexts manage global state
- Pages are organized by function (admin, room, computer)
