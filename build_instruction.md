# Kế Hoạch Xây Dựng Hệ Thống Tổng Hợp Chi Tiết

Kế hoạch này mô tả chi tiết các bước để xây dựng hệ thống quản lý máy tính, từ thiết lập ban đầu đến hoàn thiện các chức năng cốt lõi, với Agent dùng Python, Backend Node.js (PostgreSQL/Sequelize), và Frontend React.

## Giai đoạn 0: Thiết Lập Dự Án & Nền Tảng

**Mục tiêu:** Tạo cấu trúc, cài đặt thư viện, thiết lập cơ sở ban đầu.

**Bước 0.1: Khởi tạo Dự án Chung**
* Tạo thư mục con: `backend`, `frontend`, `agent`
* Tạo file `.gitignore` ở cấp gốc.

**Bước 0.2: Thiết lập Backend (Node.js)**
* `cd backend`
* `npm init -y`
* `npm install express dotenv pg pg-hstore sequelize bcrypt jsonwebtoken socket.io node-cache otp-generator`
* `npm install -D nodemon sequelize-cli`
* Tạo cấu trúc thư mục cơ bản trong `src/`: `config`, `controllers`, `database/models`, `middleware`, `routes`, `services`, `sockets`, `utils`.
* Tạo `src/app.js`: Cấu hình Express (cors, json parser, đăng ký routes...).
* Tạo `src/server.js`: Tạo http server, gắn Express app, khởi tạo Socket.IO server, lắng nghe port.
* Tạo `.env`: Định nghĩa `PORT`, `DATABASE_URL`, `JWT_SECRET`, `JWT_EXPIRES_IN`, `AGENT_TOKEN_SECRET`...
* Tạo `src/config/db.config.js`: Cấu hình Sequelize kết nối PostgreSQL từ `.env`.
* Tạo `src/database/models/index.js`: Khởi tạo Sequelize instance, import các model, thiết lập associations. Gọi hàm kết nối DB trong `server.js`.
* Tích hợp Socket.IO cơ bản vào `server.js`.
* Chạy `npx sequelize-cli init` (điều chỉnh `config/config.json` nếu cần).

**Bước 0.3: Thiết lập Frontend (React + Vite)**
* `cd ../frontend`
* `npm create vite@latest . --template react`
* `npm install axios socket.io-client react-router-dom`
* `npm install -D tailwindcss postcss autoprefixer`
* Chạy `npm install tailwindcss @tailwindcss/vite`. Add the @tailwindcss/vite plugin to Vite configuration., Add an @import to your CSS file `index.css`.
* Tạo cấu trúc thư mục cơ bản trong `src/`: `assets`, `components`, `contexts`, `hooks`, `layouts`, `pages`, `router`, `services`, `styles`, `utils`.
* Tạo `src/router/index.jsx`: Dùng `createBrowserRouter`, định nghĩa route `/login`, `/`.
* Cập nhật `src/App.jsx` để dùng `RouterProvider`.

**Bước 0.4: Thiết lập Agent (Python)**
* `cd ../agent`
* `python -m venv venv` & kích hoạt môi trường ảo.
* Tạo `requirements.txt`, thêm `requests`, `websocket-client` (hoặc `python-socketio`), `psutil`, `keyring` (tùy chọn).
* `pip install -r requirements.txt`.
* Tạo cấu trúc thư mục `src/` (với `modules/`, `utils/`), `config/`, `logs/`, `storage/`.
* Tạo `src/main.py`, `src/agent.py`.

**Bước 0.5: Tài liệu ban đầu**
* Viết `README.md` cơ bản cho từng phần.
* Phác thảo `docs/architecture.md`, `docs/setup.md`.

## Giai đoạn 1: Thiết Kế Database & Models Cốt Lõi

**Mục tiêu:** Định nghĩa cấu trúc DB và tạo models Sequelize.

