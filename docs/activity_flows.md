# Chi Tiết Các Luồng Hoạt Động Hệ Thống

Tài liệu này mô tả chi tiết các bước thực hiện, file liên quan và API/Event cho từng luồng hoạt động chính của hệ thống quản lý máy tính.

## 1. Luồng Đăng ký Agent (MFA qua HTTP)

**Mục tiêu:** Agent mới kết nối lần đầu, xác thực qua MFA được Admin cung cấp, nhận và lưu Agent Token.

### Agent: Gửi Định danh
* **Component:** Agent (Python)
* **Action:** Kiểm tra token đã lưu (`token_manager.load_token`). Nếu không có, chuẩn bị và gửi request định danh.
* **Files/Modules & Functions:** 
  * `src/main.py` (điều phối)
  * `src/auth/token_manager.py` (`load_token`) 
  * `src/communication/http_client.py` (`identify_agent`)
* **API/Event:** Gọi HTTP `POST /api/agent/identify`
* **Request Body:** `{"unique_agent_id": "agent-uuid-123"}`

### Backend: Tiếp nhận Định danh & Khởi tạo MFA
* **Component:** Backend (Node.js)
* **Action:** Route xử lý request, gọi controller.
* **Files/Modules & Functions:** 
  * `src/routes/agent.routes.js`
  * `src/controllers/agent.controller.js` (`handleIdentifyRequest`)
* **API/Event:** Xử lý `POST /api/agent/identify`
* **Action (Controller):** Gọi service để kiểm tra agent và khởi tạo MFA nếu cần.
* **Files/Modules & Functions:** 
  * `src/controllers/agent.controller.js` (`handleIdentifyRequest`)
  * `src/services/computer.service.js` (`findComputerByAgentId`)
  * `src/services/mfa.service.js` (`generateAndStoreMfa`)
  * `src/services/websocket.service.js` (`notifyAdminsNewMfa`)
* **Action (Service `findComputerByAgentId`):** Truy vấn DB `computers` theo `unique_agent_id`. Trả về record hoặc null.
* **Action (Service `generateAndStoreMfa`):** Nếu agent mới: Tạo mã MFA (6 chữ số, 5 phút), lưu vào cache (node-cache hoặc Redis) với key là `unique_agent_id`. Trả về mã MFA.
* **Action (Service `notifyAdminsNewMfa`):** Lấy danh sách socket Admin đang online, gửi event `admin:new_agent_mfa` với payload `{ unique_agent_id, mfaCode, expiresAt }`.
* **Action (Controller):** Dựa vào kết quả kiểm tra và tạo MFA, quyết định response.
* **Response HTTP:** `{"status": "mfa_required"}` hoặc `{"status": "authentication_required"}`

### Frontend: Admin Nhận Mã MFA
* **Component:** Frontend (React)
* **Action:** `SocketContext` lắng nghe và nhận event, cập nhật state hoặc gọi callback để hiển thị thông báo.
* **Files/Modules & Functions:** 
  * `src/contexts/SocketContext.jsx` (`useEffect` để lắng nghe)
  * `src/components/admin/MfaNotification.jsx` (Component hiển thị thông báo MFA).
* **API/Event:** Lắng nghe WS Event `admin:new_agent_mfa`.

### Agent: Nhận Yêu cầu & Gửi MFA
* **Component:** Agent (Python)
* **Action:** Nhận response `mfa_required`. Yêu cầu người dùng nhập mã MFA (qua console hoặc UI đơn giản). Gửi mã MFA đã nhập lên Backend.
* **Files/Modules & Functions:** 
  * `src/main.py` (xử lý response)
  * `src/auth/mfa_handler.py` (`prompt_for_mfa`) 
  * `src/communication/http_client.py` (`verify_mfa`).
* **API/Event:** Gọi HTTP `POST /api/agent/verify-mfa`
* **Request Body:** `{"unique_agent_id": "agent-uuid-123", "mfaCode": "user_entered_code"}`

