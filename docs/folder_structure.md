# Cấu Trúc Thư Mục Của Dự Án Computer Management System

Dưới đây là cấu trúc thư mục hiện tại của dự án, bao gồm ba thành phần chính: Backend (Node.js/Express), Frontend (React/Vite), và Agent (Python).

## Cấu Trúc Thư Mục

```
computer-management-system/
├── package.json              # Package.json cấp root
├── readme.md                 # Thông tin tổng quan về dự án
│
├── agent/                    # Thư mục gốc cho Agent (Python)
│   ├── requirements.txt      # Các thư viện Python cần thiết
│   ├── config/               # Cấu hình Agent
│   │   └── agent_config.json # File cấu hình Agent
│   └── src/                  # Mã nguồn chính của Agent
│       ├── __init__.py       # File khởi tạo
│       ├── main.py           # Điểm vào của Agent
│       ├── communication/    # Module giao tiếp
│       │   ├── __init__.py
│       │   ├── http_client.py   # Client HTTP
│       │   └── ws_client.py     # Client WebSocket
│       ├── config/           # Module quản lý cấu hình
│       │   ├── __init__.py
│       │   ├── config_manager.py # Quản lý cấu hình
│       │   └── state_manager.py  # Quản lý trạng thái
│       ├── core/             # Module xử lý chính
│       │   ├── __init__.py
│       │   ├── agent.py         # Logic chính của Agent
│       │   └── command_executor.py # Thực thi lệnh
│       ├── monitoring/       # Module giám sát
│       │   ├── __init__.py
│       │   └── system_monitor.py # Giám sát hệ thống
│       ├── ui/               # Module giao diện người dùng
│       │   ├── __init__.py
│       │   └── ui_console.py    # Giao diện console
│       └── utils/            # Tiện ích hỗ trợ
│           ├── __init__.py
│           ├── logger.py     # Xử lý logging
│           └── utils.py      # Các hàm tiện ích
│
├── backend/                  # Thư mục gốc cho Backend (Node.js/Express)
│   ├── package.json          # Quản lý dependencies của Backend
│   └── src/                  # Mã nguồn chính của Backend
│       ├── app.js            # Khởi tạo Express app
│       ├── server.js         # Khởi động server HTTP và Socket.IO
│       ├── config/           # Cấu hình ứng dụng
│       │   ├── auth.config.js # Cấu hình xác thực, JWT
│       │   └── db.config.js  # Cấu hình kết nối cơ sở dữ liệu
│       ├── controllers/      # Xử lý các request HTTP
│       │   ├── agent.controller.js    # Xử lý yêu cầu từ Agent
│       │   ├── auth.controller.js     # Xử lý xác thực
│       │   ├── computer.controller.js # Quản lý máy tính
│       │   ├── room.controller.js     # Quản lý phòng
│       │   ├── statics.controller.js  # Xử lý dữ liệu thống kê
│       │   └── user.controller.js     # Quản lý người dùng
│       ├── database/         # Tương tác với cơ sở dữ liệu
│       │   ├── migrations/   # Database migrations
│       │   │   ├── 20250407000001-create-users.js
│       │   │   ├── 20250407000002-create-rooms.js
│       │   │   └── ...
│       │   ├── models/       # Định nghĩa các model
│       │   └── seeders/      # Dữ liệu mẫu
│       ├── middleware/       # Middleware
│       │   ├── authAdmin.js           # Kiểm tra quyền Admin
│       │   ├── authAgentToken.js      # Xác thực token Agent
│       │   ├── authComputerAccess.js  # Kiểm tra quyền truy cập Computer
│       │   ├── authJwt.js             # Xác thực JWT token
│       │   └── authRoomAccess.js      # Kiểm tra quyền truy cập Room
│       ├── routes/           # Định nghĩa các routes API
│       │   ├── agent.routes.js
│       │   ├── auth.routes.js
│       │   ├── computer.routes.js
│       │   ├── index.js      # Tổng hợp và export tất cả routes
│       │   ├── room.routes.js
│       │   ├── statics.routes.js
│       │   └── user.routes.js
│       ├── services/         # Logic nghiệp vụ
│       │   ├── auth.service.js
│       │   ├── computer.service.js
│       │   ├── mfa.service.js
│       │   ├── room.service.js
│       │   ├── statics.service.js
│       │   ├── user.service.js
│       │   └── websocket.service.js
│       └── sockets/          # Xử lý WebSocket connections
│           ├── index.js
│           └── handlers/     # Xử lý các sự kiện WebSocket
│
├── docs/                     # Tài liệu dự án
│   ├── activity_flows.md     # Mô tả các luồng hoạt động
│   ├── api.md                # Tài liệu API chi tiết
│   └── folder_structure.md   # Mô tả cấu trúc thư mục (file này)
│
└── frontend/                 # Thư mục gốc cho Frontend (React/Vite)
    ├── eslint.config.js      # Cấu hình ESLint
    ├── index.html            # Trang HTML chính
    ├── package.json          # Quản lý dependencies Frontend
    ├── README.md             # Thông tin và hướng dẫn dành cho Frontend
    ├── vite.config.js        # Cấu hình Vite
    ├── public/               # Tài nguyên tĩnh
    │   └── vite.svg          # Logo Vite
    └── src/                  # Mã nguồn chính của Frontend
        ├── App.jsx           # Component chính của ứng dụng
        ├── index.css         # CSS toàn cục
        ├── main.jsx          # Điểm khởi đầu của ứng dụng React
        ├── assets/           # Tài nguyên như hình ảnh, fonts
        │   └── react.svg     # Logo React
        ├── components/       # Các component tái sử dụng
        │   ├── admin/        # Component cho trang Admin
        │   ├── common/       # Component dùng chung
        │   ├── computer/     # Component quản lý máy tính
        │   └── room/         # Component quản lý phòng
        ├── contexts/         # React Contexts
        │   ├── AuthContext.jsx       # Quản lý trạng thái xác thực
        │   ├── CommandHandleContext.jsx # Quản lý xử lý lệnh
        │   └── SocketContext.jsx     # Quản lý kết nối Socket
        ├── hooks/            # Custom React hooks
        │   ├── useCopyToClipboard.js # Hook sao chép vào clipboard
        │   ├── useFormatting.js      # Hook định dạng dữ liệu
        │   ├── useModalState.js      # Hook quản lý trạng thái modal
        │   └── useSimpleFetch.js     # Hook gọi API đơn giản
        ├── layouts/          # Layout components
        │   ├── Header.jsx    # Component header
        │   └── MainLayout.jsx # Layout chính của ứng dụng
        ├── pages/            # Các trang chính
        │   ├── LoginPage.jsx # Trang đăng nhập
        │   ├── Admin/        # Các trang quản trị
        │   ├── computer/     # Các trang quản lý máy tính
        │   ├── dashboard/    # Trang dashboard
        │   ├── room/         # Các trang quản lý phòng
        │   └── user/         # Các trang quản lý người dùng
        ├── router/           # Cấu hình routing
        │   ├── index.jsx     # Định nghĩa routes chính
        │   └── ProtectedRoute.jsx # Bảo vệ route cho người dùng đã đăng nhập
        └── services/         # Các service giao tiếp với Backend
            ├── api.js        # Cấu hình axios và các hàm xử lý HTTP chung
            ├── auth.service.js     # Service xác thực
            ├── computer.service.js # Service quản lý máy tính
            ├── room.service.js     # Service quản lý phòng
            ├── statics.service.js  # Service dữ liệu thống kê
            └── user.service.js     # Service quản lý người dùng
```

## Đặc điểm cấu trúc:

### Agent (Python)
- Cấu trúc mô-đun hóa với điểm vào là `main.py`
- Logic chính nằm trong `core/agent.py`
- Module `communication/` xử lý giao tiếp HTTP và WebSocket
- Module `monitoring/` thu thập thông tin hệ thống
- Module `config/` quản lý cấu hình và trạng thái Agent
- Module `utils/` cung cấp các tiện ích như logging

### Backend (Node.js/Express)
- Mô hình MVC với Sequelize ORM
- Controllers trong `controllers/` xử lý các request API
- Routes trong `routes/` định nghĩa các endpoint
- Services trong `services/` chứa logic nghiệp vụ
- WebSocket được xử lý trong `sockets/`
- Database migrations trong `database/migrations/`

### Frontend (React/Vite)
- Cấu trúc theo tính năng với các thư mục `components/`, `pages/`, `contexts/`, `hooks/`
- Sử dụng React Router cho quản lý routing
- Services trong `services/` xử lý giao tiếp với Backend API
- Contexts quản lý trạng thái toàn cục
- Các trang được tổ chức theo chức năng (admin, phòng, máy tính)
