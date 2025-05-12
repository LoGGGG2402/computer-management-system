# Kiến trúc Hệ thống CMSAgent

**Phiên bản Tài liệu:** 1.0 (Dựa trên "Tài liệu Toàn Diện CMSAgent v7.4")
**Ngày cập nhật:** 13 tháng 5 năm 2025

## 1. Tổng Quan Kiến Trúc

Hệ thống CMSAgent được thiết kế theo kiến trúc module, bao gồm các thành phần chính hoạt động trên máy client Windows và giao tiếp với một server trung tâm. Mục tiêu chính là thu thập thông tin, giám sát, thực thi lệnh từ xa và tự động cập nhật một cách hiệu quả và an toàn.

Các thành phần chính bao gồm:

- **CMSAgent Service:** Thành phần cốt lõi chạy như một Windows Service, chịu trách nhiệm cho hầu hết các hoạt động.
- **CMSUpdater:** Một tiến trình độc lập chịu trách nhiệm cập nhật CMSAgent Service lên phiên bản mới.
- **Server Trung Tâm (Bên ngoài phạm vi tài liệu này):** Nơi CMSAgent gửi dữ liệu và nhận lệnh.

Kiến trúc này nhấn mạnh tính module, khả năng bảo trì và khả năng mở rộng.

## 2. Kiến trúc CMSAgent Service

CMSAgent Service là một ứng dụng .NET được thiết kế để chạy nền liên tục.

### 2.1. Chạy như Windows Service

- Được đăng ký và quản lý bởi Service Control Manager (SCM) của Windows.
- Khởi động tự động cùng hệ thống.
- Chạy dưới tài khoản `LocalSystem` để có đủ quyền truy cập tài nguyên hệ thống.

### 2.2. Các Module Chính và Trách Nhiệm

CMSAgent Service được cấu thành từ nhiều module, mỗi module có trách nhiệm cụ thể:

- **Core (`CMSAgent/Core/`)**:
    - `AgentService.cs`: Quản lý vòng đời của Windows Service (`OnStart`, `OnStop`). Điều phối hoạt động của các module khác.
    - `StateManager.cs`: Theo dõi và quản lý các trạng thái hoạt động của agent (ví dụ: `INITIALIZING`, `CONNECTED`, `DISCONNECTED`, `UPDATING`, `ERROR`).
    - `SingletonMutex.cs`: Đảm bảo chỉ một instance của CMSAgent được chạy trên máy.
    - `WorkerServiceBase.cs`: Lớp cơ sở cho các tác vụ chạy nền định kỳ.
- **Communication (`CMSAgent/Communication/`)**:
    - `HttpClientWrapper.cs`: Đóng gói logic gửi yêu cầu HTTP (cho API calls đến server như `/identify`, `/hardware-info`, `/check-update`, `/report-error`). Xử lý retry.
    - `WebSocketConnector.cs`: Quản lý kết nối WebSocket (Socket.IO) đến server, bao gồm xác thực, gửi/nhận sự kiện, và tự động kết nối lại.
    - `ServerApiEndpoints.cs`: Định nghĩa các hằng số cho đường dẫn API và cấu trúc payload.
- **Configuration (`CMSAgent/Configuration/`)**:
    - `ConfigLoader.cs`: Tải cấu hình từ `appsettings.json` (sử dụng `IConfiguration` của .NET) và `runtime_config.json`.
    - `Models/CmsAgentSettingsOptions.cs`: Lớp Options được binding từ section `CMSAgentSettings` trong `appsettings.json`, chứa các cài đặt hoạt động của agent. Sử dụng Data Annotations để xác thực.
    - `Models/RuntimeConfig.cs`: Đại diện cho cấu trúc của `runtime_config.json`.
- **Commands (`CMSAgent/Commands/`)**:
    - `CommandExecutor.cs`: Nhận lệnh từ server (qua WebSocket), đưa vào hàng đợi, và điều phối việc thực thi thông qua các worker threads.
    - `CommandHandlerFactory.cs`: Tạo ra các handler cụ thể dựa trên `commandType`.
    - `Handlers/`: Chứa các lớp xử lý cho từng loại lệnh (ví dụ: `ConsoleCommandHandler.cs` để chạy lệnh console, `SystemActionCommandHandler.cs` cho các hành động hệ thống).
    - `Models/`: Các lớp định nghĩa cấu trúc của yêu cầu lệnh và kết quả lệnh.
