# Cấu Trúc Thư Mục Đề Xuất (Cập nhật)

Dưới đây là cấu trúc thư mục gợi ý cho các thành phần chính của dự án: Backend, Frontend và Agent, đã bao gồm thư mục `docs` và file `README.md` cấp gốc. Cấu trúc này giúp tổ chức code một cách logic và dễ quản lý.

## Cấu Trúc Thư Mục

```
computer-management-system/
├── backend/                  # Thư mục gốc cho Backend (Node.js)
│   ├── src/                  # Mã nguồn chính
│   │   ├── config/           # Cấu hình (database, env, jwt, ...)
│   │   ├── controllers/      # Xử lý request HTTP và gọi services
│   │   ├── database/         # Tương tác với cơ sở dữ liệu (models, migrations, seeders)
│   │   ├── middleware/       # Các middleware (xác thực, phân quyền, log,...)
│   │   ├── routes/           # Định nghĩa các API routes
│   │   ├── services/         # Logic nghiệp vụ chính
│   │   ├── sockets/          # Xử lý sự kiện WebSocket
│   │   ├── utils/            # Các hàm tiện ích, hằng số
│   │   ├── app.js            # Khởi tạo và cấu hình Express app
│   │   └── server.js         # Khởi chạy server HTTP và Socket.IO
│   ├── tests/                # Thư mục chứa tests
│   ├── .env                  # Biến môi trường
│   ├── .gitignore
│   ├── package.json
│   └── README.md             # README riêng cho Backend
│
├── frontend/                 # Thư mục gốc cho Frontend (React + Vite)
│   ├── public/               # Chứa các file tĩnh (vd: favicon)
│   ├── src/                  # Mã nguồn chính
│   │   ├── assets/           # Hình ảnh, fonts,...
│   │   ├── components/       # Các UI components tái sử dụng
│   │   ├── contexts/         # React Contexts
│   │   ├── hooks/            # Custom hooks
│   │   ├── layouts/          # Main layout components
│   │   ├── pages/            # Các trang ứng với routes
│   │   ├── router/           # Cấu hình routing
│   │   ├── services/         # Các hàm gọi API Backend
│   │   ├── styles/           # CSS toàn cục, cấu hình Tailwind mở rộng
│   │   ├── utils/            # Hàm tiện ích, hằng số
│   │   ├── App.jsx           # Component gốc của ứng dụng
│   │   └── main.jsx          # Điểm vào của ứng dụng
│   ├── .env                  # Biến môi trường (VITE_...)
│   ├── .gitignore
│   ├── index.html
│   ├── package.json
│   ├── postcss.config.js
│   ├── tailwind.config.js
│   ├── vite.config.js
│   └── README.md             # README riêng cho Frontend
│
├── agent/                    # Thư mục gốc cho Agent (Python)
│   ├── src/                  # Mã nguồn chính (hoặc tên package của bạn)
│   │   ├── config/           # Load cấu hình (server address, paths,...)
│   │   │   └── settings.py
│   │   ├── modules/          # Các module chức năng cốt lõi
│   │   │   ├── system_info.py        # Thu thập thông tin hệ thống (dùng psutil)
│   │   │   ├── http_client.py        # Giao tiếp HTTP với Backend (dùng requests)
│   │   │   ├── ws_client.py          # Quản lý kết nối WS và nhận lệnh (dùng websocket-client/python-socketio)
│   │   │   ├── command_executor.py   # Thực thi lệnh an toàn (dùng subprocess)
│   │   │   ├── token_manager.py      # Quản lý Agent Token (lưu/đọc an toàn)
│   │   │   └── mfa_handler.py        # Xử lý nhập MFA từ người dùng (dùng input())
│   │   ├── utils/            # Hàm tiện ích
│   │   └── agent.py          # Logic điều phối chính của Agent
│   ├── venv/                 # Thư mục môi trường ảo Python
│   ├── config/               # File cấu hình (vd: config.ini, config.yaml)
│   ├── storage/              # (Tùy chọn) Nơi lưu trữ token/ID (cần bảo mật)
│   ├── logs/                 # Thư mục chứa log files
│   ├── .gitignore
│   ├── requirements.txt      # Danh sách các thư viện Python cần cài đặt
│   ├── main.py               # Điểm vào để chạy Agent
│   ├── README.md             # README riêng cho Agent
│   └── setup.py              # (Tùy chọn) Nếu đóng gói thành thư viện/ứng dụng phức tạp hơn
│
├── docs/                     # Thư mục chứa tài liệu dự án
│   ├── architecture.md       # Sơ đồ kiến trúc, giải thích luồng
│   ├── api.md                # Tài liệu API chi tiết
│   └── setup.md              # Hướng dẫn cài đặt
│
└── README.md                 # README tổng quan cho toàn bộ dự án
```

## Lưu ý:

* `README.md` ở cấp gốc nên chứa thông tin tổng quan về dự án, cách cài đặt chung, cách chạy các thành phần (Backend, Frontend, Agent), và liên kết đến các tài liệu chi tiết hơn trong thư mục `docs/`.
* Mỗi thành phần (backend, frontend, agent) cũng nên có file `README.md` riêng để mô tả chi tiết hơn về thành phần đó.
* Thư mục `docs/` có thể chứa nhiều loại tài liệu khác nhau tùy theo nhu cầu của dự án (thiết kế, hướng dẫn sử dụng, quy trình đóng góp,...).