**Bước 1.1: Định nghĩa Models Sequelize**
* Tạo/Sửa file `backend/src/database/models/user.model.js`: Định nghĩa model `User` với các trường, kiểu dữ liệu (`DataTypes`), ràng buộc.
* Tạo/Sửa file `backend/src/database/models/room.model.js`: Định nghĩa model `Room`.
* Tạo/Sửa file `backend/src/database/models/computer.model.js`: Định nghĩa model `Computer` (bao gồm `errors: DataTypes.JSONB`).
* Tạo/Sửa file `backend/src/database/models/userRoomAssignment.model.js`: Định nghĩa model liên kết.

**Bước 1.2: Định nghĩa Quan hệ**
* Trong `backend/src/database/models/index.js` (hoặc trong các file model), dùng `associate` method để định nghĩa các mối quan hệ: `User.belongsToMany(Room)`, `Room.belongsToMany(User)`, `Room.hasMany(Computer)`, `Computer.belongsTo(Room)`, etc.

**Bước 1.3: Tạo và Chạy Migrations**
* Chạy `npx sequelize-cli migration:generate --name create-users` (và tương tự cho `rooms`, `computers`, `user_room_assignments`).
* Chỉnh sửa các file migration vừa tạo trong `backend/database/migrations/` để định nghĩa chính xác cấu trúc bảng PostgreSQL (bao gồm cả kiểu `JSONB` cho `errors`).
* Chạy `npx sequelize-cli db:migrate`.

**Bước 1.4: (Tùy chọn) Tạo và Chạy Seeder**
* Tạo file seeder (vd: `backend/database/seeders/admin-user.js`).
* Chạy `npx sequelize-cli db:seed:all`.

## Giai đoạn 2: Xác thực & Phân quyền (User/Admin)

**Mục tiêu:** Xây dựng chức năng đăng nhập, xác thực JWT, phân quyền cơ bản.

**Bước 2.1: Backend - Xử lý Mật khẩu**
* Trong `user.model.js`, thêm instance methods `validPassword(password)` (dùng `bcrypt.compare`) và hook `beforeCreate`, `beforeUpdate` để hash password (dùng `bcrypt.hash`).

**Bước 2.2: Backend - Service Xác thực**
* Tạo `src/services/auth.service.js`. Viết hàm `login(username, password)`: tìm user bằng model `User`, gọi `user.validPassword()`, nếu đúng thì tạo JWT (dùng `jsonwebtoken.sign`) chứa `id`, `username`, `role`.

**Bước 2.3: Backend - Controller & Route Xác thực**
* Tạo `src/controllers/auth.controller.js` với hàm `handleLogin`, `handleGetMe`.
* Tạo `src/routes/auth.routes.js`, định nghĩa route `POST /login`, `GET /me`. Gắn controller tương ứng. Đăng ký router này trong `app.js`.

**Bước 2.4: Backend - Middleware Xác thực/Phân quyền**
* Tạo `src/middleware/authJwt.js`: Viết hàm `verifyToken` (xác minh JWT, tìm user bằng `User.findByPk`, gắn `req.user`).
* Tạo `src/middleware/authAdmin.js`: Viết hàm `isAdmin` (kiểm tra `req.user.role === 'admin'`).
* Tạo `src/middleware/authRoomAccess.js` (để trống logic kiểm tra assignment).
* Áp dụng `verifyToken` cho route `GET /me`.

**Bước 2.5: Backend - User CRUD (Admin)**
* Tạo `UserService`, `UserController`, `UserRoutes`. Triển khai các hàm CRUD cơ bản (Create, Read All, Read One, Update role/status, Delete/Inactivate) dùng model `User`. Áp dụng middleware `verifyToken` và `isAdmin` cho các route này.

**Bước 2.6: Frontend - Trang Login**
* Tạo `src/pages/LoginPage.jsx`. Xây dựng form đăng nhập.
* Tạo `src/services/auth.service.js` với hàm `login(username, password)` gọi API `/api/auth/login`.

