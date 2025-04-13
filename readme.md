# Kế Hoạch Phát Triển Hệ Thống Quản Lý Máy Tính

Dự án này nhằm mục đích tạo ra một ứng dụng web để quản lý và giám sát các máy tính trong công ty, được tổ chức theo phòng (Room), với khả năng gửi lệnh (qua WebSocket), theo dõi trạng thái real-time (online/offline xác định qua WS, %CPU/%RAM Agent gửi qua HTTP), hiển thị vị trí trực quan, hệ thống phân quyền người dùng (Admin, User), cơ chế đăng ký Agent an toàn (MFA/Token), chức năng báo cáo và xử lý lỗi máy tính, và khả năng lọc dữ liệu trên các API danh sách. Agent sẽ được phát triển bằng Python.

## 1. Kiến Trúc Hệ Thống

* **Agent:** Chạy trên máy Windows. Thu thập thông tin hệ thống (%CPU, %RAM). Giao tiếp với Backend chủ yếu qua HTTP(S) để đăng ký, gửi trạng thái CPU/RAM, gửi kết quả lệnh (xác thực bằng Agent Token). Duy trì kết nối WebSocket chỉ để nhận lệnh từ Backend (xác thực bằng Agent Token). Trạng thái online/offline được suy ra từ kết nối WS này.
* **Backend (Node.js):** Trung tâm xử lý. Xử lý request HTTP(S) từ Agent và Frontend. Quản lý kết nối WebSocket với Agent (xác thực, gửi lệnh) và Frontend (gửi MFA, broadcast status, kết quả lệnh). Quản lý logic nghiệp vụ, phân quyền, giao tiếp DB (lưu lỗi vào JSONB). Quản lý bộ nhớ đệm trạng thái real-time. Xử lý tham số lọc API.
* **Frontend (React):** Giao diện web. Hiển thị giao diện theo vai trò. Hiển thị thông báo MFA (Admin). Hiển thị trạng thái máy tính real-time và thông tin lỗi (nhận qua WS). Gửi yêu cầu HTTP(S) tới Backend (đăng nhập, quản lý, báo lỗi, xử lý lỗi, yêu cầu lệnh). Cung cấp bộ lọc và gửi tham số lọc tới API.
* **Database (Ví dụ: PostgreSQL):** Lưu trữ dữ liệu cấu hình (users, rooms, computers, agent_token_hash, user assignments, errors JSONB).

## 2. Lựa Chọn Công Nghệ Chi Tiết

* **Backend:** Node.js, Express.js, Socket.IO, PostgreSQL (hoặc MongoDB), Sequelize (hoặc Mongoose), JWT, bcrypt, node-cache (hoặc Redis), otp-generator, crypto/uuid. Middleware xác thực Agent Token HTTP header. Thư viện xử lý query parameters (vd: có sẵn trong Express).
* **Frontend:** React, Vite, Tailwind CSS, React Router DOM, Socket.IO Client, axios. Context API/Zustand/Redux. CSS Positioning/SVG/Canvas. Thư viện quản lý form (vd: React Hook Form).
* **Agent:** Python , `requests`, `websocket-client` (hoặc `python-socketio`), `psutil`, `keyring` (tùy chọn), `pyinstaller` / `cx_Freeze` (đóng gói). Cơ chế lưu trữ token an toàn.

## 3. Thiết Kế Cơ Sở Dữ Liệu Chi Tiết (Ví dụ với PostgreSQL)

* **Bảng `users`:**
    * `id`: SERIAL PRIMARY KEY
    * `username`: VARCHAR(255) UNIQUE NOT NULL
    * `password_hash`: VARCHAR(255) NOT NULL
    * `role`: VARCHAR(50) NOT NULL CHECK (role IN ('admin', 'user'))
    * `is_active`: BOOLEAN DEFAULT true
    * `created_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
    * `updated_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