### Backend: Xác minh MFA & Cấp Token
* **Component:** Backend (Node.js)
* **Action:** Route xử lý request, gọi controller.
* **Files/Modules & Functions:** 
  * `src/routes/agent.routes.js`
  * `src/controllers/agent.controller.js` (`handleVerifyMfa`)
* **API/Event:** Xử lý `POST /api/agent/verify-mfa`
* **Action (Controller):** Gọi service để xác minh MFA và tạo token/record.
* **Files/Modules & Functions:** 
  * `src/controllers/agent.controller.js` (`handleVerifyMfa`)
  * `src/services/mfa.service.js` (`verifyMfa`)
  * `src/services/computer.service.js` (`registerNewAgent` hoặc `updateAgentToken`)
  * `src/services/websocket.service.js` (`notifyAdminsAgentRegistered`)
* **Action (Service `verifyMfa`):** Lấy MFA đã lưu từ cache, so sánh mã, kiểm tra thời hạn. Xóa MFA khỏi cache nếu hợp lệ. Trả về boolean.
* **Action (Service `registerNewAgent/updateAgentToken`):** Nếu `verifyMfa` trả về true: Tạo token mới (`crypto.randomBytes`), hash token (`bcrypt.hash`), tạo record Computer mới hoặc cập nhật `agent_token_hash` cho record cũ, lưu vào DB. Trả về token gốc (plain token).
* **Action (Service `notifyAdminsAgentRegistered`):** Gửi event `admin:agent_registered` tới Admin qua WS với payload `{ unique_agent_id, computerId }`.
* **Action (Controller):** Dựa vào kết quả xác minh, trả về response phù hợp.
* **Response HTTP:** `{"agentToken": "plain_agent_token_string"}` (200 OK) hoặc lỗi 401/400.

### Agent: Lưu Token & Kết nối WS
* **Component:** Agent (Python)
* **Action:** Xử lý response từ `/verify-mfa`. Nếu thành công, gọi hàm lưu token. Sau đó, khởi tạo kết nối WebSocket.
* **Files/Modules & Functions:** 
  * `src/main.py` (xử lý response)
  * `src/auth/token_manager.py` (`save_token`)
  * `src/communication/ws_client.py` (`connect`).

## 2. Luồng Xác thực Agent (Token)

**Mục tiêu:** Xác thực Agent cho các request HTTP và kết nối WS.

### Xác thực HTTP Request:
* **Component:** Agent (Python) -> Backend (Node.js Middleware)
* **Action:** Agent (trong `communication/http_client.py`) tự động thêm header `X-Agent-ID` và `Authorization: Bearer <token>` (lấy từ `auth/token_manager.load_token`) vào các request cần xác thực.
* **Files/Modules & Functions (Backend):** 
  * `src/middleware/authAgentToken.js` (`verifyAgentToken`): Trích xuất headers, gọi `computerService.verifyAgentToken(agentId, token)` (hàm này sẽ tìm computer, so sánh hash), nếu hợp lệ thì `req.computerId = ...; next()`, ngược lại trả lỗi 401.

### Xác thực Kết nối WebSocket:
* **Component:** Agent (Python) -> Backend (Node.js WS Handler)
* **Action:** Agent (`communication/ws_client.py`) sau khi kết nối WS thành công, gửi event `agent:authenticate_ws` với payload `{ unique_agent_id, agentToken }`.
* **Files/Modules & Functions (Backend):** 
  * `src/sockets/index.js` (handler `agent:authenticate_ws`): Nhận event, gọi `computerService.verifyAgentToken`, nếu thành công thì lưu `socket.data.computerId`, `socket.data.isAuthenticated = true`, gọi `websocketService.registerAgentSocket(computerId, socket.id)`, gửi lại `agent:ws_auth_success`. Nếu thất bại, gửi `agent:ws_auth_failed` và ngắt kết nối.

## 3. Luồng Cập nhật Trạng thái (%CPU/%RAM)

