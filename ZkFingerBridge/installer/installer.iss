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

// Create the service (but don't start it yet)
procedure CreateService();
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
  
  ServiceInstalled := True;
end;

// Start the service
procedure StartService();
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'start ' + '{#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Run the setup wizard and wait for it to complete
procedure RunSetupWizard();
var
  ResultCode: Integer;
  ExePath: String;
begin
  ExePath := ExpandConstant('{app}\{#MyAppExeName}');
  
  // Run the wizard with --setup flag and WAIT for it to complete
  Exec(ExePath, '--setup', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
end;

// Called after installation files are copied
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Step 1: Create the Windows Service (but don't start it yet)
    CreateService();
    
    // Step 2: Run the setup wizard and wait for user to configure
    RunSetupWizard();
    
    // Step 3: Start the service AFTER wizard completes
    StartService();
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
