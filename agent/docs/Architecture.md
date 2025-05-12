# Kiến trúc Hệ thống CMSAgent

**Phiên bản Tài liệu:** 1.1 (Dựa trên "Tài liệu Toàn Diện CMSAgent v7.4")
**Ngày cập nhật:** 13 tháng 5 năm 2025

## 1. Tổng Quan Kiến Trúc

Hệ thống CMSAgent được thiết kế theo kiến trúc module, tập trung vào việc thu thập thông tin, giám sát, thực thi lệnh từ xa và tự động cập nhật một cách hiệu quả, an toàn và ổn định trên các máy client Windows. Hệ thống giao tiếp với một server trung tâm để nhận chỉ thị và gửi dữ liệu.

Các thành phần chính của hệ thống bao gồm:

- **CMSAgent Service:** Đây là thành phần cốt lõi, hoạt động như một Windows Service trên máy client. Nó chịu trách nhiệm thực hiện hầu hết các chức năng chính của agent, bao gồm thu thập dữ liệu, giám sát tài nguyên, duy trì kết nối với server, thực thi các lệnh nhận được, và khởi tạo quá trình tự cập nhật.
- **CMSUpdater:** Là một tiến trình thực thi (.exe) độc lập, được CMSAgent Service kích hoạt khi có phiên bản mới. Nhiệm vụ chính của CMSUpdater là thực hiện việc thay thế các file của CMSAgent Service hiện tại bằng các file của phiên bản mới một cách an toàn, bao gồm cả việc dừng service cũ, sao lưu, triển khai file mới và khởi động lại service đã cập nhật.
- **Server Trung Tâm (Bên ngoài phạm vi tài liệu này):** Là một ứng dụng backend mà CMSAgent kết nối đến. Server này quản lý các agent, lưu trữ thông tin thu thập được, gửi lệnh điều khiển từ xa đến các agent, và cung cấp các gói cập nhật phiên bản mới.

Kiến trúc này nhấn mạnh các nguyên tắc thiết kế sau:

- **Tính Module (Modularity):** Các chức năng được chia thành các module riêng biệt, dễ quản lý, phát triển và kiểm thử độc lập.
- **An Toàn (Security):** Ưu tiên các cơ chế bảo mật trong giao tiếp (HTTPS, WSS), mã hóa dữ liệu nhạy cảm (token), và quản lý quyền truy cập chặt chẽ.
- **Ổn Định và Tin Cậy (Stability & Reliability):** Đảm bảo agent hoạt động liên tục, có khả năng tự phục hồi sau lỗi, và xử lý các tình huống ngoại lệ một cách an toàn.
- **Hiệu Quả (Efficiency):** Tối ưu hóa việc sử dụng tài nguyên hệ thống (CPU, RAM, mạng) để không ảnh hưởng đến hiệu suất của máy client.
- **Khả năng bảo trì (Maintainability):** Cấu trúc code rõ ràng, tài liệu đầy đủ giúp dễ dàng bảo trì và nâng cấp.
- **Khả năng mở rộng (Scalability):** Mặc dù khả năng mở rộng chủ yếu phụ thuộc vào kiến trúc server, thiết kế của agent cũng cần đảm bảo có thể xử lý hiệu quả khi số lượng lệnh hoặc tần suất giao tiếp tăng lên.

## 2. Kiến trúc CMSAgent Service

CMSAgent Service là một ứng dụng .NET được thiết kế để chạy nền liên tục trên máy client.

### 2.1. Chạy như Windows Service

- Được đăng ký và quản lý bởi Service Control Manager (SCM) của Windows, cho phép quản lý vòng đời (start, stop, restart).
- Cấu hình để khởi động tự động cùng hệ thống, đảm bảo agent luôn hoạt động khi máy tính được bật.
- Chạy dưới tài khoản `LocalSystem` theo mặc định, cung cấp các quyền cần thiết để truy cập tài nguyên hệ thống, đọc thông tin phần cứng, và thực thi các tác vụ quản trị.

### 2.2. Các Module Chính và Trách Nhiệm

CMSAgent Service được cấu thành từ nhiều module logic, mỗi module có trách nhiệm cụ thể và tương tác với các module khác để hoàn thành nhiệm vụ chung:

