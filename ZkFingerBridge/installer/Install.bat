@echo off
echo ========================================
echo   ZkFingerBridge Installer
echo ========================================
echo.
echo This will install ZkFingerBridge as a Windows Service.
echo Please run this as Administrator.
echo.
pause

PowerShell -ExecutionPolicy Bypass -File "%~dp0Install-ZkFingerBridge.ps1"
