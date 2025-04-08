# Cấu Trúc Thư Mục Của Dự Án Computer Management System

Dưới đây là cấu trúc thư mục hiện tại của dự án, bao gồm ba thành phần chính: Backend (Node.js/Express), Frontend (React/Vite), và Agent (Python).

## Cấu Trúc Thư Mục

```
computer-management-system/
├── build_instruction.md      # Hướng dẫn xây dựng và triển khai
├── package.json              # Package.json cấp root
├── readme.md                 # Thông tin tổng quan về dự án
│
├── agent/                    # Thư mục gốc cho Agent (Python)
│   ├── config/               # Cấu hình Agent
│   │   └── agent_config.json # File cấu hình Agent
│   ├── src/                  # Mã nguồn chính của Agent
│   │   ├── __init__.py       # File khởi tạo
│   │   ├── main.py           # Điểm vào của Agent
│   │   ├── auth/             # Module xử lý xác thực
│   │   │   ├── __init__.py
│   │   │   ├── mfa_handler.py   # Xử lý MFA
│   │   │   └── token_manager.py # Quản lý token xác thực
│   │   ├── communication/    # Module giao tiếp
│   │   │   ├── __init__.py
│   │   │   ├── http_client.py   # Client HTTP
│   │   │   └── ws_client.py     # Client WebSocket
│   │   ├── config/           # Module quản lý cấu hình
│   │   │   ├── __init__.py
│   │   │   └── config_manager.py
│   │   ├── core/             # Module xử lý chính
│   │   │   ├── __init__.py
│   │   │   └── agent.py         # Logic xử lý chính của Agent
│   │   ├── monitoring/       # Module giám sát
│   │   │   ├── __init__.py
│   │   │   ├── process_monitor.py # Giám sát quy trình
│   │   │   └── system_monitor.py  # Giám sát hệ thống
│   │   └── utils/            # Tiện ích hỗ trợ
│   │       ├── __init__.py
│   │       ├── logger.py     # Xử lý logging
│   │       └── utils.py      # Các hàm tiện ích
│   ├── storage/              # Lưu trữ dữ liệu của Agent
│   │   ├── device_id         # ID thiết bị
│   │   ├── room_config.json  # Cấu hình phòng
│   │   └── logs/             # Thư mục logs
│   └── requirements.txt      # Các thư viện Python cần thiết
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
│       │   └── user.controller.js     # Quản lý người dùng
│       ├── database/         # Tương tác với cơ sở dữ liệu
│       │   ├── migrations/   # Database migrations
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
│       │   └── user.routes.js
│       ├── services/         # Logic nghiệp vụ
│       │   ├── auth.service.js
│       │   ├── computer.service.js
│       │   ├── mfa.service.js
│       │   ├── room.service.js
│       │   ├── user.service.js
│       │   └── websocket.service.js
│       ├── sockets/          # Xử lý WebSocket connections
│       │   └── index.js
│       └── utils/            # Tiện ích
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
        │   ├── admin/
        │   ├── computer/
        │   └── room/
        ├── contexts/         # React Contexts
        │   ├── AuthContext.jsx # Context quản lý trạng thái xác thực
        │   └── SocketContext.jsx # Context quản lý kết nối Socket
        ├── layouts/          # Layout components
        │   ├── Header.jsx    # Component header
        │   └── MainLayout.jsx # Layout chính của ứng dụng
        ├── pages/            # Các trang chính
        │   ├── LoginPage.jsx # Trang đăng nhập
        │   ├── Admin/
        │   ├── dashboard/
        │   └── room/
        ├── router/           # Cấu hình routing
        │   ├── index.jsx     # Định nghĩa routes chính
        │   └── ProtectedRoute.jsx # Bảo vệ route cho người dùng đã đăng nhập
        ├── services/         # Các service giao tiếp với Backend
            ├── api.js        # Cấu hình axios và các hàm xử lý HTTP chung
            ├── auth.service.js # Service xác thực
            ├── computer.service.js # Service quản lý máy tính
            ├── room.service.js # Service quản lý phòng
            └── user.service.js # Service quản lý người dùng
```

## Đặc điểm cấu trúc:

### Backend (Node.js/Express)
- Sử dụng mô hình MVC (Model-View-Controller) với Sequelize ORM để tương tác với cơ sở dữ liệu.
- Models được định nghĩa trong `database/models/`.
- Controllers xử lý logic request trong `controllers/`.
- Routes định nghĩa các endpoint API trong `routes/`.
- Services chứa logic nghiệp vụ phức tạp trong `services/`.
- WebSocket được xử lý trong `sockets/`.

### Frontend (React/Vite)
- Sử dụng React với Vite làm build tool.
- Cấu trúc theo tính năng với các thư mục `components/`, `pages/`, `contexts/`, `hooks/`.
- Sử dụng React Router để quản lý routing.
- Services trong `services/` giúp giao tiếp với Backend API.

### Agent (Python)
- Cấu trúc mô-đun hóa với điểm vào là `main.py`.
- Tổ chức theo chức năng với các modules: `auth/`, `communication/`, `config/`, `core/`, `monitoring/`, `utils/`.
- Logic chính nằm trong `core/agent.py`.
- `auth/` xử lý xác thực (MFA, token).
- `communication/` quản lý giao tiếp HTTP và WebSocket.
- `monitoring/` thu thập thông tin hệ thống.
- Cấu hình được lưu trong `config/agent_config.json`.
- Dữ liệu cục bộ được lưu trong `storage/`.