- **Core (`CMSAgent/Core/`)**:
    - `AgentService.cs`: Là trái tim của service, quản lý vòng đời (`OnStart`, `OnStop`). Khởi tạo và điều phối hoạt động của tất cả các module khác. Xử lý các tín hiệu từ SCM.
    - `StateManager.cs`: Theo dõi và quản lý trạng thái hoạt động hiện tại của agent (ví dụ: `INITIALIZING`, `CONNECTED`, `DISCONNECTED`, `UPDATING`, `ERROR`). Các module khác có thể truy vấn hoặc cập nhật trạng thái này.
    - `SingletonMutex.cs`: Sử dụng Mutex toàn cục để đảm bảo chỉ một instance của CMSAgent Service được chạy trên một máy tại một thời điểm, ngăn chặn xung đột.
    - `WorkerServiceBase.cs`: Một lớp cơ sở (nếu được triển khai) cho các tác vụ chạy nền định kỳ, ví dụ như việc gửi báo cáo trạng thái hoặc kiểm tra cập nhật, giúp quản lý vòng đời của các tác vụ này.
- **Communication (`CMSAgent/Communication/`)**:
    - `HttpClientWrapper.cs`: Đóng gói logic để gửi các yêu cầu HTTP/HTTPS đến API của Server Trung Tâm. Chịu trách nhiệm xử lý việc thêm các header cần thiết (như token xác thực), tuần tự hóa request body, giải tuần tự hóa response body, và triển khai cơ chế retry cho các lỗi mạng tạm thời. Tương tác với module `Configuration` để lấy URL server và module `Security` để lấy token.
    - `WebSocketConnector.cs`: Quản lý kết nối WebSocket (Socket.IO) đến Server Trung Tâm. Xử lý việc thiết lập kết nối, xác thực (gửi token), đăng ký các handler cho sự kiện nhận từ server (như `command:execute`, `agent:new_version_available`), gửi sự kiện lên server (như `agent:status_update`, `agent:command_result`), và tự động kết nối lại khi mất kết nối.
    - `ServerApiEndpoints.cs`: Chứa các hằng số định nghĩa đường dẫn API và cấu trúc payload JSON cho các yêu cầu HTTP và sự kiện WebSocket, giúp đảm bảo tính nhất quán và dễ bảo trì khi có thay đổi từ phía server.
- **Configuration (`CMSAgent/Configuration/`)**:
    - `ConfigLoader.cs`: Chịu trách nhiệm đọc và phân tích cú pháp các file cấu hình. Tải cấu hình từ `appsettings.json` (sử dụng `IConfiguration` của .NET) và từ `runtime_config.json` (chứa thông tin định danh và token). Cung cấp các cấu hình đã được binding vào các lớp Options (như `CmsAgentSettingsOptions`) cho các module khác sử dụng.
    - `Models/CmsAgentSettingsOptions.cs`: Lớp C# được binding từ section `CMSAgentSettings` trong `appsettings.json`. Nó chứa các cài đặt hoạt động của agent và sử dụng Data Annotations để xác thực các giá trị cấu hình khi được nạp.
    - `Models/RuntimeConfig.cs`: Đại diện cho cấu trúc của file `runtime_config.json`, chứa `device_id`, `room_config`, và `agent_token_encrypted`.
- **Commands (`CMSAgent/Commands/`)**:
    - `CommandExecutor.cs`: Nhận các yêu cầu lệnh từ module `Communication` (cụ thể là `WebSocketConnector`), đưa chúng vào một hàng đợi (queue) để xử lý bất đồng bộ. Quản lý một hoặc nhiều luồng worker để lấy lệnh từ hàng đợi và thực thi chúng.
    - `CommandHandlerFactory.cs`: Dựa vào `commandType` của lệnh nhận được, factory này sẽ tạo ra một instance của handler xử lý lệnh phù hợp.
    - `Handlers/`: Chứa các lớp triển khai logic cụ thể cho từng `commandType`. Ví dụ, `ConsoleCommandHandler.cs` thực thi các lệnh console, thu thập stdout/stderr; `SystemActionCommandHandler.cs` thực hiện các hành động hệ thống như reboot, shutdown. Mỗi handler chịu trách nhiệm thực thi lệnh và trả về kết quả.
    - `Models/`: Các lớp C# định nghĩa cấu trúc của payload lệnh nhận từ server và cấu trúc của kết quả lệnh sẽ được gửi trả lại server.
- **Monitoring (`CMSAgent/Monitoring/`)**:
    - `SystemMonitor.cs`: Định kỳ thu thập thông tin về việc sử dụng tài nguyên hệ thống như % CPU, % RAM, % Disk. Sử dụng các API hệ thống của Windows như WMI hoặc Performance Counters. Cung cấp dữ liệu này cho module `Communication` để gửi báo cáo trạng thái.
    - `HardwareInfoCollector.cs`: Thu thập thông tin chi tiết về phần cứng của máy (OS, CPU model, GPU model, tổng RAM, tổng dung lượng đĩa) khi agent khởi động lần đầu hoặc khi có yêu cầu cụ thể từ server.