- **Monitoring (`CMSAgent/Monitoring/`)**:
    - `SystemMonitor.cs`: Thu thập thông tin sử dụng tài nguyên (CPU, RAM, Disk) theo thời gian thực, thường sử dụng WMI hoặc Performance Counters.
    - `HardwareInfoCollector.cs`: Thu thập thông tin chi tiết về phần cứng của máy khi agent khởi động lần đầu hoặc khi có yêu cầu.
- **Update (`CMSAgent/Update/`)**:
    - `UpdateHandler.cs`: Xử lý logic kiểm tra phiên bản mới, tải gói cập nhật, xác minh checksum, giải nén, và khởi chạy `CMSUpdater.exe`.
    - `Models/UpdateInfo.cs`: Lưu trữ thông tin về phiên bản cập nhật.
- **Logging (`CMSAgent/Logging/`)**:
    - `LoggingSetup.cs`: Chứa các helper để cấu hình Serilog dựa trên `appsettings.json` (mặc dù phần lớn cấu hình Serilog được nạp tự động bởi .NET Host).
- **Security (`CMSAgent/Security/`)**:
    - `TokenProtector.cs`: Sử dụng DPAPI (`ProtectedData`) để mã hóa và giải mã `agentToken` lưu trong `runtime_config.json`.
- **Cli (`CMSAgent/Cli/`)**:
    - `CliHandler.cs`: Sử dụng thư viện `System.CommandLine` để định nghĩa và xử lý các lệnh dòng lệnh của `CMSAgent.exe` (như `configure`, `start`, `stop`, `uninstall`, `debug`).
    - `Commands/`: Các lớp triển khai logic cho từng lệnh CLI.
- **Persistence (`CMSAgent/Persistence/`)**:
    - `OfflineQueueManager.cs`: Quản lý việc lưu trữ tạm (queue) các thông điệp (báo cáo trạng thái, kết quả lệnh, báo cáo lỗi) vào đĩa khi agent không thể kết nối đến server, và gửi lại khi kết nối được khôi phục.

### 2.3. Luồng Dữ Liệu Chính

1. **Khởi tạo:** Agent tải cấu hình, xác thực, kết nối WebSocket.
2. **Thu thập dữ liệu:** `SystemMonitor` liên tục thu thập dữ liệu tài nguyên.
3. **Gửi dữ liệu:** Dữ liệu trạng thái được gửi định kỳ qua WebSocket (`agent:status_update`). Thông tin phần cứng được gửi qua HTTP POST.
4. **Nhận lệnh:** Lệnh từ server được nhận qua WebSocket (`command:execute`).
5. **Thực thi lệnh:** `CommandExecutor` xử lý và gửi kết quả lại qua WebSocket (`agent:command_result`).
6. **Cập nhật:** `UpdateHandler` kiểm tra, tải và kích hoạt `CMSUpdater.exe`.
7. **Logging:** Tất cả các hoạt động quan trọng và lỗi đều được ghi log.

## 3. Kiến trúc CMSUpdater

`CMSUpdater.exe` là một ứng dụng console .NET độc lập, được thiết kế để thực hiện việc thay thế file của CMSAgent Service một cách an toàn.

- **Độc lập:** Chạy như một tiến trình riêng biệt, được khởi chạy bởi CMSAgent Service hiện tại.
- **Tham số dòng lệnh:** Nhận các thông tin cần thiết (PID của agent cũ, đường dẫn agent mới, thư mục cài đặt, thư mục log) qua tham số dòng lệnh.
- **Luồng hoạt động:**
    1. Chờ tiến trình CMSAgent cũ dừng hoàn toàn (dựa trên PID).
    2. Sao lưu thư mục cài đặt của agent cũ.
    3. Xóa/Di chuyển các file của agent cũ.
    4. Sao chép/Di chuyển các file của agent mới vào thư mục cài đặt.
    5. Sử dụng Service Control Manager (SCM) để khởi động lại CMSAgent Service (phiên bản mới).
    6. Thực hiện rollback (khôi phục bản sao lưu và khởi động lại agent cũ) nếu có lỗi trong quá trình triển khai hoặc nếu agent mới không khởi động thành công (cơ chế Watchdog).
    7. Dọn dẹp file tạm và thư mục sao lưu nếu cập nhật thành công.
    8. Ghi log chi tiết toàn bộ quá trình.