**Bước 2.7: Frontend - Auth Context & State**
* Tạo `src/contexts/AuthContext.jsx`. Dùng `useState`, `useMemo`, `useCallback` để quản lý state `user`, `token`, `isAuthenticated`. Cung cấp hàm `loginAction`, `logoutAction`.
* Bọc `App` bằng `AuthProvider`.
* Trong `LoginPage`, gọi `loginAction` khi submit form thành công (lưu token vào `localStorage`).

**Bước 2.8: Frontend - Protected Routes & Logout**
* Tạo `src/router/ProtectedRoute.jsx` đọc state từ `AuthContext`.
* Cập nhật `src/router/index.jsx` để dùng `ProtectedRoute`.
* Tạo `src/services/api.js` (cấu hình Axios instance), thêm interceptor để gắn header `Authorization`.
* Triển khai `logoutAction` (xóa token, reset context). Thêm nút Logout vào `layout/header`.

## Giai đoạn 3: Quản lý Room & Computer Cơ bản (Admin)

**Mục tiêu:** Xây dựng chức năng quản lý Room, gán User, quản lý Computer cơ bản.

**Bước 3.1: Backend - Room API**
* Tạo `RoomService`, `RoomController`, `RoomRoutes`.
* Triển khai API `POST /api/rooms` (Admin). 
* Cấu trúc JSON của trường `layout` cần tuân theo định dạng:
  ```json
  {
      "columns": "integer - Số máy tính theo chiều ngang (trục X)",
      "rows": "integer - Số máy tính theo chiều dọc (trục Y)"
  }
  ```
  Cấu trúc này định nghĩa lưới máy tính trong phòng với số lượng tối đa là `columns` × `rows` máy tính. Mỗi máy tính sẽ có tọa độ `pos_x` và `pos_y` tương ứng với vị trí của nó trong lưới.
* Triển khai API `GET /api/rooms` (Admin/User - lọc theo quyền). Logic lọc: Nếu admin, lấy hết. Nếu user, tìm các `roomId` từ `UserRoomAssignment` của user đó, rồi lấy các room tương ứng. Sử dụng `where` và `Op.like` của Sequelize cho filter `?name`.
* Triển khai API `GET /api/rooms/:id` (Admin/User - kiểm tra quyền). Dùng `include` của Sequelize để lấy danh sách computers thuộc room.
* Triển khai API `PUT /api/rooms/:id` (Admin).
* Triển khai API `DELETE /api/rooms/:id` (Admin).

**Bước 3.2: Backend - Assignment API**
* Trong `RoomController` hoặc controller riêng, triển khai API `POST /api/rooms/:roomId/assign`, `DELETE /api/rooms/:roomId/unassign`, `GET /api/rooms/:roomId/users` (Admin). Thao tác trên model `UserRoomAssignment`.

**Bước 3.3: Backend - Computer API (Admin)**
* Tạo `ComputerService`, `ComputerController`, `ComputerRoutes`.
* Triển khai API `PUT /api/computers/:id` (Admin - cập nhật name, room, pos).
* Triển khai API `DELETE /api/computers/:id` (Admin).
* Triển khai API `GET /api/computers/:id` (Admin/User - kiểm tra quyền room).
* Triển khai API `GET /api/computers` (Admin/User - lọc theo quyền room, hỗ trợ filter cơ bản).

**Bước 3.4: Frontend - Room Management (Admin)**
* Tạo `pages/Admin/RoomManagementPage.jsx`.
* Tạo `components/room/RoomList.jsx`, `components/room/RoomForm.jsx`.
* Tạo `services/room.service.js`. Gọi API Room CRUD.

**Bước 3.5: Frontend - User Management (Admin)**
* Tạo `pages/Admin/UserManagementPage.jsx`.
* Tạo `components/admin/UserList.jsx`, `components/admin/UserForm.jsx`.
* Tạo `services/user.service.js`. Gọi API User CRUD.

**Bước 3.6: Frontend - Assignment UI (Admin)**
* Trong trang chi tiết Room hoặc User, thêm component `components/admin/AssignmentComponent.jsx` để gọi API gán/gỡ user.