**Mục tiêu:** Agent gửi CPU/RAM, Backend cập nhật cache và broadcast.

### Agent: Thu thập & Gửi Status
* **Component:** Agent (Python)
* **Action:** Hàm chạy định kỳ trong `main.py` (dùng `threading.Timer` hoặc `schedule`).
* **Files/Modules & Functions:** 
  * `src/main.py` (scheduler)
  * `src/monitoring/system_monitor.py` (`get_cpu_usage`, `get_ram_usage`, `get_processes`)
  * `src/communication/http_client.py` (`update_status`).
* **API/Event:** Gọi HTTP `PUT /api/agent/status`
* **Request Body:** `{"cpu": ..., "ram": ..., "processes": [...] }`
* **Headers:** `X-Agent-ID`, `Authorization: Bearer <AgentToken>`

### Backend: Nhận Status & Broadcast
* **Component:** Backend (Node.js)
* **Action:** Middleware `authAgentToken` xác thực. Controller gọi service.
* **Files/Modules & Functions:** 
  * `src/middleware/authAgentToken.js`
  * `src/routes/agent.routes.js`
  * `src/controllers/agent.controller.js` (`handleStatusUpdate`)
  * `src/services/websocket.service.js` (`updateRealtimeCache`, `getAgentOnlineStatus`, `getComputerRoomId`, `broadcastStatusUpdate`)
  * `src/services/computer.service.js` (`updateLastSeen`).
* **Action (Service `updateRealtimeCache`):** Cập nhật giá trị `cpu`, `ram`, `processes` trong `agentRealtimeStatus[computerId]`.
* **Action (Service `updateLastSeen`):** Cập nhật trường `last_seen` trong DB.
* **Action (Controller/Service):** Sau khi cập nhật cache/DB, gọi `websocketService.broadcastStatusUpdate(computerId)` (hàm này sẽ lấy status online, cpu, ram, processes từ cache/map, lấy roomId, rồi `io.to(room).emit(...)`).
* **API/Event:** Xử lý `PUT /api/agent/status`. Gửi WS Event `computer:status_update` tới Frontend.
* **Response HTTP:** 204 No Content.

## 4. Luồng Cập nhật Trạng thái Online/Offline

**Mục tiêu:** Cập nhật và broadcast trạng thái online/offline dựa trên WS.

### Online (Khi WS xác thực thành công):
* **Component:** Backend (Node.js)
* **Action:** Trong handler `agent:authenticate_ws` (sau khi xác thực thành công).
* **Files/Modules & Functions:** 
  * `src/sockets/index.js` (handler `agent:authenticate_ws`)
  * `src/services/websocket.service.js` (`registerAgentSocket`, `updateRealtimeCache`, `broadcastStatusUpdate`)
  * `src/services/computer.service.js` (`getComputerRoomId`).
* **Action (Service):** Gọi `registerAgentSocket` để lưu `socket.id`. Gọi `updateRealtimeCache` để set `status: 'online'`. Gọi `broadcastStatusUpdate` để gửi trạng thái mới (online, cpu, ram) tới Frontend.

### Offline (Khi WS ngắt kết nối):
* **Component:** Backend (Node.js)
* **Action:** Trong handler sự kiện `disconnect`.
* **Files/Modules & Functions:** 
  * `src/sockets/index.js` (handler `disconnect`)
  * `src/services/websocket.service.js` (`findComputerIdBySocketId`, `unregisterAgentSocket`, `updateRealtimeCache`, `broadcastStatusUpdate`)
  * `src/services/computer.service.js` (`getComputerRoomId`, `updateComputerDbStatus` - tùy chọn).
* **Action (Service):** Tìm `computerId` từ `socket.id`. Gọi `unregisterAgentSocket` để xóa khỏi map. Gọi `updateRealtimeCache` để set `status: 'offline'`. Gọi `broadcastStatusUpdate` để gửi trạng thái mới (offline, cpu, ram) tới Frontend. (Tùy chọn gọi `updateComputerDbStatus`).