## 4. Thư viện chung (CMSAgent.Common)

Dự án `CMSAgent.Common` chứa các thành phần được chia sẻ giữa `CMSAgent` và có thể cả `CMSUpdater` (nếu cần).

- **DTOs (Data Transfer Objects):** Các lớp C# thuần túy định nghĩa cấu trúc dữ liệu cho các payload API và sự kiện WebSocket.
- **Enums:** Định nghĩa các kiểu liệt kê dùng chung (ví dụ: `AgentState`, `CommandType`, `ErrorType`, `UpdateStatus`).
- **Constants:** Các hằng số dùng chung (ví dụ: tên Mutex, tên sự kiện WebSocket, đường dẫn API).
- **Interfaces:** Các interface dùng cho Dependency Injection và để định nghĩa các hợp đồng giữa các module.

Mục đích là để giảm sự trùng lặp code và đảm bảo tính nhất quán.

## 5. Giao tiếp (Communication)

### 5.1. HTTP API

- Sử dụng cho các yêu cầu có tính chất "request-response" một lần, như định danh agent, gửi thông tin phần cứng, kiểm tra cập nhật, báo cáo lỗi.
- Tất cả các giao tiếp API đều phải qua HTTPS.
- Sử dụng `X-Agent-Id` và `Authorization: Bearer <agent_token>` cho các yêu cầu cần xác thực.

### 5.2. WebSocket (Socket.IO)

- Sử dụng cho giao tiếp hai chiều, thời gian thực giữa agent và server.
- Các trường hợp sử dụng chính:
    - Gửi báo cáo trạng thái định kỳ từ agent lên server.
    - Server gửi lệnh thực thi đến agent.
    - Agent gửi kết quả thực thi lệnh về server.
    - Server thông báo có phiên bản mới cho agent.
    - Xác thực kết nối WebSocket.
- Tất cả kết nối WebSocket phải qua WSS (WebSocket Secure).

## 6. Cấu hình (Configuration)

Hệ thống sử dụng hai loại file cấu hình chính:

- **`appsettings.json`:**
    - Nằm trong thư mục cài đặt của agent (`C:\Program Files\CMSAgent`).
    - Chứa các cấu hình chính của ứng dụng và các thiết lập hoạt động của agent (ví dụ: `ServerUrl`, các khoảng thời gian, cấu hình logging Serilog chi tiết, giới hạn tài nguyên, cài đặt cho các module).
    - Được tải bằng cơ chế `IConfiguration` chuẩn của .NET.
    - Có thể được ghi đè bởi các file theo môi trường (ví dụ: `appsettings.Production.json`).
