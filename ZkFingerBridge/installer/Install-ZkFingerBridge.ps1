# Install-ZkFingerBridge.ps1
# Run as Administrator to install the Windows Service

#Requires -RunAsAdministrator

$ServiceName = "ZkFingerBridge"
$DisplayName = "ZkFingerBridge Attendance Sync"
$Description = "Syncs fingerprint attendance logs from ZK devices to Firstsoft HR system"
$InstallPath = "C:\Program Files (x86)\ZkFingerBridge"
$SourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ZkFingerBridge Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if service exists and stop it
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

# Create installation directory
if (-not (Test-Path $InstallPath)) {
    Write-Host "Creating installation directory: $InstallPath" -ForegroundColor Green
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Copy files
Write-Host "Copying files to $InstallPath..." -ForegroundColor Green
$publishPath = Join-Path $SourcePath "publish"
if (Test-Path $publishPath) {
    Copy-Item -Path "$publishPath\*" -Destination $InstallPath -Recurse -Force
} else {
    Copy-Item -Path "$SourcePath\*" -Destination $InstallPath -Recurse -Force -Exclude "*.ps1"
}

# Install the service
$exePath = Join-Path $InstallPath "ZkFingerBridge.exe"
Write-Host "Installing Windows Service..." -ForegroundColor Green
sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= $DisplayName | Out-Null
sc.exe description $ServiceName $Description | Out-Null

# Run the setup wizard
Write-Host ""
Write-Host "Running Setup Wizard..." -ForegroundColor Cyan
Write-Host "Please select your company and branch in the wizard." -ForegroundColor Yellow
Write-Host ""

Start-Process -FilePath $exePath -ArgumentList "--setup" -Wait

# Start the service
Write-Host "Starting service..." -ForegroundColor Green
Start-Service -Name $ServiceName

# Verify
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Service Status: $($service.Status)" -ForegroundColor Green
    Write-Host "Install Path:   $InstallPath" -ForegroundColor White
    Write-Host ""
    Write-Host "The service will start automatically on Windows startup." -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "Warning: Service may not have started properly." -ForegroundColor Yellow
    Write-Host "Status: $($service.Status)" -ForegroundColor Yellow
    Write-Host "Check Windows Event Viewer for details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
