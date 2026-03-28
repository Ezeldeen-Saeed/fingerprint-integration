# ============================================================================
# Build-InstallerWizard.ps1
# Builds the ZkFingerBridge installer wizard EXE
# ============================================================================

param(
    [string]$Configuration = "Release",
    [switch]$CreatePackage
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ZkFingerBridgeDir = Join-Path $ProjectRoot ".." 
$OutputDir = Join-Path $ProjectRoot "bin\Release\net8.0-windows\win-x86\publish"
$PayloadDir = Join-Path $ProjectRoot "payload"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ZkFingerBridge Installer Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build ZkFingerBridge first
Write-Host "[1/4] Building ZkFingerBridge..." -ForegroundColor Yellow
Push-Location $ZkFingerBridgeDir
try {
    dotnet publish -c $Configuration -r win-x86 --self-contained true -o "$PayloadDir"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build ZkFingerBridge"
    }
    Write-Host "      ZkFingerBridge built successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Step 2: Build the installer wizard
Write-Host "[2/4] Building Installer Wizard..." -ForegroundColor Yellow
Push-Location $ProjectRoot
try {
    dotnet publish -c $Configuration -r win-x86 --self-contained true -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build installer wizard"
    }
    Write-Host "      Installer Wizard built successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Step 3: Create the distribution package
Write-Host "[3/4] Creating distribution package..." -ForegroundColor Yellow
$DistDir = Join-Path $ProjectRoot "dist"
if (Test-Path $DistDir) {
    Remove-Item $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir | Out-Null

# Copy installer EXE
$InstallerExe = Get-ChildItem -Path $OutputDir -Filter "*.exe" | Select-Object -First 1
if ($InstallerExe) {
    Copy-Item $InstallerExe.FullName (Join-Path $DistDir "ZkFingerBridge-Setup.exe")
    Write-Host "      Copied installer EXE" -ForegroundColor Green
}

# Create payload.zip
if ($CreatePackage) {
    Write-Host "[4/4] Creating payload archive..." -ForegroundColor Yellow
    $PayloadZip = Join-Path $DistDir "payload.zip"
    Compress-Archive -Path "$PayloadDir\*" -DestinationPath $PayloadZip -Force
    Write-Host "      Created payload.zip" -ForegroundColor Green
} else {
    # Just copy payload folder
    Write-Host "[4/4] Copying payload folder..." -ForegroundColor Yellow
    Copy-Item $PayloadDir (Join-Path $DistDir "payload") -Recurse
    Write-Host "      Copied payload folder" -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Distribution files created in: $DistDir" -ForegroundColor White
Get-ChildItem $DistDir | ForEach-Object {
    $size = if ($_.PSIsContainer) { "(folder)" } else { "$([math]::Round($_.Length / 1MB, 2)) MB" }
    Write-Host "  - $($_.Name): $size" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "To install:" -ForegroundColor Yellow
Write-Host "  1. Copy the 'dist' folder contents to target machine" -ForegroundColor White
Write-Host "  2. Run ZkFingerBridge-Setup.exe as Administrator" -ForegroundColor White
Write-Host ""