**Bước 3.7: Frontend - Computer List**
* Tạo `components/computer/ComputerList.jsx`. Gọi API `GET /api/computers` (có thể lọc theo room ID). Hiển thị danh sách. Tích hợp vào `RoomDetailPage`.

## Giai đoạn 4: Đăng ký & Xác thực Agent (MFA + Token)

**Mục tiêu:** Triển khai luồng đăng ký Agent mới và xác thực token.

**Bước 4.1: Backend - Agent API Routes & Controller**
* Tạo `routes/agent.routes.js`. Định nghĩa `POST /identify`, `POST /verify-mfa`.
* Tạo `controllers/agent.controller.js` với hàm `handleIdentifyRequest`, `handleVerifyMfa`.

**Bước 4.2: Backend - MFA Service**
* Tạo `services/mfa.service.js`. Hàm `generateAndStoreMfa` (dùng `otp-generator`, lưu vào `node-cache` với TTL). Hàm `verifyMfa` (lấy từ cache, so sánh, xóa).

**Bước 4.3: Backend - Identify Logic**
* Trong `handleIdentifyRequest`, gọi `computerService.findComputerByAgentId`. Nếu mới, gọi `mfaService.generateAndStoreMfa`, gọi `websocketService.notifyAdminsNewMfa`, trả `{status: 'mfa_required'}`. Nếu cũ, trả `{status: 'authentication_required'}`.

**Bước 4.4: Backend - Verify MFA & Token Logic**
* Trong `handleVerifyMfa`, gọi `mfaService.verifyMfa`. Nếu true, gọi `computerService.registerOrUpdateAgent` (tạo token, hash, tạo/update record DB), gọi `websocketService.notifyAdminsAgentRegistered`, trả `{agentToken: plainToken}`. Nếu false, trả lỗi 401.

**Bước 4.5: Backend - Agent Token Middleware (HTTP)**
* Tạo `middleware/authAgentToken.js`. Hàm `verifyAgentToken` (đọc header, gọi `computerService.verifyAgentToken` - hàm này tìm computer, so sánh hash, trả về `computerId` nếu hợp lệ). Gắn `req.computerId`.

**Bước 4.6: Backend - Agent WS Auth Handler**
* Tạo `sockets/agent.handler.js`. Hàm `handleAuthenticateWs(socket, payload)` (nhận event `agent:authenticate_ws`). Gọi `computerService.verifyAgentToken`. Nếu thành công, lưu `socket.data`, gọi `websocketService.registerAgentSocket`, emit `agent:ws_auth_success`. Ngược lại, emit `agent:ws_auth_failed`, disconnect.

**Bước 4.7: Agent (Python) - HTTP Calls**
* Trong `modules/http_client.py`, viết hàm `identify_agent`, `verify_mfa`.

**Bước 4.8: Agent (Python) - MFA Input**
* Trong `modules/mfa_handler.py`, viết hàm `prompt_for_mfa` (dùng `input()`).

**Bước 4.9: Agent (Python) - Token Storage**
* Trong `modules/token_manager.py`, viết hàm `save_token`, `load_token` (dùng file hoặc `keyring`).

**Bước 4.10: Agent (Python) - WS Client**
* Trong `modules/ws_client.py`, viết class/hàm `connect_and_authenticate` (kết nối WS, gửi `agent:authenticate_ws`).

**Bước 4.11: Agent (Python) - Main Logic**
* Trong `agent.py`, phối hợp các bước: load token, nếu không có thì gọi `identify`, xử lý response, gọi `prompt MFA`, gọi `verify`, save token. Sau đó gọi `ws_client.connect_and_authenticate`.

**Bước 4.12: Frontend - Admin Notifications**
* Trong `SocketContext`, lắng nghe `admin:new_agent_mfa`, `admin:agent_registered`. Hiển thị thông báo.

