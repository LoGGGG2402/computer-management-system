#define MyAppName "CMSAgent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Computer Management System"
#define MyAppExeName "CMSAgent.Service.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
AppId={{A1B2C3D4-E5F6-4A5B-8C7D-9E0F1A2B3C4D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Uncomment the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=Setup.CMSAgent.v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "Updater\CMSUpdater.exe"; DestDir: "{app}\Updater"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "configure"; Description: "Configure CMSAgent"; Flags: postinstall nowait

[Code]
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
begin
  if CurStep = ssPostInstall then
  begin
    // Set permissions for ProgramData directory
    Exec('icacls.exe', ExpandConstant('"{commonappdata}\CMSAgent" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F" /grant "BUILTIN\Administrators:(OI)(CI)RX"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Register Windows Service
    Exec('sc.exe', 'create CMSAgentService binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto DisplayName= "Computer Management System Agent"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Register Event Log Source
    Exec('eventcreate.exe', '/ID 1 /L APPLICATION /T INFORMATION /SO CMSAgentService /D "Computer Management System Agent Service"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end; 