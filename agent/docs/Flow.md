# Sơ đồ Luồng Hoạt động CMSAgent

Phần này cung cấp các sơ đồ luồng (flowcharts) mô tả các quy trình hoạt động chính của CMSAgent, dựa trên "Tài liệu Toàn Diện CMSAgent v7.4" và "Kiến trúc Hệ thống CMSAgent". Các sơ đồ được biểu diễn bằng cú pháp Mermaid.

## 1. Luồng Cài đặt và Cấu Hình Ban Đầu (Phần III - Tài liệu Toàn Diện)

```mermaid
graph TD
    A["Người dùng chạy Setup.CMSAgent.exe với quyền Admin"] --> B{"Kiểm tra quyền Admin"};
    B -- "Thiếu quyền" --> BA["Thông báo lỗi, thoát"];
    B -- "Đủ quyền" --> C["Sao chép file ứng dụng vào C:\\Program Files\\CMSAgent"];
    C --> D["Tạo cấu trúc thư mục dữ liệu tại C:\\ProgramData\\CMSAgent"];
    D --> E["Thiết lập quyền Full Control cho LocalSystem trên C:\\ProgramData\\CMSAgent"];
    E --> F["Trình cài đặt thực thi 'CMSAgent.exe configure'"];
    F --> G{"Mở Console tương tác"};
    G --> H["Kiểm tra/Tạo Device ID trong runtime_config.json"];
    H --> I{"Nhập thông tin vị trí (RoomName, PosX, PosY)"};
    I -- "Hủy (Ctrl+C)" --> IA["Thoát tiến trình cấu hình"];
    I -- "Nhập xong" --> J["Gửi yêu cầu POST /api/agent/identify đến Server"];
    J --> K{"Xử lý phản hồi Server"};
    K -- "Lỗi vị trí (position_error)" --> L{"Hỏi người dùng thử lại?"};
    L -- "Có" --> I;
    L -- "Không" --> IA;
    K -- "Yêu cầu MFA (mfa_required)" --> M["Thông báo yêu cầu MFA"];
    M --> N{"Nhập mã MFA"};
    N -- "Hủy/Bỏ trống" --> IA;
    N -- "Nhập xong" --> O["Gửi POST /api/agent/verify-mfa"];
    O --> P{"Xử lý phản hồi MFA"};
    P -- "MFA Thất bại" --> Q{"Hỏi người dùng thử lại?"};
    Q -- "Có" --> N;
    Q -- "Không" --> IA;
    P -- "MFA Thành công (có agentToken)" --> R["Lưu agentToken tạm thời"];
    K -- "Định danh thành công (có agentToken)" --> R;
    K -- "Định danh thành công (agent đã tồn tại, không token mới)" --> S["Thông báo agent đã đăng ký"];
    S --> T["Lưu cấu hình runtime và token (mã hóa)"];
    R --> T;
    K -- "Lỗi khác (HTTP 500, mạng)" --> U{"Hỏi người dùng thử lại?"};
    U -- "Có" --> J;
    U -- "Không" --> IA;
    T --> V["Thông báo lưu cấu hình thành công"];
    V --> W["Trình cài đặt đăng ký CMSAgent làm Windows Service"];
    W -- "ServiceName: CMSAgentService" --> W1["DisplayName: Computer Management System Agent"];
    W1 -- "StartType: Automatic" --> W2["ServiceAccount: LocalSystem"];
    W2 --> X["Trình cài đặt khởi động Service"];
    X --> Y["Hoàn tất cài đặt, thông báo thành công"];
```

## 2. Luồng Hoạt động Thường Xuyên của Agent (Phần IV - Tài liệu Toàn Diện)

