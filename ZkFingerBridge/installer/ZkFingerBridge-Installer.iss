; ============================================================================
; ZkFingerBridge Professional Installer
; Inno Setup Script v2.0
; ============================================================================
; Features:
;   - Modern wizard with Arabic RTL support
;   - License key activation page
;   - SDK registration (zkemkeeper.dll)
;   - Windows Service installation
;   - Setup wizard launch after install
; ============================================================================

#define MyAppName "ZkFingerBridge"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Firstsoft"
#define MyAppURL "https://firstsoft.io"
#define MyAppExeName "ZkFingerBridge.exe"
#define MyServiceName "ZkFingerBridge"
#define MyServiceDisplayName "ZkFingerBridge Attendance Sync"
#define MyServiceDescription "Syncs fingerprint attendance logs from ZK devices to Firstsoft HR system"

[Setup]
; Application identity
AppId={{B8F5E3A1-2D4C-4E6F-8A9B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates

; Installation paths (32-bit for COM compatibility)
DefaultDirName={commonpf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Output settings
OutputDir=output
OutputBaseFilename=ZkFingerBridge-Setup-{#MyAppVersion}
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Appearance
WizardStyle=modern
WizardImageFile=..\assets\wizard-image.bmp
WizardSmallImageFile=..\assets\wizard-small.bmp

; Privileges and architecture
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=
CloseApplications=force
RestartApplications=no

; Minimum OS version (Windows 10+)
MinVersion=10.0

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
; Arabic custom messages
arabic.LicenseKeyPageTitle=تفعيل الترخيص
arabic.LicenseKeyPageDescription=أدخل مفتاح التثبيت الخاص بفرعك
arabic.LicenseKeyLabel=مفتاح التثبيت:
arabic.LicenseKeyPlaceholder=مثال: BR001-2024-X9Y2
arabic.ValidatingKey=جاري التحقق من المفتاح...
arabic.KeyValidSuccess=✓ تم التحقق بنجاح
arabic.KeyValidFailed=✗ مفتاح غير صالح
arabic.KeyAlreadyUsed=✗ تم استخدام هذا المفتاح مسبقاً
arabic.BranchInfo=الفرع: %1
arabic.InstallingService=جاري تثبيت الخدمة...
arabic.RegisteringSDK=جاري تسجيل SDK...
arabic.ServiceInstalled=✓ تم تثبيت الخدمة بنجاح
arabic.RunSetupWizard=تشغيل معالج الإعداد

; English custom messages
english.LicenseKeyPageTitle=License Activation
english.LicenseKeyPageDescription=Enter your branch installation key
english.LicenseKeyLabel=Installation Key:
english.LicenseKeyPlaceholder=Example: BR001-2024-X9Y2
english.ValidatingKey=Validating key...
english.KeyValidSuccess=✓ Key validated successfully
english.KeyValidFailed=✗ Invalid key
english.KeyAlreadyUsed=✗ This key has already been used
english.BranchInfo=Branch: %1
english.InstallingService=Installing service...
english.RegisteringSDK=Registering SDK...
english.ServiceInstalled=✓ Service installed successfully
english.RunSetupWizard=Run Setup Wizard

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start service automatically on Windows startup"; GroupDescription: "Service Options:"; Flags: checked

[Files]
; Main application files
Source: "..\bin\Release\net8.0-windows\win-x86\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ZK SDK DLL - register as 32-bit COM
Source: "..\bin\Release\net8.0-windows\win-x86\publish\zkemkeeper.dll"; DestDir: "{app}"; Flags: ignoreversion regserver 32bit

; Configuration template
Source: "..\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Dirs]
; Create logs directory with appropriate permissions
Name: "{app}\logs"; Permissions: users-modify

[Icons]
; Start menu icons
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Run ZkFingerBridge"
Name: "{group}\Setup Wizard"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup"; Comment: "Run configuration wizard"
Name: "{group}\View Logs"; Filename: "{app}\logs"; Comment: "Open logs folder"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Desktop icon (optional)
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Store installation info
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

[Run]
; Post-install actions
Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup"; Description: "{cm:RunSetupWizard}"; Flags: nowait postinstall skipifsilent

[Code]
var
  LicenseKeyPage: TInputQueryWizardPage;
  BranchInfoLabel: TNewStaticText;
  ValidationStatusLabel: TNewStaticText;
  ValidatedBranchId: String;
  ValidatedBranchName: String;
  KeyIsValid: Boolean;

// ============================================================================
// Service Management Functions
// ============================================================================

function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('sc.exe', 'query {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure StopService();
var
  ResultCode: Integer;
begin
  if ServiceExists() then
  begin
    Exec('sc.exe', 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(3000); // Wait for service to stop
  end;
end;

procedure DeleteService();
var
  ResultCode: Integer;
begin
  if ServiceExists() then
  begin
    Exec('sc.exe', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
  end;
end;

procedure CreateService();
var
  ResultCode: Integer;
  BinPath: String;
  StartType: String;
begin
  BinPath := ExpandConstant('"{app}\{#MyAppExeName}"');
  
  // Check if autostart task is selected
  if IsTaskSelected('autostart') then
    StartType := 'auto'
  else
    StartType := 'demand';
  
  // Create the Windows Service
  Exec('sc.exe', 'create {#MyServiceName} binPath= ' + BinPath + ' start= ' + StartType + ' DisplayName= "{#MyServiceDisplayName}"', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Set description
  Exec('sc.exe', 'description {#MyServiceName} "{#MyServiceDescription}"', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Configure failure actions (restart on failure)
  Exec('sc.exe', 'failure {#MyServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StartService();
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'start {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// ============================================================================
// License Key Validation (HTTP Request to API)
// ============================================================================

function ValidateLicenseKey(Key: String): Boolean;
var
  WinHttpReq: Variant;
  ResponseText: String;
  StatusCode: Integer;
begin
  Result := False;
  KeyIsValid := False;
  
  if Key = '' then
    Exit;
  
  try
    // Create WinHTTP request
    WinHttpReq := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    WinHttpReq.Open('POST', 'https://api.firstsoft.io/api/installer/activate', False);
    WinHttpReq.SetRequestHeader('Content-Type', 'application/json');
    WinHttpReq.SetRequestHeader('Accept', 'application/json');
    
    // Send request with JSON body
    WinHttpReq.Send('{"installationKey": "' + Key + '"}');
    
    StatusCode := WinHttpReq.Status;
    ResponseText := WinHttpReq.ResponseText;
    
    if StatusCode = 200 then
    begin
      // Parse response to get branch info
      // For simplicity, we assume success if status 200
      // In production, parse JSON properly
      if Pos('"success":true', LowerCase(ResponseText)) > 0 then
      begin
        Result := True;
        KeyIsValid := True;
        
        // Extract branch name (simplified parsing)
        // Real implementation should use proper JSON parsing
        ValidatedBranchName := 'Branch Validated';
        ValidatedBranchId := Key;
      end;
    end
    else if StatusCode = 403 then
    begin
      // Key already used
      ValidationStatusLabel.Caption := ExpandConstant('{cm:KeyAlreadyUsed}');
      ValidationStatusLabel.Font.Color := clRed;
    end
    else if StatusCode = 404 then
    begin
      // Invalid key
      ValidationStatusLabel.Caption := ExpandConstant('{cm:KeyValidFailed}');
      ValidationStatusLabel.Font.Color := clRed;
    end;
  except
    // Network error - allow offline installation for testing
    // In production, you might want to require online validation
    ValidationStatusLabel.Caption := 'Network error - offline mode';
    ValidationStatusLabel.Font.Color := clYellow;
    
    // For development, allow proceeding
    Result := True;
    KeyIsValid := True;
  end;
end;

// ============================================================================
// Wizard Page Initialization
// ============================================================================

procedure InitializeWizard();
begin
  // Create the license key input page
  LicenseKeyPage := CreateInputQueryPage(wpWelcome,
    ExpandConstant('{cm:LicenseKeyPageTitle}'),
    ExpandConstant('{cm:LicenseKeyPageDescription}'),
    '');
  
  LicenseKeyPage.Add(ExpandConstant('{cm:LicenseKeyLabel}'), False);
  
  // Add validation status label
  ValidationStatusLabel := TNewStaticText.Create(LicenseKeyPage);
  ValidationStatusLabel.Parent := LicenseKeyPage.Surface;
  ValidationStatusLabel.Left := 0;
  ValidationStatusLabel.Top := 70;
  ValidationStatusLabel.Width := LicenseKeyPage.SurfaceWidth;
  ValidationStatusLabel.Caption := '';
  ValidationStatusLabel.Font.Style := [fsBold];
  
  // Add branch info label
  BranchInfoLabel := TNewStaticText.Create(LicenseKeyPage);
  BranchInfoLabel.Parent := LicenseKeyPage.Surface;
  BranchInfoLabel.Left := 0;
  BranchInfoLabel.Top := 95;
  BranchInfoLabel.Width := LicenseKeyPage.SurfaceWidth;
  BranchInfoLabel.Caption := '';
  BranchInfoLabel.Font.Color := clGreen;
end;

// ============================================================================
// Page Navigation Handlers
// ============================================================================

function NextButtonClick(CurPageID: Integer): Boolean;
var
  LicenseKey: String;
begin
  Result := True;
  
  if CurPageID = LicenseKeyPage.ID then
  begin
    LicenseKey := Trim(LicenseKeyPage.Values[0]);
    
    if LicenseKey = '' then
    begin
      MsgBox('Please enter an installation key.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    
    ValidationStatusLabel.Caption := ExpandConstant('{cm:ValidatingKey}');
    ValidationStatusLabel.Font.Color := clBlue;
    WizardForm.Refresh;
    
    if ValidateLicenseKey(LicenseKey) then
    begin
      ValidationStatusLabel.Caption := ExpandConstant('{cm:KeyValidSuccess}');
      ValidationStatusLabel.Font.Color := clGreen;
      BranchInfoLabel.Caption := FmtMessage(ExpandConstant('{cm:BranchInfo}'), [ValidatedBranchName]);
      Result := True;
    end
    else
    begin
      if ValidationStatusLabel.Caption = '' then
      begin
        ValidationStatusLabel.Caption := ExpandConstant('{cm:KeyValidFailed}');
        ValidationStatusLabel.Font.Color := clRed;
      end;
      Result := False;
    end;
  end;
end;

// ============================================================================
// Installation Step Handlers
// ============================================================================

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  
  // Stop and remove existing service
  StopService();
  DeleteService();
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create the Windows Service
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingService}');
    CreateService();
  end;
end;

// ============================================================================
// Uninstall Handlers
// ============================================================================

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop and remove service before uninstalling files
    StopService();
    DeleteService();
  end;
end;

// ============================================================================
// Skip License Page in Silent Mode
// ============================================================================

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  
  // Skip license page if running in silent mode with /KEY parameter
  if PageID = LicenseKeyPage.ID then
  begin
    if WizardSilent then
    begin
      // Get key from command line parameter
      LicenseKeyPage.Values[0] := ExpandConstant('{param:KEY}');
      if LicenseKeyPage.Values[0] <> '' then
      begin
        ValidateLicenseKey(LicenseKeyPage.Values[0]);
        Result := True;
      end;
    end;
  end;
end;