* **Bảng `rooms`:**
    * `id`: SERIAL PRIMARY KEY
    * `name`: VARCHAR(255) NOT NULL
    * `description`: TEXT
    * `layout`: JSONB - Định nghĩa cấu trúc và bố trí của phòng với định dạng:
      ```json
      {
        "width": "integer - Chiều rộng của phòng trong pixels",
        "height": "integer - Chiều cao của phòng trong pixels",
        "background": "string - Mã màu nền (ví dụ: '#f5f5f5')",
        "grid": {
          "columns": "integer - Số máy tính theo chiều ngang (trục X)",
          "rows": "integer - Số máy tính theo chiều dọc (trục Y)",
          "spacing_x": "integer - Khoảng cách ngang giữa các máy tính (pixels)",
          "spacing_y": "integer - Khoảng cách dọc giữa các máy tính (pixels)"
        }
      }
      ```
      Cấu trúc này cho phép tạo lưới máy tính với số lượng tối đa là `columns` × `rows` máy tính. Mỗi máy tính sẽ được định vị bằng tọa độ `pos_x` và `pos_y` trong bảng `computers`.
    * `created_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
    * `updated_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
* **Bảng `computers`:** (Cập nhật)
    * `id`: SERIAL PRIMARY KEY
    * `name`: VARCHAR(255) (NULL ban đầu)
    * `room_id`: INTEGER REFERENCES rooms(id) ON DELETE SET NULL (NULL ban đầu)
    * `pos_x`: INTEGER DEFAULT 0
    * `pos_y`: INTEGER DEFAULT 0
    * `ip_address`: VARCHAR(50)
    * `unique_agent_id`: VARCHAR(255) UNIQUE NOT NULL
    * `agent_token_hash`: VARCHAR(255) (NULL cho đến khi đăng ký thành công)
    * `last_update`: TIMESTAMPTZ
    * `os_info`: VARCHAR(255)
    * `total_ram`: BIGINT
    * `cpu_info`: VARCHAR(255)
    * `errors`: JSONB DEFAULT '[]'::jsonb (Lưu một mảng các đối tượng lỗi. Ví dụ cấu trúc một đối tượng lỗi: `{ "id": "uuid", "type": "string", "description": "text", "reported_by": "integer (user_id)", "reported_at": "timestamp", "status": "string ('active'|'resolved')", "resolved_by": "integer (user_id, optional)", "resolved_at": "timestamp (optional)" }`)
    * `created_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
    * `updated_at`: TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
* **Bảng `user_room_assignments`:**
    * `user_id`: INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE
    * `room_id`: INTEGER NOT NULL REFERENCES rooms(id) ON DELETE CASCADE
    * PRIMARY KEY (user_id, room_id)

## 4. Backend API Endpoints Chi Tiết (Đặc Tả) bao gồm cả các agent api

(Phần này giữ nguyên đặc tả chi tiết như đã trình bày ở phiên bản trước)

### 1. Xác thực User/Admin (Authentication)
* `POST /api/auth/login`
* `GET /api/auth/me`

### 2. Quản lý Users (Yêu cầu quyền Admin)
* `POST /api/users`
* `GET /api/users` (Hỗ trợ lọc: `?role=`, `?is_active=`, `?username=`)
* `PUT /api/users/:id`
* `DELETE /api/users/:id`

### 3. Quản lý Rooms
* `POST /api/rooms` (Admin Only)
* `GET /api/rooms` (Hỗ trợ lọc: `?name=`, `?assigned_user_id=`)
* `GET /api/rooms/:id`
* `PUT /api/rooms/:id` (Admin Only)
* `DELETE /api/rooms/:id` (Admin Only)

### 4. Quản lý Phân công Room (Yêu cầu quyền Admin)
* `POST /api/rooms/:roomId/assign`
* `DELETE /api/rooms/:roomId/unassign`
* `GET /api/rooms/:roomId/users`

### 5. Quản lý Computers (Bởi Admin/User Frontend)
* `GET /api/computers` (Hỗ trợ lọc: `?room_id=`, `?name=`, `?status=`, `?has_errors=`, `?unique_agent_id=`)
* `PUT /api/computers/:id` (Admin cập nhật chi tiết)
* `GET /api/computers/:id` (Response bao gồm `errors`)
* `DELETE /api/computers/:id` (Admin xóa computer)
* `POST /api/computers/:id/command` (Gửi yêu cầu thực thi lệnh)
* `POST /api/computers/:id/errors` (Báo cáo lỗi)
* `PUT /api/computers/:computerId/errors/:errorId/resolve` (Xử lý lỗi - Admin Only)

### 6. API Dành Riêng Cho Agent
* `POST /api/agent/identify`
* `POST /api/agent/verify-mfa`
* `PUT /api/agent/status` (Body: `cpu`, `ram`)
* `POST /api/agent/command-result` (Body: `commandId`, `stdout`, `stderr`, `exitCode`)

## 5. Các luồng hoạt động chi tiết (Elaborated)

### Luồng Đăng ký Agent (MFA qua HTTP):
1.  **Agent:** Gửi HTTP `POST /api/agent/identify` với body `{"unique_agent_id": "agent-uuid-123"}`.
2.  **Backend:**
    * Nhận request, trích xuất `unique_agent_id`.
    * Truy vấn DB: `SELECT id, agent_token_hash FROM computers WHERE unique_agent_id = 'agent-uuid-123'`.
    * **Nếu không tìm thấy record hoặc `agent_token_hash` là NULL:** (Agent mới)
        * Tạo mã MFA (vd: 6 chữ số, dùng `otp-generator`): `mfaCode = '123456'`.
        * Tạo timestamp hết hạn: `expiresAt = Date.now() + 5 * 60 * 1000` (5 phút).
        * Lưu tạm vào cache/map: `mfaStore['agent-uuid-123'] = { code: '123456', expires: expiresAt }`. (Cần cơ chế dọn dẹp cache hết hạn).
        * Lấy danh sách các `socket.id` của Admin đang online (từ `socket.data.role`).
        * Gửi sự kiện WS `admin:new_agent_mfa` tới từng Admin: `socket.emit('admin:new_agent_mfa', { unique_agent_id: 'agent-uuid-123', mfaCode: '123456' })`.
        * Trả về HTTP Response (200 OK): `{"status": "mfa_required"}`.
    * **Nếu tìm thấy record và `agent_token_hash` không NULL:** (Agent đã đăng ký)
        * Trả về HTTP Response (200 OK): `{"status": "authentication_required"}`.
3.  **Agent:** Nhận response.
    * Nếu `status === 'mfa_required'`: Hiển thị yêu cầu nhập MFA cho người dùng.
4.  **(Người dùng):** Nhận mã MFA từ Admin, nhập vào Agent.
5.  **Agent:** Gửi HTTP `POST /api/agent/verify-mfa` với body `{"unique_agent_id": "agent-uuid-123", "mfaCode": "user_entered_code"}`.
6.  **Backend:**
    * Nhận request, trích xuất `unique_agent_id`, `mfaCode`.
    * Lấy thông tin MFA đã lưu: `storedMfa = mfaStore['agent-uuid-123']`.
    * Kiểm tra:
        * `storedMfa` có tồn tại không?
        * `Date.now() < storedMfa.expires?` (Còn hạn không?)
        * `mfaCode === storedMfa.code?` (Mã có khớp không?)
    * **Nếu tất cả đều hợp lệ:**
        * Xóa MFA khỏi cache: `delete mfaStore['agent-uuid-123']`.
        * Tạo Agent Token mới (chuỗi ngẫu nhiên an toàn): `plainToken = crypto.randomBytes(32).toString('hex')`.
        * Hash token: `hashedToken = await bcrypt.hash(plainToken, 10)`.
        * Tạo record mới trong DB: `INSERT INTO computers (unique_agent_id, agent_token_hash, ...) VALUES ('agent-uuid-123', hashedToken, ...)` hoặc `UPDATE computers SET agent_token_hash = hashedToken WHERE unique_agent_id = 'agent-uuid-123'` (nếu record đã tồn tại nhưng chưa có hash). Lấy `computerId` mới/hiện có.
        * Gửi sự kiện WS `admin:agent_registered` tới tất cả Admin: `{ unique_agent_id: 'agent-uuid-123', computerId: newComputerId }`.
        * Trả về HTTP Response (200 OK): `{"agentToken": plainToken}`.
    * **Nếu không hợp lệ:**
        * Trả về HTTP Response (vd: 401 Unauthorized): `{"message": "Invalid or expired MFA code"}`.
7.  **Agent:** Nhận response.
    * Nếu thành công (có `agentToken`): Lưu token vào nơi an toàn (`tokenManager.save(agentToken)`). Tiến hành kết nối WebSocket.
    * Nếu thất bại: Báo lỗi cho người dùng.

### Luồng Xác thực Agent (Token qua HTTP Header và WS Event):

* **HTTP Request (vd: Status Update):**
    1.  **Agent:** Gửi `PUT /api/agent/status` với header `X-Agent-ID: agent-uuid-123`, `Authorization: Bearer <saved_agent_token>`, và body `{ cpu, ram }`.
    2.  **Backend (Middleware `authAgentToken`):**
        * Trích xuất `unique_agent_id` từ `X-Agent-ID` và `token` từ `Authorization`.
        * Kiểm tra sự tồn tại của cả hai header.
        * Truy vấn DB: `SELECT id, agent_token_hash FROM computers WHERE unique_agent_id = 'agent-uuid-123'`.
        * Nếu không tìm thấy record hoặc `agent_token_hash` là NULL: Trả lỗi 401 Unauthorized.
        * So sánh token: `isValid = await bcrypt.compare(token, record.agent_token_hash)`.
        * Nếu `isValid`: Gắn thông tin vào request: `req.computerId = record.id`, `req.unique_agent_id = 'agent-uuid-123'`. Gọi `next()`.
        * Nếu không `isValid`: Trả lỗi 401 Unauthorized.
    3.  **Backend (Controller):** Nếu middleware `next()` được gọi, xử lý request (cập nhật status).

* **WebSocket Connection:**
    1.  **Agent:** Kết nối tới WS server. Ngay sau khi connect, gửi event `agent:authenticate_ws` với payload `{ unique_agent_id: 'agent-uuid-123', agentToken: '<saved_agent_token>' }`.
    2.  **Backend (WS Handler/Middleware):**
        * Nhận event `agent:authenticate_ws`.
        * Truy vấn DB: `SELECT id, agent_token_hash FROM computers WHERE unique_agent_id = 'agent-uuid-123'`.
        * Nếu không tìm thấy hoặc hash là NULL: Gửi event `agent:ws_auth_failed`, ngắt kết nối.
        * So sánh token: `isValid = await bcrypt.compare(agentToken, record.agent_token_hash)`.
        * Nếu `isValid`:
            * Lưu thông tin vào socket: `socket.data.computerId = record.id`, `socket.data.unique_agent_id = 'agent-uuid-123'`, `socket.data.isAuthenticated = true`.
            * Lưu `socket.id` vào map: `agentCommandSockets[record.id] = socket.id`.
            * Gửi event `agent:ws_auth_success`.
            * (Trigger Luồng Cập nhật Online Status)
        * Nếu không `isValid`: Gửi event `agent:ws_auth_failed`, ngắt kết nối.

### Luồng Cập nhật Trạng thái (%CPU/%RAM):
1.  **Agent:** Định kỳ (30s), dùng `psutil` lấy `cpu = psutil.cpu_percent()`, `ram = psutil.virtual_memory().percent`.
2.  **Agent:** Gửi HTTP `PUT /api/agent/status` với header xác thực và body `{"cpu": cpu, "ram": ram}`.
3.  **Backend (HTTP Controller):**
    * Middleware `authAgentToken` xác thực thành công, `req.computerId` đã có.
    * Lấy `cpu`, `ram` từ `req.body`.
    * Cập nhật cache: `agentRealtimeStatus[req.computerId] = { ...agentRealtimeStatus[req.computerId], cpu: cpu, ram: ram }`.
    * Cập nhật DB: `UPDATE computers SET last_update = NOW() WHERE id = req.computerId`.
    * Trả về HTTP Response (204 No Content).
4.  **Backend (Logic sau HTTP Response hoặc dùng event emitter):**
    * Lấy trạng thái online hiện tại: `isOnline = !!agentCommandSockets[req.computerId]`.
    * Lấy `roomId` của computer từ DB (hoặc cache nếu có).
    * Lấy dữ liệu đầy đủ từ cache: `currentStatus = agentRealtimeStatus[req.computerId]`.
    * Tạo payload: `{ computerId: req.computerId, status: isOnline ? 'online' : 'offline', cpu: currentStatus.cpu, ram: currentStatus.ram }`.
    * Broadcast qua WS tới room của Frontend: `io.to('room_' + roomId).emit('computer:status_updated', payload)`.

### Luồng Cập nhật Trạng thái Online/Offline:

* **Online (Khi WS xác thực thành công):**
    1.  **Backend (WS Handler):** Sau khi `bcrypt.compare` thành công trong `agent:authenticate_ws`:
        * Lấy `computerId`, `roomId`.
        * Lấy CPU/RAM cuối cùng từ cache (nếu có): `lastCpu = agentRealtimeStatus[computerId]?.cpu`, `lastRam = agentRealtimeStatus[computerId]?.ram`.
        * Cập nhật cache: `agentRealtimeStatus[computerId] = { status: 'online', cpu: lastCpu, ram: lastRam }`.
        * Lưu `socket.id`: `agentCommandSockets[computerId] = socket.id`.
        * Broadcast WS: `io.to('room_' + roomId).emit('computer:status_updated', { computerId: computerId, status: 'online', cpu: lastCpu, ram: lastRam })`.

* **Offline (Khi WS ngắt kết nối):**
    1.  **Backend (WS disconnect Handler):**
        * Tìm `computerId` tương ứng với `socket.id` vừa ngắt kết nối (duyệt qua `agentCommandSockets`).
        * Nếu tìm thấy `computerId`:
            * Xóa khỏi map: `delete agentCommandSockets[computerId]`.
            * Lấy CPU/RAM cuối cùng từ cache: `lastCpu = agentRealtimeStatus[computerId]?.cpu`, `lastRam = agentRealtimeStatus[computerId]?.ram`.
            * Cập nhật cache: `agentRealtimeStatus[computerId] = { status: 'offline', cpu: lastCpu, ram: lastRam }`.
            * Lấy `roomId`.
            * Broadcast WS: `io.to('room_' + roomId).emit('computer:status_updated', { computerId: computerId, status: 'offline', cpu: lastCpu, ram: lastRam })`.
            * (Tùy chọn) Cập nhật `status_db` trong DB.

### Luồng Gửi và Nhận Lệnh:
1.  **Frontend:** User nhập lệnh, nhấn gửi -> Gọi `POST /api/computers/:id/command` với body `{"command": "user_command"}` và header JWT.
2.  **Backend (HTTP Controller):**
    * Xác thực JWT, kiểm tra quyền truy cập room cho computer `:id`.
    * Tạo `commandId = uuid.v4()`.
    * Lấy `userId` từ JWT.
    * Lưu tạm: `pendingCommands[commandId] = { userId: userId, computerId: computerId }` (Dùng cache/Redis với TTL).
    * Tìm `socketId = agentCommandSockets[computerId]`.
    * **Nếu `socketId` tồn tại:**
        * Gửi sự kiện WS tới Agent: `io.to(socketId).emit('command:execute', { command: "user_command", commandId: commandId })`.
        * Trả về HTTP Response (202 Accepted): `{"message": "Command sent", "commandId": commandId}`.
    * **Nếu `socketId` không tồn tại (Agent offline WS):**
        * Trả về HTTP Response (vd: 503 Service Unavailable): `{"message": "Agent is offline"}`.
3.  **Agent (WS Handler):**
    * Nhận sự kiện `command:execute` với `{ command, commandId }`.
    * Thực thi lệnh: `result = subprocess.run(...)`.
    * Lấy `stdout`, `stderr`, `exitCode` từ `result`.
4.  **Agent (HTTP Client):**
    * Gửi HTTP `POST /api/agent/command-result` với header xác thực Agent Token và body `{"commandId": commandId, "stdout": ..., "stderr": ..., "exitCode": ...}`.
5.  **Backend (HTTP Controller):**
    * Xác thực Agent Token.
    * Lấy `commandId` và kết quả từ `req.body`.
    * Tìm thông tin lệnh đang chờ: `commandInfo = pendingCommands[commandId]`.
    * **Nếu tìm thấy `commandInfo`:**
        * Lấy `userId = commandInfo.userId`, `computerId = commandInfo.computerId`.
        * Xóa khỏi bộ nhớ tạm: `delete pendingCommands[commandId]`.
        * Gửi sự kiện WS tới User: `io.to('user_' + userId).emit('command:completed', { commandId: commandId, computerId: computerId, result: { stdout: ..., stderr: ..., exitCode: ... } })`.
        * Trả về HTTP Response (204 No Content) cho Agent.
    * **Nếu không tìm thấy `commandInfo` (lỗi hoặc đã xử lý):**
        * Trả về HTTP Response (vd: 404 Not Found) cho Agent.

### Luồng Báo cáo Lỗi:
1.  **Frontend:** User nhập `type`/`description` -> Gọi `POST /api/computers/:id/errors` với body `{ type, description }` và header JWT.
2.  **Backend (HTTP Controller):**
    * Xác thực JWT, kiểm tra quyền truy cập room.
    * Lấy `userId` từ JWT.
    * Tạo `errorId = uuid.v4()`.
    * Tạo object lỗi: `newError = { id: errorId, type, description, reported_by: userId, reported_at: new Date(), status: 'active' }`.
    * Cập nhật DB (PostgreSQL ví dụ): `UPDATE computers SET errors = errors || jsonb_build_object('id', errorId, ...) WHERE id = computerId`. (Cần cú pháp chính xác để nối vào mảng JSONB).
    * Trả về HTTP Response (201 Created) với `newError`.

### Luồng Xử lý/Xóa Lỗi:
1.  **Frontend (Admin):** Nhấn nút Resolve cho lỗi `errorId` trên máy `computerId` -> Gọi `PUT /api/computers/:computerId/errors/:errorId/resolve` với header JWT Admin.
2.  **Backend (HTTP Controller):**
    * Xác thực JWT, kiểm tra quyền Admin.
    * Lấy `adminId` từ JWT.
    * Đọc mảng `errors` từ computer `:computerId`.
    * Tìm `index` của object lỗi có `id === errorId` và `status === 'active'` trong mảng.
    * **Nếu tìm thấy tại `index`:**
        * Tạo object lỗi đã cập nhật: `updatedError = { ...errors[index], status: 'resolved', resolved_by: adminId, resolved_at: new Date() }`.
        * Cập nhật mảng trong DB (PostgreSQL ví dụ): `UPDATE computers SET errors = jsonb_set(errors, '{index}', to_jsonb(updatedError)) WHERE id = computerId`. (Cần cú pháp chính xác để cập nhật phần tử trong mảng JSONB).
        * Trả về HTTP Response (200 OK) với `updatedError`.
    * **Nếu không tìm thấy:** Trả về lỗi 404 Not Found.

## 6. Phát triển backend chi tiết

* **Thiết lập dự án Node.js:** Sử dụng Express.js, cài đặt các thư viện cần thiết (Sequelize/Mongoose, Socket.IO, bcrypt, JWT, node-cache/Redis client, otp-generator, ...). Cấu trúc thư mục (routes, controllers, models, services, middleware, config...).
* **Kết nối Database:** Cấu hình và khởi tạo kết nối tới PostgreSQL/MongoDB. Định nghĩa Models/Schemas tương ứng với thiết kế DB.
* **Triển khai Xác thực & Phân quyền:** API đăng nhập, tạo JWT. Middleware xác thực JWT cho Frontend. Middleware kiểm tra quyền Admin. Middleware kiểm tra quyền truy cập Room. Middleware xác thực Agent Token cho API Agent.
* **Triển khai API Routes:** Tạo routers/controllers cho các nhóm chức năng (auth, users, rooms, computers, agent, errors). Áp dụng middleware phù hợp. Viết logic xử lý trong controllers. Triển khai logic lọc trong các API GET danh sách.
* **Triển khai WebSocket (Socket.IO):** Tích hợp server. Middleware xác thực WS (JWT Frontend, Agent Token Agent). Quản lý kết nối, rooms (user, room, admin). Quản lý map `agentCommandSockets`. Quản lý cache `agentRealtimeStatus`. Xử lý/Phát các sự kiện WS liên quan (authenticate, disconnect, subscribe, status update, command, MFA, registration).
* **Triển khai Logic Nghiệp vụ:** Xử lý đăng ký MFA. Xử lý cập nhật trạng thái CPU/RAM từ HTTP và broadcast WS. Xử lý gửi/nhận lệnh (HTTP request -> WS send -> Agent execute -> HTTP result -> WS notify). Xử lý báo cáo lỗi (tạo object, cập nhật JSONB). Xử lý/xóa lỗi (tìm, cập nhật JSONB).
* **Logging và Error Handling:** Tích hợp hệ thống logging (vd: Winston), xây dựng cơ chế xử lý lỗi tập trung.

## 7. Phát triển frontend chi tiết

* **Thiết lập dự án React:** Sử dụng Vite, cài đặt React Router DOM, Axios, Socket.IO Client, Tailwind CSS.
* **Cấu trúc thư mục:** Sắp xếp components, pages, services, contexts, hooks, assets...
* **Routing:** Thiết lập các routes (login, dashboard, rooms, room detail, admin pages...). Sử dụng ProtectedRoute.
* **Xác thực (Authentication):** Trang Login, gọi API login. Lưu JWT. Auth Context. Gửi JWT header. Xử lý đăng xuất.
* **Giao tiếp Backend:** Sử dụng Axios/fetch gọi API HTTP. Thiết lập kết nối Socket.IO, xác thực WS. Socket Context.
* **Quản lý State:** Context API (Auth, Socket), useState/useReducer hoặc Zustand/Redux.
* **Xây dựng Components & Pages:** Tạo UI components tái sử dụng. Xây dựng pages. Component RoomDetailPage/ComputerCard/ComputerIcon hiển thị chỉ báo lỗi. Trang chi tiết Computer hiển thị danh sách lỗi. Component Form Báo Lỗi. Chức năng Xử lý Lỗi (Admin). Thêm Bộ lọc (Filters) vào các trang danh sách.
* **Xử lý WebSocket Events:** Lắng nghe `computer:status_updated` (cập nhật online/offline, CPU/RAM, chỉ báo lỗi nếu cần). Lắng nghe `command:completed`. Admin lắng nghe `admin:new_agent_mfa`, `admin:agent_registered`.
* **Styling:** Sử dụng Tailwind CSS.

## 8. Phát Triển Agent Chi Tiết (Python)

* **Thiết lập dự án Python:** Môi trường ảo, `requirements.txt` (`requests`, `websocket-client`/`python-socketio`, `psutil`, `keyring` (tùy chọn)).
* **Khởi động & Cấu hình:** Đọc cấu hình (địa chỉ server). Tạo/Đọc `unique_agent_id`. Kiểm tra Agent Token đã lưu.
* **Luồng Đăng ký / Xác thực ban đầu (HTTP):** Sử dụng `requests.post` để gọi `/api/agent/identify`. Xử lý response. Nếu `mfa_required`, dùng `input()` để nhận mã, gọi `requests.post` tới `/api/agent/verify-mfa`. Nếu thành công, lưu token nhận được bằng module `tokenManager`.
* **Kết nối và Xác thực WebSocket:** Sử dụng `websocket.create_connection` (từ `websocket-client`) hoặc client của `python-socketio`. Sau khi kết nối, gửi message/event `agent:authenticate_ws` chứa `unique_agent_id` và `agentToken`. Xử lý phản hồi xác thực từ server. Chạy luồng nhận tin nhắn WS trong một thread riêng biệt.
* **Gửi Trạng thái định kỳ (HTTP):** Sử dụng `threading.Timer` hoặc `schedule` để lặp lại. Dùng `psutil.cpu_percent()` và `psutil.virtual_memory().percent` để lấy thông tin. Gửi `requests.put` tới `/api/agent/status` với header xác thực.
* **Lắng nghe và Thực thi Lệnh (WebSocket):** Trong thread lắng nghe WS, khi nhận được message/event `command:execute`, lấy `command` và `commandId`. Sử dụng `subprocess.run(command, shell=True, capture_output=True, text=True, check=False)` để thực thi. Lưu ý `shell=True` tiềm ẩn rủi ro bảo mật, cần xem xét kỹ hoặc tìm cách thực thi an toàn hơn nếu có thể. Thu thập `stdout`, `stderr`, `returncode`.
* **Gửi Kết quả Lệnh (HTTP):** Sau khi lệnh xong, gọi `requests.post` tới `/api/agent/command-result` với kết quả và header xác thực.
* **Lưu trữ Token An toàn:** Sử dụng file với quyền truy cập hạn chế, hoặc thư viện `keyring` để tích hợp với hệ thống quản lý credential của OS.
* **Xử lý Lỗi và Kết nối lại:** Sử dụng `try...except` để bắt lỗi mạng (HTTP, WS), lỗi thực thi lệnh. Triển khai logic kết nối lại WS với cơ chế backoff. Sử dụng module `logging` để ghi log.
* **Đóng gói:** Sử dụng `pyinstaller your_script.py --onefile --noconsole` (cho agent chạy nền) để tạo file `.exe`.

Kế hoạch này cung cấp lộ trình chi tiết qua từng giai đoạn phát triển. Thứ tự và mức độ ưu tiên của các chức năng có thể được điều chỉnh tùy theo yêu cầu thực tế.
