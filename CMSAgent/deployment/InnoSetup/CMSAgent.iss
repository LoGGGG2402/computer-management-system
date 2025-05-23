#define MyAppName "CMSAgent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Computer Management System"
#define MyAppExeName "CMSAgent.Service.exe"
#define MyServiceName "CMSAgent"
#define MyServiceDisplayName "Computer Management System Agent"
#define MyServiceDescription "Agent collects system information and executes tasks for the Computer Management System."

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
AppId={{A1B2C3D4-E5F6-4A5B-8C7D-9E0F1A2B3C4D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Require admin privileges
PrivilegesRequired=admin
; Require admin rights for all users
PrivilegesRequiredOverridesAllowed=dialog
; Show UAC shield icon
SetupIconFile=icon.ico
OutputDir=Output
OutputBaseFilename=Setup.CMSAgent.v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Add uninstall log
UninstallLogMode=overwrite

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Run configure first
Filename: "{app}\{#MyAppExeName}"; Parameters: "configure"; Description: "Configure CMSAgent"; Flags: postinstall waituntilterminated runasoriginaluser

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Check if running with admin rights
  if not IsAdmin then
  begin
    MsgBox('This setup requires administrator privileges. Please run as administrator.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  // Create required directories
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\logs')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\logs'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\runtime_config')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\runtime_config'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\updates')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\updates'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\updates\download')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\updates\download'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\updates\extracted')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\updates\extracted'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\updates\backup')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\updates\backup'));
  if not DirExists(ExpandConstant('{commonappdata}\CMSAgent\error_reports')) then
    CreateDir(ExpandConstant('{commonappdata}\CMSAgent\error_reports'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Set permissions for ProgramData directory
    Exec('icacls.exe', ExpandConstant('"{commonappdata}\CMSAgent" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F" /grant "BUILTIN\Administrators:(OI)(CI)RX"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Register Windows Service
    Exec('sc.exe', 'create {#MyServiceName} binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto DisplayName= "{#MyServiceDisplayName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Set service description
    Exec('sc.exe', 'description {#MyServiceName} "{#MyServiceDescription}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Register Event Log Source
    Exec('eventcreate.exe', '/ID 1 /L APPLICATION /T INFORMATION /SO {#MyServiceName} /D "{#MyServiceDescription}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Run configure first
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), 'configure', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
    
    // Only start the service if configure was successful
    if ResultCode = 0 then
    begin
      // Start the service
      Exec('sc.exe', 'start {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop and delete service before uninstall
    Exec('sc.exe', 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc.exe', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end; 