- **Update (`CMSAgent/Update/`)**:
    - `UpdateHandler.cs`: Xử lý toàn bộ logic liên quan đến việc tự động cập nhật agent. Module này kiểm tra phiên bản mới (qua HTTP API hoặc sự kiện WebSocket), tải gói cập nhật, xác minh tính toàn vẹn của gói (checksum), giải nén, và sau đó khởi chạy tiến trình `CMSUpdater.exe` riêng biệt, truyền các tham số cần thiết cho nó.
    - `Models/UpdateInfo.cs`: Lưu trữ thông tin về phiên bản cập nhật mới nhận được từ server (version, download URL, checksum).
- **Logging (`CMSAgent/Logging/`)**:
    - `LoggingSetup.cs`: Chứa các phương thức helper để khởi tạo và cấu hình Serilog dựa trên các thiết lập trong `appsettings.json`. Mặc dù phần lớn cấu hình Serilog có thể được nạp tự động bởi .NET Host, lớp này có thể thực hiện các tùy chỉnh bổ sung nếu cần. Cung cấp một interface `ILogger` chuẩn cho các module khác.
- **Security (`CMSAgent/Security/`)**:
    - `TokenProtector.cs`: Cung cấp các phương thức để mã hóa `agentToken` (nhận từ server) bằng DPAPI (`ProtectedData.Protect`) trước khi lưu vào `runtime_config.json` và giải mã (`ProtectedData.Unprotect`) khi agent cần sử dụng token.
- **Cli (`CMSAgent/Cli/`)**:
    - `CliHandler.cs`: Sử dụng thư viện `System.CommandLine` để định nghĩa các lệnh dòng lệnh mà `CMSAgent.exe` hỗ trợ (ví dụ: `configure`, `start`, `stop`, `uninstall`, `debug`) và ánh xạ chúng tới các hành động tương ứng.
    - `Commands/`: Các lớp triển khai logic thực thi cho từng lệnh CLI cụ thể. Ví dụ, `ConfigureCommand.cs` sẽ khởi tạo luồng tương tác với người dùng để cấu hình agent.
- **Persistence (`CMSAgent/Persistence/`)**:
    - `OfflineQueueManager.cs`: Quản lý việc lưu trữ tạm (queue) các thông điệp quan trọng (như báo cáo trạng thái, kết quả thực thi lệnh, báo cáo lỗi) vào đĩa khi agent không thể kết nối đến server. Khi kết nối được khôi phục, module này sẽ đọc dữ liệu từ queue và gửi lại cho server.

### 2.3. Luồng Dữ Liệu Chính

1. **Khởi tạo và Xác thực:**
    - Agent khởi động, `ConfigLoader` tải cấu hình từ `appsettings.json` và `runtime_config.json`.
    - `TokenProtector` giải mã `agentToken`.
    - `WebSocketConnector` sử dụng `ServerUrl` và `agentToken` để thiết lập kết nối WSS đến server và thực hiện xác thực.
    - Nếu xác thực thành công, agent chuyển sang trạng thái `CONNECTED`.
2. **Thu thập và Gửi Dữ liệu Trạng thái:**
    - `SystemMonitor` định kỳ thu thập dữ liệu sử dụng tài nguyên (CPU, RAM, Disk).
    - Dữ liệu này được đóng gói và gửi lên server thông qua `WebSocketConnector` bằng sự kiện `agent:status_update`.
    - `HardwareInfoCollector` thu thập thông tin phần cứng và gửi một lần qua `HttpClientWrapper` (POST `/api/agent/hardware-info`) sau khi kết nối thành công.
3. **Nhận và Thực thi Lệnh:**
    - `WebSocketConnector` lắng nghe sự kiện `command:execute` từ server.
    - Khi nhận được lệnh, payload lệnh được chuyển đến `CommandExecutor`.
    - `CommandExecutor` đưa lệnh vào hàng đợi. Worker thread lấy lệnh, `CommandHandlerFactory` tạo handler tương ứng.
    - Handler thực thi lệnh (ví dụ: `ConsoleCommandHandler` chạy một tiến trình console).
    - Kết quả (stdout, stderr, exit code) được thu thập và đóng gói.
    - `CommandExecutor` gửi kết quả về server thông qua `WebSocketConnector` bằng sự kiện `agent:command_result`.