**Bước 4.13: Frontend - Computer Management Update**
* Cập nhật trang quản lý Computer (Admin) để có thể sửa thông tin (tên, phòng) cho các máy mới đăng ký.

## Giai đoạn 5: Trạng thái Real-time & Thực thi Lệnh

**Mục tiêu:** Hiển thị trạng thái real-time và cho phép gửi lệnh.

**Bước 5.1: Backend - Agent Status API**
* Tạo route `PUT /api/agent/status` trong `agent.routes.js`.
* Trong `agent.controller.js`, tạo `handleStatusUpdate`. Áp dụng middleware `authAgentToken`.
* Trong `handleStatusUpdate`, gọi `websocketService.updateRealtimeCache(req.computerId, req.body.cpu, req.body.ram)`. Gọi `computerService.updateLastSeen(req.computerId)`. Gọi `websocketService.broadcastStatusUpdate(req.computerId)`.

**Bước 5.2: Backend - WS Online/Offline Logic**
* Trong `agent.handler.js` (`handleAuthenticateWs` thành công): Gọi `websocketService.updateRealtimeCache` (set status online), `websocketService.broadcastStatusUpdate`.
* Trong `connection.handler.js` (`handleDisconnect`): Gọi `websocketService.handleAgentDisconnect(socket.id)` (hàm này tìm `computerId`, cập nhật cache offline, broadcast).

**Bước 5.3: Backend - Command API**
* Tạo route `POST /api/computers/:id/command` trong `computer.routes.js`.
* Trong `computer.controller.js`, tạo `handleSendCommand`. Áp dụng `authJwt`, `authRoomAccess`.
* Logic `handleSendCommand`: Tạo `commandId`, lưu pending command (dùng `websocketService`), gọi `websocketService.sendCommandToAgent(computerId, command, commandId)`.

**Bước 5.4: Backend - Command Result API**
* Tạo route `POST /api/agent/command-result` trong `agent.routes.js`.
* Trong `agent.controller.js`, tạo `handleCommandResult`. Áp dụng `authAgentToken`.
* Logic `handleCommandResult`: Gọi `websocketService.notifyCommandCompletion(commandId, result)`.

**Bước 5.5: Agent (Python) - Periodic Status**
* Trong `agent.py`, tạo `threading.Timer` hoặc dùng `schedule` để chạy hàm gửi status mỗi 30s. Hàm này gọi `system_info.get_stats()` và `http_client.update_status()`.

**Bước 5.6: Agent (Python) - Command Handling**
* Trong `ws_client.py`, định nghĩa hàm callback `on_command_execute(data)`.
* Trong callback, gọi `command_executor.run_command(data['command'])`.

**Bước 5.7: Agent (Python) - Send Result**
* Sau khi `run_command` xong, gọi `http_client.send_command_result(commandId, stdout, stderr, exitCode)`.

**Bước 5.8: Frontend - Status Display**
* Trong `SocketContext`, xử lý `computer:status_updated`. Cập nhật state chung hoặc state của component liên quan.
* Các component (`ComputerIcon`, `ComputerCard`, `ComputerDetailPage`) đọc state và hiển thị đúng trạng thái/CPU/RAM.

**Bước 5.9: Frontend - Send Command**
* Tạo `components/computer/CommandInput.jsx`.
* Khi submit, gọi `computerService.sendCommand(computerId, command)`.

**Bước 5.10: Frontend - Display Result**
* Trong `SocketContext`, xử lý `command:completed`. Tìm cách hiển thị kết quả (vd: thông báo, modal, khu vực output riêng).

## Giai đoạn 6: Báo cáo Lỗi & Lọc API

**Mục tiêu:** Thêm chức năng quản lý lỗi và lọc API.

**Bước 6.1: Backend - Error Reporting API**
* Tạo route `POST /api/computers/:id/errors` trong `computer.routes.js`.
* Tạo `controllers/error.controller.js` với `handleReportError`. Áp dụng `authJwt`, `authRoomAccess`.
* Trong `handleReportError`, gọi `computerService.addComputerError(computerId, userId, type, description)`. Hàm service này sẽ tạo object lỗi và cập nhật `JSONB` trong DB.