## 5. Luồng Gửi và Nhận Lệnh

**Mục tiêu:** Gửi lệnh từ Frontend, thực thi trên Agent, nhận kết quả.

### Frontend: Gửi Yêu cầu Lệnh
* **Component:** Frontend (React)
* **Action:** Gọi service function để gửi lệnh.
* **Files/Modules & Functions:** 
  * `src/components/computer/CommandInput.jsx` (lấy lệnh)
  * `src/services/computer.service.js` (`sendCommand`).
* **API/Event:** Gọi HTTP `POST /api/computers/:id/command`
* **Request Body:** `{"command": "user_command"}`
* **Headers:** `Authorization: Bearer <jwt>`

### Backend: Tiếp nhận Yêu cầu & Chuyển Lệnh
* **Component:** Backend (Node.js)
* **Action:** Middleware xác thực JWT, quyền room. Controller tạo `commandId`, lưu pending command (key: `commandId`, value: `{ userId, computerId }` vào cache/Redis). Gọi service gửi lệnh WS.
* **Files/Modules & Functions:** 
  * `src/routes/computer.routes.js`
  * `src/controllers/computer.controller.js` (`handleSendCommand`)
  * `src/middleware/authJwt.js`
  * `src/middleware/authComputerAccess.js`
  * `src/services/websocket.service.js` (`sendCommandToAgent`, quản lý `pendingCommands`), map `agentCommandSockets`.
* **API/Event:** Xử lý `POST /api/computers/:id/command`. Gửi WS Event `command:execute` tới Agent.
* **Response HTTP:** 202 Accepted, body: `{"commandId": "..."}`

### Agent: Nhận & Thực thi Lệnh
* **Component:** Agent (Python)
* **Action:** WS client nhận event `command:execute`. Gọi module thực thi lệnh.
* **Files/Modules & Functions:** 
  * `src/communication/ws_client.py` (`on_command_execute`)
  * `src/core/agent.py` (điều phối xử lý lệnh)
  * `src/monitoring/process_monitor.py` (`execute_command`).
* **API/Event:** Nhận WS Event `command:execute`.

### Agent: Gửi Kết quả
* **Component:** Agent (Python)
* **Action:** Sau khi `execute_command` hoàn tất, gọi hàm gửi kết quả HTTP.
* **Files/Modules & Functions:** 
  * `src/monitoring/process_monitor.py`
  * `src/communication/http_client.py` (`send_command_result`).
* **API/Event:** Gọi HTTP `POST /api/agent/command-result`
* **Request Body:** `{"commandId": ..., "stdout": ..., "stderr": ..., "exitCode": ...}`
* **Headers:** `X-Agent-ID`, `Authorization: Bearer <AgentToken>`

### Backend: Nhận Kết quả & Thông báo Frontend
* **Component:** Backend (Node.js)
* **Action:** Middleware xác thực Agent Token. Controller nhận kết quả, gọi service xử lý. Service tìm `userId` từ `commandId` trong `pendingCommands`, xóa pending command, gửi kết quả tới user qua WS.
* **Files/Modules & Functions:** 
  * `src/routes/agent.routes.js`
  * `src/controllers/agent.controller.js` (`handleCommandResult`)
  * `src/middleware/authAgentToken.js`
  * `src/services/websocket.service.js` (`notifyCommandCompletion`, quản lý `pendingCommands`).
* **API/Event:** Xử lý `POST /api/agent/command-result`. Gửi WS Event `command:result` tới Frontend.
* **Response HTTP:** 204 No Content (cho Agent).

### Frontend: Hiển thị Kết quả
* **Component:** Frontend (React)
* **Action:** `SocketContext` lắng nghe `command:result`, cập nhật state hoặc hiển thị kết quả.
* **Files/Modules & Functions:** 
  * `src/contexts/SocketContext.jsx`
  * `src/components/computer/CommandOutput.jsx` (Component hiển thị kết quả lệnh).