4. **Xử lý Cập nhật:**
    - `UpdateHandler` kiểm tra phiên bản mới (định kỳ qua `HttpClientWrapper` GET `/api/agent/check-update` hoặc nhận sự kiện `agent:new_version_available` từ `WebSocketConnector`).
    - Nếu có cập nhật, tải gói (`.zip`) về, xác minh checksum.
    - Nếu hợp lệ, giải nén và khởi chạy `CMSUpdater.exe` với các tham số cần thiết. Agent Service sau đó sẽ tự dừng.
5. **Logging:**
    - Tất cả các module sử dụng một instance `ILogger` (cung cấp bởi Serilog) để ghi lại các hoạt động quan trọng, thông tin debug, cảnh báo và lỗi vào các sink đã cấu hình (file, Event Log, console).

## 3. Kiến trúc CMSUpdater

`CMSUpdater.exe` là một ứng dụng console .NET độc lập, được thiết kế để thực hiện việc thay thế file của CMSAgent Service một cách an toàn và tin cậy.

- **Độc lập:** Chạy như một tiến trình riêng biệt, được khởi chạy bởi CMSAgent Service hiện tại khi có yêu cầu cập nhật. Điều này cho phép `CMSUpdater` có thể dừng và thay thế các file của CMSAgent Service mà không bị khóa.
- **Tham số dòng lệnh:** Nhận các thông tin cần thiết từ CMSAgent Service qua tham số dòng lệnh, bao gồm:
    - PID của tiến trình CMSAgent Service cũ (để chờ nó dừng hoàn toàn).
    - Đường dẫn đến thư mục chứa các file của phiên bản agent mới đã được giải nén.
    - Đường dẫn đến thư mục cài đặt hiện tại của CMSAgent Service.
    - Đường dẫn đến thư mục log nơi `CMSUpdater` sẽ ghi lại hoạt động của nó.
    - Phiên bản hiện tại của agent (để tạo tên thư mục sao lưu rõ ràng).
- **Luồng hoạt động:**
    1. **Thiết lập Logging Riêng:** Khởi tạo hệ thống logging riêng để ghi lại toàn bộ quá trình cập nhật vào một file log cụ thể của updater.
    2. **Chờ Agent Cũ Dừng Hoàn Toàn:** Sử dụng PID nhận được, `CMSUpdater` liên tục kiểm tra xem tiến trình CMSAgent Service cũ đã thoát hoàn toàn chưa. Có một cơ chế timeout (ví dụ: 2-5 phút) để tránh chờ vô hạn. Nếu quá timeout, ghi log lỗi và thoát (cân nhắc thực hiện rollback nếu đã có bước sao lưu trước đó).
    3. **Sao Lưu Agent Cũ:** Xác định thư mục cài đặt của agent cũ. Đổi tên thư mục này thành một tên sao lưu (ví dụ: `CMSAgent_backup_<phiên_bản_cũ>_<timestamp>`) hoặc sao chép toàn bộ nội dung sang một thư mục sao lưu trong thư mục `updates/backup/`. Ưu tiên đổi tên để nhanh hơn và an toàn hơn. Nếu bước này thất bại, ghi log lỗi nghiêm trọng và thoát updater.
    4. **Triển Khai Agent Mới:** Lấy đường dẫn đến thư mục chứa agent mới. Di chuyển hoặc sao chép toàn bộ nội dung của thư mục agent mới này vào thư mục cài đặt gốc của agent (nơi agent cũ vừa được sao lưu/đổi tên đi).
    5. **Xử lý Lỗi Triển Khai và Rollback:** Nếu bước triển khai agent mới thất bại (ví dụ: lỗi copy file):
        - Cố gắng xóa các file/thư mục mới đã được copy/di chuyển (nếu có).
        - Thực hiện Rollback: Đổi tên lại thư mục sao lưu của agent cũ về tên gốc (hoặc copy lại từ bản sao lưu). Ghi log chi tiết các bước rollback.
        - Ghi log lỗi chi tiết về việc triển khai thất bại và thoát updater.
    6. **Khởi Động Agent Mới (Windows Service):** `CMSUpdater.exe` sử dụng các lệnh của Service Control Manager (ví dụ, gọi `sc.exe start <tên_service_agent>`) để khởi động Windows Service của `CMSAgent.exe` phiên bản mới. Điều này thường đòi hỏi `CMSUpdater.exe` phải chạy với quyền Administrator (được kế thừa từ CMSAgent Service hoặc được nâng quyền).
    7. **Xử lý Lỗi Khởi Động Service Mới và Rollback (bao gồm cơ chế Watchdog):**
        - Nếu khởi động service mới thất bại ngay lập tức (ví dụ, SCM trả về lỗi), hoặc nếu service mới khởi động nhưng bị crash liên tục ngay sau đó (được phát hiện bởi cơ chế "Watchdog" – `CMSUpdater` đợi một khoảng thời gian ngắn, ví dụ 1-2 phút, và kiểm tra trạng thái/PID của service mới):
            - Thực hiện Rollback: Dừng service mới (nếu đang chạy), khôi phục agent cũ từ bản sao lưu, và cố gắng khởi động lại service của agent cũ.
            - Ghi log chi tiết các bước rollback và lỗi (bao gồm lỗi từ SCM nếu có).
            - Agent cũ (nếu được khôi phục và khởi động lại thành công) nên gửi báo cáo lỗi về việc cập nhật thất bại lên server.
            - Thoát `CMSUpdater`.
    8. **Dọn Dẹp:** Nếu agent mới khởi động thành công và ổn định (không bị rollback bởi watchdog):
        - Xóa thư mục sao lưu của agent cũ.
        - Xóa các file tạm của gói cập nhật đã được agent cũ tải về và giải nén (trong thư mục `updates/download/` và `updates/extracted/`).
        - Ghi log cập nhật thành công.
    9. **Thoát:** `CMSUpdater.exe` thoát. Agent mới (sau khi khởi động và xác thực) sẽ gửi thông báo cập nhật thành công lên server.

