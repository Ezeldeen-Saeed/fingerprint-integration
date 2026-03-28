@echo off
:: ============================================================================
:: Build-Installer.bat
:: Quick launcher for Build-Installer.ps1
:: ============================================================================

echo.
echo ========================================
echo   ZkFingerBridge Installer Builder
echo ========================================
echo.

:: Check for PowerShell
where powershell >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: PowerShell not found!
    pause
    exit /b 1
)

:: Run the PowerShell build script
powershell -ExecutionPolicy Bypass -File "%~dp0Build-Installer.ps1"

if %errorlevel% neq 0 (
    echo.
    echo Build failed! Check the errors above.
    pause
    exit /b %errorlevel%
)

echo.
pause