- **`runtime_config.json`:**
    - Nằm trong thư mục dữ liệu của agent (`C:\ProgramData\CMSAgent\runtime_config\`).
    - Chứa các thông tin định danh duy nhất cho từng instance agent và không nên được đóng gói cùng bộ cài đặt, bao gồm: `device_id`, `room_config` (tên phòng, tọa độ), và `agent_token_encrypted` (token đã được mã hóa).
    - Được tạo và cập nhật bởi lệnh `CMSAgent.exe configure`.

Việc xác thực cấu hình được thực hiện thông qua .NET Options Pattern với Data Annotations cho `appsettings.json` và kiểm tra thủ công cho `runtime_config.json`.

## 7. Bảo mật (Security Considerations)

- **Mã hóa Token:** `agentToken` được mã hóa bằng DPAPI (`DataProtectionScope.LocalMachine`) trước khi lưu vào `runtime_config.json`.
- **Kết nối an toàn:** Bắt buộc HTTPS cho API và WSS cho WebSocket.
- **Quyền truy cập thư mục:** Thiết lập quyền chặt chẽ cho thư mục cài đặt và thư mục dữ liệu để bảo vệ file cấu hình và log.
- **Tài khoản LocalSystem:** Mặc dù cung cấp quyền cần thiết, cần ý thức về rủi ro và xem xét nguyên tắc Least Privilege.
- **Xác thực lệnh từ xa:** Server chịu trách nhiệm xác thực trước khi gửi lệnh. Agent thực hiện input validation.
- **Mutex duy nhất:** Đảm bảo tên Mutex có GUID để tránh xung đột.
- **Làm mới Token:** Có cơ chế làm mới token chủ động và khi gặp lỗi.

## 8. Logging

- Sử dụng Serilog làm framework logging chính.
- Cấu hình logging được định nghĩa trong `appsettings.json`.
- Ghi log ra nhiều "sink":
    - File (xoay vòng theo ngày).
    - Windows Event Log (cho các sự kiện quan trọng).
    - Console (khi chạy ở chế độ debug).
- Hỗ trợ nhiều mức độ log và ghi log theo ngữ cảnh (SourceContext).
- Có khả năng thu thập và gửi log từ xa theo yêu cầu của server.

## 9. Triển khai và Cài đặt (Deployment and Installation)

- Agent được đóng gói thành một bộ cài đặt (`Setup.CMSAgent.exe`) sử dụng Inno Setup.
- Trình cài đặt chịu trách nhiệm:
    1. Sao chép các file ứng dụng (`CMSAgent.exe`, `CMSUpdater.exe`, `appsettings.json` mặc định, DLLs).
    2. Tạo cấu trúc thư mục dữ liệu trong `C:\ProgramData\CMSAgent`.
    3. Thiết lập quyền truy cập thư mục cần thiết.
    4. Thực thi `CMSAgent.exe configure` để người dùng cấu hình ban đầu.
    5. Đăng ký và khởi động CMSAgent Service.

## 10. Sơ đồ Kiến trúc Cấp cao (Mô tả bằng Text)

```
+---------------------+      HTTPS/API       +---------------------+
|     Máy Client      |<-------------------->|    Server Trung Tâm   |
|  (Windows Service)  |      WSS/Socket.IO   | (Backend Application)|
| +-----------------+ |<-------------------->+---------------------+
| |   CMSAgent.exe  | |
| | +-------------+ | |
| | | Core Logic  | | |
| | +-------------+ | |
| | | Communication|+-+ (HTTP, WebSocket)
| | +-------------+ | |
| | | Configuration|+-+ (appsettings.json, runtime_config.json)
| | +-------------+ | |
| | | Commands    | | |
| | +-------------+ | |
| | | Monitoring  | | |
| | +-------------+ | |
| | | Update      |---+-->[CMSUpdater.exe]
| | +-------------+ | |
| | | Logging     | | |
| | +-------------+ | |
| | | Security    | | |
| | +-------------+ | |
| | | CLI Handler | | |
| | +-------------+ | |
| | | Persistence | | |
| | +-------------+ | |
| +-----------------+ |
| +-----------------+ |
| | CMSUpdater.exe  | |  (Tiến trình cập nhật độc lập)
| +-----------------+ |
+---------------------+
        |
        | (Đọc/Ghi)
        v
+---------------------+
|   Hệ Thống File     |
| (C:\Program Files\) |
| (C:\ProgramData\)   |
+---------------------+

```

**Luồng chính:**

1. **CMSAgent Service** chạy nền, thu thập dữ liệu, gửi lên **Server Trung Tâm** qua WebSocket.
2. **Server Trung Tâm** gửi lệnh xuống **CMSAgent Service** qua WebSocket.
3. **CMSAgent Service** thực thi lệnh, gửi kết quả lại.
4. Khi có cập nhật, **CMSAgent Service** tải gói, khởi chạy **CMSUpdater.exe**.
5. **CMSUpdater.exe** dừng agent cũ, thay thế file, khởi động agent mới.
6. Cấu hình được đọc từ **`appsettings.json`** và **`runtime_config.json`**.
7. Log được ghi vào thư mục log và Event Log.