## 4. Thư viện chung (CMSAgent.Common)

Dự án `CMSAgent.Common` chứa các thành phần được chia sẻ giữa `CMSAgent` và `CMSUpdater` (nếu cần thiết), nhằm mục đích giảm sự trùng lặp code và đảm bảo tính nhất quán.

- **DTOs (Data Transfer Objects):** Các lớp C# thuần túy (POCO - Plain Old CLR Object) định nghĩa cấu trúc dữ liệu cho các payload được gửi và nhận qua API HTTP và các sự kiện WebSocket. Ví dụ: `IdentifyRequest`, `HardwareInfo`, `CommandPayload`, `CommandResultPayload`, `StatusUpdatePayload`.
- **Enums:** Định nghĩa các kiểu liệt kê được sử dụng xuyên suốt ứng dụng để biểu diễn các trạng thái hoặc loại cụ thể. Ví dụ: `AgentState` (INITIALIZING, CONNECTED, etc.), `CommandType` (console, system_action), `ErrorType` (cho báo cáo lỗi), `UpdateStatus` (cho thông báo trạng thái cập nhật).
- **Constants:** Chứa các giá trị hằng số được sử dụng ở nhiều nơi trong ứng dụng. Ví dụ: tên Mutex toàn cục, tên các sự kiện WebSocket, các đường dẫn API cơ sở, các key cấu hình.
- **Interfaces:** Định nghĩa các hợp đồng (abstractions) cho các service hoặc component chính, hỗ trợ Dependency Injection và kiểm thử đơn vị (unit testing). Ví dụ: `IConfigLoader`, `ITokenProtector`, `ISystemMonitor`, `ICommandExecutor`, `IUpdateHandler`.

## 5. Giao tiếp (Communication)

### 5.1. HTTP API

- Sử dụng cho các tương tác có tính chất "request-response" một lần, không yêu cầu kết nối duy trì liên tục.
- Các trường hợp sử dụng chính:
    - Định danh agent và lấy token (`POST /api/agent/identify`).
    - Xác thực MFA (`POST /api/agent/verify-mfa`).
    - Gửi thông tin phần cứng ban đầu (`POST /api/agent/hardware-info`).
    - Kiểm tra phiên bản cập nhật mới (`GET /api/agent/check-update`).
    - Báo cáo lỗi phát sinh trong agent (`POST /api/agent/report-error`).
    - Tải gói cập nhật (`GET /download/agent-packages/:filename`).
- Tất cả các giao tiếp API đều phải được thực hiện qua **HTTPS** để đảm bảo mã hóa dữ liệu truyền đi.
- Các yêu cầu cần xác thực sẽ sử dụng header `X-Agent-Id` (chứa `device_id`) và `Authorization: Bearer <agent_token>`.

### 5.2. WebSocket (Socket.IO)