```mermaid
graph TD
    subgraph Khởi Động Agent
        A["SCM khởi động CMSAgent.exe"] --> B("Trạng thái: INITIALIZING");
        B --> C["Thiết lập Logging"];
        C --> D["Đảm bảo cấu trúc thư mục dữ liệu"];
        D --> E["Đảm bảo chỉ một instance (Mutex)"];
        E -- "Đã có instance khác" --> EA["Ghi log lỗi, thoát"];
        E -- "Chiếm được Mutex" --> F["Tải cấu hình từ appsettings.json và runtime_config.json"];
        F --> G["Xác thực cấu hình"];
        G -- "Lỗi cấu hình" --> GA["Trạng thái: ERROR, thoát"];
        G -- "Cấu hình hợp lệ" --> H["Giải mã agent_token"];
        H -- "Lỗi giải mã" --> GA;
        H -- "Giải mã thành công" --> I["Khởi tạo các Modules"];
    end

    subgraph Kết Nối và Xác Thực
        I --> J("Trạng thái: AUTHENTICATING");
        J --> K["Kết nối WebSocket đến Server"];
        K -- "Thất bại" --> L{"Thử lại kết nối WS theo cấu hình"};
        L --> K;
        K -- "Thành công" --> M["Gửi thông tin xác thực (Header/Event)"];
        M --> N{"Chờ phản hồi xác thực WS"};
        N -- "agent:ws_auth_success" --> O("Trạng thái: CONNECTED");
        N -- "agent:ws_auth_failed" --> P["Thử POST /api/agent/identify"];
        P -- "Identify thành công, có token mới" --> Q["Cập nhật token, quay lại kết nối WS"];
        Q --> K;
        P -- "Identify yêu cầu MFA / Thất bại khác" --> R("Trạng thái: DISCONNECTED");
        R --> L;
    end

    O --> S["Gửi thông tin phần cứng ban đầu (POST /hardware-info)"];
    S -- "Lỗi" --> SA["Ghi log, tiếp tục"];

    subgraph Vòng Lặp Hoạt Động Chính
        O --> T["Bắt đầu vòng lặp chính (Trạng thái: CONNECTED)"];
        T --> U["Gửi báo cáo trạng thái định kỳ (agent:status_update)"];
        T --> V{"Kiểm tra cập nhật phiên bản mới?"};
        V -- "Có phiên bản mới" --> W["Kích hoạt Luồng Cập Nhật Agent (Phần V)"];
        W --> WB("Trạng thái: UPDATING");
        T --> X{"Lắng nghe lệnh từ Server (command:execute)"};
        X -- "Nhận lệnh" --> Y["Đưa lệnh vào hàng đợi"];
        Y --> Z["Worker xử lý lệnh"];
        Z --> AA["Thực thi lệnh, thu thập kết quả"];
        AA --> AB["Gửi kết quả (agent:command_result)"];
        T --> AC{"Có lỗi không mong muốn?"};
        AC -- "Có" --> AD["Báo cáo lỗi (POST /api/agent/report-error)"];
        AD -- "Gửi thất bại" --> AE["Lưu lỗi vào error_reports/"];
        T --> AF{"Kết nối WebSocket còn tốt?"};
        AF -- "Mất kết nối" --> R;
    end

    subgraph Dừng Hoạt Động
        AG["SCM yêu cầu dừng Service"] --> AH("Trạng thái: STOPPING");
        AH --> AI["Ngắt kết nối WebSocket"];
        AI --> AJ["Hoàn thành các lệnh đang xử lý"];
        AJ --> AK["Hủy các Timers"];
        AK --> AL["Giải phóng Mutex"];
        AL --> AM["Ghi log dừng hoàn tất, thoát"];
    end
```

## 3. Luồng Xử lý Lệnh từ Server (Phần IV.10 - Tài liệu Toàn Diện)

```mermaid
graph TD
    A["Agent ở trạng thái CONNECTED, lắng nghe WebSocket"] --> B{"Nhận sự kiện 'command:execute' từ Server"};
    B -- "Có lệnh mới (commandId, command, commandType)" --> C["Đưa lệnh vào hàng đợi (Command Queue)"];
    C --> D{"Hàng đợi có đầy không? (max_queue_size)"};
    D -- "Đầy" --> DA["Ghi log lỗi COMMAND_QUEUE_FULL, có thể từ chối lệnh"];
    D -- "Không đầy" --> E["Một Worker Thread lấy lệnh từ hàng đợi"];
    E --> F["CommandHandlerFactory tạo Handler dựa trên commandType"];
    F --> G{"Loại Handler?"};
    G -- "ConsoleCommandHandler" --> H["Thực thi lệnh console"];
    G -- "SystemActionCommandHandler" --> I["Thực thi hành động hệ thống"];
    G -- "Handler khác" --> J["Thực thi theo logic handler đó"];
    subgraph Thực Thi Lệnh
        K["Bắt đầu thực thi lệnh"] --> L["Theo dõi thời gian thực thi (giới hạn bởi default_timeout_sec)"];
        L --> M["Thu thập stdout, stderr (nếu có), exitCode"];
        M --> N{"Lệnh hoàn thành/Timeout/Lỗi?"};
    end
    H --> K;
    I --> K;
    J --> K;
    N -- "Hoàn thành thành công" --> O["Chuẩn bị kết quả: success=true, result={stdout, stderr, exitCode}"];
    N -- "Lệnh lỗi/Timeout" --> P["Chuẩn bị kết quả: success=false, result={errorMessage, errorCode}"];
    O --> Q["Gửi kết quả (commandId, success, type, result) về Server qua WebSocket 'agent:command_result'"];
    P --> Q;
    Q --> A;
```

## 4. Luồng Cập nhật Agent (Phần V - Tài liệu Toàn Diện)

