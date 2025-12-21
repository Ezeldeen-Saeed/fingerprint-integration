; ZkFingerBridge Installer Script
; Inno Setup Script for ZkFingerBridge Windows Service

#define MyAppName "ZkFingerBridge"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Firstsoft"
#define MyAppExeName "ZkFingerBridge.exe"
#define MyServiceName "ZkFingerBridge"

[Setup]
AppId={{B8F5E3A1-2D4C-4E6F-8A9B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\installer-output
OutputBaseFilename=ZkFingerBridge-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=
CloseApplications=force
RestartApplications=no

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy all files from publish folder
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Run Setup Wizard"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Run setup wizard after installation (as the current user, not as service)
Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup"; Description: "Run Setup Wizard"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
var
  ServiceInstalled: Boolean;

// Check if service exists
function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('sc.exe', 'query ' + '{#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

// Stop the service
procedure StopService();
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'stop ' + '{#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000); // Wait for service to stop
end;

// Delete the service
procedure DeleteService();
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'delete ' + '{#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
end;

// Create and start the service
procedure InstallService();
var
  ResultCode: Integer;
  BinPath: String;
begin
  BinPath := ExpandConstant('"{app}\{#MyAppExeName}"');
  
  // Create the service with auto-start
  Exec('sc.exe', 'create ' + '{#MyServiceName}' + 
       ' binPath= ' + BinPath + 
       ' start= auto' +
       ' DisplayName= "ZkFingerBridge Attendance Sync"', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Set service description
  Exec('sc.exe', 'description ' + '{#MyServiceName}' + 
       ' "Syncs fingerprint attendance logs from ZK devices to Firstsoft HR system"', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Start the service
  Exec('sc.exe', 'start ' + '{#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  ServiceInstalled := True;
end;

// Called after installation files are copied
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Install the Windows Service
    InstallService();
  end;
end;

// Called before uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop and remove the service before uninstalling
    if ServiceExists() then
    begin
      StopService();
      DeleteService();
    end;
  end;
end;

// Called during installation preparation
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  
  // If service exists from previous installation, stop and remove it
  if ServiceExists() then
  begin
    StopService();
    DeleteService();
  end;
end;
