@echo off
:: Build the installer wizard
echo Building ZkFingerBridge Installer Wizard...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0Build-InstallerWizard.ps1"

if %errorlevel% neq 0 (
    echo.
    echo Build failed!
    pause
    exit /b %errorlevel%
)

echo.
pause