```mermaid
graph TD
    A["Agent nhận thông tin phiên bản mới (HTTP hoặc WebSocket)"] --> B("Trạng thái: UPDATING");
    B --> C["Thông báo Server: 'update_started'"];
    C --> D{"Tải gói cập nhật (.zip) về updates/download/"};
    D -- "Lỗi tải" --> DA["Xử lý lỗi tải (retry, báo lỗi server, về CONNECTED)"];
    D -- "Tải thành công" --> DB["Thông báo Server: 'update_downloaded'"];
    DB --> E{"Xác minh Checksum gói cập nhật"};
    E -- "Checksum không khớp" --> EA["Xóa file, báo lỗi server, về CONNECTED"];
    E -- "Checksum khớp" --> F["Giải nén gói cập nhật vào updates/extracted/"];
    F -- "Lỗi giải nén" --> FA["Báo lỗi server, về CONNECTED"];
    F -- "Giải nén thành công" --> FB["Thông báo Server: 'update_extracted'"];
    FB --> G["Xác định CMSUpdater.exe (ưu tiên bản mới)"];
    G --> H["Khởi chạy CMSUpdater.exe với các tham số cần thiết"];
    H --> HA["Thông báo Server: 'updater_launched'"];
    HA --> I["Agent cũ (Service) bắt đầu quá trình dừng an toàn"];

    subgraph CMSUpdater.exe Process
        J["CMSUpdater.exe bắt đầu"] --> K["Thiết lập logging riêng"];
        K --> L{"Chờ CMSAgent.exe cũ dừng hoàn toàn (timeout)"};
        L -- "Timeout" --> LA["Ghi log lỗi, thoát Updater, cân nhắc rollback nếu đã backup"];
        L -- "Agent cũ đã dừng" --> M["Sao lưu thư mục cài đặt agent cũ"];
        M -- "Lỗi sao lưu" --> MA["Ghi log lỗi nghiêm trọng, thoát Updater"];
        M -- "Sao lưu thành công" --> N["Di chuyển/Sao chép file agent mới vào thư mục cài đặt"];
        N -- "Lỗi triển khai" --> O{"Thực hiện Rollback"};
        O -- "Rollback thành công" --> OA["Ghi log, thoát Updater"];
        O -- "Rollback thất bại" --> OB["Ghi log lỗi nghiêm trọng, thoát Updater"];
        N -- "Triển khai thành công" --> P["Khởi động CMSAgent Service mới (qua SCM)"];
        P -- "Lỗi khởi động Service mới" --> Q{"Thực hiện Rollback, cố gắng khởi động Service cũ"};
        Q -- "Rollback và khởi động Service cũ thành công" --> QA["Agent cũ báo lỗi cập nhật lên Server, thoát Updater"];
        Q -- "Rollback thất bại / Service cũ không khởi động được" --> QB["Ghi log lỗi nghiêm trọng, thoát Updater"];
        P -- "Service mới khởi động thành công" --> R{"Watchdog: Theo dõi Service mới trong thời gian ngắn"};
        R -- "Service mới ổn định" --> S["Dọn dẹp: Xóa backup, file tạm"];
        S --> SA["Ghi log cập nhật thành công"];
        SA --> SB["Agent mới (sau khi kết nối) thông báo Server: 'update_success'"];
        SB --> SC["CMSUpdater.exe thoát"];
        R -- "Service mới crash liên tục" --> Q;
    end
    I --> J;
```

## 5. Luồng Xác thực WebSocket và Làm mới Token (Phần IV.9, VIII.6 - Tài liệu Toàn Diện)

```mermaid
graph TD
    A["Agent cần kết nối/đã mất kết nối WebSocket"] --> B("Trạng thái: AUTHENTICATING");
    B --> C{"Có agent_token hợp lệ cục bộ không?"};
    C -- "Có" --> D["Kết nối WebSocket với token hiện tại"];
    C -- "Không / Token có thể hết hạn" --> E["Thực hiện POST /api/agent/identify"];
    E -- "Phản hồi thành công, có token mới" --> F["Lưu token mới (mã hóa), cập nhật token cục bộ"];
    F --> D;
    E -- "Phản hồi yêu cầu MFA" --> FA["Ghi log, không thể xử lý MFA tự động, trạng thái DISCONNECTED, thử lại sau"];
    E -- "Phản hồi lỗi khác" --> FB["Ghi log, trạng thái DISCONNECTED, thử lại sau"];

    D --> G{"Chờ phản hồi xác thực từ WebSocket Server"};
    G -- "agent:ws_auth_success" --> H("Trạng thái: CONNECTED");
    G -- "agent:ws_auth_failed (ví dụ: token không hợp lệ/hết hạn)" --> I["Ghi log lỗi xác thực WS"];
    I --> E;

    subgraph Làm mới Token Chủ Động
        J["Timer định kỳ (ví dụ: 24 giờ) kích hoạt"] --> K["Thực hiện POST /api/agent/identify với forceRenewToken:true"];
        K -- "Phản hồi thành công, có token mới" --> L["Lưu token mới (mã hóa), cập nhật token cục bộ"];
        K -- "Phản hồi lỗi" --> M["Ghi log lỗi làm mới token, agent tiếp tục với token cũ nếu còn"];
    end

    H --> N{"Agent đang hoạt động (CONNECTED)"};
    N -- "Gặp lỗi HTTP 401 khi gọi API" --> I;
```