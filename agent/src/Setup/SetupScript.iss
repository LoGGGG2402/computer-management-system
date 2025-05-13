[Setup]
; Tên ứng dụng và phiên bản
AppName=CMSAgent
AppVersion=1.1.0
AppPublisher=Company Name
AppPublisherURL=https://company.example.com
AppSupportURL=https://support.example.com
AppUpdatesURL=https://updates.example.com

; Thư mục cài đặt mặc định
DefaultDirName={pf}\CMSAgent
DefaultGroupName=CMSAgent

; Yêu cầu quyền quản trị viên để cài đặt
PrivilegesRequired=admin

; Không cho phép cài đặt trên Windows các phiên bản cũ
MinVersion=10.0

; Tùy chọn giao diện
WizardStyle=modern
SetupIconFile=..\CMSAgent\Resources\app_icon.ico
UninstallDisplayIcon={app}\CMSAgent.exe

; Tạo file uninstall
Uninstallable=yes
UninstallDisplayName=CMSAgent
CreateUninstallRegKey=yes

; Thư mục tạm trong quá trình cài đặt
OutputDir=..\..\build\installer
OutputBaseFilename=Setup.CMSAgent

; Các tùy chọn nén
Compression=lzma
SolidCompression=yes

; Không hiển thị cảnh báo khi cài đặt trên hệ thống không hỗ trợ
UsePreviousAppDir=no

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"

[Files]
; Sao chép file thực thi chính và các file hỗ trợ
Source: "..\..\build\release\CMSAgent\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\..\build\release\CMSUpdater\*"; DestDir: "{app}\CMSUpdater"; Flags: ignoreversion recursesubdirs
Source: "..\Setup\set_permissions.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

; Sao chép file cấu hình mặc định
Source: "..\CMSAgent\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\CMSAgent"; Filename: "{app}\CMSAgent.exe"
Name: "{group}\Cấu hình CMSAgent"; Filename: "{app}\CMSAgent.exe"; Parameters: "configure"
Name: "{group}\Gỡ cài đặt CMSAgent"; Filename: "{uninstallexe}"

[Run]
; Đặt quyền cho thư mục dữ liệu bằng script PowerShell
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\set_permissions.ps1"" -DataPath ""C:\ProgramData\CMSAgent"""; Flags: runhidden; Description: "Thiết lập quyền cho thư mục dữ liệu"; StatusMsg: "Đang thiết lập quyền cho thư mục dữ liệu..."

; Chạy lệnh cấu hình sau khi cài đặt
Filename: "{app}\CMSAgent.exe"; Parameters: "configure"; Flags: runasoriginaluser; Description: "Cấu hình CMSAgent"; StatusMsg: "Đang cấu hình CMSAgent..."; Check: ShouldRunConfigure

; Đăng ký và khởi động Windows Service
Filename: "{app}\CMSAgent.exe"; Parameters: "install-service"; Flags: runhidden; Description: "Đăng ký và khởi động Windows Service"; StatusMsg: "Đang đăng ký và khởi động dịch vụ..."

; Tùy chọn khởi động CMSAgent sau khi cài đặt
Filename: "{app}\CMSAgent.exe"; Parameters: "start"; Flags: nowait postinstall runhidden; Description: "Khởi động CMSAgent"; StatusMsg: "Đang khởi động CMSAgent..."

[UninstallRun]
; Dừng và gỡ bỏ service khi gỡ cài đặt
Filename: "{app}\CMSAgent.exe"; Parameters: "stop"; Flags: runhidden
Filename: "{app}\CMSAgent.exe"; Parameters: "uninstall"; Flags: runhidden

[Registry]
; Đăng ký đường dẫn cài đặt trong registry
Root: HKLM; Subkey: "SOFTWARE\CMSAgent"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\CMSAgent"; ValueType: string; ValueName: "DataPath"; ValueData: "C:\ProgramData\CMSAgent"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\CMSAgent"; ValueType: string; ValueName: "Version"; ValueData: "{#SetupSetting('AppVersion')}"; Flags: uninsdeletekey

[Code]
function ShouldRunConfigure: Boolean;
begin
  // Luôn chạy cấu hình khi cài đặt mới
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Thực hiện các hành động sau khi cài đặt hoàn tất
    // Có thể thêm code kiểm tra hoặc ghi log tại đây
  end;
end;
