# CMSAgent - Agent Quản lý Máy tính Khách

**Ngày cập nhật:** 12 tháng 5 năm 2025

## Giới thiệu

CMSAgent là một ứng dụng client mạnh mẽ được thiết kế để chạy trên các máy tính Windows. Nhiệm vụ chính của nó là thu thập thông tin hệ thống, giám sát tài nguyên, giao tiếp an toàn với một server trung tâm, thực thi các lệnh từ xa và tự động cập nhật phiên bản mới. CMSAgent được thiết kế để hoạt động ổn định như một Windows Service, đảm bảo hoạt động nền, liên tục và tự khởi động cùng hệ thống.

Dự án này bao gồm mã nguồn cho CMSAgent, tiến trình cập nhật CMSUpdater, và các thành phần phụ trợ khác.

## Các Tính Năng Chính

- **Thu thập thông tin hệ thống:**
    - Lấy thông tin chi tiết về phần cứng (OS, CPU, GPU, RAM, Disk).
    - Theo dõi trạng thái sử dụng tài nguyên (CPU, RAM, Disk) theo thời gian thực.
- **Giao tiếp an toàn với Server:**
    - Thiết lập và duy trì kết nối WebSocket (Socket.IO) bảo mật (WSS) với server trung tâm.
    - Sử dụng token xác thực (Agent Token) cho mọi giao tiếp.
    - Giao tiếp API qua HTTPS.
- **Thực thi lệnh từ xa:**
    - Nhận và thực thi các lệnh được gửi từ server (ví dụ: chạy script console, thực hiện các hành động hệ thống).
    - Báo cáo kết quả về server.
- **Tự động cập nhật:**
    - Kiểm tra phiên bản mới định kỳ hoặc nhận thông báo từ server.
    - Tải gói cập nhật, xác minh checksum.
    - Sử dụng một tiến trình `CMSUpdater.exe` riêng biệt để thay thế file một cách an toàn và khởi động lại service.
    - Hỗ trợ rollback tự động nếu phiên bản mới gặp sự cố khi khởi động.
- **Hoạt động như Windows Service:**
    - Chạy nền, liên tục.
    - Tự động khởi động cùng Windows.
    - Đảm bảo chỉ một instance của agent chạy trên mỗi máy.
- **Cấu hình linh hoạt:**
    - Sử dụng `appsettings.json` cho các cấu hình chính của agent và logging (Serilog).
    - Sử dụng `runtime_config.json` cho các thông tin định danh và token đặc thù của từng máy (được tạo trong quá trình `configure`).
- **Logging chi tiết:**
    - Ghi log ra file, Windows Event Log, và console (khi debug).
    - Hỗ trợ nhiều mức độ log.
    - Có khả năng thu thập log từ xa theo yêu cầu của server.
- **Xử lý lỗi và phục hồi:**
    - Cơ chế retry cho các lỗi mạng.
    - Lưu trữ tạm (queue) dữ liệu khi offline và gửi lại khi có kết nối.
    - Xử lý an toàn các lỗi nghiêm trọng.
- **Giao diện dòng lệnh (CLI):**
    - `CMSAgent.exe configure`: Cấu hình agent lần đầu hoặc cấu hình lại.
    - `CMSAgent.exe start/stop/uninstall/debug`: Quản lý service và gỡ lỗi.

## Cấu trúc Thư mục Dự án

(Tham khảo Phần XII trong "Tài liệu Toàn Diện CMSAgent v7.4" để biết chi tiết cấu trúc thư mục dự án.)

```
agent/
├── src/
│   ├── CMSAgent/           # Dự án chính (Windows Service & CLI)
│   ├── CMSUpdater/         # Dự án cho tiến trình Updater
│   ├── CMSAgent.Common/    # Thư viện dùng chung (DTOs, Enums, Constants, Interfaces)
│   └── Setup/              # Script tạo bộ cài đặt (ví dụ: Inno Setup)
├── tests/
│   ├── CMSAgent.UnitTests/
│   └── CMSUpdater.UnitTests/
│   └── CMSAgent.IntegrationTests/
├── docs/                     # Tài liệu dự án
├── scripts/                  # Các script hỗ trợ
├── .gitignore
├── CMSAgent.sln
└── README.md                 # File này

```