- Sử dụng cho giao tiếp hai chiều, thời gian thực, và duy trì kết nối liên tục giữa agent và server.
- Các trường hợp sử dụng chính:
    - Agent gửi báo cáo trạng thái (CPU, RAM, Disk usage) định kỳ lên server (`agent:status_update`).
    - Server gửi lệnh yêu cầu thực thi đến agent (`command:execute`).
    - Agent gửi kết quả thực thi lệnh về server (`agent:command_result`).
    - Server thông báo có phiên bản agent mới khả dụng (`agent:new_version_available`).
    - Agent gửi các thông báo về trạng thái cập nhật (`agent:update_status`).
    - Xác thực kết nối WebSocket ban đầu (`agent:authenticate` hoặc qua header).
- Tất cả kết nối WebSocket phải được thiết lập qua **WSS (WebSocket Secure)**, là phiên bản bảo mật của WebSocket sử dụng TLS.

## 6. Cấu hình (Configuration)

Hệ thống sử dụng hai loại file cấu hình chính, được quản lý và tải bởi module `Configuration`:

- **`appsettings.json`:**
    - Nằm trong thư mục cài đặt của agent (ví dụ: `C:\Program Files\CMSAgent`). Đây là file cấu hình chính của ứng dụng.
    - Chứa các cấu hình chung của ứng dụng .NET và các thiết lập hoạt động cụ thể của CMSAgent. Ví dụ: `ServerUrl`, các khoảng thời gian hoạt động (`StatusReportIntervalSec`, `AutoUpdateIntervalSec`), cấu hình chi tiết cho Serilog (mức độ log, sinks, enrichers), giới hạn tài nguyên (`ResourceLimits`), cài đặt cho các module HTTP client, WebSocket client, và command executor.
    - Được tải bằng cơ chế `IConfiguration` chuẩn của .NET khi ứng dụng khởi động.
    - Hỗ trợ cấu hình theo môi trường (ví dụ: `appsettings.Development.json`, `appsettings.Production.json`) cho phép ghi đè các cài đặt cho các môi trường triển khai khác nhau.
