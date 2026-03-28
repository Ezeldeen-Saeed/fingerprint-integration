# ============================================================================
# Build-Installer.ps1
# Builds the ZkFingerBridge installer package
# ============================================================================

#Requires -Version 5.1

param(
    [string]$Configuration = "Release",
    [string]$Version = "2.0.0",
    [switch]$SkipPublish,
    [switch]$SkipAssets,
    [switch]$Silent
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$InstallerDir = Join-Path $ProjectRoot "installer"
$OutputDir = Join-Path $InstallerDir "output"
$AssetsDir = Join-Path $InstallerDir "assets"
$PublishDir = Join-Path $ProjectRoot "bin\Release\net8.0-windows\win-x86\publish"

# Colors for output
function Write-Step($message) {
    Write-Host "`n=== $message ===" -ForegroundColor Cyan
}

function Write-Success($message) {
    Write-Host "✓ $message" -ForegroundColor Green
}

function Write-Warning($message) {
    Write-Host "⚠ $message" -ForegroundColor Yellow
}

function Write-Error($message) {
    Write-Host "✗ $message" -ForegroundColor Red
}

# ============================================================================
# Step 1: Check Prerequisites
# ============================================================================

Write-Step "Checking Prerequisites"

# Check for .NET SDK
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error ".NET SDK not found. Please install .NET 8 SDK."
    exit 1
}
Write-Success ".NET SDK found: $(dotnet --version)"

# Check for Inno Setup
$innoSetupPath = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $innoSetupPath) {
    Write-Error "Inno Setup 6 not found. Please install from https://jrsoftware.org/isdl.php"
    exit 1
}
Write-Success "Inno Setup found: $innoSetupPath"

# ============================================================================
# Step 2: Create Assets (if needed)
# ============================================================================

if (-not $SkipAssets) {
    Write-Step "Creating Assets"
    
    if (-not (Test-Path $AssetsDir)) {
        New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null
    }
    
    # Create placeholder icon if not exists
    $iconPath = Join-Path $AssetsDir "icon.ico"
    if (-not (Test-Path $iconPath)) {
        Write-Warning "icon.ico not found. Please add a proper icon file."
        # Create empty placeholder (installer will use default)
    }
    
    # Check for wizard images
    $wizardImage = Join-Path $AssetsDir "wizard-image.bmp"
    $wizardSmall = Join-Path $AssetsDir "wizard-small.bmp"
    
    if (-not (Test-Path $wizardImage)) {
        Write-Warning "wizard-image.bmp not found. Using Inno Setup defaults."
    }
    if (-not (Test-Path $wizardSmall)) {
        Write-Warning "wizard-small.bmp not found. Using Inno Setup defaults."
    }
    
    Write-Success "Assets directory ready"
}

# ============================================================================
# Step 3: Publish Application
# ============================================================================

if (-not $SkipPublish) {
    Write-Step "Publishing Application (win-x86, self-contained)"
    
    Push-Location $ProjectRoot
    try {
        dotnet publish -c $Configuration -r win-x86 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
            exit 1
        }
        Write-Success "Application published to $PublishDir"
    }
    finally {
        Pop-Location
    }
    
    # Verify publish output
    $exePath = Join-Path $PublishDir "ZkFingerBridge.exe"
    if (-not (Test-Path $exePath)) {
        Write-Error "Published executable not found at $exePath"
        exit 1
    }
    
    # Verify zkemkeeper.dll exists
    $zkDllPath = Join-Path $PublishDir "zkemkeeper.dll"
    if (-not (Test-Path $zkDllPath)) {
        Write-Warning "zkemkeeper.dll not found in publish output. COM registration may fail."
        
        # Try to copy from Standalone-SDK if available
        $sdkDll = Join-Path $ProjectRoot "..\Standalone-SDK\zkemkeeper.dll"
        if (Test-Path $sdkDll) {
            Copy-Item $sdkDll $PublishDir -Force
            Write-Success "Copied zkemkeeper.dll from Standalone-SDK"
        }
    }
}

# ============================================================================
# Step 4: Create Output Directory
# ============================================================================

Write-Step "Preparing Output Directory"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
Write-Success "Output directory ready: $OutputDir"

# ============================================================================
# Step 5: Build Installer
# ============================================================================

Write-Step "Building Installer"

$issFile = Join-Path $InstallerDir "ZkFingerBridge-Installer.iss"

if (-not (Test-Path $issFile)) {
    Write-Error "Installer script not found: $issFile"
    exit 1
}

# Update version in ISS file
$issContent = Get-Content $issFile -Raw
$issContent = $issContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""
$issContent | Set-Content $issFile -Encoding UTF8

# Run Inno Setup Compiler
$isccArgs = @(
    "/O`"$OutputDir`"",
    "/DMyAppVersion=`"$Version`"",
    $issFile
)

if ($Silent) {
    $isccArgs += "/Q"
}

Write-Host "Running: $innoSetupPath $($isccArgs -join ' ')"
& $innoSetupPath @isccArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed with exit code $LASTEXITCODE"
    exit 1
}

# ============================================================================
# Step 6: Verify Output
# ============================================================================

Write-Step "Verifying Output"

$installerPath = Join-Path $OutputDir "ZkFingerBridge-Setup-$Version.exe"
if (Test-Path $installerPath) {
    $fileInfo = Get-Item $installerPath
    Write-Success "Installer created successfully!"
    Write-Host ""
    Write-Host "  File: $installerPath" -ForegroundColor White
    Write-Host "  Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor White
    Write-Host ""
} else {
    Write-Error "Installer file not found at expected location"
    exit 1
}

# ============================================================================
# Done
# ============================================================================

Write-Host "============================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "To install:" -ForegroundColor Cyan
Write-Host "  1. Copy '$installerPath' to target machine"
Write-Host "  2. Run as Administrator"
Write-Host "  3. Enter the installation key for the branch"
Write-Host "  4. Complete the setup wizard"
Write-Host ""
Write-Host "Silent install:" -ForegroundColor Cyan
Write-Host "  ZkFingerBridge-Setup-$Version.exe /VERYSILENT /KEY=BR001-2024-XXXX"
Write-Host ""