## Yêu Cầu Hệ Thống và Phần Mềm Phụ Thuộc

- **Hệ điều hành hỗ trợ:** Windows 10 (1903+), Windows 11, Windows Server 2016/2019/2022 (chỉ 64-bit).
- **.NET Runtime:** Phiên bản .NET mà agent được biên dịch (ví dụ: .NET 6.0 LTS hoặc .NET 8.0 LTS).
- **Thư viện bên ngoài:** Xem chi tiết phiên bản trong Phần II của "Tài liệu Toàn Diện CMSAgent v7.4".

## Cài đặt và Cấu hình

1. **Build dự án:** Build solution `CMSAgent.sln` để tạo các file thực thi `CMSAgent.exe` và `CMSUpdater.exe`.
2. **Tạo gói cài đặt:** Sử dụng script Inno Setup trong thư mục `src/Setup/` để đóng gói các file cần thiết thành một file `Setup.CMSAgent.exe`.
3. **Chạy trình cài đặt:** Thực thi `Setup.CMSAgent.exe` với quyền Administrator trên máy client.
4. **Cấu hình ban đầu:**
    - Sau khi cài đặt, trình cài đặt sẽ tự động chạy lệnh:
    `"C:\Program Files\CMSAgent\CMSAgent.exe" configure`
    - Làm theo hướng dẫn trên giao diện dòng lệnh để nhập thông tin phòng, tọa độ. Agent sẽ kết nối server để xác thực và lấy `agentToken`.
    - Thông tin này (ngoại trừ token đã mã hóa) sẽ được lưu trong `C:\ProgramData\CMSAgent\runtime_config\runtime_config.json`.
    - Các cấu hình hoạt động chính của agent (như URL server, khoảng thời gian báo cáo) được đặt trong `C:\Program Files\CMSAgent\appsettings.json` và có thể được chỉnh sửa thủ công nếu cần.
5. **Service sẽ tự động khởi động** sau khi cấu hình thành công.

## Sử dụng Giao diện Dòng lệnh (CLI)

Sau khi cài đặt, bạn có thể sử dụng `CMSAgent.exe` từ thư mục cài đặt với các tham số sau (yêu cầu quyền Administrator cho hầu hết các lệnh):

- `CMSAgent.exe configure`: Cấu hình lại agent.
- `CMSAgent.exe start`: Khởi động CMSAgent Service.
- `CMSAgent.exe stop`: Dừng CMSAgent Service.
- `CMSAgent.exe uninstall`: Gỡ cài đặt agent.
    - `CMSAgent.exe uninstall --remove-data`: Gỡ cài đặt và xóa toàn bộ dữ liệu của agent.
- `CMSAgent.exe debug`: Chạy agent ở chế độ console để gỡ lỗi.

## Logging

- Log chính của Agent Service: `C:\ProgramData\CMSAgent\logs\agent_YYYYMMDD.log`
- Log của Updater: `C:\ProgramData\CMSAgent\logs\updater_YYYYMMDD_HHMMSS.log`
- Log của tiến trình cấu hình: `C:\ProgramData\CMSAgent\logs\configure_YYYYMMDD_HHMMSS.log`
- Các sự kiện quan trọng cũng được ghi vào Windows Event Log (Application log, Source: "CMSAgentService").

## Đóng góp

(Phần này có thể được bổ sung nếu dự án là mã nguồn mở và chấp nhận đóng góp từ cộng đồng, bao gồm hướng dẫn về cách báo lỗi, đề xuất tính năng, hoặc gửi pull request.)

## Giấy phép

(Thông tin về giấy phép của dự án, ví dụ: MIT, Apache 2.0, hoặc giấy phép độc quyền.)

Để biết thông tin chi tiết hơn về hoạt động, giao tiếp, cấu hình, bảo mật và xử lý lỗi, vui lòng tham khảo tài liệu đầy đủ: **"Tài liệu Toàn Diện: Hoạt động, Giao tiếp và Cấu hình CMSAgent v7.4"**.