* **API/Event:** Lắng nghe WS Event `command:result`.

## 6. Luồng Báo cáo Lỗi

**Mục tiêu:** User/Admin báo cáo lỗi cho máy tính.

### Frontend: Gửi Báo cáo
* **Component:** Frontend (React)
* **Action:** Gọi service function sau khi submit form.
* **Files/Modules & Functions:** 
  * `src/components/computer/ReportErrorForm.jsx`
  * `src/services/computer.service.js` (`reportError`).
* **API/Event:** Gọi HTTP `POST /api/computers/:id/errors`
* **Request Body:** `{"type": "...", "description": "..."}`
* **Headers:** `Authorization: Bearer <jwt>`

### Backend: Nhận & Lưu Lỗi
* **Component:** Backend (Node.js)
* **Action:** Middleware xác thực JWT, quyền room. Controller gọi service. Service tạo object lỗi mới (gán id uuid, status 'active', thông tin user báo cáo). Cập nhật mảng `errors` JSONB trong record Computer.
* **Files/Modules & Functions:** 
  * `src/routes/computer.routes.js`
  * `src/controllers/computer.controller.js` (`handleReportError`)
  * `src/middleware/authJwt.js`
  * `src/middleware/authComputerAccess.js`
  * `src/services/computer.service.js` (`addComputerError`)
  * `src/database/models/computer.model.js`.
* **API/Event:** Xử lý `POST /api/computers/:id/errors`. Sau khi lưu thành công, gửi WS event `error_reported`.
* **Response HTTP:** 201 Created, body: `{ status: "success", data: { error_object } }`.

## 7. Luồng Xử lý/Xóa Lỗi

**Mục tiêu:** Admin đánh dấu lỗi đã xử lý.

### Frontend: Gửi Yêu cầu Resolve
* **Component:** Frontend (React)
* **Action:** Admin nhấn nút Resolve -> Gọi service function.
* **Files/Modules & Functions:** 
  * `src/components/computer/ErrorList.jsx`
  * `src/services/computer.service.js` (`resolveError`).
* **API/Event:** Gọi HTTP `PUT /api/computers/:computerId/errors/:errorId/resolve`
* **Request Body:** `{ "resolutionNotes": "..." }`
* **Headers:** `Authorization: Bearer <admin_jwt>`

### Backend: Tìm & Cập nhật Lỗi
* **Component:** Backend (Node.js)
* **Action:** Middleware xác thực JWT, quyền Admin. Controller gọi service. Service đọc mảng `errors` JSONB, tìm lỗi theo `errorId`, cập nhật `status`, `resolved_by`, `resolved_at`, `resolutionNotes`. Lưu lại vào DB.
* **Files/Modules & Functions:** 
  * `src/routes/computer.routes.js`
  * `src/controllers/computer.controller.js` (`handleResolveError`)
  * `src/middleware/authJwt.js`
  * `src/middleware/authAdmin.js`
  * `src/services/computer.service.js` (`resolveComputerError`)
  * `src/database/models/computer.model.js`.
* **API/Event:** Xử lý `PUT /api/computers/:computerId/errors/:errorId/resolve`. Sau khi cập nhật thành công, gửi WS event `error_resolved`.
* **Response HTTP:** 200 OK, body: `{ status: "success", data: { updated_error_object } }`.

### Frontend: Cập nhật UI
* **Component:** Frontend (React)
* **Action:** Nếu API gọi thành công, cập nhật trạng thái errors trong state. Nếu nhận được event WS, cũng cập nhật state tương tự.
* **Files/Modules & Functions:** 
  * `src/components/computer/ErrorList.jsx`
  * `src/contexts/SocketContext.jsx` (lắng nghe `error_resolved`).
* **Hiển thị:** Chuyển UI hiển thị lỗi từ 'active' sang 'resolved', hiển thị thông tin người xử lý và ghi chú.