- **`runtime_config.json`:**
    - Nằm trong thư mục dữ liệu của agent (ví dụ: `C:\ProgramData\CMSAgent\runtime_config\`).
    - Chứa các thông tin định danh duy nhất cho từng instance agent cụ thể và các thông tin được tạo ra trong quá trình hoạt động, không nên được đóng gói cùng bộ cài đặt. Bao gồm:
        - `device_id`: Mã định danh duy nhất của agent trên máy client.
        - `room_config`: Thông tin vị trí (tên phòng, tọa độ X, Y) do người dùng cung cấp.
        - `agent_token_encrypted`: Token xác thực đã được mã hóa bằng DPAPI.
    - File này được tạo và cập nhật chủ yếu bởi lệnh `CMSAgent.exe configure`.

Việc xác thực các giá trị cấu hình từ `appsettings.json` được thực hiện thông qua .NET Options Pattern, sử dụng các lớp Options (ví dụ: `CmsAgentSettingsOptions`) với Data Annotations để kiểm tra tính hợp lệ. Đối với `runtime_config.json`, agent thực hiện kiểm tra thủ công sự tồn tại và định dạng cơ bản của các trường bắt buộc khi tải.

## 7. Bảo mật (Security Considerations)

Các biện pháp bảo mật sau được áp dụng để đảm bảo an toàn cho CMSAgent và dữ liệu nó xử lý:

- **Mã hóa Token:** `agentToken` nhận từ server được mã hóa bằng DPAPI (sử dụng `DataProtectionScope.LocalMachine`) trước khi lưu trữ trong `runtime_config.json`. Điều này đảm bảo token chỉ có thể được giải mã trên cùng một máy và bởi các tiến trình có quyền phù hợp (như LocalSystem).
- **Kết nối an toàn:** Bắt buộc sử dụng HTTPS cho tất cả các API call và WSS (WebSocket Secure) cho giao tiếp Socket.IO. Điều này mã hóa toàn bộ dữ liệu truyền tải giữa agent và server.
- **Quyền truy cập thư mục:** Thiết lập quyền truy cập chặt chẽ cho thư mục cài đặt (`C:\Program Files\CMSAgent`) và thư mục dữ liệu (`C:\ProgramData\CMSAgent`) để bảo vệ các file thực thi, file cấu hình và file log khỏi truy cập trái phép.
- **Tài khoản LocalSystem:** Mặc dù cung cấp các quyền cần thiết cho agent hoạt động, việc chạy dưới tài khoản LocalSystem cũng mang lại bề mặt tấn công lớn nếu agent bị xâm phạm. Cần xem xét nguyên tắc "Đặc quyền Tối thiểu" (Least Privilege) và nghiên cứu khả năng sử dụng một tài khoản dịch vụ tùy chỉnh với chỉ những quyền thực sự cần thiết, mặc dù điều này phức tạp hơn trong việc thiết lập.
- **Xác thực và Phân quyền Lệnh Từ Xa:** Server trung tâm chịu trách nhiệm xác thực mạnh mẽ các yêu cầu từ giao diện quản lý trước khi gửi lệnh đến agent. Cân nhắc việc phân loại các lệnh theo mức độ nguy hiểm và yêu cầu xác thực bổ sung cho các lệnh nhạy cảm. Agent nên có danh sách trắng (whitelist) các lệnh an toàn hoặc kiểm tra chữ ký số của các script/lệnh trước khi thực thi (nếu server hỗ trợ).
- **Input Validation:** Agent phải xác thực kỹ lưỡng mọi dữ liệu nhận được từ server, đặc biệt là nội dung của các lệnh, để tránh các lỗ hổng bảo mật như command injection.
- **Mutex duy nhất:** Đảm bảo tên Mutex (`Global\\CMSAgentSingletonMutex_<GUID>`) là duy nhất trên toàn hệ thống bằng cách sử dụng GUID để tránh xung đột với các ứng dụng khác.
- **Làm mới Token:** Triển khai cơ chế làm mới token chủ động (nếu server hỗ trợ cung cấp thời gian hết hạn) và làm mới khi gặp lỗi xác thực (HTTP 401) để duy trì phiên làm việc an toàn.

## 8. Logging

Hệ thống logging đóng vai trò quan trọng trong việc theo dõi hoạt động, gỡ lỗi và giám sát an ninh.

- Sử dụng **Serilog** làm framework logging chính, cho phép cấu hình linh hoạt và mở rộng.
- Cấu hình logging được định nghĩa trong file `appsettings.json`, cho phép tùy chỉnh mức độ log, định dạng output, và các "sink" (nơi ghi log).
- Ghi log ra nhiều "sink" đồng thời:
    - **File:** Log được ghi vào các file text (ví dụ: `agent_YYYYMMDD.log`) trong thư mục `C:\ProgramData\CMSAgent\logs\`. File log được xoay vòng theo ngày và có giới hạn về số lượng file cũ được giữ lại (cấu hình trong `appsettings.json`).
    - **Windows Event Log:** Các sự kiện quan trọng, đặc biệt là các lỗi nghiêm trọng hoặc các thông báo về khởi động/dừng service, được ghi vào Windows Event Log (thường là log "Application") với một "Source" tùy chỉnh (ví dụ: "CMSAgentService").
    - **Console:** Khi agent được chạy ở chế độ debug (`CMSAgent.exe debug`), log cũng được xuất ra console để dễ dàng theo dõi trực tiếp.
- Hỗ trợ nhiều mức độ log (Verbose, Debug, Information, Warning, Error, Fatal) để kiểm soát chi tiết của thông tin được ghi.
- Ghi log theo ngữ cảnh (SourceContext), thường là namespace và tên lớp phát sinh log, giúp dễ dàng xác định nguồn gốc của thông báo log.
- Có khả năng thu thập và gửi log từ xa theo yêu cầu của server (xem Phần IX.5 của Tài liệu Toàn Diện).

## 9. Triển khai và Cài đặt (Deployment and Installation)

Quá trình triển khai và cài đặt CMSAgent trên máy client được thực hiện thông qua một bộ cài đặt.

- Agent được đóng gói thành một bộ cài đặt duy nhất (ví dụ: `Setup.CMSAgent.exe`) sử dụng công cụ tạo bộ cài đặt như **Inno Setup**.
- Trình cài đặt (`Setup.CMSAgent.exe`) sẽ thực hiện các tác vụ sau với quyền Administrator:
    1. **Sao chép file ứng dụng:** Sao chép `CMSAgent.exe`, `CMSUpdater.exe`, `appsettings.json` (phiên bản mặc định đi kèm bộ cài), và tất cả các thư viện DLL cần thiết vào thư mục cài đặt (ví dụ: `C:\Program Files\CMSAgent`).
    2. **Tạo cấu trúc thư mục dữ liệu:** Tạo thư mục `C:\ProgramData\CMSAgent` và các thư mục con cần thiết (`logs`, `runtime_config`, `updates`, `error_reports`, `offline_queue`).
    3. **Thiết lập quyền truy cập thư mục:** Áp dụng các quyền truy cập cần thiết cho thư mục cài đặt và thư mục dữ liệu (chi tiết trong Phần VIII.3).
    4. **Thực thi cấu hình ban đầu:** Tự động chạy lệnh `CMSAgent.exe configure` để người dùng cung cấp thông tin định danh ban đầu (vị trí, phòng) và để agent thực hiện quá trình xác thực đầu tiên với server để lấy `agentToken`.
    5. **Đăng ký và khởi động Windows Service:** Đăng ký `CMSAgent.exe` làm một Windows Service với các thông số như ServiceName ("CMSAgentService"), DisplayName, Description, StartType (Automatic), và ServiceAccount (LocalSystem). Sau đó, khởi động service.

## 10. Sơ đồ Kiến trúc Cấp cao (Mô tả bằng Text)

Sơ đồ dưới đây minh họa các thành phần chính và luồng tương tác cơ bản trong hệ thống CMSAgent:

```
+---------------------+      HTTPS/API (Định danh, Cập nhật, Lỗi)   +---------------------+
|     Máy Client      |<------------------------------------------>|    Server Trung Tâm   |
|  (Windows Service)  |      WSS/Socket.IO (Trạng thái, Lệnh, KQ)  | (Backend Application)|
| +-----------------+ |<------------------------------------------>+---------------------+
| |   CMSAgent.exe  | |
| | +-------------+ | |
| | | Core Logic  | | |
| | | (Quản lý    | | |
| | |  trạng thái,| | |
| | |  điều phối) | | |
| | +-------------+ | |
| | | Communication|---+-----> (Giao tiếp HTTP & WebSocket)
| | | (HTTP, WS)  |   |
| | +-------------+ <---+ (Nhận lệnh, thông báo)
| | | Configuration|---+-----> (Đọc appsettings.json, runtime_config.json)
| | +-------------+ | |
| | | Commands    |---+-----> (Thực thi lệnh, xử lý kết quả)
| | +-------------+ | |
| | | Monitoring  |---+-----> (Thu thập thông tin HW, tài nguyên)
| | +-------------+ | |
| | | Update      |---+-----> (Kích hoạt CMSUpdater.exe)
| | +-------------+ | |
| | | Logging     |---+-----> (Ghi log ra file, EventLog)
| | +-------------+ | |
| | | Security    |---+-----> (Mã hóa token)
| | +-------------+ | |
| | | CLI Handler |---+-----> (Xử lý lệnh từ console)
| | +-------------+ | |
| | | Persistence |---+-----> (Lưu/đọc queue offline)
| | +-------------+ | |
| +-----------------+ |
|         ^           |
|         |           | (Khởi chạy)
|         +-----------+
|                     |
| +-----------------+ |
| | CMSUpdater.exe  | |  (Tiến trình cập nhật độc lập, thay thế file CMSAgent.exe)
| +-----------------+ |
+---------------------+
        |
        | (Đọc/Ghi file cấu hình, log, gói cập nhật)
        v
+-------------------------------------------------+
|   Hệ Thống File trên Máy Client                 |
| (C:\Program Files\CMSAgent - Thư mục cài đặt)   |
| (C:\ProgramData\CMSAgent - Thư mục dữ liệu)     |
+-------------------------------------------------+

```

**Luồng chính trong hoạt động:**

1. **CMSAgent Service** (`CMSAgent.exe`) chạy nền, liên tục thực hiện các tác vụ theo cấu hình.
2. **Thu thập dữ liệu:** Module `Monitoring` thu thập thông tin tài nguyên và phần cứng.
3. **Giao tiếp:** Module `Communication` gửi dữ liệu trạng thái lên **Server Trung Tâm** qua WebSocket và nhận lệnh/thông báo từ server. Các tác vụ như định danh, kiểm tra cập nhật, báo cáo lỗi sử dụng HTTP API.
4. **Thực thi lệnh:** Module `Commands` xử lý các lệnh nhận được từ server.
5. **Cập nhật:** Module `Update` kiểm tra, tải gói cập nhật, sau đó khởi chạy **CMSUpdater.exe**.
6. **CMSUpdater.exe** thực hiện việc dừng CMSAgent Service cũ, thay thế các file bằng phiên bản mới, và khởi động lại CMSAgent Service đã được cập nhật.
7. **Cấu hình:** Module `Configuration` đọc các thiết lập từ **`appsettings.json`** (cấu hình chính) và **`runtime_config.json`** (cấu hình runtime đặc thù cho máy).
8. **Logging:** Module `Logging` ghi lại các hoạt động và lỗi vào hệ thống file và Windows Event Log.
9. **Persistence:** Module `Persistence` xử lý việc lưu trữ dữ liệu tạm thời khi không có kết nối mạng.