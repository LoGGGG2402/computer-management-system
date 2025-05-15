[Setup]
; Tên ứng dụng và phiên bản
AppName=CMSAgent
AppVersion={#AppVersion}
AppPublisher=Company Name
AppPublisherURL=https://company.example.com
AppSupportURL=https://support.example.com
AppUpdatesURL=https://updates.example.com

; Thư mục cài đặt mặc định
DefaultDirName={commonpf}\CMSAgent
DefaultGroupName=CMSAgent

; Yêu cầu quyền quản trị viên để cài đặt
PrivilegesRequired=admin

; Không cho phép cài đặt trên Windows các phiên bản cũ
MinVersion=10.0

; Tùy chọn giao diện
WizardStyle=modern
SetupIconFile=..\Setup\icon.ico
UninstallDisplayIcon={app}\CMSAgent.exe

; Tạo file uninstall
Uninstallable=yes
UninstallDisplayName=CMSAgent
CreateUninstallRegKey=yes

; Thư mục tạm trong quá trình cài đặt
OutputDir=..\..\build\installer
OutputBaseFilename=Setup.CMSAgent.v{#AppVersion}

; Các tùy chọn nén
Compression=lzma
SolidCompression=yes

; Không hiển thị cảnh báo khi cài đặt trên hệ thống không hỗ trợ
UsePreviousAppDir=no

[Files]
; Sao chép file thực thi chính và các file hỗ trợ
Source: "..\..\build\release\CMSAgent\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Sao chép file cấu hình mặc định
Source: "..\CMSAgent\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\CMSAgent"; Filename: "{app}\CMSAgent.exe"
Name: "{group}\Cấu hình CMSAgent"; Filename: "{app}\CMSAgent.exe"; Parameters: "configure"
Name: "{group}\Gỡ cài đặt CMSAgent"; Filename: "{uninstallexe}"

[Dirs]
; Tạo các thư mục dữ liệu cần thiết theo tài liệu
Name: "{commonappdata}\CMSAgent"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\logs"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\runtime_config"; Permissions: admins-readexec system-full
Name: "{commonappdata}\CMSAgent\updates"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\updates\download"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\updates\extracted"; Permissions: admins-full system-full 
Name: "{commonappdata}\CMSAgent\updates\backup"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\error_reports"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\offline_queue"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\offline_queue\status_reports"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\offline_queue\command_results"; Permissions: admins-full system-full
Name: "{commonappdata}\CMSAgent\offline_queue\error_reports"; Permissions: admins-full system-full

[Run]
; Chạy lệnh cấu hình sau khi cài đặt
Filename: "{app}\CMSAgent.exe"; Parameters: "configure"; Flags: runasoriginaluser; Description: "Cấu hình CMSAgent"; StatusMsg: "Đang cấu hình CMSAgent..."; Check: ShouldRunConfigure

; Đăng ký và khởi động Windows Service
Filename: "sc.exe"; Parameters: "create CMSAgentService binPath= ""{app}\CMSAgent.exe"""; Flags: runhidden; Description: "Đăng ký Windows Service"; StatusMsg: "Đang đăng ký Windows Service..."
Filename: "sc.exe"; Parameters: "config CMSAgentService start= auto obj= LocalSystem"; Flags: runhidden; Description: "Cấu hình Windows Service"; StatusMsg: "Đang cấu hình Windows Service..."
Filename: "sc.exe"; Parameters: "start CMSAgentService"; Flags: runhidden; Description: "Khởi động Windows Service"; StatusMsg: "Đang khởi động Windows Service..."

[UninstallRun]
; Dừng và gỡ bỏ service khi gỡ cài đặt
Filename: "sc.exe"; Parameters: "stop CMSAgentService"; Flags: runhidden; RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete CMSAgentService"; Flags: runhidden; RunOnceId: "DeleteService"

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

// Thiết lập quyền truy cập cho thư mục dữ liệu
procedure SetupFolderPermissions();
var
  DataPath: String;
  RuntimeConfigPath: String;
  ResultCode: Integer;
begin
  DataPath := ExpandConstant('{commonappdata}\CMSAgent');
  RuntimeConfigPath := DataPath + '\runtime_config';
  
  // Hiển thị thông báo
  Log('Thiết lập quyền cho thư mục dữ liệu...');
  Log('Đường dẫn dữ liệu: ' + DataPath);

  try
    // Thiết lập quyền cho thư mục dữ liệu chính
    Log('Thiết lập quyền cho thư mục dữ liệu chính...');

    // Xóa các quyền kế thừa cũ trước khi áp dụng quyền mới
    Exec('icacls.exe', '"' + DataPath + '" /inheritance:r /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Đã xóa quyền kế thừa cho thư mục chính');

    // Cấp quyền Full Control cho SYSTEM với kế thừa
    Exec('icacls.exe', '"' + DataPath + '" /grant "SYSTEM:(OI)(CI)F" /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Đã cấp quyền Full Control cho SYSTEM trên thư mục chính');

    // Cấp quyền Full Control cho Administrators với kế thừa
    Exec('icacls.exe', '"' + DataPath + '" /grant "Administrators:(OI)(CI)F" /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Đã cấp quyền Full Control cho Administrators trên thư mục chính');

    // Thiết lập quyền đặc biệt cho thư mục runtime_config
    Log('Thiết lập quyền đặc biệt cho thư mục runtime_config...');

    // Xóa quyền kế thừa cho thư mục runtime_config
    Exec('icacls.exe', '"' + RuntimeConfigPath + '" /inheritance:r /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Đã xóa quyền kế thừa cho thư mục runtime_config');

    // Cấp quyền Full Control cho SYSTEM
    Exec('icacls.exe', '"' + RuntimeConfigPath + '" /grant "SYSTEM:(OI)(CI)F" /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Đã cấp quyền Full Control cho SYSTEM trên thư mục runtime_config');

    // Cấp quyền Read & Execute cho Administrators (giới hạn quyền)
    Exec('icacls.exe', '"' + RuntimeConfigPath + '" /grant "Administrators:(OI)(CI)RX" /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Đã cấp quyền Read & Execute cho Administrators trên thư mục runtime_config');

    Log('Thiết lập quyền thành công!');
  except
    Log('Không thể thiết lập quyền!');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Tạo cấu trúc thư mục dữ liệu bổ sung (trong trường hợp [Dirs] không đủ)
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\logs'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\runtime_config'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\updates\download'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\updates\extracted'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\updates\backup'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\error_reports'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\offline_queue\status_reports'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\offline_queue\command_results'));
    ForceDirectories(ExpandConstant('{commonappdata}\CMSAgent\offline_queue\error_reports'));
    
    // Thiết lập quyền cho các thư mục
    SetupFolderPermissions();
  end;
end;
