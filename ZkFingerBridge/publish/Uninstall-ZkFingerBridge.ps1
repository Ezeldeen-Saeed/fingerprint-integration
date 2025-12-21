# Uninstall-ZkFingerBridge.ps1
# Run as Administrator to uninstall the Windows Service

#Requires -RunAsAdministrator

$ServiceName = "ZkFingerBridge"
$InstallPath = "C:\Program Files (x86)\ZkFingerBridge"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ZkFingerBridge Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Host "Service '$ServiceName' not found." -ForegroundColor Yellow
} else {
    # Stop service
    Write-Host "Stopping service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    
    # Delete service
    Write-Host "Removing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
    
    Write-Host "Service removed successfully." -ForegroundColor Green
}

# Remove files
if (Test-Path $InstallPath) {
    Write-Host "Removing installation files..." -ForegroundColor Yellow
    Remove-Item -Path $InstallPath -Recurse -Force
    Write-Host "Files removed." -ForegroundColor Green
} else {
    Write-Host "Installation directory not found." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Uninstallation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
