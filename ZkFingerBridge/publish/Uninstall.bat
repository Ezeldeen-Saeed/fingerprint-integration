@echo off
echo ========================================
echo   ZkFingerBridge Uninstaller
echo ========================================
echo.
echo This will uninstall ZkFingerBridge Windows Service.
echo Please run this as Administrator.
echo.
pause

PowerShell -ExecutionPolicy Bypass -File "%~dp0Uninstall-ZkFingerBridge.ps1"
