```
CMSAgentSolution/
├── src/
│   ├── CMSAgent.Service/               # Dự án Windows Service chính (điểm vào)
│   │   ├── CMSAgent.Service.csproj
│   │   ├── Program.cs                  # Cấu hình Generic Host, DI, logging
│   │   ├── AgentWorker.cs              # Kế thừa từ BackgroundService, điều phối AgentCore
│   │   ├── appsettings.json
│   │   └── runtime_config/             # (Thư mục này sẽ được tạo bởi agent lúc chạy)
│   │       └── runtime_config.json     # (Chứa AgentId, RoomConfig, AgentTokenEncrypted)
│   │
│   ├── CMSAgent.Core/                  # Logic nghiệp vụ cốt lõi của agent
│   │   ├── CMSAgent.Core.csproj
│   │   ├── AgentCoreOrchestrator.cs    # Điều phối các module chức năng
│   │   ├── StateManager.cs             # Quản lý trạng thái của agent
│   │   └── Interfaces/                 # Các interface cho các dịch vụ cốt lõi
│   │       └── IAgentCoreOrchestrator.cs
│   │       └── IStateManager.cs
│   │
│   ├── CMSAgent.Common/                # Các lớp, enums, DTOs, hằng số dùng chung
│   │   ├── CMSAgent.Common.csproj
│   │   ├── Constants/
│   │   ├── DTOs/
│   │   ├── Enums/
│   │   └── Models/                     # Các model cấu hình (AgentSettings, RuntimeConfig)
│   │
│   ├── CMSAgent.Communication/         # Xử lý giao tiếp mạng
│   │   ├── CMSAgent.Communication.csproj
│   │   ├── WebSocketClient.cs          # Client cho Socket.IO (WSS)
│   │   ├── HttpClientWrapper.cs        # Wrapper cho HttpClient (HTTPS), sử dụng IHttpClientFactory, Polly
│   │   └── Interfaces/
│   │       └── IWebSocketClient.cs
│   │       └── IHttpClientWrapper.cs
│   │       └── IOfflineQueueManager.cs # Quản lý hàng đợi offline
│   │   └── OfflineQueueManager.cs      # Triển khai hàng đợi offline (có thể dùng Channels, file, SQLite)
│   │
│   ├── CMSAgent.SystemInformation/     # Thu thập thông tin hệ thống
│   │   ├── CMSAgent.SystemInformation.csproj
│   │   ├── HardwareCollector.cs        # Thu thập thông tin phần cứng (WMI)
│   │   ├── ResourceMonitor.cs          # Giám sát tài nguyên (PerformanceCounter)
│   │   └── Interfaces/
│   │       └── IHardwareCollector.cs
│   │       └── IResourceMonitor.cs
│   │
│   ├── CMSAgent.CommandExecution/      # Thực thi lệnh từ xa
│   │   ├── CMSAgent.CommandExecution.csproj
│   │   ├── CommandExecutor.cs          # Quản lý hàng đợi lệnh và điều phối thực thi
│   │   ├── CommandHandlerFactory.cs    # Tạo handler cho từng loại lệnh
│   │   ├── Handlers/                   # Các handler cụ thể
│   │   │   ├── ConsoleCommandHandler.cs
│   │   │   ├── SystemActionCommandHandler.cs
│   │   │   └── SoftwareInstallCommandHandler.cs # (Cho tính năng cài đặt phần mềm mới)
│   │   └── Interfaces/
│   │       └── ICommandExecutor.cs
│   │       └── ICommandHandlerFactory.cs
│   │       └── ICommandHandler.cs
│   │
│   ├── CMSAgent.Update/                # Xử lý tự động cập nhật agent
│   │   ├── CMSAgent.Update.csproj
│   │   ├── UpdateManager.cs            # Logic kiểm tra, tải, xác minh và khởi chạy updater
│   │   └── Interfaces/
│   │       └── IUpdateManager.cs
│   │
│   ├── CMSAgent.SoftwareManagement/    # (MỚI) Xử lý cài đặt/gỡ cài đặt phần mềm theo yêu cầu
│   │   ├── CMSAgent.SoftwareManagement.csproj
│   │   ├── SoftwareDeploymentService.cs # Logic tải, xác minh, cài đặt/gỡ phần mềm
│   │   └── Interfaces/
│   │       └── ISoftwareDeploymentService.cs
│   │
│   ├── CMSAgent.Security/              # Các thành phần liên quan đến bảo mật
│   │   ├── CMSAgent.Security.csproj
│   │   ├── TokenProtector.cs           # Mã hóa/giải mã token (DPAPI)
│   │   ├── SingletonMutex.cs           # Đảm bảo một instance
│   │   └── Interfaces/
│   │       └── ITokenProtector.cs
│   │       └── ISingletonMutex.cs
│   │
│   ├── CMSAgent.Cli/                   # Xử lý các lệnh giao diện dòng lệnh (CLI)
│   │   ├── CMSAgent.Cli.csproj
│   │   ├── CliOrchestrator.cs          # Điều phối các lệnh CLI
│   │   ├── Commands/                   # Các lớp xử lý cho từng lệnh CLI
│   │   │   ├── ConfigureCommand.cs
│   │   │   ├── InstallServiceCommand.cs
│   │   │   ├── UninstallServiceCommand.cs
│   │   │   ├── StartServiceCommand.cs
│   │   │   ├── StopServiceCommand.cs
│   │   │   └── DebugCommand.cs
│   │   └── ServiceUtils.cs             # Tiện ích tương tác với Windows SCM (sc.exe)
│   │   └── Interfaces/
│   │       └── ICliOrchestrator.cs
│   │
│   ├── CMSUpdater/                     # Dự án riêng cho CMSUpdater.exe (ứng dụng console)
│   │   ├── CMSUpdater.csproj
│   │   ├── Program.cs
│   │   ├── UpdaterCore.cs              # Logic chính của updater (dừng service, sao lưu, thay thế, khởi động)
│   │   ├── RollbackManager.cs          # Xử lý rollback
│   │   ├── FileOperations.cs           # Các tiện ích file
│   │   ├── ServiceControl.cs           # Tương tác với SCM (tối giản)
│   │   └── appsettings.json            # Cấu hình riêng cho Updater (nếu cần)
│   │
│   └── Setup/                          # (Thư mục này chứa script Inno Setup)
│       └── SetupScript.iss
│
├── tests/
│   ├── CMSAgent.Core.Tests/
│   ├── CMSAgent.Communication.Tests/
│   ├── CMSAgent.CommandExecution.Tests/
│   └── ... (Các project unit test khác cho từng module)
│   └── CMSAgent.Integration.Tests/     # Kiểm thử tích hợp
│
├── docs/                               # Tài liệu dự án (Markdown)
│   ├── CMSAgent_Comprehensive_Doc.md
│   └── Flow.md
│
└── build/                              # Các script và output của quá trình build
    ├── build.ps1                       # Script build chính, tạo bộ cài đặt
    ├── build-update.ps1                # Script build gói cập nhật
    └── ... (output sẽ nằm trong các thư mục con như release, update)
```