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
PrivilegesRequiredOverridesAllowed=commandline
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

[Code]
var
  SetupCompleted: Boolean;
  CurrentStage: Integer;

function InitializeSetup(): Boolean;
begin
  Result := True;
  SetupCompleted := False;
  CurrentStage := 0;
  // Check if running with admin rights
  if not IsAdmin then
  begin
    MsgBox('This setup requires administrator privileges. Please run as administrator.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  // Stage 1: Create required directories
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

procedure CleanupInstallation;
var
  ResultCode: Integer;
begin
  // Stop and delete service
  Exec('sc.exe', 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Delete ProgramData directory
  DelTree(ExpandConstant('{commonappdata}\CMSAgent'), True, True, True);
  
  // Delete Program Files directory
  DelTree(ExpandConstant('{app}'), True, True, True);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  ConfigFailureFile: string;
begin
  if CurStep = ssPostInstall then
  begin
    // Stage 1: Set permissions and register service
    if CurrentStage = 0 then
    begin
      // Set permissions for ProgramData directory
      Exec('icacls.exe', ExpandConstant('"{commonappdata}\CMSAgent" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F" /grant "BUILTIN\Administrators:(OI)(CI)RX"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      
      // Register Windows Service
      Exec('sc.exe', 'create {#MyServiceName} binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto DisplayName= "{#MyServiceDisplayName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      
      // Set service description
      Exec('sc.exe', 'description {#MyServiceName} "{#MyServiceDescription}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      
      // Register Event Log Source
      Exec('eventcreate.exe', '/ID 1 /L APPLICATION /T INFORMATION /SO {#MyServiceName} /D "{#MyServiceDescription}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      
      CurrentStage := 1;
    end;
    
    // Stage 2: Run configuration
    if CurrentStage = 1 then
    begin
      // Run configure
      Exec(ExpandConstant('{app}\{#MyAppExeName}'), 'configure', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
      
      // Check for configuration failure flag
      ConfigFailureFile := ExpandConstant('{commonappdata}\CMSAgent\config_failure.flag');
      if FileExists(ConfigFailureFile) then
      begin
        // Delete the flag file
        DeleteFile(ConfigFailureFile);
        
        // Show warning message about cleanup
        MsgBox('Configuration failed. The installation will be rolled back and all CMSAgent data will be removed.' + #13#10#13#10 +
               'The following actions will be performed:' + #13#10 +
               '1. Stop and remove CMSAgent service' + #13#10 +
               '2. Delete all CMSAgent data from ProgramData' + #13#10 +
               '3. Remove CMSAgent from Program Files', mbInformation, MB_OK);
        
        // Cleanup installation
        CleanupInstallation();
        
        // Show completion message
        MsgBox('CMSAgent has been completely removed from your system.', mbInformation, MB_OK);
        
        // Mark setup as incomplete
        SetupCompleted := False;
        
        // Abort installation
        Abort;
      end;
      
      CurrentStage := 2;
    end;
    
    // Stage 3: Start service if configuration was successful
    if CurrentStage = 2 then
    begin
      if ResultCode = 0 then
      begin
        // Start the service
        Exec('sc.exe', 'start {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        // Mark setup as complete
        SetupCompleted := True;
      end
      else
      begin
        // Configuration failed but no flag file was created
        MsgBox('Configuration failed. The installation will be rolled back.', mbInformation, MB_OK);
        CleanupInstallation();
        SetupCompleted := False;
        Abort;
      end;
    end;
  end
  else if CurStep = ssDone then
  begin
    if not SetupCompleted then
    begin
      MsgBox('Setup was not completed successfully. Please check the installation log for details.', mbError, MB_OK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Show warning message about cleanup
    MsgBox('You are about to uninstall CMSAgent. All CMSAgent data will be removed.' + #13#10#13#10 +
           'The following actions will be performed:' + #13#10 +
           '1. Stop and remove CMSAgent service' + #13#10 +
           '2. Delete all CMSAgent data from ProgramData' + #13#10 +
           '3. Remove CMSAgent from Program Files', mbInformation, MB_OK);
    
    // Cleanup installation
    CleanupInstallation();
    
    // Show completion message
    MsgBox('CMSAgent has been completely removed from your system.', mbInformation, MB_OK);
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{commonappdata}\CMSAgent" 



