**Bước 6.2: Backend - Error Resolution API**
* Tạo route `PUT /api/computers/:computerId/errors/:errorId/resolve` trong `computer.routes.js`.
* Trong `error.controller.js`, tạo `handleResolveError`. Áp dụng `authJwt`, `authAdmin`.
* Trong `handleResolveError`, gọi `computerService.resolveComputerError(computerId, errorId, adminId, notes)`. Hàm service này tìm và cập nhật lỗi trong `JSONB`.

**Bước 6.3: Backend - API Filtering**
* Trong các controller GET danh sách (`user.controller`, `room.controller`, `computer.controller`), đọc `req.query`.
* Truyền các tham số lọc vào các hàm service tương ứng (`userService.findAll`, `roomService.findAll`, `computerService.findAll`).
* Trong các hàm service, xây dựng đối tượng `where` của Sequelize dựa trên các tham số lọc (dùng `Op.like`, `Op.eq`, `Op.in`, toán tử `JSONB`...).

**Bước 6.4: Frontend - Error Reporting UI**
* Tạo `components/computer/ReportErrorForm.jsx`.
* Tích hợp vào `ComputerDetailPage` hoặc `ComputerCard`. Gọi `computerService.reportError`.

**Bước 6.5: Frontend - Error Display UI**
* Tạo `components/computer/ErrorList.jsx`.
* Trong `ComputerDetailPage`, gọi API lấy chi tiết computer (đã bao gồm `errors`), truyền `errors` cho `ErrorList`.
* Cập nhật `ComputerIcon/Card` để hiển thị chỉ báo lỗi (`has_active_errors`).

**Bước 6.6: Frontend - Error Resolution UI (Admin)**
* Trong `ErrorList`, thêm nút "Resolve" cho Admin. Gọi `computerService.resolveError`. Cập nhật UI sau khi thành công.

**Bước 6.7: Frontend - Filter UI**
* Thêm các input/select vào `UserManagementPage`, `RoomListPage`, `ComputerListPage`.
* Quản lý state cho các filter. Khi state thay đổi, gọi lại hàm lấy danh sách từ service với các tham số filter mới.

## Giai đoạn 7: Hoàn thiện UI/UX, Kiểm thử & Triển khai

**Mục tiêu:** Tinh chỉnh, kiểm thử và triển khai.

**Bước 7.1: Frontend - UI/UX:** Rà soát, chỉnh sửa styling (Tailwind), đảm bảo responsive, thêm loading/error states. Triển khai `RoomLayout.jsx` vẽ sơ đồ phòng.
**Bước 7.2: Backend - Testing:** Viết unit test cho services, middleware. Viết integration test cho API routes (dùng Supertest).
**Bước 7.3: Backend - Optimization & Security:** Kiểm tra các câu lệnh SQL (Sequelize log), thêm index DB nếu cần. Rà soát bảo mật.
**Bước 7.4: Agent (Python) - Robustness:** Thêm logging chi tiết (logging module). Xử lý exception kỹ lưỡng. Test thực thi lệnh.
**Bước 7.5: Agent (Python) - Packaging:** Dùng `pyinstaller` tạo file `.exe`.
**Bước 7.6: Deployment Prep:** Tạo Dockerfile cho Backend, Frontend. Thiết lập CI/CD (Github Actions, Gitlab CI,...).
**Bước 7.7: Deploy:** Chọn cloud/server. Deploy DB (PostgreSQL). Deploy Backend container. Deploy Frontend static files. Cấu hình Nginx/Load Balancer, HTTPS.
**Bước 7.8: Agent Deployment:** Thực hiện cài đặt Agent lên máy client theo kế hoạch.
**Bước 7.9: Documentation:** Hoàn thiện `README.md`, viết hướng dẫn sử dụng, tài liệu API trong `docs/`.
